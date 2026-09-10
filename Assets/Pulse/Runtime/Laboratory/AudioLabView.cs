using System;
using System.Globalization;
using System.Linq;
using Pulse.Music;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Pulse.Laboratory
{
    /// <summary>Separate Pulse-styled workbench; existing gameplay HUD remains untouched.</summary>
    public sealed class AudioLabView
    {
        private readonly AudioLabController lab;
        private readonly GameObject root;
        private readonly RectTransform content;
        private readonly Font font;
        private readonly Text title,metadata,status,playLabel,position,inspection,pageLabel,empty,estimate,sandboxLabel;
        private readonly Text[] rowTexts=new Text[6],axis=new Text[9],sectionLabels=new Text[16];
        private readonly Text[] momentLabels=new Text[12];
        private readonly Button[] rows=new Button[6];
        private readonly Button analyse,reanalyse,cancel,play,restart,overrideButton;
        private readonly Image progress,cursor,overviewFill;
        private readonly InputField directory,bpm,offset;
        public AudioTimeline Timeline { get; }
        private int page;
        private float nextRefresh;
        private static readonly Color Mint=new Color(.5f,1,.88f),Muted=new Color(.48f,.62f,.69f),White=new Color(.91f,.96f,.97f),Ink=new Color(.018f,.038f,.065f,.97f);
        public AudioLabView(AudioLabController lab,Transform parent)
        {
            this.lab=lab; font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            root=new GameObject("Audio laboratory",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)); root.transform.SetParent(parent,false);
            var canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=10;
            var scaler=root.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1600,900); scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var back=Box(root.transform,0,0,1600,900,new Color(.005f,.014f,.028f,.38f));
            back.rectTransform.anchorMin=Vector2.zero; back.rectTransform.anchorMax=Vector2.one; back.rectTransform.offsetMin=back.rectTransform.offsetMax=Vector2.zero;
            var body=new GameObject("Workbench",typeof(RectTransform)); body.transform.SetParent(root.transform,false); content=(RectTransform)body.transform;
            content.anchorMin=content.anchorMax=new Vector2(.5f,.5f); content.sizeDelta=new Vector2(1600,900);
            Label(content,"P U L S E",48,29,260,40,25,White,true);
            Label(content,"M U S I C   /   I N T E L L I G E N C E",50,72,500,25,10,Muted);
            Button(content,"<  BACK TO AFTERLIGHT",1322,32,230,43,lab.Close);
            Label(content,"Inside the signal.",46,103,900,64,43,White,true);
            Label(content,"AUDIO LABORATORY   /   LOCAL ANALYSIS   /   DEVELOPER PREVIEW",50,165,1100,25,12,Mint);
            Box(content,48,211,306,628,Ink); Box(content,48,211,306,2,Mint);
            Label(content,"YOUR SONGS",68,230,220,24,13,Mint,true);
            directory=Field(content,68,267,266,37,"Songs folder");
            Button(content,"USE FOLDER",68,314,128,34,()=>lab.SetDirectory(directory.text));
            Button(content,"SCAN",206,314,128,34,()=>lab.Scan());
            Label(content,"MP3 / WAV   ·   automatic discovery",68,363,268,28,11,Muted);
            for(int i=0;i<rows.Length;i++)
            {
                int slot=i;
                rows[i]=Button(content,"",68,404+i*57,266,51,()=> { int index=page*6+slot; if(index<lab.Songs.Count) lab.Select(lab.Songs[index]); });
                rowTexts[i]=rows[i].GetComponentInChildren<Text>(); rowTexts[i].alignment=TextAnchor.MiddleLeft; rowTexts[i].fontSize=13; rowTexts[i].fontStyle=FontStyle.Normal;
            }
            Button(content,"<",68,767,42,34,()=> { page=Math.Max(0,page-1); RebuildLibrary(); });
            pageLabel=Label(content,"0 SONGS",119,775,164,24,11,Muted); pageLabel.alignment=TextAnchor.MiddleCenter;
            Button(content,">",292,767,42,34,()=> { page=Math.Min(Math.Max(0,(lab.Songs.Count-1)/6),page+1); RebuildLibrary(); });
            title=Label(content,"Choose your frequency.",394,218,870,52,32,White,true);
            metadata=Label(content,"Select a song from the library. Your audio stays on this computer.",396,276,890,44,14,Muted);
            analyse=Button(content,"ANALYSE  >",1350,220,202,45,()=>lab.Analyse(),true);
            reanalyse=Button(content,"REANALYSE / DEBUG",1350,275,202,34,()=>lab.Analyse(true));
            sandboxLabel=Button(content,"VISUAL SANDBOX  OFF",1130,111,422,44,()=> { lab.Sandbox=!lab.Sandbox; }).GetComponentInChildren<Text>();
            var panel=Box(content,390,369,1162,387,new Color(.017f,.037f,.06f,.96f));
            var timelineGo=new GameObject("Musical timeline",typeof(RectTransform),typeof(AudioTimeline)); Place(timelineGo,panel.transform,0,0,1162,380);
            Timeline=timelineGo.GetComponent<AudioTimeline>(); Timeline.Controller=lab; Timeline.raycastTarget=true;
            for(int i=0;i<axis.Length;i++) axis[i]=Label(panel.transform,"",Timeline.PlotLeft+Timeline.PlotWidth*i/8-22,3,68,22,11,Muted);
            Label(panel.transform,"WAVE",14,55,80,24,11,Muted,true);
            Label(panel.transform,"RHYTHM",14,123,90,24,11,Mint,true);
            Label(panel.transform,"ATTACKS",14,181,90,24,11,new Color(1,.43f,.49f),true);
            Label(panel.transform,"ENERGY",14,250,90,24,11,Mint,true);
            Label(panel.transform,"L",14,280,20,24,10,new Color(1,.79f,.39f));
            Label(panel.transform,"M",42,280,20,24,10,new Color(.62f,.53f,1));
            Label(panel.transform,"H",70,280,20,24,10,new Color(1,.43f,.49f));
            Label(panel.transform,"STRUCTURE",14,343,95,24,10,Muted,true);
            for(int i=0;i<sectionLabels.Length;i++) sectionLabels[i]=Label(panel.transform,"",0,342,100,24,10,White);
            for(int i=0;i<momentLabels.Length;i++) momentLabels[i]=Label(panel.transform,"",0,217,110,18,10,new Color(1,.79f,.39f));
            cursor=Box(panel.transform,Timeline.PlotLeft,25,2,350,White); cursor.raycastTarget=false;
            empty=Label(panel.transform,"ANALYSE A SONG TO REVEAL ITS TIMELINE\n\nBeats · transients · energy · spectral balance · structural hypotheses",198,116,820,150,20,Muted); empty.alignment=TextAnchor.MiddleCenter;
            string[] layers={"WAVE","BEATS","ONSETS","ENERGY","BANDS","SECTIONS","MOMENTS"};
            for(int i=0;i<layers.Length;i++)
            {
                int layer=i; var b=Button(content,layers[i],390+i*111,326,103,30,()=>ToggleLayer(layer));
                b.GetComponentInChildren<Text>().fontSize=11;
            }
            Button(content,"−",1192,326,40,30,()=>Timeline.Zoom(1.5));
            Button(content,"+",1238,326,40,30,()=>Timeline.Zoom(1/1.5));
            Button(content,"FIT",1284,326,66,30,Timeline.Fit);
            Button(content,"FOLLOW",1360,326,92,30,()=>Timeline.Follow=!Timeline.Follow);
            Button(content,"PAN >",1462,326,90,30,()=> { Timeline.Follow=false; Timeline.SetView(Timeline.StartTime+Timeline.Span*.65,Timeline.Span); });
            inspection=Label(content,"",396,766,1156,27,12,Muted);
            play=Button(content,"PLAY  >",392,806,132,42,lab.TogglePlay,true); playLabel=play.GetComponentInChildren<Text>();
            restart=Button(content,"RESTART",534,806,118,42,lab.Restart);
            position=Label(content,"00:00.000 / 00:00",672,818,280,27,14,White);
            Label(content,"BPM",982,799,60,20,10,Muted); bpm=Field(content,982,820,73,30,"120");
            Label(content,"PHASE (s)",1067,799,110,20,10,Muted); offset=Field(content,1067,820,73,30,"0"); offset.text="0";
            overrideButton=Button(content,"APPLY GRID",1152,820,128,30,()=>lab.OverrideTempo(bpm.text,offset.text));
            estimate=Label(content,"SPACE  play / pause   ·   R  restart   ·   ← / →  seek 5s",392,857,910,23,11,Muted);
            cancel=Button(content,"CANCEL",1424,849,128,30,lab.Cancel);
            status=Label(content,"",50,850,318,39,11,Muted);
            progress=Box(content,48,835,0,3,Mint);
            overviewFill=Box(content,390,791,0,2,Mint);
        }
        public void SetVisible(bool visible) { root.SetActive(visible); }
        public void SetDirectory(string path) { directory.text=path.Replace('\\','/'); }
        public void SetMap(SongMap map,bool reset=true)
        {
            Timeline.SetMap(map,reset); empty.gameObject.SetActive(map==null);
            if(map!=null) { bpm.text=map.EffectiveBpm.ToString("F2",CultureInfo.InvariantCulture); offset.text=map.OverrideOffset.ToString("F3",CultureInfo.InvariantCulture); }
            nextRefresh=0;
        }
        public void RebuildLibrary()
        {
            page=Math.Min(page,Math.Max(0,(lab.Songs.Count-1)/6));
            for(int i=0;i<rows.Length;i++)
            {
                int index=page*6+i; bool exists=index<lab.Songs.Count; rows[i].gameObject.SetActive(exists); if(!exists) continue;
                var song=lab.Songs[index]; bool selected=lab.Selected!=null && song.Identifier==lab.Selected.Identifier;
                rows[i].GetComponent<Image>().color=selected?new Color(.10f,.30f,.32f):new Color(.055f,.105f,.14f);
                string name=song.Title.Length>28?song.Title.Substring(0,26)+"…":song.Title;
                string state=selected && lab.Map!=null?"READY":song.State.ToString().ToUpperInvariant();
                rowTexts[i].text=name+"\n"+(song.Duration>0?Stamp(song.Duration,false)+"   ·   ":"")+(song.Bpm>0?$"{song.Bpm:F1} BPM   ·   ":"")+state;
                rowTexts[i].color=selected?Mint:White;
            }
            pageLabel.text=$"{lab.Songs.Count} SONGS  /  {page+1}";
        }
        public void Refresh()
        {
            double time=lab.Position; Timeline.FollowPosition(time);
            float x=Timeline.XAt(time); cursor.gameObject.SetActive(lab.Map!=null && x>=Timeline.PlotLeft && x<=Timeline.PlotLeft+Timeline.PlotWidth);
            cursor.rectTransform.anchoredPosition=new Vector2(x,-25);
            overviewFill.rectTransform.sizeDelta=new Vector2(lab.Transport.Ready?(float)(time/lab.Transport.Duration)*1162:0,2);
            if(Time.unscaledTime<nextRefresh) return; nextRefresh=Time.unscaledTime+.1f;
            title.text=lab.Selected?.Title??"Choose your frequency.";
            if(title.text.Length>51) title.text=title.text.Substring(0,49)+"…";
            var map=lab.Map;
            metadata.text=map==null?(lab.Selected==null?"Select a song from the library. Your audio stays on this computer.":$"{lab.Selected.FileName}   /   {Stamp(lab.Selected.Duration,false)}   /   {lab.Selected.State}"):
                $"{Stamp(map.Duration,false)}   /   {map.EffectiveBpm:F1} BPM {(map.OverrideBpm>0?"MANUAL":$"· {map.BpmConfidence:P0} confidence")}   /   {map.Beats.Length} beats   /   {map.Onsets.Length} onsets\n"+
                $"Tempo alternatives: {string.Join("  ·  ",map.TempoCandidates.Take(4).Select(c=>$"{c.Bpm:F1} ({c.Score:P0})"))}   /   downbeats: {(map.DownbeatConfidence>=.08f?$"4-beat hypothesis {map.DownbeatConfidence:P0}":"unknown")}";
            position.text=Stamp(time,true)+" / "+Stamp(lab.Transport.Duration,false);
            playLabel.text=lab.Playing?"PAUSE  II":"PLAY  >";
            sandboxLabel.text="VISUAL SANDBOX  "+(lab.Sandbox?"ON   /   SONGMAP → LIGHT":"OFF   /   ENABLE MUSIC REACTIONS");
            sandboxLabel.fontSize=12;
            status.text=lab.Busy?lab.ProgressStage:lab.Status; status.color=lab.Selected?.State==AnalysisState.Error?new Color(1,.43f,.49f):Muted;
            progress.rectTransform.sizeDelta=new Vector2(lab.Busy?Math.Max(3,lab.Progress*306):0,3);
            cancel.gameObject.SetActive(lab.Busy);
            analyse.interactable=!lab.Busy && lab.Transport.Ready && map==null;
            reanalyse.interactable=!lab.Busy && lab.Transport.Ready && map!=null;
            play.interactable=restart.interactable=!lab.Busy && lab.Transport.Ready;
            overrideButton.interactable=map!=null && !lab.Busy;
            inspection.text=Timeline.Inspection;
            estimate.text=$"SPACE play / pause   ·   R restart   ·   ← / → seek   /   {Timeline.Span:F1}s window   /   follow {(Timeline.Follow?"ON":"OFF")}";
            for(int i=0;i<axis.Length;i++) { double at=Timeline.StartTime+Timeline.Span*i/8; axis[i].text=Stamp(at,false)+$".{(int)(at*10)%10}"; }
            int labelIndex=0;
            if(map!=null && Timeline.ShowSections)
                foreach(var section in map.Sections)
                {
                    if(section.End<=Timeline.StartTime || section.Start>=Timeline.StartTime+Timeline.Span || labelIndex>=sectionLabels.Length) continue;
                    float a=Timeline.XAt(Math.Max(section.Start,Timeline.StartTime)),b=Timeline.XAt(Math.Min(section.End,Timeline.StartTime+Timeline.Span));
                    if(b-a<46) continue; var label=sectionLabels[labelIndex++]; label.rectTransform.anchoredPosition=new Vector2(a+7,-342); label.rectTransform.sizeDelta=new Vector2(b-a-12,22);
                    label.text=SectionName(section.Kind); label.color=AudioTimeline.SectionColor(section.Kind);
                }
            for(int i=0;i<sectionLabels.Length;i++) sectionLabels[i].gameObject.SetActive(i<labelIndex);
            int momentIndex=0; float lastMomentX=-100;
            if(map!=null && Timeline.ShowMoments)
                foreach(var moment in map.Moments)
                {
                    if(moment.Time<Timeline.StartTime || moment.Time>Timeline.StartTime+Timeline.Span || momentIndex>=momentLabels.Length) continue;
                    float mx=Timeline.XAt(moment.Time); if(mx-lastMomentX<90 || mx>Timeline.PlotLeft+Timeline.PlotWidth-90) continue;
                    var label=momentLabels[momentIndex++]; label.rectTransform.anchoredPosition=new Vector2(mx+5,-217); label.text=moment.Kind.ToString().ToUpperInvariant()+"?"; lastMomentX=mx;
                }
            for(int i=0;i<momentLabels.Length;i++) momentLabels[i].gameObject.SetActive(i<momentIndex);
        }
        private static string SectionName(SectionKind kind) => kind==SectionKind.HighEnergy?"HIGH ENERGY":kind==SectionKind.LowEnergy?"LOW ENERGY":kind==SectionKind.Build?"BUILD?":"MID ENERGY";
        private void ToggleLayer(int layer)
        {
            switch(layer) { case 0:Timeline.ShowWave=!Timeline.ShowWave;break; case 1:Timeline.ShowBeats=!Timeline.ShowBeats;break; case 2:Timeline.ShowOnsets=!Timeline.ShowOnsets;break; case 3:Timeline.ShowEnergy=!Timeline.ShowEnergy;break; case 4:Timeline.ShowBands=!Timeline.ShowBands;break; case 5:Timeline.ShowSections=!Timeline.ShowSections;break; default:Timeline.ShowMoments=!Timeline.ShowMoments;break; }
            bool enabled=layer==0?Timeline.ShowWave:layer==1?Timeline.ShowBeats:layer==2?Timeline.ShowOnsets:layer==3?Timeline.ShowEnergy:layer==4?Timeline.ShowBands:layer==5?Timeline.ShowSections:Timeline.ShowMoments;
            var selected=EventSystem.current?.currentSelectedGameObject; if(selected!=null && selected.GetComponent<Image>()!=null) selected.GetComponent<Image>().color=enabled?new Color(.085f,.145f,.18f):new Color(.035f,.055f,.07f);
            Timeline.SetVerticesDirty(); nextRefresh=0;
        }
        public static string Stamp(double seconds,bool precise) { seconds=Math.Max(0,seconds); return $"{(int)seconds/60:00}:{(int)seconds%60:00}"+(precise?$".{(int)(seconds*1000)%1000:000}":""); }
        private RectTransform Place(GameObject go,Transform parent,float x,float y,float w,float h)
        { go.transform.SetParent(parent,false); var r=go.GetComponent<RectTransform>(); r.anchorMin=r.anchorMax=new Vector2(0,1); r.pivot=new Vector2(0,1); r.anchoredPosition=new Vector2(x,-y); r.sizeDelta=new Vector2(w,h); return r; }
        private Image Box(Transform parent,float x,float y,float w,float h,Color color)
        { var go=new GameObject("Panel",typeof(RectTransform),typeof(Image)); Place(go,parent,x,y,w,h); var image=go.GetComponent<Image>(); image.color=color; image.raycastTarget=false; return image; }
        private Text Label(Transform parent,string text,float x,float y,float w,float h,int size,Color color,bool bold=false)
        {
            var go=new GameObject("Label",typeof(RectTransform),typeof(Text)); Place(go,parent,x,y,w,h); var label=go.GetComponent<Text>();
            label.text=text; label.font=font; label.fontSize=size; label.color=color; label.raycastTarget=false; label.supportRichText=false;
            label.fontStyle=bold?FontStyle.Bold:FontStyle.Normal; return label;
        }
        private Button Button(Transform parent,string text,float x,float y,float w,float h,Action action,bool primary=false)
        {
            var image=Box(parent,x,y,w,h,primary?Mint:new Color(.085f,.145f,.18f,.94f)); image.gameObject.name=text; image.raycastTarget=true;
            var button=image.gameObject.AddComponent<Button>(); button.targetGraphic=image; button.navigation=new Navigation { mode=Navigation.Mode.None };
            button.onClick.AddListener(()=> { action(); EventSystem.current?.SetSelectedGameObject(null); });
            var label=Label(image.transform,text,10,0,w-20,h,12,primary?Ink:White,true); label.alignment=TextAnchor.MiddleCenter; return button;
        }
        private InputField Field(Transform parent,float x,float y,float w,float h,string hint)
        {
            var image=Box(parent,x,y,w,h,new Color(.035f,.075f,.10f)); image.raycastTarget=true;
            var field=image.gameObject.AddComponent<InputField>(); field.targetGraphic=image; field.navigation=new Navigation { mode=Navigation.Mode.None }; field.characterLimit=1024;
            var text=Label(image.transform,"",9,0,w-18,h,12,White); text.alignment=TextAnchor.MiddleLeft;
            var placeholder=Label(image.transform,hint,9,0,w-18,h,12,Muted); placeholder.alignment=TextAnchor.MiddleLeft;
            field.textComponent=text; field.placeholder=placeholder; return field;
        }
    }
}
