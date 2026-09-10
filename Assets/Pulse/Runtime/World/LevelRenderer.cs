using Pulse.Domain;
using Pulse.Rendering;
using Pulse.Visuals;
using UnityEngine;

namespace Pulse.World
{
    public sealed class LevelRenderer
    {
        private readonly MeshCanvas canvas;
        private readonly LevelDefinition level;
        private readonly RunnerTuning tuning;
        public LevelRenderer(Transform parent,LevelDefinition level,RunnerTuning tuning)
        { canvas=MeshCanvas.Create("Readable geometry",10,parent); this.level=level; this.tuning=tuning; }
        private static Color Alpha(Color c,float a) { c.a=a; return c; }
        public void Clear() { canvas.Begin(); canvas.End(); }

        public void Render(CameraDirector camera,double songTime,Theme theme,float pulse,bool glow)
        {
            canvas.Begin();
            float left=camera.CenterX-camera.HalfWidth-2, right=camera.CenterX+camera.HalfWidth+2;
            foreach(var solid in level.Solids)
            {
                if(solid.Right<left || solid.Left>right) continue;
                float x=Mathf.Max(left,(float)solid.Left), end=Mathf.Min(right,(float)solid.Right), y=(float)solid.Top;
                canvas.Gradient(x,(float)solid.Bottom,end-x,y-(float)solid.Bottom,new Color(.014f,.022f,.052f),new Color(.025f,.062f,.10f));
                if(glow) canvas.Gradient(x,y-.32f,end-x,.32f,Alpha(theme.Accent,0),Alpha(theme.Accent,.18f+pulse*.08f));
                canvas.Rect(x,y-.10f,end-x,.10f,Alpha(theme.Accent,.18f));
                canvas.Rect(x,y-.025f,end-x,.025f,theme.Accent);
                float spacing=(float)(LevelDefinition.BeatSeconds*tuning.Speed);
                for(int i=Mathf.FloorToInt(x/spacing);i<=Mathf.CeilToInt(end/spacing);i++)
                {
                    float px=i*spacing;
                    if(px<x || px>end) continue;
                    canvas.Line(new Vector2(px,y-.15f),new Vector2(px,y-3),.014f,Alpha(theme.Secondary,.11f));
                    canvas.Line(new Vector2(px,y-.85f),new Vector2(Mathf.Min(px+spacing,end),y-2.2f),.013f,Alpha(theme.Secondary,.07f));
                    canvas.Rect(px+.12f,y-.22f,.12f,.027f,Alpha(theme.Accent,.35f));
                }
                canvas.Line(new Vector2((float)solid.Left,y),new Vector2((float)solid.Left,-6),.035f,Alpha(theme.Accent,.5f));
                canvas.Line(new Vector2((float)solid.Right,y),new Vector2((float)solid.Right,-6),.035f,Alpha(theme.Accent,.5f));
            }
            foreach(var spike in level.Spikes)
            {
                if(spike.X<left-1 || spike.X>right+1) continue;
                Vector2 a=new Vector2((float)(spike.X-spike.Width/2),(float)spike.Base);
                Vector2 b=new Vector2((float)spike.X,(float)(spike.Base+spike.Height));
                Vector2 c=new Vector2((float)(spike.X+spike.Width/2),(float)spike.Base);
                if(glow) canvas.Glow(b,.6f,Alpha(theme.Hazard,.18f));
                canvas.Triangle(a,b,c,new Color(.24f,.058f,.11f));
                canvas.Triangle(Vector2.Lerp(a,b,.18f),Vector2.Lerp(b,(a+c)/2,.18f),Vector2.Lerp(c,b,.18f),Alpha(theme.Hazard,.25f+pulse*.10f));
                canvas.Line(a,b,.035f,theme.Hazard); canvas.Line(b,c,.035f,theme.Hazard);
                canvas.Line(a,c,.025f,Alpha(theme.Hazard,.7f));
                canvas.Glow(b,.075f,new Color(1,.84f,.77f));
            }
            // Ground diamonds mark the intended takeoff rhythm; their positions are explicit chart cues.
            foreach(double beat in level.JumpBeats)
            {
                float x=(float)(beat*LevelDefinition.BeatSeconds*tuning.Speed);
                if(x<left || x>right) continue;
                float approach=1-Mathf.Clamp01(Mathf.Abs((float)(beat*LevelDefinition.BeatSeconds-songTime))/.6f);
                canvas.Line(new Vector2(x-.13f,.10f),new Vector2(x,.19f),.024f,Alpha(theme.Accent,.45f+approach*.4f));
                canvas.Line(new Vector2(x,.19f),new Vector2(x+.13f,.10f),.024f,Alpha(theme.Accent,.45f+approach*.4f));
                if(glow) canvas.Glow(new Vector2(x,.05f),.48f,Alpha(theme.Accent,approach*.3f));
            }
            // A finish arch gives the outro a physical destination.
            float finish=(float)(LevelDefinition.Duration*tuning.Speed);
            if(finish>left && finish<right)
            {
                canvas.Line(new Vector2(finish,0),new Vector2(finish,5),.07f,theme.Accent);
                canvas.Line(new Vector2(finish,5),new Vector2(finish+2,5),.07f,theme.Accent);
                if(glow) canvas.Glow(new Vector2(finish,2.5f),3,Alpha(theme.Accent,.15f));
            }
            canvas.End();
        }
    }
}
