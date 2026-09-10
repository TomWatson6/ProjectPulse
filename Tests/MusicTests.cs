using System;
using System.IO;
using System.Linq;
using System.Threading;
using Pulse.Music;

internal static class MusicTests
{
    private static int passed;
    private static void Check(string name,Action test) { test(); Console.WriteLine("PASS  "+name); passed++; }
    private static void True(bool value,string message="Assertion failed") { if(!value) throw new Exception(message); }
    private static void Near(double a,double b,double tolerance) { True(Math.Abs(a-b)<=tolerance,$"Expected {b}, got {a} (±{tolerance})"); }
    private static SongMap Analyse(float[] signal,string hash="fixture") => new AudioAnalyser().Analyse(signal,22050,44100,2,hash,CancellationToken.None);
    private static float[] Clicks(double bpm,double seconds=20,double offset=.2)
    {
        var pcm=new float[(int)(seconds*22050)];
        for(int beat=0;offset+beat*60/bpm<seconds;beat++)
        {
            int start=(int)Math.Round((offset+beat*60/bpm)*22050);
            for(int j=0;j<900 && start+j<pcm.Length;j++) pcm[start+j]+=(float)(.8*Math.Exp(-j/130.0)*Math.Cos(j*2*Math.PI*1400/22050));
        }
        return pcm;
    }
    private static void Main()
    {
        var map=Analyse(Clicks(120));
        Check("120 BPM pulse accuracy",()=>Near(map.EstimatedBpm,120,.35));
        Check("beat timestamps match attacks",()=> { True(map.Beats.Length>=35); foreach(var b in map.Beats.Skip(1).Take(34)) Near((b.Time-.2)*2,Math.Round((b.Time-.2)*2),.045); });
        Check("tempo ambiguity is represented",()=>True(map.TempoCandidates.Any(c=>Math.Abs(c.Bpm-60)<1) && map.TempoCandidates.Any(c=>Math.Abs(c.Bpm-240)<1)));
        Check("confidence is bounded and nonzero",()=>True(map.BpmConfidence>.25 && map.BpmConfidence<=1));
        Check("current beat before start is null",()=>True(map.CurrentBeat(-1)==null));
        Check("current inclusive / next exclusive",()=> { var b=map.Beats[4]; True(map.CurrentBeat(b.Time)==b); True(map.NextBeat(b.Time)==map.Beats[5]); });
        Check("nearest beat endpoints",()=> { True(map.NearestBeat(-10)==map.Beats[0]); True(map.NearestBeat(500)==map.Beats.Last()); True(map.NextBeat(500)==null); });
        Check("interpolated subdivision",()=> { double middle=(map.Beats[3].Time+map.Beats[4].Time)/2; Near(map.NearestSubdivision(middle+.01).Value,middle,1e-8); });
        Check("section lookup and exclusive end",()=> { True(map.SectionAt(0)!=null); True(map.SectionAt(map.Duration)==null); True(map.SectionAt(-1)==null); });
        string json=SongMapJson.Serialize(map);
        Check("serialisation roundtrip and validation",()=> { var restored=SongMapJson.Deserialize(json); True(restored.IsValid("fixture")); True(SongMapJson.Serialize(restored)==json); });
        Check("deterministic output",()=>True(json==SongMapJson.Serialize(Analyse(Clicks(120)))));
        Check("manual tempo keeps original estimate",()=> { var manual=SongMapJson.Deserialize(json); manual.ApplyTempoOverride(174,.1); Near(manual.EstimatedBpm,120,.35); Near(manual.Beats[20].Time,.1+20*60.0/174,1e-10); True(manual.IsValid("fixture")); True(manual.DownbeatConfidence==0); });
        Check("128 BPM noninteger sample period",()=> { var m=Analyse(Clicks(128,25,.137)); Near(m.EstimatedBpm,128,.4); var b=m.Beats.Last(); Near((b.Time-.137)/(60.0/128),Math.Round((b.Time-.137)/(60.0/128)),.05); });
        Check("174 BPM allows half-time interpretation",()=> { var m=Analyse(Clicks(174)); True(Math.Min(Math.Abs(m.EstimatedBpm-174),Math.Abs(m.EstimatedBpm-87))<.5); True(m.TempoCandidates.Any(c=>Math.Abs(c.Bpm-174)<.5)); });
        var silence=Analyse(new float[22050*5]);
        Check("silence has no invented rhythm",()=>True(silence.Beats.Length==0 && silence.Onsets.Length==0 && silence.BpmConfidence==0 && silence.NearestSubdivision(1)==null && silence.IntensityAt(1)==0));
        Check("unstructured noise does not claim a confident pulse",()=> { var random=new Random(19); var noise=new float[22050*12]; for(int i=0;i<noise.Length;i++) noise[i]=(float)(random.NextDouble()-.5)*.25f; var m=Analyse(noise); True(m.BpmConfidence<.25,$"Noise confidence {m.BpmConfidence}"); });
        Check("tiny background signal does not become loud energy",()=> { var quiet=Clicks(120,5); for(int i=0;i<quiet.Length;i++) quiet[i]*=.00001f; True(Analyse(quiet).Energy.Max(e=>e.Intensity)<.01); });
        Check("non-finite PCM is rejected",()=> { var invalid=new float[22050]; invalid[500]=float.NaN; bool rejected=false; try { Analyse(invalid); } catch(ArgumentException) { rejected=true; } True(rejected); });
        Check("invalid manual grid is rejected",()=> { bool rejected=false; try { map.ApplyTempoOverride(120,.8); } catch(ArgumentOutOfRangeException) { rejected=true; } True(rejected); });
        Check("cancelled analysis stops",()=> { var c=new CancellationTokenSource(); c.Cancel(); bool cancelled=false; try { new AudioAnalyser().Analyse(new float[22050],22050,44100,1,"x",c.Token); } catch(OperationCanceledException) { cancelled=true; } True(cancelled); });
        var energySignal=new float[22050*20];
        for(int i=0;i<energySignal.Length;i++) { double t=i/22050.0; double amplitude=t<5?.05:t<10?.05+(t-5)*.07:t<15?.85:.08; energySignal[i]=(float)(amplitude*Math.Sin(t*2*Math.PI*100)); }
        var energyMap=Analyse(energySignal);
        Check("normalised energy follows sustained changes",()=> { True(energyMap.IntensityAt(2)<.15); True(energyMap.IntensityAt(12)>.9); True(energyMap.IntensityAt(18)<.2); True(energyMap.IntensityAt(8)>energyMap.IntensityAt(6)); });
        Check("low frequency signal inhabits low band",()=>True(energyMap.Energy[240].Low>energyMap.Energy[240].Mid*5));
        Check("plausible drop and breakdown heuristics",()=> { string detail=string.Join(", ",energyMap.Moments.Select(m=>$"{m.Kind}@{m.Time:F2}")); True(energyMap.Moments.Any(m=>m.Kind==MomentKind.Drop && Math.Abs(m.Time-10)<1.5),detail); True(energyMap.Moments.Any(m=>m.Kind==MomentKind.Breakdown && Math.Abs(m.Time-15)<1.5),detail); });
        Check("sections cover full duration without gaps",()=> { Near(energyMap.Sections[0].Start,0,0); Near(energyMap.Sections.Last().End,20,0); for(int i=1;i<energyMap.Sections.Length;i++) Near(energyMap.Sections[i].Start,energyMap.Sections[i-1].End,0); });
        Check("resampler preserves duration and rejects alias",()=>
        {
            var high=new float[48000]; for(int i=0;i<high.Length;i++) high[i]=(float)Math.Sin(2*Math.PI*16000*i/48000);
            var reduced=PcmResampler.ToAnalysisRate(high,48000,CancellationToken.None); True(reduced.Length==22050);
            True(Math.Sqrt(reduced.Skip(100).Take(21000).Average(v=>v*v))<.03);
        });
        Check("file filtering is case insensitive and exact",()=> { True(FolderSongSource.Supports("hi.MP3")); True(FolderSongSource.Supports("hi.WaV")); True(!FolderSongSource.Supports("hi.wav.exe")); True(!FolderSongSource.Supports("hi.flac")); });
        string temp=Path.Combine(Path.GetTempPath(),"pulse-music-tests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
        try
        {
            var cache=new SongCache(Path.Combine(temp,"cache"));
            Check("cache roundtrip",()=> { cache.Save(map); True(cache.Load("fixture")!=null); True(cache.Load("different")==null); });
            Check("schema invalidation",()=> { var stale=SongMapJson.Deserialize(json); stale.SchemaVersion++; File.WriteAllText(cache.PathFor("fixture"),SongMapJson.Serialize(stale)); True(cache.Load("fixture")==null); });
            Check("algorithm invalidation",()=> { var stale=SongMapJson.Deserialize(json); stale.AlgorithmVersion="old"; File.WriteAllText(cache.PathFor("fixture"),SongMapJson.Serialize(stale)); True(cache.Load("fixture")==null); });
            Check("settings invalidation",()=> { var stale=SongMapJson.Deserialize(json); stale.SettingsId="other"; True(!stale.IsValid("fixture")); });
            Check("corrupt cache is a miss",()=> { File.WriteAllText(cache.PathFor("fixture"),"{corrupt"); True(cache.Load("fixture")==null); cache.Save(map); });
            Check("atomic replacement leaves valid cache",()=> { cache.Save(map); True(cache.Load("fixture")!=null); True(Directory.GetFiles(Path.Combine(temp,"cache"),"*.tmp").Length==0); });
            string song=Path.Combine(temp,"Test.WAV"); File.WriteAllText(song,"audio fixture");
            Check("source metadata discovery",()=> { File.WriteAllText(Path.Combine(temp,"ignored.txt"),"x"); var songs=new FolderSongSource(temp).Discover(CancellationToken.None); True(songs.Count==1 && songs[0].Title=="Test" && songs[0].AudioHash.Length==64); });
            Check("content hash catches same-size changes",()=> { string first=SongCache.HashFile(song,CancellationToken.None); File.WriteAllText(song,"Audio fixture"); True(SongCache.HashFile(song,CancellationToken.None)!=first); });
            Check("malformed timestamps rejected",()=> { var bad=SongMapJson.Deserialize(json); bad.Beats[2].Time=bad.Beats[1].Time; True(!bad.IsValid("fixture")); });
        }
        finally { Directory.Delete(temp,true); }
        Console.WriteLine($"{passed} music tests passed.");
    }
}
