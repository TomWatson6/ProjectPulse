using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Pulse.Music
{
    public sealed class AnalysisProgress
    {
        public readonly float Fraction;
        public readonly string Stage;
        public AnalysisProgress(float fraction,string stage) { Fraction=fraction; Stage=stage; }
    }
    public interface IAudioAnalyser
    {
        SongMap Analyse(float[] mono,int rate,int sourceRate,int channels,string hash,CancellationToken cancellation,Action<AnalysisProgress> progress=null);
    }

    /// <summary>Original deterministic spectral-flux / periodicity analyser. All work may run off the Unity thread.</summary>
    public sealed class AudioAnalyser : IAudioAnalyser
    {
        public const int AnalysisRate=22050;
        private const int Size=2048, Hop=256;
        public SongMap Analyse(float[] mono,int rate,int sourceRate,int channels,string hash,CancellationToken cancellation,Action<AnalysisProgress> progress=null)
        {
            if(mono==null || rate!=AnalysisRate || mono.Length<rate/10 || mono.Length>(long)rate*1200 || sourceRate<8000 || sourceRate>192000 || channels<1 || channels>8 || string.IsNullOrEmpty(hash))
                throw new ArgumentException("Analysis requires 0.1–1200 seconds of 22050 Hz mono PCM and valid source metadata.");
            cancellation.ThrowIfCancellationRequested();
            var map=new SongMap { AudioHash=hash,Duration=(double)mono.Length/rate,SampleRate=sourceRate,Channels=channels };
            int frames=(mono.Length+Hop-1)/Hop;
            var novelty=new double[frames]; var low=new double[frames]; var mid=new double[frames]; var high=new double[frames];
            var real=new double[Size]; var imaginary=new double[Size]; var previous=new double[Size/2+1]; var window=new double[Size];
            for(int i=0;i<Size;i++) window[i]=.5-.5*Math.Cos(2*Math.PI*i/(Size-1));
            for(int f=0;f<frames;f++)
            {
                if(f%64==0) { cancellation.ThrowIfCancellationRequested(); progress?.Invoke(new AnalysisProgress(.05f+.50f*f/frames,"Reading spectral motion")); }
                int start=f*Hop-Size/2;
                for(int j=0;j<Size;j++)
                {
                    int k=start+j; float sample=k<0 || k>=mono.Length?0:mono[k];
                    if(!SongMap.Finite(sample)) throw new ArgumentException("PCM contains non-finite samples.");
                    real[j]=Math.Max(-1,Math.Min(1,sample))*window[j]; imaginary[j]=0;
                }
                Fft(real,imaginary);
                double flux=0;
                for(int b=1;b<=Size/2;b++)
                {
                    double power=(real[b]*real[b]+imaginary[b]*imaginary[b])/(Size*Size);
                    double hz=(double)b*rate/Size;
                    if(hz<250) low[f]+=power; else if(hz<4000) mid[f]+=power; else high[f]+=power;
                    double value=Math.Log(1+1000*Math.Sqrt(power));
                    flux+=Math.Max(0,value-previous[b]); previous[b]=value;
                }
                novelty[f]=flux;
            }
            progress?.Invoke(new AnalysisProgress(.57f,"Finding attacks and pulse"));
            // Subtract a local mean so sustained spectra cannot masquerade as repeated attacks.
            var prefix=Prefix(novelty);
            for(int i=0;i<frames;i++) novelty[i]=Math.Max(0,novelty[i]-.75*Mean(prefix,i-12,i+13));
            double scale=Percentile(novelty,.99);
            if(scale>1e-6) for(int i=0;i<frames;i++) novelty[i]=Math.Min(2,novelty[i]/scale);
            else Array.Clear(novelty,0,frames);
            var onsets=new List<Onset>();
            for(int i=2;i<frames-2;i++)
            {
                if(novelty[i]<.13 || novelty[i]<novelty[i-1] || novelty[i]<=novelty[i+1]) continue;
                double t=RefineAttack(mono,(double)i*Hop/rate,rate);
                float strength=Clamp(novelty[i]);
                var onset=new Onset { Time=t,Strength=strength,Confidence=Clamp(.25+.6*novelty[i]) };
                if(onsets.Count>0 && t-onsets[onsets.Count-1].Time<.07)
                { if(strength>onsets[onsets.Count-1].Strength) onsets[onsets.Count-1]=onset; }
                else if(t<map.Duration) onsets.Add(onset);
            }
            map.Onsets=onsets.ToArray();
            EstimateTempo(map,novelty,cancellation);
            progress?.Invoke(new AnalysisProgress(.76f,"Tracing energy and frequency bands"));
            map.Energy=BuildEnergy(mono,rate,map,low,mid,high,cancellation);
            map.Waveform=BuildWaveform(mono,rate,map,cancellation);
            TrackBeats(map,cancellation);
            progress?.Invoke(new AnalysisProgress(.90f,"Interpreting structural changes"));
            StructureAnalyser.Populate(map,cancellation);
            cancellation.ThrowIfCancellationRequested();
            if(!map.IsValid(hash)) throw new InvalidOperationException("Analysis produced an invalid SongMap.");
            progress?.Invoke(new AnalysisProgress(1,"Analysis complete"));
            return map;
        }

        private static void EstimateTempo(SongMap map,double[] novelty,CancellationToken cancellation)
        {
            if(map.Onsets.Length<4 || map.Duration<3) return;
            double fps=(double)AnalysisRate/Hop;
            int min=(int)(fps*60/240),max=Math.Min(novelty.Length/2,(int)(fps*60/40));
            if(max<=min+2) return;
            double mean=novelty.Average();
            var scores=new double[max+1];
            for(int lag=min;lag<=max;lag++)
            {
                cancellation.ThrowIfCancellationRequested();
                double dot=0,a=0,b=0;
                // Remove the DC component: random dense attacks are not evidence of periodicity.
                for(int i=lag;i<novelty.Length;i++) { double x=novelty[i]-mean,y=novelty[i-lag]-mean; dot+=x*y; a+=x*x; b+=y*y; }
                scores[lag]=dot/Math.Max(1e-9,Math.Sqrt(a*b));
            }
            var candidates=new List<TempoCandidate>();
            for(int lag=min+1;lag<max;lag++)
            {
                if(scores[lag]<.12 || scores[lag]<scores[lag-1] || scores[lag]<scores[lag+1]) continue;
                double denominator=scores[lag-1]-2*scores[lag]+scores[lag+1];
                double delta=Math.Abs(denominator)<1e-8?0:.5*(scores[lag-1]-scores[lag+1])/denominator;
                double period=(lag+Math.Max(-.5,Math.Min(.5,delta)))/fps;
                // Refine quantised autocorrelation with measured attack-to-attack intervals.
                var intervals=new List<double>();
                for(int i=0;i<map.Onsets.Length;i++)
                    for(int j=i+1;j<Math.Min(i+10,map.Onsets.Length);j++)
                    {
                        double interval=map.Onsets[j].Time-map.Onsets[i].Time;
                        int multiple=(int)Math.Round(interval/period);
                        if(multiple>=1 && multiple<=4 && Math.Abs(interval/multiple-period)<period*.04) intervals.Add(interval/multiple);
                    }
                if(intervals.Count>=4) { intervals.Sort(); period=intervals[intervals.Count/2]; }
                double bpm=60/period;
                if(bpm>=40 && bpm<=240 && !candidates.Any(c=>Math.Abs(c.Bpm-bpm)<2)) candidates.Add(new TempoCandidate { Bpm=bpm,Score=Clamp(scores[lag]) });
            }
            // A mild, visible metrical prior. Raw periodicity scores remain in the exported candidates.
            candidates=candidates.OrderByDescending(c=>c.Score*(c.Bpm>=85 && c.Bpm<=180?1.20:1)).ThenBy(c=>c.Bpm).Take(8).ToList();
            if(candidates.Count==0) return;
            var best=candidates[0]; map.EstimatedBpm=best.Bpm;
            double competing=candidates.Skip(1).Where(c=>Math.Abs(c.Bpm/best.Bpm-.5)>.06 && Math.Abs(c.Bpm/best.Bpm-2)>.12).Select(c=>(double)c.Score).DefaultIfEmpty(0).Max();
            map.BpmConfidence=Clamp(best.Score*(.6+.4*Math.Max(0,1-competing/Math.Max(.001,best.Score))));
            // Always expose plausible octave interpretations, but don't assign invented periodicity evidence.
            foreach(double bpm in new[]{best.Bpm/2,best.Bpm*2})
                if(bpm>=40 && bpm<=300 && !candidates.Any(c=>Math.Abs(c.Bpm-bpm)<2)) candidates.Add(new TempoCandidate { Bpm=bpm,Score=0 });
            map.TempoCandidates=candidates.ToArray();
        }

        private static void TrackBeats(SongMap map,CancellationToken cancellation)
        {
            if(map.EstimatedBpm<=0 || map.BpmConfidence<.18f) return;
            double period=60/map.EstimatedBpm;
            const int bins=512;
            var phases=new double[bins];
            foreach(var o in map.Onsets)
            {
                int center=(int)Math.Round((o.Time%period)/period*bins)%bins;
                int radius=Math.Max(2,(int)(.035/period*bins));
                for(int j=-radius;j<=radius;j++) phases[(center+j+bins)%bins]+=o.Strength*Math.Exp(-4.5*j*j/(radius*radius));
            }
            int peak=0; for(int i=1;i<bins;i++) if(phases[i]>phases[peak]) peak=i;
            double phase=(double)peak/bins*period;
            // Resolve wrap-around to the first beat, including attacks at sample zero.
            if(period-phase<.025) phase=0;
            var beats=new List<Beat>();
            for(int i=0;;i++)
            {
                cancellation.ThrowIfCancellationRequested();
                double predicted=phase+i*period; if(predicted>=map.Duration) break;
                int start=map.NextOnsetIndex(predicted-period*.12-1e-9); Onset nearest=null; double best=double.MinValue;
                for(int j=start;j<map.Onsets.Length && map.Onsets[j].Time<=predicted+period*.12;j++)
                {
                    var o=map.Onsets[j]; double score=o.Strength-.7*Math.Abs(o.Time-predicted)/(period*.12);
                    if(score>best) { best=score; nearest=o; }
                }
                // Local phase correction is bounded; absent attacks keep a low-confidence predicted pulse.
                double time=nearest==null?predicted:nearest.Time;
                beats.Add(new Beat { Index=i,Time=time,Strength=nearest?.Strength??map.IntensityAt(time)*.3f,
                    Confidence=nearest==null?map.BpmConfidence*.25f:Math.Min(map.BpmConfidence,nearest.Confidence) });
            }
            map.Beats=beats.ToArray();
            // Estimate four-beat accent grouping only when it has measurable evidence; never assert a meter.
            if(beats.Count>=16)
            {
                var accents=new double[4]; var counts=new int[4];
                for(int i=0;i<beats.Count;i++) { int e=Math.Min(map.Energy.Length-1,(int)(beats[i].Time/map.EnergyStep)); accents[i%4]+=beats[i].Strength+map.Energy[e].Low; counts[i%4]++; }
                for(int i=0;i<4;i++) accents[i]/=Math.Max(1,counts[i]);
                int strongest=0; for(int i=1;i<4;i++) if(accents[i]>accents[strongest]) strongest=i;
                double other=accents.Where((v,i)=>i!=strongest).Max();
                map.DownbeatConfidence=Clamp((accents[strongest]-other)/Math.Max(.001,accents[strongest])*.65);
                if(map.DownbeatConfidence>=.08f) for(int i=0;i<beats.Count;i++) beats[i].BarPosition=(i-strongest+4)%4;
            }
        }

        private static EnergySample[] BuildEnergy(float[] pcm,int rate,SongMap map,double[] lows,double[] mids,double[] highs,CancellationToken cancellation)
        {
            int n=(int)Math.Ceiling(map.Duration/map.EnergyStep); var result=new EnergySample[n];
            var rms=new double[n]; var low=new double[n]; var mid=new double[n]; var high=new double[n];
            for(int i=0;i<n;i++)
            {
                cancellation.ThrowIfCancellationRequested();
                int a=Math.Max(0,(int)((i*map.EnergyStep-.05)*rate)),b=Math.Min(pcm.Length,(int)((i*map.EnergyStep+.05)*rate));
                double power=0; for(int j=a;j<b;j++) power+=pcm[j]*pcm[j]; rms[i]=Math.Sqrt(power/Math.Max(1,b-a));
                int first=Math.Max(0,(int)((i*map.EnergyStep-.08)*rate/Hop)),last=Math.Min(lows.Length,(int)((i*map.EnergyStep+.08)*rate/Hop)+1);
                for(int j=first;j<last;j++) { low[i]+=lows[j]; mid[i]+=mids[j]; high[i]+=highs[j]; }
                low[i]=Math.Sqrt(low[i]/Math.Max(1,last-first)); mid[i]=Math.Sqrt(mid[i]/Math.Max(1,last-first)); high[i]=Math.Sqrt(high[i]/Math.Max(1,last-first));
            }
            // A common reference for all bands preserves spectral balance; RMS remains absolute.
            double bandScale=Math.Max(1e-5,Percentile(low.Zip(mid,(l,m)=>l+m).Zip(high,(lm,h)=>lm+h).ToArray(),.95));
            var smooth=new double[n];
            for(int i=0;i<n;i++) smooth[i]=i==0?rms[i]:smooth[i-1]+.22*(rms[i]-smooth[i-1]);
            // Symmetric smoothing avoids shifting the displayed envelope behind its audible cause.
            for(int i=n-2;i>=0;i--) smooth[i]+=.65*(smooth[i+1]-smooth[i]);
            double rmsScale=Math.Max(.015,Percentile(smooth,.95));
            for(int i=0;i<n;i++) result[i]=new EnergySample { Rms=Clamp(rms[i]),Intensity=Clamp(smooth[i]/rmsScale),Low=Clamp(low[i]/bandScale),Mid=Clamp(mid[i]/bandScale),High=Clamp(high[i]/bandScale) };
            return result;
        }
        private static WavePeak[] BuildWaveform(float[] pcm,int rate,SongMap map,CancellationToken cancellation)
        {
            int n=(int)Math.Ceiling(map.Duration/map.WaveStep); var peaks=new WavePeak[n];
            for(int i=0;i<n;i++)
            {
                if(i%1024==0) cancellation.ThrowIfCancellationRequested();
                int a=(int)(i*map.WaveStep*rate),b=Math.Min(pcm.Length,(int)((i+1)*map.WaveStep*rate)); float min=0,max=0;
                for(int j=a;j<b;j++) { min=Math.Min(min,pcm[j]); max=Math.Max(max,pcm[j]); }
                peaks[i]=new WavePeak { Min=Math.Max(-1,min),Max=Math.Min(1,max) };
            }
            return peaks;
        }
        private static double RefineAttack(float[] pcm,double time,int rate)
        {
            int radius=(int)(.035*rate),center=(int)(time*rate),width=Math.Max(1,(int)(.003*rate));
            int first=Math.Max(width,center-radius),last=Math.Min(pcm.Length-width-1,center+radius);
            double before=0,after=0,best=0; int peak=Math.Max(0,center);
            if(last<first) return (double)peak/rate;
            for(int j=first-width;j<first;j++) before+=pcm[j]*pcm[j];
            for(int j=first;j<first+width;j++) after+=pcm[j]*pcm[j];
            for(int j=first;j<=last;j++)
            {
                double rise=after-before; if(rise>best) { best=rise; peak=j; }
                before+=pcm[j]*pcm[j]-pcm[j-width]*pcm[j-width];
                after+=pcm[j+width]*pcm[j+width]-pcm[j]*pcm[j];
            }
            return (double)peak/rate;
        }
        private static void Fft(double[] re,double[] im)
        {
            int n=re.Length;
            for(int i=1,j=0;i<n;i++)
            {
                int bit=n>>1; for(; (j&bit)!=0;bit>>=1) j^=bit; j^=bit;
                if(i<j) { double x=re[i]; re[i]=re[j]; re[j]=x; }
            }
            for(int length=2;length<=n;length<<=1)
            {
                double angle=-2*Math.PI/length,ur=Math.Cos(angle),ui=Math.Sin(angle);
                for(int i=0;i<n;i+=length)
                {
                    double wr=1,wi=0;
                    for(int j=0;j<length/2;j++)
                    {
                        int a=i+j,b=a+length/2;
                        double vr=re[b]*wr-im[b]*wi,vi=re[b]*wi+im[b]*wr;
                        re[b]=re[a]-vr; im[b]=im[a]-vi; re[a]+=vr; im[a]+=vi;
                        double next=wr*ur-wi*ui; wi=wr*ui+wi*ur; wr=next;
                    }
                }
            }
        }
        internal static float Clamp(double x) => (float)Math.Max(0,Math.Min(1,x));
        private static double[] Prefix(double[] values) { var p=new double[values.Length+1]; for(int i=0;i<values.Length;i++) p[i+1]=p[i]+values[i]; return p; }
        private static double Mean(double[] prefix,int a,int b) { a=Math.Max(0,a); b=Math.Min(prefix.Length-1,b); return (prefix[b]-prefix[a])/Math.Max(1,b-a); }
        private static double Percentile(double[] values,double p) { var copy=(double[])values.Clone(); Array.Sort(copy); return copy[Math.Min(copy.Length-1,(int)(p*copy.Length))]; }
    }
}
