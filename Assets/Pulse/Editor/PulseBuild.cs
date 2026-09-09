using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pulse.Editor
{
    public static class PulseBuild
    {
        public const string ScenePath="Assets/Pulse/Scenes/Afterlight.unity";
        private const string AudioPath="Assets/Pulse/Resources/Afterlight.wav";

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            if(Application.isBatchMode) return;
            EditorApplication.delayCall+=() => { if(!File.Exists(ScenePath) || !File.Exists(AudioPath)) Prepare(); };
        }

        [MenuItem("Project Pulse/Prepare vertical slice")]
        public static void Prepare()
        {
            if(!File.Exists(AudioPath)) { AfterlightComposer.Write(AudioPath); AssetDatabase.ImportAsset(AudioPath,ImportAssetOptions.ForceSynchronousImport); }
            var importer=AssetImporter.GetAtPath(AudioPath) as AudioImporter;
            if(importer!=null)
            {
                var settings=importer.defaultSampleSettings;
                settings.loadType=AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat=AudioCompressionFormat.PCM;
                settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
                settings.preloadAudioData=true;
                importer.defaultSampleSettings=settings; importer.loadInBackground=false;
                importer.SaveAndReimport();
            }
            if(!File.Exists(ScenePath))
            {
                Directory.CreateDirectory("Assets/Pulse/Scenes");
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var root=new GameObject("Project Pulse"); root.AddComponent<PulseGame>();
                EditorSceneManager.SaveScene(scene,ScenePath);
            }
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            PlayerSettings.companyName="Project Pulse"; PlayerSettings.productName="Project Pulse";
            PlayerSettings.bundleVersion="0.1.0";
            PlayerSettings.defaultScreenWidth=1440; PlayerSettings.defaultScreenHeight=900;
            PlayerSettings.fullScreenMode=FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow=true; PlayerSettings.runInBackground=false;
            PlayerSettings.colorSpace=ColorSpace.Gamma;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone,ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone,ApiCompatibilityLevel.NET_Standard_2_0);
            QualitySettings.vSyncCount=1; QualitySettings.antiAliasing=4;
            AssetDatabase.SaveAssets();
            Debug.Log("PULSE_PREPARED: open "+ScenePath+" and press Play.");
        }

        [MenuItem("Project Pulse/Build Windows player")]
        public static void Windows()
        {
            Prepare();
            Directory.CreateDirectory("Builds/Windows");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[]{ScenePath},locationPathName="Builds/Windows/Project Pulse.exe",
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.None });
            if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Windows build failed: "+report.summary.result);
            Debug.Log("PULSE_BUILD_OK: "+report.summary.totalSize+" bytes");
        }
    }
}
