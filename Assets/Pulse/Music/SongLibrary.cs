using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Pulse.Music
{
    public enum AnalysisState { Unanalysed, Cached, Analysing, Ready, Error }
    public sealed class SongMetadata
    {
        public string FileName, Title, Identifier, AudioHash, CachePath, Error;
        public double Duration, Bpm;
        public AnalysisState State;
    }
    // A document-picker provider can supply opaque identifiers + local readable copies later.
    public interface ISongSource { IReadOnlyList<SongMetadata> Discover(CancellationToken cancellation); }

    public sealed class FolderSongSource : ISongSource
    {
        public string DirectoryPath { get; }
        public FolderSongSource(string path) { DirectoryPath=Path.GetFullPath(path); }
        public static bool Supports(string path) => string.Equals(Path.GetExtension(path),".wav",StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetExtension(path),".mp3",StringComparison.OrdinalIgnoreCase);
        public IReadOnlyList<SongMetadata> Discover(CancellationToken cancellation)
        {
            if(!Directory.Exists(DirectoryPath)) return Array.Empty<SongMetadata>();
            var songs=new List<SongMetadata>();
            foreach(string path in Directory.EnumerateFiles(DirectoryPath).Where(Supports).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase))
            {
                cancellation.ThrowIfCancellationRequested();
                var song=new SongMetadata { FileName=Path.GetFileName(path),Title=Path.GetFileNameWithoutExtension(path),Identifier=path };
                try { song.AudioHash=SongCache.HashFile(path,cancellation); }
                catch(IOException e) { song.State=AnalysisState.Error; song.Error=e.Message; }
                catch(UnauthorizedAccessException e) { song.State=AnalysisState.Error; song.Error=e.Message; }
                songs.Add(song);
            }
            return songs;
        }
    }
    public static class SongMapJson
    {
        public static string Serialize(SongMap map)
        {
            using(var stream=new MemoryStream()) { new DataContractJsonSerializer(typeof(SongMap)).WriteObject(stream,map); return Encoding.UTF8.GetString(stream.ToArray()); }
        }
        public static SongMap Deserialize(string json)
        {
            using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(json))) return (SongMap)new DataContractJsonSerializer(typeof(SongMap)).ReadObject(stream);
        }
    }
    public sealed class SongCache
    {
        private readonly string directory;
        public SongCache(string directory) { this.directory=directory; }
        public static string Key(string hash) => HashText(hash+"|"+SongMap.CurrentSchema+"|"+SongMap.CurrentAlgorithm+"|"+SongMap.CurrentSettings);
        public string PathFor(string hash) => Path.Combine(directory,Key(hash)+".json");
        public SongMap Load(string hash)
        {
            if(string.IsNullOrEmpty(hash)) return null;
            string path=PathFor(hash);
            try
            {
                if(!File.Exists(path) || new FileInfo(path).Length>24*1024*1024) return null;
                var map=SongMapJson.Deserialize(File.ReadAllText(path)); return map!=null && map.IsValid(hash)?map:null;
            }
            catch(Exception e) when(e is IOException || e is UnauthorizedAccessException || e is System.Runtime.Serialization.SerializationException || e is System.Xml.XmlException || e is ArgumentException) { return null; }
        }
        public void Save(SongMap map)
        {
            if(map==null || !map.IsValid(map.AudioHash) || string.IsNullOrEmpty(map.AudioHash)) throw new InvalidDataException("Invalid SongMap cannot enter the cache.");
            Directory.CreateDirectory(directory);
            string path=PathFor(map.AudioHash),temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                File.WriteAllText(temp,SongMapJson.Serialize(map),new UTF8Encoding(false));
                if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
            }
            finally { if(File.Exists(temp)) File.Delete(temp); }
        }
        public static string HashFile(string path,CancellationToken cancellation)
        {
            using(var sha=SHA256.Create()) using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                var buffer=new byte[128*1024]; int n;
                while((n=stream.Read(buffer,0,buffer.Length))>0) { cancellation.ThrowIfCancellationRequested(); sha.TransformBlock(buffer,0,n,buffer,0); }
                sha.TransformFinalBlock(Array.Empty<byte>(),0,0); return Hex(sha.Hash);
            }
        }
        private static string HashText(string text) { using(var sha=SHA256.Create()) return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(text))); }
        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();
    }
}
