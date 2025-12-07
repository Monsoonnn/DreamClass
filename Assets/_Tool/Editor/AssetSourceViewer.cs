using UnityEngine;
using UnityEditor;
using DreamClass.Subjects;
using System.Collections.Generic;


namespace DreamClass.Tools.Editor
{
    /// <summary>
    /// Asset Source Viewer Window - Shows asset loading strategy and statistics
    /// Menu: Window > DreamClass > Asset Source Viewer
    /// </summary>
    public class AssetSourceViewer : EditorWindow
    {
        private PDFSubjectService pdfService;
        private Vector2 scrollPosition;
        private GUIStyle headerStyle;
        private GUIStyle logStyle;
        private GUIStyle successStyle;
        private GUIStyle errorStyle;

        private float bundleLoadTime = 0f;
        private float cacheLoadTime = 0f;
        private int bundleLoadCount = 0;
        private int cacheLoadCount = 0;

        [MenuItem("Window/DreamClass/Asset Source Viewer")]
        public static void ShowWindow()
        {
            GetWindow<AssetSourceViewer>("Asset Sources");
        }

        private void OnGUI()
        {
            if (pdfService == null)
                pdfService = Object.FindAnyObjectByType<PDFSubjectService>();

            InitializeStyles();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawHeader();
            GUILayout.Space(10);

            if (pdfService == null)
            {
                EditorGUILayout.HelpBox("PDFSubjectService not found in scene", MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawAssetStrategy();
            GUILayout.Space(10);

            DrawBundleSettings();
            GUILayout.Space(10);

            DrawCacheSettings();
            GUILayout.Space(10);

            DrawAssetPriority();
            GUILayout.Space(10);

            DrawRemoteSubjects();
            GUILayout.Space(10);

            DrawDebugCommands();

            EditorGUILayout.EndScrollView();
        }

        private void InitializeStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    margin = new RectOffset(0, 0, 10, 5)
                };
            }

            if (logStyle == null)
            {
                logStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 11,
                    wordWrap = true,
                    margin = new RectOffset(15, 5, 2, 2)
                };
            }

