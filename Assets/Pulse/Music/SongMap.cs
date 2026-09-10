using System;
using System.Runtime.Serialization;

namespace Pulse.Music
{
    [DataContract] public sealed class TempoCandidate
    {
        [DataMember(Order=0)] public double Bpm;
        [DataMember(Order=1)] public float Score;
    }
    [DataContract] public sealed class Beat
    {
        [DataMember(Order=0)] public double Time;
        [DataMember(Order=1)] public int Index;
        [DataMember(Order=2)] public float Strength;
        [DataMember(Order=3)] public float Confidence;
        // -1 means unknown. Four-beat grouping is only a developer hypothesis, not meter recognition.
        [DataMember(Order=4)] public int BarPosition = -1;
    }
    [DataContract] public sealed class Onset
    {
        [DataMember(Order=0)] public double Time;
        [DataMember(Order=1)] public float Strength;
        [DataMember(Order=2)] public float Confidence;
    }
    [DataContract] public sealed class EnergySample
    {
        [DataMember(Order=0)] public float Intensity;
        [DataMember(Order=1)] public float Rms;
        [DataMember(Order=2)] public float Low;
        [DataMember(Order=3)] public float Mid;
        [DataMember(Order=4)] public float High;
    }
    [DataContract] public sealed class WavePeak
    {
        [DataMember(Order=0)] public float Min;
        [DataMember(Order=1)] public float Max;
    }
    public enum SectionKind { LowEnergy, MediumEnergy, HighEnergy, Build }
    public enum MomentKind { Build, Drop, Breakdown, Transition }
    [DataContract] public sealed class SongSection
    {
        [DataMember(Order=0)] public double Start;
        [DataMember(Order=1)] public double End;
        [DataMember(Order=2)] public SectionKind Kind;
        [DataMember(Order=3)] public float Confidence;
    }
    [DataContract] public sealed class MusicalMoment
    {
        [DataMember(Order=0)] public double Time;
        [DataMember(Order=1)] public MomentKind Kind;
        [DataMember(Order=2)] public float Strength;
        [DataMember(Order=3)] public float Confidence;
    }

