using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace FingerMonster.Editor
{
    /// <summary>
    /// FingerMonster için tek tıkla Windows Standalone (.exe) derlemesi alan editör aracı.
    /// Çok oyunculu testler için aynı bilgisayarda birden fazla oyun penceresi açmayı kolaylaştırır.
    /// </summary>
    public static class BuildGameTool
    {
        private static readonly string BuildDirectory = Path.Combine(Directory.GetCurrentDirectory(), "..", "Builds");
        private static readonly string ExePath = Path.Combine(BuildDirectory, "FingerMonster.exe");

        [MenuItem("FingerMonster/Build Windows Standalone (.exe)")]
        public static void BuildStandaloneWindows()
        {
            Debug.Log("<color=cyan>=== [FINGER MONSTER WINDOWS BUILD BAŞLADI] ===</color>");

            // 1. Önce tüm sahneleri inşa et ve Build Settings'e ekle
            SceneSetupTool.BuildAllScenes();

            // 2. Çıktı klasörünü hazırla
            if (!Directory.Exists(BuildDirectory))
            {
                Directory.CreateDirectory(BuildDirectory);
            }

            // 3. Build seçeneklerini yapılandır
            string[] scenes = new string[]
            {
                "Assets/Scenes/MainMenu.unity",
                "Assets/Scenes/Game.unity"
            };

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = ExePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            Debug.Log($"[BuildGameTool] Hedef dosya yolu: {ExePath}");

            // 4. Derlemeyi başlat
            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"<color=green>=== [BUILD BAŞARILI!] Boyut: {summary.totalSize / (1024 * 1024)} MB | Süre: {summary.totalTime.TotalSeconds:F1} sn ===</color>");
                EditorUtility.RevealInFinder(ExePath);
            }
            else if (summary.result == BuildResult.Failed)
            {
                Debug.LogError($"<color=red>=== [BUILD BAŞARISIZ OLDU!] Hata Sayısı: {summary.totalErrors} ===</color>");
            }
        }
    }
}

