using UnityEditor;
using UnityEngine;
using DreamClass.Subjects;


namespace DreamClass.Tools.Editor
{
    /// <summary>
    /// Quick Console Commands for Asset Source monitoring
    /// Usage: Type in Editor Console to execute
    /// </summary>
    public static class AssetSourceQuickCommands
    {
        [MenuItem("DreamClass/Asset Source/Show Strategy")]
        public static void ShowStrategy()
        {
            var pdfService = Object.FindAnyObjectByType<PDFSubjectService>();
            if (pdfService == null)
            {
                EditorUtility.DisplayDialog("Error", "PDFSubjectService not found!", "OK");
                return;
            }

            string bundleStatus = pdfService.CheckLocalBundleFirst ? "ENABLED" : "DISABLED";
            string cacheStatus = pdfService.PreloadCachedOnStart ? "ENABLED" : "DISABLED";

            string message = "ASSET LOADING STRATEGY STATUS\n\n" +
                             $"Bundle Check: {bundleStatus}\n" +
                             $"Cache Preload: {cacheStatus}\n\n" +
                             "LOADING PRIORITY:\n" +
                             "[1] AssetBundle (if found) - Very Fast\n" +
                             "[2] Local Cache (fallback) - Medium\n" +
                             "[3] API Fetch (if cache empty) - Slow\n\n" +
                             "LOGS TO MONITOR:\n" +
                             "[BUNDLE CHECK] - Bundle file check result\n" +
                             "[CACHE CHECK] - Cache status\n" +
                             "Filter Console by these tags to track asset loading";

            EditorUtility.DisplayDialog("Asset Loading Strategy", message, "OK");
        }

        [MenuItem("DreamClass/Asset Source/Show Remote Subjects")]
        public static void ShowRemoteSubjects()
        {
            var pdfService = Object.FindAnyObjectByType<PDFSubjectService>();
            if (pdfService == null)
            {
                EditorUtility.DisplayDialog("Error", "PDFSubjectService not found!", "OK");
                return;
            }

            var subjects = pdfService.RemoteSubjects;
            if (subjects == null || subjects.Count == 0)
            {
                EditorUtility.DisplayDialog("No Remote Subjects", "No remote subjects loaded yet. Play the game first.", "OK");
                return;
            }

            string message = $"LOADED REMOTE SUBJECTS ({subjects.Count})\n\n";

            for (int i = 0; i < subjects.Count && i < 10; i++)
            {
                var subject = subjects[i];
                string status = subject.isCached ? "CACHED" : "NOT CACHED";
                message += $"{i + 1:D2}. {subject.name}\n";
                message += $"     Status: {status}\n";
                message += $"     CloudinaryFolder: {subject.cloudinaryFolder}\n";
                message += $"     Local Paths: {subject.localImagePaths?.Count ?? 0} images\n\n";
            }

            if (subjects.Count > 10)
            {
                message += $"... and {subjects.Count - 10} more subjects";
            }

            EditorUtility.DisplayDialog("Remote Subjects", message, "OK");
        }

        [MenuItem("DreamClass/Asset Source/Open Asset Viewer")]
        public static void OpenAssetViewer()
        {
            EditorWindow.GetWindow<AssetSourceViewer>("Asset Sources");
        }

        [MenuItem("DreamClass/Asset Source/Run Analysis")]
        public static void RunAnalysis()
        {
            AssetSourceAnalyzer.AnalyzeAssetSource();
        }
    }
}
