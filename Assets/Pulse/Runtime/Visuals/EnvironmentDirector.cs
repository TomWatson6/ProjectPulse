using Pulse.Domain;
using Pulse.Rendering;
using UnityEngine;

namespace Pulse.Visuals
{
    public sealed class EnvironmentDirector
    {
        private readonly MeshCanvas canvas;
        public EnvironmentDirector(Transform parent) { canvas=MeshCanvas.Create("Atmosphere",0,parent); }
        private static Color Alpha(Color c,float a) { c.a=a; return c; }
        private static float Hash(int i) => Mathf.Repeat(Mathf.Sin(i*127.1f+311.7f)*43758.5453f,1);

        public void Render(CameraDirector camera,float playerX,double beat,Theme theme,float energy,float pulse,GraphicsQualityProfile quality,bool reducedMotion,bool authoredEvents=true)
        {
            canvas.Begin();
            float cx=camera.CenterX, half=camera.HalfWidth+2;
            canvas.Gradient(cx-half,-12,half*2,30,theme.Horizon,theme.Sky);
            float time=(float)(beat*LevelDefinition.BeatSeconds);
            Vector2 sun=new Vector2(cx+3.4f-Mathf.Sin(playerX*.003f)*.4f,4.55f);
            if(quality.Glow)
            {
                canvas.Glow(sun,7,Alpha(theme.Secondary,.10f+.10f*energy));
                canvas.Glow(new Vector2(cx,1),12,Alpha(theme.Accent,.08f));
            }
            // Fine stars, with distant horizontal parallax rather than camera-locked noise.
            for(int i=0;i<quality.DustCount;i++)
            {
                float x=cx+Mathf.Repeat(Hash(i)*half*2-playerX*.045f,half*2)-half;
                float y=1.4f+Hash(i+200)*9;
                float blink=.45f+.25f*Mathf.Sin(time*.5f+i);
                float r=.009f+Hash(i+500)*.022f;
                canvas.Rect(x,y,r,r,Alpha(theme.Accent,blink));
                if(i%11==0) canvas.Line(new Vector2(x-.065f,y),new Vector2(x+.065f,y),.012f,Alpha(theme.Secondary,.3f));
            }
            // The orbit: a fragmented instrument-like halo, with a shaded body and fine inscriptions.
            int segments=quality.RingSegments;
            for(int i=0;i<32;i++)
            {
                float r=2.72f-i*.065f;
                canvas.Ring(sun,r,.073f,Alpha(Color.Lerp(theme.Horizon,theme.Sky,i/32f),.8f),segments);
            }
            float rotate=reducedMotion?0:time*.025f;
            canvas.Ring(sun,2.78f,.026f,Alpha(theme.Accent,.40f+pulse*.24f),segments);
            canvas.Ring(sun,2.84f,.008f,Alpha(theme.Accent,.25f),segments);
            canvas.Ring(sun,3.09f,.012f,Alpha(theme.Secondary,.25f),segments,-rotate,.78f);
            canvas.Ring(sun,3.27f,.035f,Alpha(theme.Accent,.30f),segments,rotate+1.1f,.28f);
            canvas.Ring(sun,3.27f,.018f,Alpha(theme.Accent,.32f),segments,rotate+4.25f,.20f);
            for(int i=0;i<64;i++)
            {
                float a=i*Mathf.PI/32+rotate;
                Vector2 dir=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                canvas.Line(sun+dir*2.94f,sun+dir*(i%4==0?3.04f:2.98f),.012f,Alpha(theme.Accent,i%4==0?.48f:.20f));
            }
            // An equatorial slit and waveform chord visually connect the orbital form to the music.
            for(int i=0;i<50;i++)
            {
                float u=i/49f, x=sun.x-2.45f+u*4.9f;
                float v=Mathf.Sin(i*1.6f+time*.7f)*Mathf.Sin(u*Mathf.PI)*(.07f+pulse*.13f)*energy;
                canvas.Line(new Vector2(x,sun.y+v),new Vector2(x+.055f,sun.y-v),.011f,Alpha(theme.Accent,.26f));
            }
            canvas.Line(new Vector2(cx-half,1.75f),new Vector2(cx+half,1.75f),.015f,Alpha(theme.Accent,.24f));

            // Multiple deterministic silhouettes. All scenery is non-colliding and scrolls at its own depth.
            for(int layer=0;layer<quality.MountainLayers;layer++)
            {
                float parallax=.04f+layer*.045f;
                float spacing=3.5f-layer*.35f;
                int start=Mathf.FloorToInt((cx-half+playerX*parallax)/spacing);
                Color shade=Color.Lerp(theme.Horizon,theme.Sky,.26f+layer*.12f);
                for(int k=start;k<start+Mathf.CeilToInt(half*2/spacing)+2;k++)
                {
                    float x=k*spacing-playerX*parallax;
                    float height=.7f+Hash(k+layer*2000)*2.5f;
                    float y=.25f+layer*.18f;
                    var peak=new Vector2(x+spacing*.4f,y+height);
                    canvas.Triangle(new Vector2(x-.2f,-3),peak,new Vector2(x+spacing+1,-3),shade);
                    if(layer==1)
                        canvas.Line(peak,new Vector2(x+spacing*.65f,y+height*.40f),.012f,Alpha(theme.Secondary,.19f));
                }
            }
            // Suspended architectural shards slowly rise during the build and fan out at the drop.
            for(int i=0;i<9;i++)
            {
                float x=cx+Mathf.Repeat(i*4.63f-playerX*.18f,half*2)-half;
                float bob=reducedMotion?0:Mathf.Sin(time*.45f+i)*.25f;
                float y=2.2f+Hash(i+75)*3.4f+bob;
                float h=.5f+Hash(i+90)*1.3f;
                canvas.Quad(new Vector2(x,y),new Vector2(x+.23f,y+.18f),new Vector2(x+.23f,y+h),new Vector2(x,y+h-.18f),Alpha(theme.Secondary,.05f),Alpha(theme.Secondary,.13f));
                canvas.Line(new Vector2(x,y+h-.18f),new Vector2(x+.23f,y+h),.018f,Alpha(theme.Accent,.3f));
            }
            if(authoredEvents && beat>=48 && beat<52 && !reducedMotion)
            {
                float age=(float)(beat-48);
                canvas.Ring(sun,2.8f+age*5,.04f,Alpha(theme.Accent,Mathf.Exp(-age)),segments);
                canvas.Ring(sun,2.8f+age*3,.08f,Alpha(theme.Secondary,Mathf.Exp(-age)*.5f),segments);
            }
            canvas.End();
        }
    }
}