            if (successStyle == null)
            {
                successStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 11,
                    wordWrap = true,
                    margin = new RectOffset(15, 5, 2, 2)
                };
                successStyle.normal.textColor = new Color(0.2f, 0.8f, 0.2f);
            }

            if (errorStyle == null)
            {
                errorStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 11,
                    wordWrap = true,
                    margin = new RectOffset(15, 5, 2, 2)
                };
                errorStyle.normal.textColor = new Color(0.8f, 0.2f, 0.2f);
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("ASSET LOADING STRATEGY MONITOR", headerStyle);
            EditorGUILayout.HelpBox(
                "This window shows the asset loading strategy for book sprites:\n" +
                "BUNDLE FIRST: Check for AssetBundle files first\n" +
                "FALLBACK TO CACHE: If no Bundle, use cached images\n" +
                "FALLBACK TO API: If no Cache, fetch from API",
                MessageType.Info);
        }

        private void DrawAssetStrategy()
        {
            EditorGUILayout.LabelField("CURRENT STRATEGY", headerStyle);
            EditorGUILayout.BeginVertical("box");

            string strategy = pdfService.CheckLocalBundleFirst 
                ? "BUNDLE -> CACHE -> API (Recommended)" 
                : "CACHE -> API (Bundle check disabled)";

            EditorGUILayout.LabelField("Strategy:", strategy, successStyle);

            if (pdfService.CheckLocalBundleFirst)
            {
                EditorGUILayout.LabelField("Will check for AssetBundle first", successStyle);
                EditorGUILayout.LabelField("Will fallback to Cache if Bundle not found", logStyle);
                EditorGUILayout.LabelField("Will fallback to API if Cache empty", logStyle);
            }
            else
            {
                EditorGUILayout.LabelField("Bundle check DISABLED", errorStyle);
                EditorGUILayout.LabelField("Will use Cache first", logStyle);
                EditorGUILayout.LabelField("Will fallback to API if Cache empty", logStyle);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawBundleSettings()
        {
            EditorGUILayout.LabelField("BUNDLE SETTINGS", headerStyle);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("Check Local Bundle First:", pdfService.CheckLocalBundleFirst ? "TRUE" : "FALSE", 
                pdfService.CheckLocalBundleFirst ? successStyle : errorStyle);

            EditorGUILayout.LabelField("Bundle Store Path:", pdfService.BundleStorePath ?? "StreamingAssets", logStyle);

            // Show bundle path info
            string bundlePath = System.IO.Path.Combine(Application.streamingAssetsPath, pdfService.BundleStorePath ?? "StreamingAssets");
            EditorGUILayout.LabelField("Full Bundle Path:", bundlePath, logStyle);

            // Check if bundle directory exists
            bool bundlePathExists = System.IO.Directory.Exists(bundlePath);
            EditorGUILayout.LabelField("Bundle Directory Exists:", bundlePathExists ? "YES" : "NO",
                bundlePathExists ? successStyle : errorStyle);

            EditorGUILayout.EndVertical();
        }

        private void DrawCacheSettings()
        {
            EditorGUILayout.LabelField("CACHE SETTINGS", headerStyle);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("Preload Cached On Start:", pdfService.PreloadCachedOnStart ? "TRUE" : "FALSE",
                pdfService.PreloadCachedOnStart ? successStyle : logStyle);

            EditorGUILayout.LabelField("Cache Folder Name:", pdfService.CacheFolderName ?? "PDFSubjectsCache", logStyle);

            // Show cache path info
            string cachePath = System.IO.Path.Combine(Application.persistentDataPath, pdfService.CacheFolderName ?? "PDFSubjectsCache");
            EditorGUILayout.LabelField("Full Cache Path:", cachePath, logStyle);

            // Check if cache directory exists
            bool cachePathExists = System.IO.Directory.Exists(cachePath);
            EditorGUILayout.LabelField("Cache Directory Exists:", cachePathExists ? "YES" : "NO",
                cachePathExists ? successStyle : errorStyle);

            // Show cache size
            if (cachePathExists)
            {
                long cacheSize = GetDirectorySize(cachePath);
                string cacheSizeStr = FormatBytes(cacheSize);
                EditorGUILayout.LabelField("Cache Size:", cacheSizeStr, logStyle);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAssetPriority()
        {
            EditorGUILayout.LabelField("LOADING PRIORITY", headerStyle);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("[1] AssetBundle (if found)", successStyle);
            EditorGUILayout.LabelField("   Fastest loading (~0.5-1 sec)", logStyle);
            EditorGUILayout.LabelField("   Pre-compiled sprites", logStyle);

            EditorGUILayout.LabelField("[2] Local Cache (if Bundle not found)", successStyle);
            EditorGUILayout.LabelField("   Medium speed (~1-3 sec)", logStyle);
            EditorGUILayout.LabelField("   Downloaded image files", logStyle);

            EditorGUILayout.LabelField("[3] API Fetch (if Cache empty)", successStyle);
            EditorGUILayout.LabelField("   Slowest (~5-10+ sec)", logStyle);
            EditorGUILayout.LabelField("   Depends on network", logStyle);

            EditorGUILayout.EndVertical();
        }

        private void DrawRemoteSubjects()
        {
            EditorGUILayout.LabelField("REMOTE SUBJECTS", headerStyle);
            EditorGUILayout.BeginVertical("box");

            var remoteSubjects = pdfService.RemoteSubjects;
            
            if (remoteSubjects == null || remoteSubjects.Count == 0)
            {
                EditorGUILayout.HelpBox("No remote subjects loaded yet. Play the game to load subjects.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField($"Total Subjects: {remoteSubjects.Count}", logStyle);
                EditorGUILayout.Space(5);

                foreach (var subject in remoteSubjects)
                {
                    EditorGUILayout.BeginVertical("box");
                    
                    string cacheStatus = subject.isCached ? "CACHED" : "NOT CACHED";
                    GUIStyle statusStyle = subject.isCached ? successStyle : errorStyle;
                    
                    EditorGUILayout.LabelField($" {subject.name}", logStyle);
                    EditorGUILayout.LabelField($"  Status: {cacheStatus}", statusStyle);
                    EditorGUILayout.LabelField($"  CloudinaryFolder: {subject.cloudinaryFolder}", logStyle);
                    
                    if (subject.localImagePaths != null && subject.localImagePaths.Count > 0)
                    {
                        EditorGUILayout.LabelField($"  Local Paths: {subject.localImagePaths.Count} images", logStyle);
                    }

                    EditorGUILayout.EndVertical();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawDebugCommands()
        {
            EditorGUILayout.LabelField("DEBUG COMMANDS", headerStyle);
            EditorGUILayout.BeginVertical("box");

            if (GUILayout.Button("Print Console Output", GUILayout.Height(30)))
            {
                PrintConsoleOutput();
            }

            if (GUILayout.Button("Open Bundle Folder", GUILayout.Height(30)))
            {
                string bundlePath = System.IO.Path.Combine(Application.streamingAssetsPath, pdfService.BundleStorePath ?? "StreamingAssets");
                EditorUtility.RevealInFinder(bundlePath);
            }

            if (GUILayout.Button("Open Cache Folder", GUILayout.Height(30)))
            {
                string cachePath = System.IO.Path.Combine(Application.persistentDataPath, pdfService.CacheFolderName ?? "PDFSubjectsCache");
                EditorUtility.RevealInFinder(cachePath);
            }

            EditorGUILayout.EndVertical();
        }

        private void PrintConsoleOutput()
        {
            string bundleStatus = pdfService.CheckLocalBundleFirst ? "ENABLED" : "DISABLED";
            string cacheStatus = pdfService.PreloadCachedOnStart ? "ENABLED" : "DISABLED";

            string message = "ASSET LOADING STRATEGY STATUS\n\n" +
                             $"Bundle Check: {bundleStatus}\n" +
                             $"Cache Preload: {cacheStatus}\n\n" +
                             "LOADING PRIORITY:\n" +
                             "[1] AssetBundle (if found)\n" +
                             "[2] Local Cache (fallback)\n" +
                             "[3] API Fetch (if cache empty)\n\n" +
                             "LOGS TO MONITOR:\n" +
                             "[BUNDLE CHECK] - Bundle file found/not found\n" +
                             "[CACHE CHECK] - Cache status check\n" +
                             "[ASSET CHECK] - Asset priority decision";

            EditorUtility.DisplayDialog("Asset Loading Strategy", message, "OK");
        }

        private long GetDirectorySize(string dirPath)
        {
            long size = 0;
            try
            {
                var dir = new System.IO.DirectoryInfo(dirPath);
                foreach (var file in dir.GetFiles("*", System.IO.SearchOption.AllDirectories))
                {
                    size += file.Length;
                }
            }
            catch { }
            return size;
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
