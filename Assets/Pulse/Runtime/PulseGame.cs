using System;
using Pulse.Audio;
using Pulse.Domain;
using Pulse.Input;
using Pulse.Platform;
using Pulse.UI;
using Pulse.Visuals;
using UnityEngine;

namespace Pulse
{
    /// <summary>Composition root and session orchestration only; movement, rendering and UI have separate owners.</summary>
    public sealed class PulseGame : MonoBehaviour
    {
        public RunnerSimulation Simulation { get; private set; }
        public RunSession Session { get; } = new RunSession();
        public SongTransport Transport { get; private set; }
        public DisplaySettings Display { get; } = new DisplaySettings();
        public VisualDirector Visuals { get; private set; }
        public PulseHud Hud { get; private set; }
        public LevelDefinition Level { get; private set; }
        public bool Verification { get; private set; }
        public bool ReferenceReplay { get; set; }
        public double LastAdvancePosition { get; private set; }
        public double LastAdvanceDelta { get; private set; }
        private readonly InputTimeline inputs=new InputTimeline();
        private IGameplayInputProvider input;
        private Camera gameCamera;
        private float fps, titleTime;
        private int referenceIndex;
        private bool initialized;

        private void Awake()
        {
            var args=Environment.GetCommandLineArgs();
            Verification=Array.IndexOf(args,"-pulse-verify")>=0;
            Display.Initialize(Verification || Array.IndexOf(args,"-pulse-windowed")>=0);
            var tuning=new RunnerTuning(); Level=LevelDefinition.Afterlight(tuning);
            Simulation=new RunnerSimulation(tuning,Level);
            var cameraObject=new GameObject("Game camera",typeof(Camera),typeof(AudioListener)); cameraObject.transform.SetParent(transform,false);
            gameCamera=cameraObject.GetComponent<Camera>(); gameCamera.orthographic=true; gameCamera.clearFlags=CameraClearFlags.SolidColor;
            gameCamera.backgroundColor=Theme.Cool.Sky; gameCamera.nearClipPlane=.1f; gameCamera.farClipPlane=100;
            gameCamera.tag="MainCamera";
            Transport=gameObject.AddComponent<SongTransport>(); Transport.Initialize(Resources.Load<AudioClip>("Afterlight"));
            Transport.DeviceChanged+=OnDeviceChanged;
            input=Application.isMobilePlatform ? (IGameplayInputProvider)new TouchInputProvider() : new DesktopInputProvider();
            Visuals=new VisualDirector(transform,gameCamera,Level,tuning);
            Hud=new PulseHud(this,transform);
            initialized=true;
            if(!Transport.Ready) Debug.LogError("Afterlight audio is missing or not loaded. Run Project Pulse > Prepare vertical slice.");
            if(Verification) gameObject.AddComponent<Pulse.Verification.PlayerVerification>().Initialize(this);
        }

        public void StartRun()
        {
            if(!Transport.Ready) return;
            Hud.HideSettings();
            inputs.Clear(); referenceIndex=0; Simulation.Reset(); Visuals.Reset();
            LastAdvancePosition=LastAdvanceDelta=0;
            Transport.Restart(); Session.Start();
        }
        public void TogglePause()
        {
            if(Hud.SettingsOpen) { Hud.HideSettings(); return; }
            if(Session.State==RunState.Playing) { Transport.PauseAt(Simulation.Time); Session.Pause(); inputs.Clear(); }
            else if(Session.State==RunState.Paused) { Transport.Resume(); Session.Resume(); inputs.Clear(); }
        }
        public void ReturnToTitle()
        {
            Hud.HideSettings(); Transport.Stop(); Session.ReturnToTitle(); Simulation.Reset(); inputs.Clear(); Visuals.Reset(); titleTime=0;
        }

