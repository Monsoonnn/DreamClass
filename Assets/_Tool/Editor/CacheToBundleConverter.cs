#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class CacheToBundleConverter : EditorWindow
{
    // CẤU HÌNH ĐƯỜNG DẪN TẠM
    const string TEMP_ASSET_PATH = "Assets/TempBundleBuild";

    private Vector2 scrollPosition;
    private string logMessages = "";
    private BuildTarget buildTarget = BuildTarget.Android; // Default to Android

    // --- OPTIMIZATION SETTINGS ---
    private int maxTextureSizeIndex = 3; // Default 2048
    private int[] maxTextureSizes = { 256, 512, 1024, 2048, 4096, 8192 };
    private string[] maxTextureSizeStrings = { "256", "512", "1024", "2048", "4096", "8192" };

    private bool useCrunchCompression = true;
    private int compressionQuality = 75; // 0-100
    private bool incrementalBuild = true;
    // -----------------------------

    [MenuItem("Tools/DreamClass/Bundles/Convert PDFCache to Bundles")]
    public static void OpenWindow()
    {
        GetWindow<CacheToBundleConverter>("Cache to Bundle Converter");
    }

    private void OnGUI()
    {
        GUILayout.Label("Convert Cached PDFs to AssetBundles", EditorStyles.boldLabel);  

        // --- BUILD TARGET SELECTION ---
        buildTarget = (BuildTarget)EditorGUILayout.EnumPopup("Build Target:", buildTarget);
        
        // Show resolved path
        string outputPath = GetOutputPath();
        GUIStyle style = new GUIStyle(EditorStyles.label);
        if (outputPath.Contains("PersistentData")) style.normal.textColor = Color.green;
        else style.normal.textColor = Color.cyan;
        
        GUILayout.Label($"-> Output Folder: {outputPath}", style);

        GUILayout.Space(10);
        GUILayout.Label("Optimization Settings", EditorStyles.boldLabel);
        
        maxTextureSizeIndex = EditorGUILayout.Popup("Max Texture Size:", maxTextureSizeIndex, maxTextureSizeStrings);
        useCrunchCompression = EditorGUILayout.Toggle("Use Crunch Compression:", useCrunchCompression);
        if (useCrunchCompression)
        {
            compressionQuality = EditorGUILayout.IntSlider("Quality (0-100):", compressionQuality, 0, 100);
        }

        GUILayout.Space(10);
        GUILayout.Label("Build Settings", EditorStyles.boldLabel);
        incrementalBuild = EditorGUILayout.Toggle("Incremental Build:", incrementalBuild);
        GUILayout.Label("   (Checks files and only rebuilds changes. Much faster!)", EditorStyles.miniLabel);

        GUILayout.Space(15);

        if (GUILayout.Button(incrementalBuild ? "UPDATE BUNDLES (INCREMENTAL)" : "BUILD ALL (FULL REBUILD)", GUILayout.Height(40)))        
        {
            Convert();
        }

        // --- TOOLS ---
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Force Clear Temp Folder", GUILayout.Height(25)))
        {
            if (Directory.Exists(TEMP_ASSET_PATH))
            {
                FileUtil.DeleteFileOrDirectory(TEMP_ASSET_PATH);
                FileUtil.DeleteFileOrDirectory(TEMP_ASSET_PATH + ".meta");
                AssetDatabase.Refresh();
                Log("Đã xóa folder tạm thủ công.");
            }
            else
            {
                Log("Không tìm thấy folder tạm.");
            }
        }

        if (GUILayout.Button("Clear Output Folder", GUILayout.Height(25)))
        {
            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, true);
                if (File.Exists(outputPath + ".meta")) File.Delete(outputPath + ".meta");
                AssetDatabase.Refresh();
                Log($"Đã xóa folder đích: {outputPath}");
            }
        }
        GUILayout.EndHorizontal();
        // ----------------

        if (GUILayout.Button("Open Output Folder", GUILayout.Height(30)))       
        {
            if (Directory.Exists(outputPath))
                EditorUtility.RevealInFinder(outputPath);
            else
                Log($"Output folder not created yet: {outputPath}");
        }

        // Log section
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));
        GUILayout.TextArea(logMessages);
        EditorGUILayout.EndScrollView();
    }

    private string GetOutputPath()
    {
        // Android -> StreamingAssets
        if (buildTarget == BuildTarget.Android) 
        {
            return "Assets/StreamingAssets/Android/PDFAssetBundles";
        }
        // Standalone (Editor) -> PersistentData
        else if (buildTarget == BuildTarget.StandaloneWindows || 
                 buildTarget == BuildTarget.StandaloneWindows64 || 
                 buildTarget == BuildTarget.StandaloneOSX)
        {
            return "Assets/PersistentData/Editor/PDFAssetBundles";
        }
        return "Assets/PersistentData/UnknownPlatform/PDFAssetBundles";
    }

    private void Log(string message)
    {
        logMessages += "-> " + message + "\n";
        Debug.Log($"[Converter] {message}");
    }

    private void LogError(string message)
    {
        logMessages += "[ERROR] " + message + "\n";
        Debug.LogError($"[Converter] {message}");
    }

    public void Convert()
    {
        logMessages = "";
        Log($"=== BẮT ĐẦU CONVERT (Target: {buildTarget} | Inc: {incrementalBuild}) ===");

        string outputPath = GetOutputPath();
        Log($"Output Path: {outputPath}");

        // 1. KIỂM TRA INPUT (CACHE)
        string cachePath = Path.Combine(Application.persistentDataPath, "PDFSubjectsCache");
        if (!Directory.Exists(cachePath))
        {
            LogError($"Không thấy folder Cache tại: {cachePath}");
            Log("Hãy chạy game và tải tài liệu về trước khi chạy tool này.");
            return;
        }

        // 2. CHUẨN BỊ FOLDER TẠM
        if (!incrementalBuild)
        {
            // Full Rebuild -> Xóa sạch Temp
            if (Directory.Exists(TEMP_ASSET_PATH))
            {
                FileUtil.DeleteFileOrDirectory(TEMP_ASSET_PATH);
                FileUtil.DeleteFileOrDirectory(TEMP_ASSET_PATH + ".meta");
                Log("Đã xóa sạch Temp Folder (Full Rebuild)");
            }
        }
        Directory.CreateDirectory(TEMP_ASSET_PATH);

        // 3. CHUẨN BỊ FOLDER ĐÍCH
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }
        else if (!incrementalBuild)
        {
            // Full Rebuild -> Xóa sạch Output
            Log($"Xóa sạch folder đích để build mới: {outputPath}");
            Directory.Delete(outputPath, true);
            Directory.CreateDirectory(outputPath);
        }
        
        // Ensure parent directories exist
        string directoryPath = Path.GetDirectoryName(outputPath);
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        AssetDatabase.Refresh();

        // 4. COPY FILE TỪ CACHE VÀO TEMP (SMART COPY)
        Log("Đang copy/sync files từ Cache sang Temp...");
        SmartCopyDirectory(cachePath, TEMP_ASSET_PATH);

        AssetDatabase.Refresh();

        // 5. TẠO BUILD MAP & APPLY IMPORT SETTINGS
        List<AssetBundleBuild> buildMap = new List<AssetBundleBuild>();
        DirectoryInfo tempRootInfo = new DirectoryInfo(TEMP_ASSET_PATH);
        ProcessFolderForBuild(tempRootInfo, tempRootInfo.FullName, buildMap);

        // 6. THỰC HIỆN BUILD
        if (buildMap.Count == 0)
        {
            LogError("Không tìm thấy ảnh hợp lệ để build.");
            return;
        }

        Log($"Tìm thấy {buildMap.Count} bundles. Bắt đầu Build...");

        try
        {
            // Build options: ChunkBasedCompression is good for performance
            // Use UncompressedAssetBundle if using Crunch Compression on Textures to avoid double compression? 
            // Usually ChunkBased is still fine.
            BuildAssetBundleOptions options = BuildAssetBundleOptions.ChunkBasedCompression;
            if (!incrementalBuild) options |= BuildAssetBundleOptions.ForceRebuildAssetBundle;

            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                outputPath,
                buildMap.ToArray(),
                options,
                buildTarget
            );

            if (manifest != null)
            {
                Log("<color=green>=== THÀNH CÔNG ===</color>");

                if (!incrementalBuild)
                {
                    // Cleanup only on full rebuild to save space? 
                    // Or keep it for next incremental build? 
                    // Keeping it allows incremental to work next time.
                    Log("Temp folder giữ lại để hỗ trợ Incremental Build lần sau.");
                }

                AssetDatabase.Refresh();
                EditorUtility.RevealInFinder(outputPath);
                EditorUtility.DisplayDialog("Thành công", $"Đã tạo/update {buildMap.Count} bundles!", "OK");
            }
            else
            {
                LogError("Build thất bại (Manifest null). Kiểm tra Console.");     
            }
        }
        catch (System.Exception ex)
        {
            LogError($"Exception: {ex.Message}");
        }
    }

    private void ProcessFolderForBuild(DirectoryInfo currentDir, string rootPath, List<AssetBundleBuild> buildMap)
    {
        FileInfo[] files = currentDir.GetFiles();
        List<string> validAssetPaths = new List<string>();

        foreach (var fileInfo in files)
        {
            if (fileInfo.Name.EndsWith(".meta")) continue;
            // Skip system files like .DS_Store
            if (fileInfo.Name.StartsWith(".")) continue;

            string fullPath = fileInfo.FullName;
            int assetsIndex = fullPath.IndexOf("Assets", System.StringComparison.OrdinalIgnoreCase);
            if (assetsIndex < 0) continue;

            string unityPath = fullPath.Substring(assetsIndex).Replace("\\", "/");       

            TextureImporter importer = AssetImporter.GetAtPath(unityPath) as TextureImporter;

            // Check if settings need update
            bool settingsChanged = false;

            if (importer == null)
            {
                AssetDatabase.ImportAsset(unityPath, ImportAssetOptions.ForceUpdate);    
                importer = AssetImporter.GetAtPath(unityPath) as TextureImporter;        
                settingsChanged = true;
            }

            if (importer != null)
            {
                // Verify settings
                if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; settingsChanged = true; }
                if (importer.mipmapEnabled) { importer.mipmapEnabled = false; settingsChanged = true; }
                
                int targetSize = maxTextureSizes[maxTextureSizeIndex];
                if (importer.maxTextureSize != targetSize) { importer.maxTextureSize = targetSize; settingsChanged = true; }
                
                if (useCrunchCompression)
                {
                    if (importer.textureCompression != TextureImporterCompression.Compressed) { importer.textureCompression = TextureImporterCompression.Compressed; settingsChanged = true; }
                    if (!importer.crunchedCompression) { importer.crunchedCompression = true; settingsChanged = true; }
                    if (importer.compressionQuality != compressionQuality) { importer.compressionQuality = compressionQuality; settingsChanged = true; }
                }
                else
                {
                    // Normal compression or none? Usually compressed.
                    if (importer.textureCompression != TextureImporterCompression.Compressed) { importer.textureCompression = TextureImporterCompression.Compressed; settingsChanged = true; }
                    if (importer.crunchedCompression) { importer.crunchedCompression = false; settingsChanged = true; }
                }

                if (settingsChanged)
                {
                    importer.SaveAndReimport();
                    // Log($"Applied settings to: {fileInfo.Name}");
                }
                
                validAssetPaths.Add(unityPath);
            }
        }

        if (validAssetPaths.Count > 0)
        {
            AssetBundleBuild build = new AssetBundleBuild();

            string relativePath = currentDir.FullName.Substring(rootPath.Length);        
            if (relativePath.StartsWith("\\") || relativePath.StartsWith("/"))
                relativePath = relativePath.Substring(1);

            string bundleName = relativePath.Replace("\\", "/").ToLower();

            if (string.IsNullOrEmpty(bundleName))
            {
                bundleName = "root_assets";
            }

            build.assetBundleName = bundleName;
            build.assetNames = validAssetPaths.ToArray();
            buildMap.Add(build);

            Log($"Included Bundle: {bundleName} ({validAssetPaths.Count} assets)");
        }

        foreach (var subDir in currentDir.GetDirectories())
        {
            ProcessFolderForBuild(subDir, rootPath, buildMap);
        }
    }

    /// <summary>
    /// Smart copy: Chỉ copy file nếu file đích không tồn tại hoặc khác kích thước/thời gian.
    /// Giúp tránh re-import không cần thiết trong Unity.
    /// </summary>
    static void SmartCopyDirectory(string sourceDir, string destinationDir)
    {
        DirectoryInfo dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists) return;

        if (!Directory.Exists(destinationDir)) Directory.CreateDirectory(destinationDir);

        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            FileInfo targetFile = new FileInfo(targetFilePath);

            bool needsCopy = true;
            if (targetFile.Exists)
            {
                // Simple check: Length matches?
                // Note: LastWriteTime might differ due to copy, so Length is safer check for binary match.
                // For exact match, hash is better but slow. 
                // Since these are static images, length + name is decent proxy.
                if (targetFile.Length == file.Length)
                {
                    needsCopy = false;
                }
            }

            if (needsCopy)
            {
                file.CopyTo(targetFilePath, true);
                // Preserve original modification time to help tracking? 
                // Unity relies on its own hash/meta.
            }
        }

        foreach (DirectoryInfo subDir in dir.GetDirectories())
        {
            string newDestDir = Path.Combine(destinationDir, subDir.Name);
            SmartCopyDirectory(subDir.FullName, newDestDir);
        }
    }
}
#endif
