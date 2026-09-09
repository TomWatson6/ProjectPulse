using Pulse.Domain;
using UnityEngine;

namespace Pulse.Visuals
{
    public readonly struct Theme
    {
        public readonly Color Sky, Horizon, Accent, Secondary, Hazard;
        public Theme(Color sky,Color horizon,Color accent,Color secondary)
        { Sky=sky; Horizon=horizon; Accent=accent; Secondary=secondary; Hazard=new Color(1,.36f,.40f); }
        private static Color Hex(uint rgb) => new Color(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f);
        public static Theme Cool => new Theme(Hex(0x060E1C),Hex(0x142B45),Hex(0x7EFFE3),Hex(0x609CF9));
        public static Theme Warm => new Theme(Hex(0x190E29),Hex(0x42294C),Hex(0xF3D49B),Hex(0xBC92FF));
        public static Theme Blend(Theme a,Theme b,float t) => new Theme(Color.Lerp(a.Sky,b.Sky,t),Color.Lerp(a.Horizon,b.Horizon,t),Color.Lerp(a.Accent,b.Accent,t),Color.Lerp(a.Secondary,b.Secondary,t));
        public static Theme At(double beat)
        {
            float warm=beat<48 ? 0 : beat<80 ? Mathf.Clamp01((float)(beat-48)/2) : 1-Mathf.Clamp01((float)(beat-80)/8);
            return Blend(Cool,Warm,warm);
        }
    }
}
