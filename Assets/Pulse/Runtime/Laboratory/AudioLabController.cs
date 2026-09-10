using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Pulse.Audio;
using Pulse.Music;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Pulse.Laboratory
{
    /// <summary>Owns imported audio lifetime and background jobs; never advances the runner simulation.</summary>
    public sealed class AudioLabController : MonoBehaviour
    {
        public bool IsOpen { get; private set; }
        public bool Busy { get; private set; }
        public bool Playing { get; private set; }
        public bool Sandbox { get; set; }
        public SongMap Map { get; private set; }
        public SongMetadata Selected { get; private set; }
        public IReadOnlyList<SongMetadata> Songs { get; private set; }=Array.Empty<SongMetadata>();
        public SongTransport Transport { get; private set; }
        public string SongsDirectory { get; private set; }
        public string Status { get; private set; }="Choose a song to inspect its signal.";
        public float Progress => workerProgress?.Fraction??0;
        public string ProgressStage => workerProgress?.Stage??Status;
        public int AnalysisRuns { get; private set; }
        public bool LastLoadWasCached { get; private set; }
        public double Position => Transport.Ready?Math.Min(Transport.Duration,Transport.Position):0;
        public AudioLabView View { get; private set; }
        private PulseGame game;
        private SongCache cache;
        private AudioClip ownedClip;
        private CancellationTokenSource cancellation;
        private volatile AnalysisProgress workerProgress;
        private int generation;
        private float nextScan;
        private bool scrubbing,resumeAfterScrub,alive=true;

        public void Initialize(PulseGame owner)
        {
            game=owner;
            SongsDirectory=PlayerPrefs.GetString("pulse.songsDirectory",Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Songs"));
            string cacheDirectory=Path.Combine(Application.persistentDataPath,"SongMaps");
            var args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++) { if(args[i]=="-pulse-songs") SongsDirectory=Path.GetFullPath(args[i+1]); if(args[i]=="-pulse-lab-cache") cacheDirectory=Path.GetFullPath(args[i+1]); }
            cache=new SongCache(cacheDirectory);
            var transportObject=new GameObject("Laboratory transport"); transportObject.transform.SetParent(transform,false);
            Transport=transportObject.AddComponent<SongTransport>(); Transport.Initialize(null); Transport.DeviceChanged+=Pause;
            View=new AudioLabView(this,transform); View.SetVisible(false);
            if(Array.IndexOf(args,"-pulse-lab-verify")>=0) gameObject.AddComponent<AudioLabVerification>().Initialize(this,game);
        }
        public void Open()
        {
            if(IsOpen) return;
            game.ReturnToTitle(); game.Hud.SetVisible(false); game.Visuals.ResetMusic();
            IsOpen=true; View.SetVisible(true); View.SetDirectory(SongsDirectory); Scan();
        }
        public void Close()
        {
            if(!IsOpen) return;
            Cancel(); ReleaseClip(); Map=null; Selected=null; Songs=Array.Empty<SongMetadata>();
            IsOpen=false; View.SetVisible(false); View.SetMap(null); game.Visuals.ResetMusic(); game.Hud.SetVisible(true);
            EventSystem.current?.SetSelectedGameObject(null);
        }
        public void Tick()
        {
            if(Playing && Position>=Transport.Duration-1.0/48000) { Transport.PauseAt(Transport.Duration); Playing=false; }
            bool editing=EventSystem.current!=null && EventSystem.current.currentSelectedGameObject!=null && EventSystem.current.currentSelectedGameObject.GetComponent<InputField>()!=null;
            if(!editing)
            {
                if(UnityEngine.Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
                if(UnityEngine.Input.GetKeyDown(KeyCode.Space)) TogglePlay();
                if(UnityEngine.Input.GetKeyDown(KeyCode.R)) Restart();
                if(UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow)) Seek(Position-5);
                if(UnityEngine.Input.GetKeyDown(KeyCode.RightArrow)) Seek(Position+5);
            }
            if(!Busy && !Playing && !scrubbing && Time.unscaledTime>nextScan) Scan(true);
            game.Visuals.RenderMusic(Map,Position,Sandbox,Playing,Time.unscaledTime);
            View.Refresh();
        }
        public void SetDirectory(string path)
        {
            if(Busy) { Status="Cancel the current operation before changing folders."; return; }
            try
            {
                string full=Path.GetFullPath(path.Trim().Trim('"'));
                if(!Directory.Exists(full)) { Status="Folder not found. Create it or enter an existing Songs folder."; return; }
                ReleaseClip(); Map=null; Selected=null; View.SetMap(null);
                SongsDirectory=full; PlayerPrefs.SetString("pulse.songsDirectory",full); PlayerPrefs.Save(); Scan();
            }
            catch(Exception e) when(e is ArgumentException || e is IOException || e is NotSupportedException || e is UnauthorizedAccessException) { Status=e.Message; }
        }
        public async void Scan(bool quiet=false)
        {
            if(Busy || !IsOpen) return;
            int operation=BeginOperation(quiet?Status:"Discovering WAV / MP3 files…"); var token=cancellation.Token;
            try
            {
                string directory=SongsDirectory;
                var songs=await Task.Run(()=>
                {
                    var found=new FolderSongSource(directory).Discover(token);
                    foreach(var song in found)
                    {
                        token.ThrowIfCancellationRequested(); if(song.AudioHash==null) continue;
                        song.CachePath=cache.PathFor(song.AudioHash); var cached=cache.Load(song.AudioHash);
                        if(cached!=null) { song.Duration=cached.Duration; song.Bpm=cached.EffectiveBpm; song.State=AnalysisState.Cached; }
                    }
                    return found;
                },token);
                if(!Current(operation)) return;
                Songs=songs;
                if(Selected!=null)
                {
                    var replacement=songs.FirstOrDefault(s=>s.Identifier==Selected.Identifier);
                    if(replacement==null || replacement.AudioHash!=Selected.AudioHash) { ReleaseClip(); Selected=null; Map=null; View.SetMap(null); Status="Source changed or was removed. Select it again to load its current content."; }
                }
                if(!quiet) Status=songs.Count==0?"No songs yet. Add .mp3 or .wav files to the folder, then scan.":$"{songs.Count} songs discovered. Select a signal to begin.";
                View.RebuildLibrary();
            }
            catch(OperationCanceledException) { }
            catch(Exception e) { if(Current(operation)) Status="Library: "+e.Message; }
            finally { Finish(operation); }
        }
        public async void Select(SongMetadata song)
        {
            if(song==null || (Selected==song && Transport.Ready)) return;
            Cancel(); ReleaseClip(); Map=null; View.SetMap(null); Selected=song; LastLoadWasCached=false;
            int operation=BeginOperation("Loading local audio…"); var token=cancellation.Token;
            AudioClip loaded=null;
            try
            {
                if(song.AudioHash==null) throw new IOException(song.Error??"Cannot read this file.");
                var found=await Task.Run(()=> { string hash=SongCache.HashFile(song.Identifier,token); return Tuple.Create(hash,cache.Load(hash)); },token);
                if(!Current(operation)) return;
                song.AudioHash=found.Item1; song.CachePath=cache.PathFor(song.AudioHash);
                using(var request=UnityWebRequestMultimedia.GetAudioClip(new Uri(song.Identifier).AbsoluteUri,Path.GetExtension(song.Identifier).Equals(".mp3",StringComparison.OrdinalIgnoreCase)?AudioType.MPEG:AudioType.WAV))
                {
                    var handler=(DownloadHandlerAudioClip)request.downloadHandler; handler.streamAudio=false; handler.compressed=false;
                    var download=request.SendWebRequest();
                    while(!download.isDone) { if(token.IsCancellationRequested) { request.Abort(); token.ThrowIfCancellationRequested(); } await Task.Yield(); }
                    token.ThrowIfCancellationRequested();
                    if(request.result!=UnityWebRequest.Result.Success) throw new IOException(request.error);
                    loaded=DownloadHandlerAudioClip.GetContent(request);
                }
                if(loaded==null || loaded.loadState!=AudioDataLoadState.Loaded) throw new IOException("Audio decoder could not read this file. Try PCM WAV or a standard MP3 export.");
                if(loaded.length<.1f || loaded.length>1200 || (long)loaded.samples*loaded.channels>32*1024*1024 || loaded.frequency<8000 || loaded.frequency>192000 || loaded.channels>8)
                    throw new IOException("Lab limit: 0.1–1200 seconds, 8–192 kHz, at most 8 channels and 128 MiB decoded PCM. Use a shorter/stereo export.");
                string verifiedHash=await Task.Run(()=>SongCache.HashFile(song.Identifier,token),token);
                if(verifiedHash!=song.AudioHash) throw new IOException("Audio changed while decoding. Select it again.");
                token.ThrowIfCancellationRequested(); if(!Current(operation)) return;
                ownedClip=loaded; loaded=null; Transport.SetClip(ownedClip); Transport.SetVolume(game.Transport.Volume);
                song.Duration=Transport.Duration; Map=found.Item2;
                // Resampling rounds by at most half an analysis sample; larger disagreement is not a reusable map.
                if(Map!=null && Math.Abs(Map.Duration-song.Duration)>.001) Map=null;
                LastLoadWasCached=Map!=null; song.State=Map==null?AnalysisState.Unanalysed:AnalysisState.Cached; song.Bpm=Map?.EffectiveBpm??0;
                View.SetMap(Map); View.RebuildLibrary();
                Status=Map==null?"Audio ready. Analyse this song to reveal its musical timeline.":"Cached SongMap loaded. No analysis needed.";
            }
            catch(OperationCanceledException) { }
            catch(Exception e) { if(Current(operation)) { song.State=AnalysisState.Error; song.Error=e.Message; Status="Load failed: "+e.Message; } }
            finally { if(loaded!=null) Destroy(loaded); Finish(operation); }
        }
        public async void Analyse(bool force=false)
        {
            if(Busy || Selected==null || !Transport.Ready) return;
            if(Map!=null && !force) { Status="This SongMap is already cached. Use REANALYSE to replace it explicitly."; return; }
            Pause(); var song=Selected;
            AnalysisRuns++;
            int operation=BeginOperation("Preparing PCM for analysis…"); var token=cancellation.Token; song.State=AnalysisState.Analysing;
            try
            {
                var clip=ownedClip; int rate=clip.frequency,channels=clip.channels;
                var mono=new float[clip.samples]; int chunkFrames=Math.Min(16384,clip.samples); var chunk=new float[chunkFrames*channels];
                // Select the highest-energy channel from distributed previews; avoid stereo phase cancellation.
                var channelEnergy=new double[channels];
                for(int probe=0;probe<8;probe++)
                {
                    int offset=Math.Min(clip.samples-chunkFrames,(int)((long)clip.samples*probe/8));
                    if(!clip.GetData(chunk,offset)) throw new IOException("Decoder did not expose readable PCM.");
                    for(int j=0;j<chunkFrames;j++) for(int c=0;c<channels;c++) channelEnergy[c]+=chunk[j*channels+c]*chunk[j*channels+c];
                }
                int channel=0; for(int c=1;c<channels;c++) if(channelEnergy[c]>channelEnergy[channel]) channel=c;
                for(int offset=0;offset<clip.samples;offset+=chunkFrames)
                {
                    token.ThrowIfCancellationRequested();
                    if(!clip.GetData(chunk,offset)) throw new IOException("PCM read failed.");
                    int count=Math.Min(chunkFrames,clip.samples-offset);
                    for(int j=0;j<count;j++) mono[offset+j]=chunk[j*channels+channel];
                    workerProgress=new AnalysisProgress(.03f*offset/clip.samples,"Copying decoded PCM");
                    // Bound each frame to four small copies. Never keep all channels in managed memory.
                    if((offset/chunkFrames)%4==3) await Task.Yield();
                }
                string identifier=song.Identifier,hash=song.AudioHash;
                var result=await Task.Run(()=>
                {
                    var reduced=PcmResampler.ToAnalysisRate(mono,rate,token);
                    var analysed=new AudioAnalyser().Analyse(reduced,AudioAnalyser.AnalysisRate,rate,channels,hash,token,p=> { if(!token.IsCancellationRequested) workerProgress=p; });
                    if(SongCache.HashFile(identifier,token)!=hash) throw new IOException("Audio changed during analysis. Select it again.");
                    token.ThrowIfCancellationRequested(); cache.Save(analysed); return analysed;
                },token);
                if(!Current(operation)) return;
                Map=result; LastLoadWasCached=false; song.State=AnalysisState.Ready; song.Bpm=Map.EffectiveBpm; song.Duration=Map.Duration;
                View.SetMap(Map); View.RebuildLibrary(); Status="SongMap saved. Play, scrub and inspect. Structural labels are hypotheses.";
            }
            catch(OperationCanceledException) { if(Current(operation)) song.State=Map==null?AnalysisState.Unanalysed:AnalysisState.Ready; }
            catch(Exception e) { if(Current(operation)) { song.State=AnalysisState.Error; song.Error=e.Message; Status="Analysis failed: "+e.Message; } }
            finally { Finish(operation); }
        }
        public async void OverrideTempo(string bpmText,string offsetText)
        {
            if(Busy || Map==null) return;
            if(!double.TryParse(bpmText,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double bpm) || !double.TryParse(offsetText,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double offset)) { Status="Enter BPM and phase seconds using a decimal point."; return; }
            int operation=BeginOperation("Saving developer tempo override…"); var token=cancellation.Token;
            try
            {
                var original=Map;
                var changed=await Task.Run(()=> { var copy=SongMapJson.Deserialize(SongMapJson.Serialize(original)); copy.ApplyTempoOverride(bpm,offset); token.ThrowIfCancellationRequested(); cache.Save(copy); return copy; },token);
                if(Current(operation)) { Map=changed; Selected.Bpm=bpm; View.SetMap(Map,false); View.RebuildLibrary(); Status="Manual grid saved. Original tempo estimate retained; reanalyse restores automatic tracking."; }
            }
            catch(Exception e) { if(Current(operation)) Status=e is OperationCanceledException?"Cancelled.":e.Message; }
            finally { Finish(operation); }
        }
        public void Cancel()
        {
            cancellation?.Cancel(); cancellation?.Dispose(); cancellation=null; generation++; Busy=false; workerProgress=null; nextScan=Time.unscaledTime+15;
            if(Selected!=null && Selected.State==AnalysisState.Analysing) Selected.State=Map==null?AnalysisState.Unanalysed:AnalysisState.Ready;
            Status="Cancelled. Existing cached analysis is retained.";
        }
        public void TogglePlay()
        {
            if(!Transport.Ready || Busy) return;
            if(Playing) Pause(); else { if(Position>=Transport.Duration-.03) Transport.Seek(0,false); Transport.Resume(); Playing=true; }
            EventSystem.current?.SetSelectedGameObject(null);
        }
        public void Pause() { if(Transport.Ready && Playing) Transport.Pause(); Playing=false; }
        public void Restart() { if(!Transport.Ready || Busy) return; Transport.Restart(); Playing=true; game.Visuals.ResetMusic(); }
        public void Seek(double time) { if(!Transport.Ready) return; Transport.Seek(time,Playing); game.Visuals.ResetMusic(); }
        public void BeginScrub() { scrubbing=true; resumeAfterScrub=Playing; Pause(); }
        public void Scrub(double time) { if(Transport.Ready) Transport.Seek(time,false); }
        public void EndScrub() { scrubbing=false; if(resumeAfterScrub && Transport.Ready) { Transport.Resume(); Playing=true; } resumeAfterScrub=false; game.Visuals.ResetMusic(); }
        private int BeginOperation(string status) { cancellation?.Dispose(); cancellation=new CancellationTokenSource(); Busy=true; Status=status; workerProgress=null; return ++generation; }
        private bool Current(int operation) => alive && IsOpen && operation==generation;
        private void Finish(int operation) { if(Current(operation)) { Busy=false; workerProgress=null; nextScan=Time.unscaledTime+15; } }
        private void ReleaseClip() { Playing=false; scrubbing=resumeAfterScrub=false; if(Transport!=null) Transport.SetClip(null); if(ownedClip!=null) Destroy(ownedClip); ownedClip=null; }
        private void OnApplicationFocus(bool focus) { if(!focus && IsOpen) Pause(); }
        private void OnApplicationPause(bool paused) { if(paused && IsOpen) Pause(); }
        private void OnDestroy() { alive=false; Cancel(); ReleaseClip(); if(Transport!=null) Transport.DeviceChanged-=Pause; }
    }
}