    /// <summary>PCM seconds, never frame deltas. Plain serialisable data: no scene, path or engine dependencies.</summary>
    [DataContract] public sealed class SongMap
    {
        public const int CurrentSchema = 1;
        public const string CurrentAlgorithm = "pulse-spectral-3";
        public const string CurrentSettings = "fft2048-hop256-mono22050-energy20-wave80";
        [DataMember(Order=0)] public int SchemaVersion = CurrentSchema;
        [DataMember(Order=1)] public string AlgorithmVersion = CurrentAlgorithm;
        [DataMember(Order=2)] public string SettingsId = CurrentSettings;
        [DataMember(Order=3)] public string AudioHash;
        [DataMember(Order=4)] public double Duration;
        [DataMember(Order=5)] public int SampleRate;
        [DataMember(Order=6)] public int Channels;
        [DataMember(Order=7)] public double EstimatedBpm;
        [DataMember(Order=8)] public float BpmConfidence;
        [DataMember(Order=9)] public TempoCandidate[] TempoCandidates = Array.Empty<TempoCandidate>();
        [DataMember(Order=10)] public double OverrideBpm;
        [DataMember(Order=11)] public double OverrideOffset;
        [DataMember(Order=12)] public Beat[] Beats = Array.Empty<Beat>();
        [DataMember(Order=13)] public Onset[] Onsets = Array.Empty<Onset>();
        [DataMember(Order=14)] public double EnergyStep = .05;
        [DataMember(Order=15)] public EnergySample[] Energy = Array.Empty<EnergySample>();
        [DataMember(Order=16)] public double WaveStep = .0125;
        [DataMember(Order=17)] public WavePeak[] Waveform = Array.Empty<WavePeak>();
        [DataMember(Order=18)] public SongSection[] Sections = Array.Empty<SongSection>();
        [DataMember(Order=19)] public MusicalMoment[] Moments = Array.Empty<MusicalMoment>();
        [DataMember(Order=20)] public float DownbeatConfidence;
        public double EffectiveBpm => OverrideBpm > 0 ? OverrideBpm : EstimatedBpm;

        // Current is inclusive; next is strictly later. Before the first beat there is no current beat.
        public int CurrentBeatIndex(double time)
        {
            int lo=0, hi=Beats.Length;
            while(lo<hi) { int mid=(lo+hi)/2; if(Beats[mid].Time<=time) lo=mid+1; else hi=mid; }
            return lo-1;
        }
        public Beat CurrentBeat(double time) { int i=CurrentBeatIndex(time); return i<0?null:Beats[i]; }
        public Beat NextBeat(double time) { int i=CurrentBeatIndex(time)+1; return i<Beats.Length?Beats[i]:null; }
        public Beat NearestBeat(double time)
        {
            if(Beats.Length==0) return null;
            int i=CurrentBeatIndex(time);
            if(i<0) return Beats[0];
            if(i==Beats.Length-1) return Beats[i];
            return time-Beats[i].Time<=Beats[i+1].Time-time?Beats[i]:Beats[i+1];
        }
        /// <summary>Interpolated hypothesis between tracked beats. Null when rhythm confidence is insufficient.</summary>
        public double? NearestSubdivision(double time,int divisions=2)
        {
            if(divisions<1 || divisions>16) throw new ArgumentOutOfRangeException(nameof(divisions));
            if(Beats.Length<2 || (OverrideBpm==0 && BpmConfidence<.25f)) return null;
            int i=Math.Max(0,Math.Min(Beats.Length-2,CurrentBeatIndex(time)));
            double start=Beats[i].Time, step=(Beats[i+1].Time-start)/divisions;
            int k=Math.Max(0,Math.Min(divisions,(int)Math.Round((time-start)/step)));
            return start+k*step;
        }
        public SongSection SectionAt(double time)
        {
            int lo=0,hi=Sections.Length;
            while(lo<hi) { int m=(lo+hi)/2; if(Sections[m].Start<=time) lo=m+1; else hi=m; }
            return lo>0 && time<Sections[lo-1].End?Sections[lo-1]:null;
        }
        public float IntensityAt(double time)
        {
            if(Energy.Length==0) return 0;
            double pos=Math.Max(0,Math.Min(Energy.Length-1,time/EnergyStep)); int a=(int)pos,b=Math.Min(a+1,Energy.Length-1);
            return Energy[a].Intensity+(Energy[b].Intensity-Energy[a].Intensity)*(float)(pos-a);
        }
        public int NextOnsetIndex(double time)
        {
            int lo=0,hi=Onsets.Length;
            while(lo<hi) { int m=(lo+hi)/2; if(Onsets[m].Time<=time) lo=m+1; else hi=m; } return lo;
        }
        public void ApplyTempoOverride(double bpm,double offset)
        {
            if(!Finite(bpm) || bpm<40 || bpm>300 || !Finite(offset) || offset<0 || offset>=60/bpm)
                throw new ArgumentOutOfRangeException("Override: BPM must be 40–300; offset must be within one beat.");
            OverrideBpm=bpm; OverrideOffset=offset; DownbeatConfidence=0;
            int count=Math.Max(0,(int)Math.Ceiling((Duration-offset)/(60/bpm)));
            Beats=new Beat[count];
            for(int i=0;i<count;i++) Beats[i]=new Beat { Index=i,Time=offset+i*(60/bpm),Confidence=0,Strength=IntensityAt(offset+i*(60/bpm)),BarPosition=-1 };
        }
        public bool IsValid(string hash)
        {
            if(SchemaVersion!=CurrentSchema || AlgorithmVersion!=CurrentAlgorithm || SettingsId!=CurrentSettings || AudioHash!=hash ||
               !Finite(Duration) || Duration<=0 || Duration>1200 || SampleRate<8000 || SampleRate>192000 || Channels<1 || Channels>8 ||
               !Finite(EstimatedBpm) || EstimatedBpm<0 || EstimatedBpm>300 || !Unit(BpmConfidence) || !Unit(DownbeatConfidence) ||
               !Finite(OverrideBpm) || (OverrideBpm!=0 && (OverrideBpm<40 || OverrideBpm>300)) || !Finite(OverrideOffset) || OverrideOffset<0 ||
               (OverrideBpm>0 && OverrideOffset>=60/OverrideBpm) || EnergyStep!=.05 || WaveStep!=.0125 ||
               Beats==null || Onsets==null || Energy==null || Waveform==null || Sections==null || Moments==null || TempoCandidates==null ||
               Beats.Length>10000 || Onsets.Length>24000 || Energy.Length!=(int)Math.Ceiling(Duration/EnergyStep) || Waveform.Length!=(int)Math.Ceiling(Duration/WaveStep)) return false;
            double previous=-1;
            for(int i=0;i<Beats.Length;i++) { var b=Beats[i]; if(b==null || !Time(b.Time,previous) || b.Index!=i || !Unit(b.Strength) || !Unit(b.Confidence) || b.BarPosition< -1 || b.BarPosition>3) return false; previous=b.Time; }
            previous=-1;
            foreach(var o in Onsets) { if(o==null || !Time(o.Time,previous) || !Unit(o.Strength) || !Unit(o.Confidence)) return false; previous=o.Time; }
            foreach(var e in Energy) if(e==null || !Unit(e.Intensity) || !Unit(e.Rms) || !Unit(e.Low) || !Unit(e.Mid) || !Unit(e.High)) return false;
            foreach(var w in Waveform) if(w==null || !Finite(w.Min) || !Finite(w.Max) || w.Min< -1 || w.Max>1 || w.Min>w.Max) return false;
            previous=0;
            foreach(var s in Sections) { if(s==null || !Finite(s.Start) || !Finite(s.End) || Math.Abs(s.Start-previous)>1e-6 || s.End<=s.Start || s.End>Duration || !Unit(s.Confidence) || !Enum.IsDefined(typeof(SectionKind),s.Kind)) return false; previous=s.End; }
            if(Sections.Length==0 || Math.Abs(previous-Duration)>1e-6) return false;
            previous=-1;
            foreach(var m in Moments) { if(m==null || !Time(m.Time,previous) || !Unit(m.Strength) || !Unit(m.Confidence) || !Enum.IsDefined(typeof(MomentKind),m.Kind)) return false; previous=m.Time; }
            foreach(var c in TempoCandidates) if(c==null || !Finite(c.Bpm) || c.Bpm<40 || c.Bpm>300 || !Unit(c.Score)) return false;
            return true;
        }
        private bool Time(double t,double previous) => Finite(t) && t>=0 && t<Duration && t>previous;
        public static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        private static bool Unit(float x) => Finite(x) && x>=0 && x<=1;
    }
}
