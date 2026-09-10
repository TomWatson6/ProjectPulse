using System;
using Pulse.Music;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Pulse.Laboratory
{
    public sealed class AudioTimeline : MaskableGraphic, IPointerDownHandler, IPointerUpHandler, IDragHandler, IScrollHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public SongMap Map { get; private set; }
        public AudioLabController Controller;
        public double StartTime { get; private set; }
        public double Span { get; private set; }=20;
        public bool Follow=true;
        public bool ShowWave=true,ShowBeats=true,ShowOnsets=true,ShowEnergy=true,ShowBands=true,ShowSections=true,ShowMoments=true;
        public string Inspection { get; private set; }="Hover an event for timestamp, strength and confidence. Scroll to zoom · drag to seek · right-drag to pan.";
        public float PlotLeft=>108;
        public float PlotWidth=>rectTransform.rect.width-130;
        private bool hovering,dragging,panning;
        private Vector2 lastPointer;
        private static readonly Color Grid=new Color(.14f,.26f,.31f,.65f);
        private static readonly Color Mint=new Color(.50f,1,.88f);
        private static readonly Color Coral=new Color(1,.43f,.49f);
        private static readonly Color Purple=new Color(.62f,.53f,1);
        private static readonly Color Gold=new Color(1,.79f,.39f);
        public void SetMap(SongMap map,bool reset=true)
        {
            Map=map; if(reset) { StartTime=0; Span=map==null?20:Math.Min(20,map.Duration); Follow=true; }
            ClampView(); SetVerticesDirty();
        }
        public void SetView(double start,double span)
        { StartTime=start; Span=span; ClampView(); SetVerticesDirty(); }
        public void Zoom(double factor,double anchor=.5)
        {
            double time=StartTime+Span*anchor; double length=Math.Max(2,Math.Min(Map?.Duration??20,Span*factor));
            SetView(time-length*anchor,length); Follow=false;
        }
        public void Fit() { SetView(0,Map?.Duration??20); Follow=true; }
        public void FollowPosition(double time)
        {
            if(Follow && Map!=null && (time<StartTime || time>StartTime+Span*.92)) SetView(Math.Max(0,time-Span*.18),Span);
            if(hovering) Inspect(UnityEngine.Input.mousePosition);
        }
        public float XAt(double time) => PlotLeft+(float)((time-StartTime)/Span)*PlotWidth;
        private void ClampView()
        { double duration=Map?.Duration??20; Span=Math.Max(Math.Min(2,duration),Math.Min(duration,Span)); StartTime=Math.Max(0,Math.Min(duration-Span,StartTime)); }
        private Vector2 Local(Vector2 screen)
        { RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,screen,null,out Vector2 local); var r=rectTransform.rect; return new Vector2(local.x-r.xMin,r.yMax-local.y); }
        private double TimeAt(Vector2 screen) { var local=Local(screen); return Math.Max(0,Math.Min(Map?.Duration??Controller.Transport.Duration,StartTime+(local.x-PlotLeft)/PlotWidth*Span)); }
        public void OnPointerDown(PointerEventData e)
        {
            if(Map==null || Controller.Busy) return;
            lastPointer=e.position; Follow=false;
            if(e.button==PointerEventData.InputButton.Right) panning=true;
            else if(e.button==PointerEventData.InputButton.Left) { dragging=true; Controller.BeginScrub(); Controller.Scrub(TimeAt(e.position)); }
        }
        public void OnPointerUp(PointerEventData e) { if(dragging) Controller.EndScrub(); dragging=panning=false; }
        public void OnDrag(PointerEventData e)
        {
            if(panning) { double delta=(Local(e.position).x-Local(lastPointer).x)/PlotWidth*Span; SetView(StartTime-delta,Span); }
            if(dragging) Controller.Scrub(TimeAt(e.position)); lastPointer=e.position;
        }
        public void OnScroll(PointerEventData e) { if(Map!=null) Zoom(e.scrollDelta.y>0?.8:1.25,Mathf.Clamp01((Local(e.position).x-PlotLeft)/PlotWidth)); }
        public void OnPointerEnter(PointerEventData e) { hovering=true; Inspect(e.position); }
        public void OnPointerExit(PointerEventData e) { hovering=false; }
        protected override void OnDisable() { if(dragging && Controller!=null) Controller.EndScrub(); dragging=panning=hovering=false; base.OnDisable(); }
        private void Inspect(Vector2 position)
        {
            if(Map==null) return;
            var local=Local(position); if(local.x<PlotLeft) return;
            double time=TimeAt(position),tolerance=Math.Max(.025,Span/PlotWidth*8);
            string detail="";
            if(local.y>=330 && ShowSections) { var section=Map.SectionAt(time); if(section!=null) detail=$"{section.Kind}  ·  {section.Start:F2}–{section.End:F2}s  ·  confidence {section.Confidence:P0}"; }
            if(local.y>=108 && local.y<156 && ShowBeats)
            {
                var beat=Map.NearestBeat(time);
                if(beat!=null && Math.Abs(beat.Time-time)<tolerance) detail=$"BEAT {beat.Index+1}  @ {beat.Time:F3}s  ·  strength {beat.Strength:P0}  ·  confidence {beat.Confidence:P0}"+(beat.BarPosition==0?$"  ·  estimated downbeat ({Map.DownbeatConfidence:P0})":"");
            }
            if(local.y>=160 && local.y<218 && ShowOnsets)
            {
                int i=Map.NextOnsetIndex(time-tolerance);
                if(i<Map.Onsets.Length && Math.Abs(Map.Onsets[i].Time-time)<tolerance) { var o=Map.Onsets[i]; detail=$"ONSET  @ {o.Time:F3}s  ·  strength {o.Strength:P0}  ·  confidence {o.Confidence:P0}"; }
            }
            if(ShowMoments && (local.y<28 || local.y>=320))
                foreach(var m in Map.Moments) if(Math.Abs(m.Time-time)<tolerance) { detail=$"{m.Kind.ToString().ToUpperInvariant()}  @ {m.Time:F3}s  ·  strength {m.Strength:P0}  ·  confidence {m.Confidence:P0}"; break; }
            Inspection=$"{time:000.000}s   /   "+(detail.Length>0?detail:$"intensity {Map.IntensityAt(time):P0}  ·  scroll to zoom / right-drag to pan");
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); float width=rectTransform.rect.width;
            foreach(float y in new[]{26f,99,153,215,321,375}) Rect(vh,0,y,width,1,Grid);
            Rect(vh,PlotLeft,26,1,349,Grid);
            for(int i=0;i<=8;i++) Rect(vh,PlotLeft+PlotWidth*i/8,26,1,349,new Color(.14f,.26f,.31f,.24f));
            if(Map==null) return;
            int columns=Math.Min(1000,(int)PlotWidth);
            if(ShowWave)
                for(int x=0;x<columns;x++)
                {
                    int a=Math.Max(0,(int)((StartTime+Span*x/columns)/Map.WaveStep));
                    int b=Math.Min(Map.Waveform.Length,Math.Max(a+1,(int)((StartTime+Span*(x+1)/columns)/Map.WaveStep)));
                    float min=0,max=0; for(int i=a;i<b;i++) { min=Mathf.Min(min,Map.Waveform[i].Min); max=Mathf.Max(max,Map.Waveform[i].Max); }
                    Rect(vh,PlotLeft+PlotWidth*x/columns,63-max*31,Math.Max(1,PlotWidth/columns*.74f),Math.Max(.7f,(max-min)*31),new Color(.40f,.75f,.72f,.8f));
                }
            if(ShowBeats)
            {
                float last=-10;
                for(int i=Math.Max(0,Map.CurrentBeatIndex(StartTime));i<Map.Beats.Length && Map.Beats[i].Time<=StartTime+Span;i++)
                {
                    var b=Map.Beats[i]; if(b.Time<StartTime) continue; float x=XAt(b.Time); if(x-last<2) continue; last=x;
                    bool down=b.BarPosition==0; Color c=down?Gold:Mint; c.a=.3f+.7f*b.Confidence;
                    Rect(vh,x,down?108:119,down?2.5f:1.2f,down?37:26,c);
                    if(Span<16 && i+1<Map.Beats.Length && Map.BpmConfidence>=.25f) Rect(vh,XAt((b.Time+Map.Beats[i+1].Time)/2),138,1,7,new Color(.5f,1,.88f,.25f));
                }
            }
            if(ShowOnsets)
            {
                float last=-10;
                for(int i=Map.NextOnsetIndex(StartTime-1e-9);i<Map.Onsets.Length && Map.Onsets[i].Time<=StartTime+Span;i++)
                {
                    var o=Map.Onsets[i]; float x=XAt(o.Time); if(x-last<2) continue; last=x;
                    float h=9+34*o.Strength; Rect(vh,x,207-h,1.4f,h,Coral); Rect(vh,x-1.5f,205-h,4,3,Coral);
                }
            }
            if(ShowEnergy || ShowBands)
            {
                Vector2 lastEnergy=Vector2.zero,lastLow=Vector2.zero,lastMid=Vector2.zero,lastHigh=Vector2.zero;
                for(int p=0;p<=columns;p++)
                {
                    double t=StartTime+Span*p/columns; int i=Math.Min(Map.Energy.Length-1,(int)(t/Map.EnergyStep)); var e=Map.Energy[i];
                    float x=PlotLeft+PlotWidth*p/columns; var point=new Vector2(x,311-e.Intensity*78);
                    var l=new Vector2(x,311-e.Low*78); var m=new Vector2(x,311-e.Mid*78); var h=new Vector2(x,311-e.High*78);
                    if(p>0)
                    {
                        if(ShowEnergy) { Rect(vh,lastEnergy.x,Math.Min(lastEnergy.y,point.y),PlotWidth/columns,311-Math.Min(lastEnergy.y,point.y),new Color(.5f,1,.88f,.08f)); Line(vh,lastEnergy,point,2,Mint); }
                        if(ShowBands) { Line(vh,lastLow,l,1,Gold); Line(vh,lastMid,m,1,Purple); Line(vh,lastHigh,h,1,Coral); }
                    }
                    lastEnergy=point; lastLow=l; lastMid=m; lastHigh=h;
                }
            }
            if(ShowSections)
                foreach(var s in Map.Sections)
                {
                    if(s.End<=StartTime || s.Start>=StartTime+Span) continue;
                    float a=XAt(Math.Max(StartTime,s.Start)),b=XAt(Math.Min(StartTime+Span,s.End)); Color c=SectionColor(s.Kind);
                    Color fill=c; fill.a=.14f; Rect(vh,a,335,b-a,30,fill); Rect(vh,a,335,2,30,c); Rect(vh,a,363,b-a,2,new Color(c.r,c.g,c.b,.35f));
                }
            if(ShowMoments)
                foreach(var m in Map.Moments)
                {
                    if(m.Time<StartTime || m.Time>StartTime+Span) continue; float x=XAt(m.Time); Color c=m.Kind==MomentKind.Drop?Coral:Gold;
                    Rect(vh,x,26,1,349,new Color(c.r,c.g,c.b,.3f)); Rect(vh,x-3,28,6,6,c); Rect(vh,x-3,323,6,6,c);
                }
        }
        public static Color SectionColor(SectionKind kind) => kind==SectionKind.Build?Gold:kind==SectionKind.HighEnergy?Mint:kind==SectionKind.LowEnergy?Purple:new Color(.45f,.65f,.7f);
        private void Rect(VertexHelper vh,float x,float y,float w,float h,Color c)
        {
            if(w<=0 || h<=0) return;
            var r=rectTransform.rect; int start=vh.currentVertCount;
            vh.AddVert(new Vector3(r.xMin+x,r.yMax-y),c,Vector2.zero); vh.AddVert(new Vector3(r.xMin+x+w,r.yMax-y),c,Vector2.zero);
            vh.AddVert(new Vector3(r.xMin+x+w,r.yMax-y-h),c,Vector2.zero); vh.AddVert(new Vector3(r.xMin+x,r.yMax-y-h),c,Vector2.zero);
            vh.AddTriangle(start,start+1,start+2); vh.AddTriangle(start,start+2,start+3);
        }
        private void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color c)
        {
            Vector2 normal=new Vector2(-(b.y-a.y),b.x-a.x).normalized*width/2;
            var r=rectTransform.rect; int start=vh.currentVertCount;
            Vector2 p=a-normal; vh.AddVert(new Vector3(r.xMin+p.x,r.yMax-p.y),c,Vector2.zero);
            p=a+normal; vh.AddVert(new Vector3(r.xMin+p.x,r.yMax-p.y),c,Vector2.zero);
            p=b+normal; vh.AddVert(new Vector3(r.xMin+p.x,r.yMax-p.y),c,Vector2.zero);
            p=b-normal; vh.AddVert(new Vector3(r.xMin+p.x,r.yMax-p.y),c,Vector2.zero);
            vh.AddTriangle(start,start+1,start+2); vh.AddTriangle(start,start+2,start+3);
        }
    }
}
