using System;
using Pulse.Domain;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Pulse.UI
{
    public sealed class PulseHud
    {
        private readonly PulseGame game;
        private readonly Font font;
        private readonly RectTransform safe;
        private readonly GameObject title,run,modal,settings,debug;
        private readonly Text progressText,sectionText,attemptText,cueText,modalHeading,modalDetail,debugText;
        private readonly Text displayLabel,qualityLabel,motionLabel,volumeLabel;
        private readonly Image progressFill;
        private readonly Text primaryLabel;
        private readonly Button primaryButton;
        private Rect lastSafe;
        private int lastWidth,lastHeight;
        private float nextDebug;
        private float nextHud;
        private RunState lastState=(RunState)(-1);
        public bool SettingsOpen => settings.activeSelf;
        public void SetVisible(bool visible) { safe.parent.gameObject.SetActive(visible); }
        public bool DebugVisible { get => debug.activeSelf; set => debug.SetActive(value); }
        private static readonly Color Ink=new Color(.018f,.038f,.065f,.97f);
        private static readonly Color Mint=new Color(.50f,1,.88f);
        private static readonly Color Muted=new Color(.48f,.62f,.69f);
        private static readonly Color White=new Color(.91f,.96f,.97f);

        public PulseHud(PulseGame game,Transform parent)
        {
            this.game=game;
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject=new GameObject("Interface",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent,false);
            var canvas=canvasObject.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1600,900); scaler.matchWidthOrHeight=.5f;
            if(EventSystem.current==null)
            {
                var events=new GameObject("UI events",typeof(EventSystem),typeof(StandaloneInputModule)); events.transform.SetParent(parent,false);
            }
            safe=Stretch("Safe area",canvasObject.transform);
            Label(safe,"P U L S E",48,30,240,40,25,White,FontStyle.Bold);
            Label(safe,"M U S I C   /   M O V E M E N T   /   S P E C T A C L E",50,69,560,26,10,Muted);

            title=Stretch("Title",safe).gameObject;
            Label(title.transform,"V O L .  0 1     /     A F T E R L I G H T",78,226,630,28,13,Mint);
            Label(title.transform,"Find your\nfrequency.",72,273,710,174,78,White,FontStyle.Bold);
            Label(title.transform,"One action. Forty-five seconds.\nFollow the light through a world of sound.",78,474,560,72,22,Muted);
            Label(title.transform,"128 BPM     /     ORIGINAL SCORE     /     00:45",78,564,610,25,13,Mint);
            Button(title.transform,"BEGIN RUN   >",78,621,270,62,game.StartRun,true);
            Button(title.transform,"SETTINGS",366,621,178,62,ShowSettings);
            Button(title.transform,"AUDIO LAB",562,621,182,62,() => game.Laboratory.Open());
            Label(title.transform,"SPACE / CLICK  jump       R  restart       ESC  pause",78,714,690,28,14,Muted);
            var edition=Label(title.transform,"RHYTHM RUNNER\nMILESTONE 01",-304,40,250,58,12,Muted);
            Anchor(edition.rectTransform,new Vector2(1,1)); edition.alignment=TextAnchor.UpperRight;
            var footer=Label(title.transform,"A N   O R I G I N A L   S I G N A L",50,-57,600,25,11,Muted);
            Anchor(footer.rectTransform,new Vector2(0,0));
            var quit=Button(title.transform,"QUIT",-156,-73,108,44,() => Application.Quit());
            Anchor((RectTransform)quit.transform,new Vector2(1,0));

            run=Stretch("Run HUD",safe).gameObject;
            sectionText=Label(run.transform,"01 / ARRIVAL",-240,37,480,34,17,Mint);
            Anchor(sectionText.rectTransform,new Vector2(.5f,1)); sectionText.alignment=TextAnchor.MiddleCenter;
            attemptText=Label(run.transform,"ATTEMPT 01",50,109,250,26,12,Muted);
            progressText=Label(run.transform,"00:00 / 00:45",-285,35,188,35,17,White);
            Anchor(progressText.rectTransform,new Vector2(1,1)); progressText.alignment=TextAnchor.MiddleRight;
            var pause=Button(run.transform,"II",-81,30,38,40,game.TogglePause);
            Anchor((RectTransform)pause.transform,new Vector2(1,1));
            var bar=Box(run.transform,"Track progress",new Color(.15f,.24f,.28f),0,0,0,3);
            var br=(RectTransform)bar.transform; br.anchorMin=new Vector2(0,1); br.anchorMax=new Vector2(1,1); br.offsetMin=new Vector2(50,-100); br.offsetMax=new Vector2(-50,-97);
            progressFill=Box(bar.transform,"Progress",Mint,0,0,0,3);
            progressFill.rectTransform.anchorMin=new Vector2(0,0); progressFill.rectTransform.anchorMax=new Vector2(0,1);
            progressFill.rectTransform.offsetMin=progressFill.rectTransform.offsetMax=Vector2.zero;
            cueText=Label(run.transform,"SPACE / CLICK   Jump as you reach a light marker",-430,-68,860,34,16,White);
            Anchor(cueText.rectTransform,new Vector2(.5f,0)); cueText.alignment=TextAnchor.MiddleCenter;

            modal=Stretch("Run menu",safe).gameObject;
            Box(modal.transform,"Dim",new Color(0,.012f,.024f,.63f),0,0,0,0,true);
            var card=Box(modal.transform,"Card",Ink,-300,-210,600,420);
            Anchor(card.rectTransform,new Vector2(.5f,.5f));
            Box(card.transform,"Accent",Mint,0,0,600,3);
            Label(card.transform,"A F T E R L I G H T",38,33,510,25,12,Mint);
            modalHeading=Label(card.transform,"Signal lost.",36,86,535,70,46,White,FontStyle.Bold);
            modalDetail=Label(card.transform,"",39,173,522,65,18,Muted);
            primaryButton=Button(card.transform,"TRY AGAIN   >",38,270,250,57,() => { if(game.Session.State==RunState.Paused) game.TogglePause(); else game.StartRun(); },true);
            primaryLabel=primaryButton.GetComponentInChildren<Text>();
            Button(card.transform,"SETTINGS",308,270,252,57,ShowSettings);
            Button(card.transform,"RETURN TO TITLE",38,346,250,40,game.ReturnToTitle);

            settings=Stretch("Settings",safe).gameObject;
            Box(settings.transform,"Backdrop",new Color(.005f,.012f,.024f,.87f),0,0,0,0,true);
            var settingsCard=Box(settings.transform,"Settings card",Ink,-335,-304,670,608);
            Anchor(settingsCard.rectTransform,new Vector2(.5f,.5f));
            Box(settingsCard.transform,"Accent",Mint,0,0,670,3);
            Label(settingsCard.transform,"YOUR SIGNAL",38,27,580,45,29,White,FontStyle.Bold);
            Label(settingsCard.transform,"Make the light, motion and sound your own.",40,82,590,34,16,Muted);
            Label(settingsCard.transform,"DISPLAY",40,143,220,40,14,Muted);
            displayLabel=Button(settingsCard.transform,"",290,136,340,46,() => { game.Display.Toggle(); UpdateSettings(); }).GetComponentInChildren<Text>();
            Label(settingsCard.transform,"VISUAL QUALITY",40,208,220,40,14,Muted);
            qualityLabel=Button(settingsCard.transform,"",290,201,340,46,() => { game.Visuals.SetQuality((GraphicsTier)(((int)game.Visuals.Quality.Tier+1)%4)); UpdateSettings(); }).GetComponentInChildren<Text>();
            Label(settingsCard.transform,"REDUCED MOTION",40,273,225,40,14,Muted);
            motionLabel=Button(settingsCard.transform,"",290,266,340,46,() => { game.Visuals.ReducedMotion=!game.Visuals.ReducedMotion; PlayerPrefs.SetInt("pulse.reducedMotion",game.Visuals.ReducedMotion?1:0); UpdateSettings(); }).GetComponentInChildren<Text>();
            Label(settingsCard.transform,"MUSIC VOLUME",40,338,220,40,14,Muted);
            Button(settingsCard.transform,"-",290,331,65,46,() => { game.Transport.SetVolume(game.Transport.Volume-.1f); UpdateSettings(); });
            volumeLabel=Label(settingsCard.transform,"75%",365,337,183,40,17,White); volumeLabel.alignment=TextAnchor.MiddleCenter;
            Button(settingsCard.transform,"+",565,331,65,46,() => { game.Transport.SetVolume(game.Transport.Volume+.1f); UpdateSettings(); });
            Label(settingsCard.transform,"F11  Display mode    F3  Diagnostics\nVSync on / 144 FPS fallback cap",40,414,590,59,14,Muted);
            Button(settingsCard.transform,"DONE",40,517,590,53,HideSettings,true);
            settings.SetActive(false);

            debug=Box(safe,"Diagnostics",new Color(.015f,.025f,.045f,.93f),50,155,455,230).gameObject;
            debugText=Label(debug.transform,"",16,12,430,208,15,Mint);
            debug.SetActive(false); run.SetActive(false); modal.SetActive(false);
            RefreshSafeArea();
        }

        public void ShowSettings()
        {
            if(game.Session.State==RunState.Playing) game.TogglePause();
            UpdateSettings(); settings.SetActive(true);
        }
        public void HideSettings() { settings.SetActive(false); PlayerPrefs.Save(); }
        private void UpdateSettings()
        {
            displayLabel.text=game.Display.Mode==Platform.DisplayMode.Borderless?"BORDERLESS  /  F11":"WINDOWED  /  F11";
            qualityLabel.text=game.Visuals.Quality.Tier.ToString().ToUpperInvariant()+"   >";
            motionLabel.text=game.Visuals.ReducedMotion?"ON":"OFF";
            volumeLabel.text=Mathf.RoundToInt(game.Transport.Volume*100)+"%";
        }
        public void Refresh(float fps)
        {
            RefreshSafeArea();
            RunState state=game.Session.State;
            title.SetActive(state==RunState.Ready);
            run.SetActive(state!=RunState.Ready);
            modal.SetActive(state==RunState.Paused || state==RunState.Dead || state==RunState.Complete);
            if(state==RunState.Ready) return;
            double time=game.Simulation.Time;
            float progress=Mathf.Clamp01((float)(time/LevelDefinition.Duration));
            progressFill.rectTransform.anchorMax=new Vector2(progress,1);
            if(Time.unscaledTime<nextHud && state==lastState) return;
            nextHud=Time.unscaledTime+.05f; lastState=state;
            progressText.text=$"00:{(int)time:00} / 00:45";
            sectionText.text=PhaseLabel(game.Visuals.CurrentEvent.Phase);
            sectionText.color=game.Visuals.CurrentTheme.Accent;
            attemptText.text=$"ATTEMPT {game.Session.Attempt:00}   /   {Mathf.FloorToInt(progress*100):00}%";
            if(time<3.5) cueText.text="SPACE / CLICK   Jump as you reach a light marker";
            else if(time<7.5) cueText.text="Coral cuts. Light guides. Keep moving.";
            else if(time>=20.6 && time<22.5) cueText.text="B R E A T H E   I N";
            else if(time>=22.5 && time<24.5) cueText.text="L E T   G O";
            else cueText.text="";
            if(state==RunState.Dead)
            {
                modalHeading.text="Signal lost.";
                modalDetail.text=$"{Mathf.FloorToInt(progress*100)}% of the journey. You know the way a little better.\nSPACE / CLICK or R to restart instantly.";
                primaryLabel.text="TRY AGAIN   >";
            }
            else if(state==RunState.Paused)
            {
                modalHeading.text="Take a breath.";
                modalDetail.text="Your place in the music is waiting.\nESC to resume. R to begin again.";
                primaryLabel.text="RESUME   >";
            }
            else if(state==RunState.Complete)
            {
                modalHeading.text="Through the light.";
                modalDetail.text=$"45 seconds. {game.Simulation.JumpCount} jumps. One complete signal.\nAFTERLIGHT / COMPLETE";
                primaryLabel.text="PLAY AGAIN   >";
            }
            if(debug.activeSelf && Time.unscaledTime>=nextDebug)
            {
                nextDebug=Time.unscaledTime+.1f;
                var p=game.Simulation.Player;
                debugText.text=$"POSITION   {p.X,8:F3}  {p.Y,7:F3}\nGAME       {time,8:F4} s\nSONG DSP   {game.Transport.Position,8:F4} s\nAUDIO PCM  {game.Transport.PlaybackPosition,8:F4} s\nSIM - DSP  {(time-game.Transport.Position)*1000,8:F2} ms\nPCM - DSP  {(game.Transport.PlaybackPosition-game.Transport.Position)*1000,8:F2} ms\nBEAT       {time/LevelDefinition.BeatSeconds,8:F2}\nFRAME      {fps,8:F1} FPS   /   {state}";
            }
        }
        private void RefreshSafeArea()
        {
            if(lastSafe==Screen.safeArea && lastWidth==Screen.width && lastHeight==Screen.height) return;
            lastSafe=Screen.safeArea; lastWidth=Screen.width; lastHeight=Screen.height;
            safe.anchorMin=new Vector2(lastSafe.xMin/Screen.width,lastSafe.yMin/Screen.height);
            safe.anchorMax=new Vector2(lastSafe.xMax/Screen.width,lastSafe.yMax/Screen.height);
            safe.offsetMin=safe.offsetMax=Vector2.zero;
        }
        private static string PhaseLabel(VisualPhase phase)
        {
            switch(phase)
            {
                case VisualPhase.Arrival:return "01   /   ARRIVAL";
                case VisualPhase.Flow:return "02   /   FIND THE FLOW";
                case VisualPhase.Ascent:return "03   /   ASCENT";
                case VisualPhase.Silence:return "03   /   BREATHE";
                case VisualPhase.Afterlight:return "04   /   AFTERLIGHT";
                case VisualPhase.Drift:return "05   /   DRIFT";
                default:return "06   /   HOME";
            }
        }

        private RectTransform Stretch(string name,Transform parent)
        {
            var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform; rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero; return rect;
        }
        private static void Anchor(RectTransform rect,Vector2 anchor) { rect.anchorMin=rect.anchorMax=anchor; }
        private RectTransform Place(GameObject go,Transform parent,float x,float y,float width,float height)
        {
            go.transform.SetParent(parent,false); var rect=go.GetComponent<RectTransform>();
            rect.anchorMin=rect.anchorMax=new Vector2(0,1); rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(x,-y); rect.sizeDelta=new Vector2(width,height); return rect;
        }
        private Text Label(Transform parent,string value,float x,float y,float width,float height,int size,Color color,FontStyle style=FontStyle.Normal)
        {
            var go=new GameObject("Text",typeof(RectTransform),typeof(Text)); Place(go,parent,x,y,width,height);
            var text=go.GetComponent<Text>(); text.text=value; text.font=font; text.fontSize=size; text.color=color;
            text.fontStyle=style; text.raycastTarget=false; text.horizontalOverflow=HorizontalWrapMode.Wrap; text.verticalOverflow=VerticalWrapMode.Truncate;
            return text;
        }
        private Image Box(Transform parent,string name,Color color,float x,float y,float width,float height,bool stretch=false)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image)); var rect=Place(go,parent,x,y,width,height);
            if(stretch) { rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero; }
            var image=go.GetComponent<Image>(); image.color=color; image.raycastTarget=false; return image;
        }
        private Button Button(Transform parent,string value,float x,float y,float width,float height,Action action,bool primary=false)
        {
            var image=Box(parent,value,primary?Mint:new Color(.085f,.145f,.18f,.9f),x,y,width,height);
            image.raycastTarget=true;
            var button=image.gameObject.AddComponent<Button>(); button.targetGraphic=image;
            var colors=button.colors; colors.highlightedColor=new Color(.85f,1,.96f); colors.pressedColor=new Color(.55f,.8f,.76f); button.colors=colors;
            button.navigation=new Navigation { mode=Navigation.Mode.None };
            button.onClick.AddListener(() => action());
            var text=Label(image.transform,value,12,0,width-24,height,15,primary?Ink:White,FontStyle.Bold); text.alignment=TextAnchor.MiddleCenter;
            return button;
        }
    }
}
