using Pulse.Domain;
using Pulse.Rendering;
using Pulse.World;
using UnityEngine;

namespace Pulse.Visuals
{
    public sealed class VisualDirector
    {
        public CameraDirector Camera { get; }
        public GraphicsQualityProfile Quality { get; private set; }
        public bool ReducedMotion { get; set; }
        public VisualEvent CurrentEvent { get; private set; }
        public Theme CurrentTheme { get; private set; }
        private readonly VisualEventTrack track=VisualEventTrack.Afterlight();
        private readonly EnvironmentDirector environment;
        private readonly LevelRenderer world;
        private readonly MeshCanvas foreground;
        private readonly ParticleField particles=new ParticleField();
        private readonly Vector2[] trail=new Vector2[32];
        private readonly Vector2[] playerOutline=new Vector2[8];
        private int trailCount, trailCursor;
        private float lastTrailTime;
        private int lastBeat=-1;
        public VisualDirector(Transform parent,Camera camera,LevelDefinition level,RunnerTuning tuning)
        {
            Camera=new CameraDirector(camera); environment=new EnvironmentDirector(parent);
            world=new LevelRenderer(parent,level,tuning); foreground=MeshCanvas.Create("Runner and sparks",20,parent);
            int tier=Mathf.Clamp(PlayerPrefs.GetInt("pulse.quality",2),0,3);
            Quality=new GraphicsQualityProfile((GraphicsTier)tier);
            ReducedMotion=PlayerPrefs.GetInt("pulse.reducedMotion",0)==1;
        }
        public void SetQuality(GraphicsTier tier) { Quality=new GraphicsQualityProfile(tier); particles.Clear(); PlayerPrefs.SetInt("pulse.quality",(int)tier); }
        public void Reset() { particles.Clear(); trailCount=trailCursor=0; lastTrailTime=-1; lastBeat=-1; }
        public void OnJump(PlayerState player,float now) => particles.Burst(new Vector2((float)player.X,(float)player.Y-.3f),Theme.Cool.Accent,8,now,Quality.ParticleCapacity);
        public void OnLand(PlayerState player,float now) => particles.Burst(new Vector2((float)player.X,(float)player.Y-.3f),CurrentTheme.Accent,12,now,Quality.ParticleCapacity,.65f);
        public void OnDeath(PlayerState player,float now) => particles.Burst(new Vector2((float)player.X,(float)player.Y),CurrentTheme.Hazard,Quality.ParticleCapacity/3,now,Quality.ParticleCapacity,2);

        public void Render(PlayerState player,double time,RunState state,float now)
        {
            double beat=time/LevelDefinition.BeatSeconds;
            CurrentEvent=track.At(beat); CurrentTheme=Theme.At(beat);
            float pulse=Mathf.Exp(-(float)(beat-System.Math.Floor(beat))*5);
            if(CurrentEvent.Phase==VisualPhase.Silence) pulse*=.15f;
            float x=(float)player.X;
            Camera.Evaluate(x,beat,CurrentEvent.Energy,ReducedMotion);
            environment.Render(Camera,x,beat,CurrentTheme,CurrentEvent.Energy,pulse,Quality,ReducedMotion);
            world.Render(Camera,time,CurrentTheme,pulse,Quality.Glow);

            if(state==RunState.Playing && (int)beat!=lastBeat)
            {
                if((int)beat==48)
                    particles.Burst(new Vector2(x+9,4),CurrentTheme.Accent,Quality.ParticleCapacity/2,now,Quality.ParticleCapacity,3);
                lastBeat=(int)beat;
            }
            Vector2 p=new Vector2(x,(float)player.Y);
            bool alive=state!=RunState.Dead && state!=RunState.Ready;
            if(state==RunState.Playing && now-lastTrailTime>.008f)
            {
                trail[trailCursor]=p; trailCursor=(trailCursor+1)%trail.Length;
                trailCount=Mathf.Min(trailCount+1,trail.Length); lastTrailTime=now;
            }
            foreground.Begin();
            for(int i=1;i<trailCount;i++)
            {
                int a=(trailCursor-i+trail.Length)%trail.Length, b=(trailCursor-i-1+trail.Length)%trail.Length;
                float fade=1-(float)i/trailCount;
                Color c=CurrentTheme.Accent; c.a=fade*.38f;
                foreground.Line(trail[a],trail[b],fade*.19f,c);
            }
            if(alive)
            {
                if(Quality.Glow) { Color g=CurrentTheme.Accent; g.a=.26f; foreground.Glow(p,.85f,g); }
                // Original capsule: a bevelled kinetic instrument with a bright central lens.
                float rotation=player.Grounded?0:Mathf.Clamp01((12.8f-(float)player.VelocityY)/25.6f)*Mathf.PI;
                Vector2 Rotate(float px,float py) => p+new Vector2(px*Mathf.Cos(rotation)-py*Mathf.Sin(rotation),px*Mathf.Sin(rotation)+py*Mathf.Cos(rotation));
                var points=playerOutline;
                points[0]=Rotate(-.22f,-.34f); points[1]=Rotate(.22f,-.34f); points[2]=Rotate(.30f,-.24f); points[3]=Rotate(.30f,.24f);
                points[4]=Rotate(.22f,.34f); points[5]=Rotate(-.22f,.34f); points[6]=Rotate(-.30f,.24f); points[7]=Rotate(-.30f,-.24f);
                for(int i=0;i<8;i++)
                {
                    foreground.Triangle(p,points[i],points[(i+1)%8],new Color(.045f,.16f,.20f));
                    foreground.Line(points[i],points[(i+1)%8],.036f,CurrentTheme.Accent);
                }
                foreground.Ring(p,.13f,.03f,new Color(.9f,1,1),16);
                foreground.Line(Rotate(.12f,0),Rotate(.22f,0),.032f,Color.white);
                foreground.Glow(p,.09f,Color.white);
            }
            particles.Render(foreground,now,Quality.ParticleCapacity,Quality.Glow);
            foreground.End();
        }
    }
}