        private void Update()
        {
            if(!initialized) return;
            fps=Mathf.Lerp(fps,1/Mathf.Max(.0001f,Time.unscaledDeltaTime),.08f);
            if(UnityEngine.Input.GetKeyDown(KeyCode.F11)) Display.Toggle();
            if(UnityEngine.Input.GetKeyDown(KeyCode.F3)) Hud.DebugVisible=!Hud.DebugVisible;
            GameplayActions actions=input.Poll();
            if(actions.Pause) TogglePause();
            if(!Hud.SettingsOpen)
            {
                if(actions.Restart) StartRun();
                else if(actions.PrimaryAction)
                {
                    if(Session.State==RunState.Ready || Session.State==RunState.Dead || Session.State==RunState.Complete) StartRun();
                    else if(Session.State==RunState.Playing) inputs.Enqueue(Transport.Position,Simulation.Tick);
                }
            }
            if(Session.State==RunState.Playing) Advance();
            double time=Simulation.Time;
            if(Session.State==RunState.Playing) time=Math.Min(LevelDefinition.Duration,Transport.Position);
            if(Session.State==RunState.Ready) { titleTime+=Time.unscaledDeltaTime; time=titleTime*.4; }
            // Render interpolation is at most one 240-Hz tick behind the audio clock; collisions stay authoritative.
            PlayerState render=Simulation.Player;
            if(Session.State==RunState.Playing && Simulation.Tick>0)
            {
                float fraction=Mathf.Clamp01((float)((time-Simulation.Time)/RunnerTuning.Step));
                render.X=Simulation.Previous.X+(render.X-Simulation.Previous.X)*fraction;
                render.Y=Simulation.Previous.Y+(render.Y-Simulation.Previous.Y)*fraction;
            }
            Visuals.Render(render,time,Session.State,Time.unscaledTime);
            Hud.Refresh(fps);
        }

        private void Advance()
        {
            double target=Math.Min(LevelDefinition.Duration,Transport.Position);
            // A long stall pauses at a coherent position, instead of fast-forwarding through unseen hazards.
            if(target-Simulation.Time>.25)
            {
                Transport.PauseAt(Simulation.Time); Session.Pause(); inputs.Clear();
                Debug.LogWarning("PULSE_STALL: paused after a frame stall; restart or resume when ready.");
                return;
            }
            long targetTick=(long)Math.Floor(target*RunnerTuning.TickRate+1e-8);
            while(Simulation.Tick<targetTick && !Simulation.Player.Dead && !Simulation.Player.Complete)
            {
                long next=Simulation.Tick+1;
                bool primary=inputs.Consume(next);
                if(ReferenceReplay && referenceIndex<Level.JumpBeats.Count)
                {
                    long cue=(long)Math.Round(Level.JumpBeats[referenceIndex]*LevelDefinition.BeatSeconds*RunnerTuning.TickRate);
                    if(next>=cue) { primary=true; referenceIndex++; }
                }
                Simulation.Step(primary);
                if(Simulation.JumpedThisTick) Visuals.OnJump(Simulation.Player,Time.unscaledTime);
                if(Simulation.LandedThisTick) Visuals.OnLand(Simulation.Player,Time.unscaledTime);
            }
            LastAdvancePosition=target;
            LastAdvanceDelta=Simulation.Time-target;
            if(Simulation.Player.Dead)
            {
                Session.Die(); Transport.PauseAt(Simulation.Time); inputs.Clear();
                Visuals.OnDeath(Simulation.Player,Time.unscaledTime);
            }
            else if(Simulation.Player.Complete) { Session.Complete(); Transport.PauseAt(Simulation.Time); inputs.Clear(); }
        }
        private void OnApplicationFocus(bool focus) { if(initialized && !focus && !Verification && Session.State==RunState.Playing) TogglePause(); }
        private void OnApplicationPause(bool paused) { if(initialized && paused && Session.State==RunState.Playing) TogglePause(); }
        private void OnDeviceChanged()
        {
            if(initialized && Session.State==RunState.Playing) { Transport.PauseAt(Simulation.Time); Session.Pause(); inputs.Clear(); }
        }
        private void OnDestroy() { if(Transport!=null) Transport.DeviceChanged-=OnDeviceChanged; PlayerPrefs.Save(); }
    }
}
