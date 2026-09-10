using System;
using System.Collections.Generic;
using System.Threading;

namespace Pulse.Music
{
    /// <summary>Replaceable conservative energy heuristics, NOT semantic verse/chorus or learned EDM recognition.</summary>
    public static class StructureAnalyser
    {
        public static void Populate(SongMap map,CancellationToken cancellation)
        {
            int n=(int)Math.Ceiling(map.Duration); var energy=new double[n]; var low=new double[n];
            for(int s=0;s<n;s++)
            {
                cancellation.ThrowIfCancellationRequested(); int count=0;
                for(int j=(int)(s/map.EnergyStep);j<map.Energy.Length && j*map.EnergyStep<s+1;j++) { energy[s]+=map.Energy[j].Intensity; low[s]+=map.Energy[j].Low; count++; }
                energy[s]/=Math.Max(1,count); low[s]/=Math.Max(1,count);
            }
            var sections=new List<SongSection>(); var moments=new List<MusicalMoment>();
            var ranked=(double[])energy.Clone(); Array.Sort(ranked);
            double quiet=ranked[(int)((n-1)*.2)],loud=ranked[(int)((n-1)*.85)],range=loud-quiet;
            double lowThreshold=range>.12?quiet+range*.18:.32,highThreshold=range>.12?quiet+range*.68:.68;
            SectionKind current=Kind(energy[0],lowThreshold,highThreshold),pendingKind=current; double start=0; int pending=0; double lastMoment=-10;
            for(int s=1;s<n;s++)
            {
                double before=Average(energy,s-3,s),after=Average(energy,s,s+3),difference=after-before;
                double lowDifference=Average(low,s,s+3)-Average(low,s-3,s);
                bool building=s>=3 && s+2<n && difference>.12 && energy[s]-energy[s-3]>.12 && energy[Math.Min(n-1,s+2)]>=energy[s] && Math.Abs(energy[s]-energy[s-1])<.16;
                SectionKind target=building?SectionKind.Build:Kind(energy[s],lowThreshold,highThreshold);
                if(target!=current) { if(target!=pendingKind) pending=0; pendingKind=target; pending++; } else pending=0;
                if(pending>=2)
                {
                    double boundary=s-1;
                    sections.Add(new SongSection { Start=start,End=boundary,Kind=current,Confidence=current==SectionKind.Build?.5f:.7f });
                    current=target; start=boundary; pending=0;
                }
                if(s+2>=n) continue;
                MomentKind? moment=null; double strength=Math.Abs(difference);
                if(difference>.16 && after>before*1.25 && lowDifference>.05 && after>.5 && energy[s]-energy[s-1]>.08) moment=MomentKind.Drop;
                else if(difference< -.22 && before>.45 && after<before*.75) moment=MomentKind.Breakdown;
                else if(building) { moment=MomentKind.Build; strength=Math.Max(.2,difference); }
                else if(Math.Abs(difference)>.28) moment=MomentKind.Transition;
                if(moment.HasValue)
                {
                    // A build must not suppress its own release through the refractory window.
                    bool releaseAfterBuild=moments.Count>0 && moments[moments.Count-1].Kind==MomentKind.Build && moment!=MomentKind.Build;
                    if(s-lastMoment<(releaseAfterBuild?2:4)) continue;
                    // Snap abrupt changes to the strongest nearby energy derivative, then a nearby attack.
                    double time=s;
                    if(moment!=MomentKind.Build)
                    {
                        float best=0;
                        for(int j=Math.Max(1,(int)((s-.6)/map.EnergyStep));j<map.Energy.Length && j*map.EnergyStep<=s+.6;j++)
                        { float d=Math.Abs(map.Energy[j].Intensity-map.Energy[j-1].Intensity); if(d>best) { best=d; time=j*map.EnergyStep; } }
                        int i=map.NextOnsetIndex(time-.12); if(i<map.Onsets.Length && Math.Abs(map.Onsets[i].Time-time)<.15) time=map.Onsets[i].Time;
                    }
                    moments.Add(new MusicalMoment { Time=time,Kind=moment.Value,Strength=AudioAnalyser.Clamp(strength*1.5),Confidence=AudioAnalyser.Clamp(.3+strength*.6) }); lastMoment=s;
                }
            }
            sections.Add(new SongSection { Start=start,End=map.Duration,Kind=current,Confidence=current==SectionKind.Build?.5f:.7f });
            map.Sections=sections.ToArray(); map.Moments=moments.ToArray();
        }
        private static SectionKind Kind(double energy,double low,double high) => energy<low?SectionKind.LowEnergy:energy>high?SectionKind.HighEnergy:SectionKind.MediumEnergy;
        private static double Average(double[] a,int start,int end) { start=Math.Max(0,start); end=Math.Min(a.Length,end); double v=0; for(int i=start;i<end;i++) v+=a[i]; return v/Math.Max(1,end-start); }
    }
}
