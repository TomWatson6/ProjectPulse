using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Pulse.Laboratory
{
    /// <summary>Opt-in real-player integration checks; never present in the normal lab workflow.</summary>
    public sealed class AudioLabVerification : MonoBehaviour
    {
        private AudioLabController lab;
        private PulseGame game;
        private string output;
        private readonly List<string> checks=new List<string>();
        private bool failed,finished,expectCache;
        private float deadline,maxFrame,analysisStart;
        private readonly float[] outputSamples=new float[256];
        public void Initialize(AudioLabController controller,PulseGame owner)
        {
            lab=controller; game=owner; var args=Environment.GetCommandLineArgs(); int i=Array.IndexOf(args,"-pulse-output");
            output=i>=0?args[i+1]:Path.Combine(Application.persistentDataPath,"LabVerification"); Directory.CreateDirectory(output);
            expectCache=Array.IndexOf(args,"-pulse-expect-cache")>=0; deadline=Time.realtimeSinceStartup+180;
            Application.runInBackground=true; Application.logMessageReceived+=OnLog;
            StartCoroutine(Run());
        }
        private void Update()
        {
            if(finished) return;
            if(lab.Busy) maxFrame=Math.Max(maxFrame,Time.unscaledDeltaTime);
            if(Time.realtimeSinceStartup>deadline) { Record(false,"Laboratory timed out: "+lab.Status); Finish(); }
        }
        private IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(.5f);
            Screen.SetResolution(1600,900,FullScreenMode.Windowed); lab.Open();
            while(lab.Busy) yield return null;
            Record(lab.Songs.Count>=2,"WAV and MP3 discovered without scene configuration");
            var wav=lab.Songs.FirstOrDefault(s=>s.FileName.EndsWith(".wav",StringComparison.OrdinalIgnoreCase));
            if(wav==null) { Record(false,"WAV fixture missing"); Finish(); yield break; }
            lab.Select(wav); while(lab.Busy) yield return null;
            Record(lab.Transport.Ready,"Local WAV decoded to playable PCM");
            if(!lab.Transport.Ready) { Finish(); yield break; }
            if(expectCache) Record(lab.LastLoadWasCached && lab.AnalysisRuns==0,"Fresh process reused WAV cache without analysis");
            if(lab.Map==null) { analysisStart=Time.realtimeSinceStartup; lab.Analyse(); while(lab.Busy) yield return null; checks.Add($"WAV analysis seconds: {Time.realtimeSinceStartup-analysisStart:F3}"); }
            Record(lab.Map!=null && lab.Map.IsValid(wav.AudioHash),"WAV SongMap generated and validated");
            if(lab.Map==null) { checks.Add(lab.Status); Finish(); yield break; }
            Record(Math.Abs(lab.Map.EstimatedBpm-128)<1,"Original-score tempo resolves to 128 BPM");
            Record(lab.Map.Moments.Any(m=>m.Kind==Pulse.Music.MomentKind.Drop && Math.Abs(m.Time-22.5)<1),"Original-score main release detected near 22.5 seconds");
            checks.Add($"WAV estimate: {lab.Map.EstimatedBpm:F3} BPM / confidence {lab.Map.BpmConfidence:P1}; beats {lab.Map.Beats.Length}; onsets {lab.Map.Onsets.Length}; moments "+string.Join(", ",lab.Map.Moments.Select(m=>$"{m.Kind}@{m.Time:F2}")));
            lab.View.Timeline.Fit(); yield return Capture("01-lab-overview.png");
            lab.Sandbox=true; lab.View.Timeline.SetView(14,20); lab.Seek(Math.Min(23,lab.Transport.Duration*.5)); lab.TogglePlay();
            yield return new WaitForSecondsRealtime(.4f);
            Record(lab.Playing && lab.Transport.Audible,"Imported song reaches scheduled playback");
            Record(Math.Abs(lab.Transport.PlaybackPosition-lab.Position)<.02,"WAV playback cursor agrees with PCM within 20ms");
            bool heard=false;
            for(int k=0;k<40;k++) { AudioListener.GetOutputData(outputSamples,0); if(outputSamples.Any(v=>Math.Abs(v)>.0001f)) heard=true; yield return null; }
            Record(heard,"Imported audio reaches AudioListener output"); yield return Capture("02-lab-playing.png");
            lab.Pause(); double paused=lab.Position; yield return new WaitForSecondsRealtime(.25f);
            Record(Math.Abs(lab.Position-paused)<.0001,"Pause holds DSP cursor");
            lab.Seek(3.14159); Record(Math.Abs(lab.Position-lab.Transport.PlaybackPosition)<.0001,"Paused scrub quantises clock and PCM to same sample");
            lab.BeginScrub(); lab.Scrub(7.123); lab.EndScrub(); Record(!lab.Playing && Math.Abs(lab.Position-7.123)<.001,"Paused drag retains paused state");
            lab.TogglePlay(); yield return new WaitForSecondsRealtime(.15f);
            lab.BeginScrub(); lab.Scrub(12.345); lab.EndScrub(); yield return new WaitForSecondsRealtime(.25f);
            Record(lab.Playing && Math.Abs(lab.Transport.PlaybackPosition-lab.Position)<.02,"Playing drag resumes in sync after seek");
            lab.Restart(); Record(lab.Position<.001,"Restart resets imported song clock"); lab.Pause();
            int runs=lab.AnalysisRuns; lab.Close();
            Record(!lab.IsOpen && lab.Map==null && !lab.Transport.Ready,"Close releases map, audio clip and lab ownership");
            lab.Open(); while(lab.Busy) yield return null;
            lab.Select(lab.Songs.First(s=>s.Identifier==wav.Identifier)); while(lab.Busy) yield return null;
            Record(lab.LastLoadWasCached && lab.AnalysisRuns==runs,"Reopen reuses valid cached SongMap");
            var mp3=lab.Songs.FirstOrDefault(s=>s.FileName.EndsWith(".mp3",StringComparison.OrdinalIgnoreCase));
            if(mp3!=null)
            {
                lab.Select(mp3); while(lab.Busy) yield return null;
                Record(lab.Transport.Ready,"Local MP3 decoded to readable PCM");
                if(expectCache) Record(lab.LastLoadWasCached && lab.AnalysisRuns==0,"Fresh process reused MP3 cache without analysis");
                if(lab.Map==null) { lab.Analyse(); while(lab.Busy) yield return null; }
                Record(lab.Map!=null && lab.Map.IsValid(mp3.AudioHash),"MP3 SongMap produced on decoded PCM timeline");
                if(lab.Map!=null)
                {
                    lab.Seek(Math.Min(8.5,lab.Transport.Duration/2)); lab.TogglePlay(); yield return new WaitForSecondsRealtime(.3f);
                    Record(Math.Abs(lab.Transport.PlaybackPosition-lab.Position)<.02,"MP3 seek retains clock/PCM synchronisation");
                    lab.View.Timeline.SetView(0,12); yield return Capture("03-mp3-timeline.png"); lab.Pause();
                    Screen.SetResolution(1920,820,FullScreenMode.Windowed); yield return new WaitForSecondsRealtime(.3f); yield return Capture("04-lab-ultrawide.png");
                    lab.Seek(lab.Transport.Duration-.1); lab.TogglePlay(); yield return new WaitForSecondsRealtime(.4f);
                    Record(!lab.Playing,"End of song stops authoritative cursor");
                }
            }
            checks.Add($"Largest frame while scanning/analysing: {maxFrame*1000:F2}ms (includes decode, allocation and cache reads)");
            lab.Close(); Record(game.Session.State==Pulse.Domain.RunState.Ready,"Returns to intact Afterlight title");
            yield return Capture("05-return-to-title.png"); Finish();
        }
        private IEnumerator Capture(string name) { yield return new WaitForSecondsRealtime(.12f); yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(output,name)); yield return null; }
        private void Record(bool ok,string message) { checks.Add((ok?"PASS  ":"FAIL  ")+message); if(!ok) failed=true; Debug.Log(checks[checks.Count-1]); }
        private void OnLog(string message,string stack,LogType type) { if(type==LogType.Exception || type==LogType.Error || type==LogType.Assert) { failed=true; checks.Add("ERROR "+message+"\n"+stack); } }
        private void Finish() { if(finished) return; finished=true; Application.logMessageReceived-=OnLog; File.WriteAllLines(Path.Combine(output,"verification.txt"),checks); Application.Quit(failed?1:0); }
    }
}
