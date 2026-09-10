using System;
using Pulse.Domain;
using UnityEngine;

namespace Pulse.Audio
{
    public sealed class SongTransport : MonoBehaviour
    {
        private AudioSource source;
        public SongClock Clock { get; } = new SongClock();
        public double Position => Clock.Read(AudioSettings.dspTime);
        public double PlaybackPosition => source == null || source.clip == null ? 0 : (double)source.timeSamples/source.clip.frequency;
        public double LeadSeconds { get; private set; }
        public bool Ready => source != null && source.clip != null && source.clip.loadState == AudioDataLoadState.Loaded;
        public bool Audible => source != null && source.isPlaying && AudioSettings.dspTime >= Clock.ScheduledStart;
        public double Duration => source==null || source.clip==null?0:(double)source.clip.samples/source.clip.frequency;
        public event Action DeviceChanged;

        public void Initialize(AudioClip clip)
        {
            source=gameObject.AddComponent<AudioSource>(); source.playOnAwake=false; source.loop=false;
            source.clip=clip; source.spatialBlend=0; source.priority=0;
            source.volume=PlayerPrefs.GetFloat("pulse.volume",.75f);
            if(clip!=null) clip.LoadAudioData();
            RecalculateLead();
            AudioSettings.OnAudioConfigurationChanged+=OnAudioConfigurationChanged;
        }
        private void RecalculateLead()
        {
            AudioSettings.GetDSPBufferSize(out int length,out int count);
            LeadSeconds=Math.Max(.06,(double)length*count/Math.Max(1,AudioSettings.outputSampleRate)+.015);
        }
        public void Restart()
        {
            source.Stop(); source.timeSamples=0;
            Clock.Start(AudioSettings.dspTime,LeadSeconds);
            source.PlayScheduled(Clock.ScheduledStart);
        }
        public void Pause() { PauseAt(Position); }
        public void PauseAt(double position)
        {
            source.Stop();
            source.timeSamples=Mathf.Clamp((int)Math.Round(position*source.clip.frequency),0,source.clip.samples-1);
            Clock.Start(AudioSettings.dspTime,0,position); Clock.Pause(AudioSettings.dspTime);
        }
        public void Resume()
        {
            double position=Position;
            source.Stop();
            source.timeSamples=Mathf.Clamp((int)Math.Round(position*source.clip.frequency),0,source.clip.samples-1);
            Clock.Resume(AudioSettings.dspTime,LeadSeconds);
            source.PlayScheduled(Clock.ScheduledStart);
        }
        public void Stop() { source.Stop(); Clock.Reset(); }
        // Laboratory extension. The authored runner continues to use its original transport operations.
        public void SetClip(AudioClip clip) { Stop(); source.clip=clip; }
        public void Seek(double position,bool play)
        {
            if(!Ready) return;
            position=Math.Max(0,Math.Min(Duration,position));
            // Quantise clock and source to the SAME decoded sample, including paused seeks.
            int sample=Math.Min(source.clip.samples-1,(int)Math.Round(position*source.clip.frequency));
            position=(double)sample/source.clip.frequency;
            PauseAt(position);
            if(play) Resume();
        }
        public void SetVolume(float volume) { source.volume=Mathf.Clamp01(volume); PlayerPrefs.SetFloat("pulse.volume",source.volume); }
        public float Volume => source.volume;
        private void OnAudioConfigurationChanged(bool changed) { RecalculateLead(); DeviceChanged?.Invoke(); }
        private void OnDestroy() { AudioSettings.OnAudioConfigurationChanged-=OnAudioConfigurationChanged; }
    }
}
