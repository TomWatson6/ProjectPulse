using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Pulse.Domain;
using Pulse.Platform;
using UnityEngine;

namespace Pulse.Verification
{
    /// <summary>Opt-in player smoke test. Normal gameplay never enables reference input.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class PlayerVerification : MonoBehaviour
    {
        private PulseGame game;
        private string output;
        private readonly List<string> checks=new List<string>();
        private readonly List<double> drift=new List<double>();
        private readonly List<double> frameTimes=new List<double>();
        private readonly List<double> observedSimDrift=new List<double>();
        private bool failed;
        private float timeout;
        private double maxSimDrift;
        private readonly float[] audioBuffer=new float[256];
        private bool audioObserved;
        public void Initialize(PulseGame game)
        {
            this.game=game; timeout=Time.realtimeSinceStartup+90;
            var args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,"-pulse-output");
            output=index>=0 && index+1<args.Length ? args[index+1] : Path.Combine(Application.persistentDataPath,"Verification");
            Directory.CreateDirectory(output);
            Application.runInBackground=true;
            Application.logMessageReceived+=OnLog;
            StartCoroutine(Run());
        }
        private void Update()
        {
            if(Time.realtimeSinceStartup>timeout) { Record(false,"Verification timed out"); Finish(); }
            if(game.Session.State==RunState.Playing && game.Transport.Position>1 && game.Transport.Audible)
            {
                double audioDelta=game.Transport.PlaybackPosition-game.Transport.Position;
                drift.Add(audioDelta);
                // Compare against the DSP sample used by Advance, not a later clock read after rendering work.
                maxSimDrift=Math.Max(maxSimDrift,Math.Abs(game.LastAdvanceDelta));
                observedSimDrift.Add(Math.Abs(game.Simulation.Time-game.Transport.Position));
                frameTimes.Add(Time.unscaledDeltaTime);
                AudioListener.GetOutputData(audioBuffer,0);
                for(int i=0;i<audioBuffer.Length;i++) if(Math.Abs(audioBuffer[i])>.0001f) audioObserved=true;
            }
        }
        private IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(1);
            Record(game.Transport.Ready,"Imported PCM audio loaded");
            yield return Capture("01-title.png");
            game.StartRun();
            while(game.Session.State==RunState.Playing) yield return null;
            Record(game.Session.State==RunState.Dead,"No-input run dies on the first spike");
            yield return Capture("02-death.png");
            game.ReferenceReplay=true; game.StartRun();
            Record(game.Simulation.Tick==0 && !game.Simulation.Player.Dead,"Retry resets simulation immediately");
            Record(game.Transport.Position==0,"Retry resets audio clock to zero");
            Record(game.Transport.LeadSeconds<.2,"Retry audio schedule is below 200ms");
            while(game.Simulation.Time<4.05 && game.Session.State==RunState.Playing) yield return null;
            yield return Capture("03-jump.png");
            game.TogglePause(); double paused=game.Simulation.Time;
            yield return new WaitForSecondsRealtime(.35f);
            Record(game.Session.State==RunState.Paused && Math.Abs(game.Transport.Position-paused)<.001,"Pause holds simulation and song time");
            game.Hud.ShowSettings(); yield return Capture("04-settings.png"); game.Hud.HideSettings();
            game.Display.Apply(DisplayMode.Borderless,false);
            yield return new WaitForSecondsRealtime(.4f);
            Record(Screen.fullScreenMode==FullScreenMode.FullScreenWindow,"Borderless fullscreen applied in player");
            game.Display.Apply(DisplayMode.Windowed,false);
            yield return new WaitForSecondsRealtime(.4f);
            Record(Screen.fullScreenMode==FullScreenMode.Windowed,"Windowed mode applied in player");
            Screen.SetResolution(1600,900,FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(.3f);
            game.TogglePause();
            while(game.Simulation.Time<8 && game.Session.State==RunState.Playing) yield return null;
            Record(game.Session.State==RunState.Playing && game.Simulation.Time>paused,"Resumed song and simulation advance");
            yield return Capture("05-flow-16x9.png");
            while(game.Simulation.Time<22.65 && game.Session.State==RunState.Playing) yield return null;
            yield return Capture("06-drop.png");
            while(game.Simulation.Time<24 && game.Session.State==RunState.Playing) yield return null;
            yield return Capture("07-afterlight.png");
            while(game.Simulation.Time<30 && game.Session.State==RunState.Playing) yield return null;
            game.TogglePause();
            game.Visuals.SetQuality(GraphicsTier.Low); Screen.SetResolution(1920,820,FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(.35f);
            game.TogglePause();
            yield return new WaitForSecondsRealtime(.2f);
            yield return Capture("08-ultrawide-low.png");
            game.Visuals.SetQuality(GraphicsTier.High);
            while(game.Session.State==RunState.Playing) yield return null;
            Record(game.Session.State==RunState.Complete,"Full reference replay completes in actual Unity player");
            Record(game.Simulation.JumpCount==game.Level.JumpBeats.Count,"Every authored cue produced one jump");
            Record(audioObserved,"Nonzero music samples reached AudioListener output");
            drift.Sort(); frameTimes.Sort();
            double pcmP95=0, frameP95=0, observedP95=0, observedMax=0;
            if(drift.Count>0)
            {
                var absolute=new List<double>(); foreach(double d in drift) absolute.Add(Math.Abs(d)); absolute.Sort();
                pcmP95=absolute[(int)((absolute.Count-1)*.95)];
                frameP95=frameTimes[(int)((frameTimes.Count-1)*.95)];
                observedSimDrift.Sort();
                observedP95=observedSimDrift[(int)((observedSimDrift.Count-1)*.95)];
                observedMax=observedSimDrift[observedSimDrift.Count-1];
            }
            Record(maxSimDrift<RunnerTuning.Step+1e-6,"Simulation is within one tick of the exact DSP sample it advances to");
            Record(observedP95<.020,"95th percentile post-update simulation/DSP difference below 20ms");
            Record(drift.Count>100 && pcmP95<.05,"95th percentile reported PCM/DSP difference below 50ms");
            AudioSettings.GetDSPBufferSize(out int dspLength,out int dspCount);
            checks.Add($"MEASURE  sampleCount={drift.Count}, maxStepDriftMs={maxSimDrift*1000:F3}, observedSimP95Ms={observedP95*1000:F3}, observedSimMaxMs={observedMax*1000:F3}, pcmP95Ms={pcmP95*1000:F3}, frameP95Ms={frameP95*1000:F3}, scheduleLeadMs={game.Transport.LeadSeconds*1000:F1}, dspBuffer={dspLength}x{dspCount}, sampleRate={AudioSettings.outputSampleRate}");
            yield return Capture("09-complete.png");
            game.StartRun();
            Record(game.Session.State==RunState.Playing && game.Simulation.Tick==0 && game.Simulation.JumpCount==0,"Restart after completion starts a clean attempt");
            Finish();
        }
        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output,name));
            yield return null;
        }
        private void Record(bool condition,string description)
        {
            if(!condition) failed=true;
            string line=(condition?"PASS ":"FAIL ")+description; checks.Add(line); Debug.Log("PULSE_VERIFY "+line);
        }
        private void OnLog(string message,string stack,LogType type)
        {
            if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) { failed=true; checks.Add("ERROR "+message+"\n"+stack); }
        }
        private void Finish()
        {
            StopAllCoroutines();
            File.WriteAllLines(Path.Combine(output,"verification.txt"),checks);
            Debug.Log("PULSE_VERIFY_FINISHED "+(failed?"FAIL":"PASS"));
            enabled=false; Application.logMessageReceived-=OnLog;
            Application.Quit(failed?1:0);
        }
        private void OnDestroy() { Application.logMessageReceived-=OnLog; }
    }
}
