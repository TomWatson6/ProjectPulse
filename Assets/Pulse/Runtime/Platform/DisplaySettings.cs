using UnityEngine;

namespace Pulse.Platform
{
    public enum DisplayMode { Borderless, Windowed }
    public sealed class DisplaySettings
    {
        public DisplayMode Mode { get; private set; }
        public void Initialize(bool forceWindowed)
        {
            Mode=forceWindowed ? DisplayMode.Windowed : (PlayerPrefs.GetInt("pulse.display",0)==1 ? DisplayMode.Windowed : DisplayMode.Borderless);
            QualitySettings.vSyncCount=1; Application.targetFrameRate=144;
            Apply(Mode,!forceWindowed);
        }
        public void Toggle() => Apply(Mode==DisplayMode.Borderless ? DisplayMode.Windowed : DisplayMode.Borderless);
        public void Apply(DisplayMode mode,bool save=true)
        {
            Mode=mode;
            if(!Application.isMobilePlatform)
            {
                if(mode==DisplayMode.Borderless)
                    Screen.SetResolution(Display.main.systemWidth,Display.main.systemHeight,FullScreenMode.FullScreenWindow);
                else Screen.SetResolution(Mathf.Min(1440,Display.main.systemWidth),Mathf.Min(900,Display.main.systemHeight),FullScreenMode.Windowed);
            }
            if(save) PlayerPrefs.SetInt("pulse.display",(int)mode);
        }
    }
}
