using System;
using System.Collections.Generic;

namespace Pulse.Domain
{
    public enum VisualPhase { Arrival, Flow, Ascent, Silence, Afterlight, Drift, Home }
    public readonly struct VisualEvent
    {
        public readonly double Beat;
        public readonly VisualPhase Phase;
        public readonly float Energy;
        public VisualEvent(double beat, VisualPhase phase, float energy) { Beat=beat; Phase=phase; Energy=energy; }
    }

    /// <summary>Absolute, seekable event evaluation; no event callbacks to leak across retries.</summary>
    public sealed class VisualEventTrack
    {
        private readonly VisualEvent[] events;
        public IReadOnlyList<VisualEvent> Events { get; }
        public VisualEventTrack(params VisualEvent[] events)
        {
            if(events==null || events.Length==0 || events[0].Beat!=0) throw new ArgumentException("A visual track must start at beat zero.");
            this.events=(VisualEvent[])events.Clone();
            for(int i=1;i<events.Length;i++) if(events[i].Beat<=events[i-1].Beat) throw new ArgumentException("Events must be strictly ordered.");
            Events=Array.AsReadOnly(this.events);
        }
        public VisualEvent At(double beat)
        {
            int index=0;
            while(index+1<events.Length && events[index+1].Beat<=beat) index++;
            return events[index];
        }
        public static VisualEventTrack Afterlight() => new VisualEventTrack(
            new VisualEvent(0,VisualPhase.Arrival,.30f), new VisualEvent(16,VisualPhase.Flow,.48f),
            new VisualEvent(32,VisualPhase.Ascent,.72f), new VisualEvent(47,VisualPhase.Silence,.12f),
            new VisualEvent(48,VisualPhase.Afterlight,1f), new VisualEvent(64,VisualPhase.Afterlight,.9f),
            new VisualEvent(80,VisualPhase.Drift,.5f), new VisualEvent(88,VisualPhase.Home,.3f));
    }

    public enum GraphicsTier { Low, Medium, High, Ultra }
    public sealed class GraphicsQualityProfile
    {
        public GraphicsTier Tier { get; }
        public int DustCount { get; }
        public int ParticleCapacity { get; }
        public int RingSegments { get; }
        public int MountainLayers { get; }
        public bool Glow { get; }
        public GraphicsQualityProfile(GraphicsTier tier)
        {
            if(!Enum.IsDefined(typeof(GraphicsTier),tier)) throw new ArgumentOutOfRangeException(nameof(tier));
            Tier=tier; int i=(int)tier;
            DustCount=new[]{24,48,80,130}[i]; ParticleCapacity=new[]{48,96,160,260}[i];
            RingSegments=new[]{48,64,96,128}[i]; MountainLayers=new[]{2,3,4,5}[i]; Glow=i>0;
        }
    }
}
