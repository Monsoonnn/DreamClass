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
        // ------------------------------

        GUILayout.Space(10);

        if (GUILayout.Button("START CONVERT (FULL CLEAN)", GUILayout.Height(40)))        
        {
            Convert();
        }

        // --- NÚT MỚI ---
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
        Log($"=== BẮT ĐẦU CONVERT (Target: {buildTarget}) ===");

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

        // 2. CHUẨN BỊ FOLDER TẠM (CLEAN TEMP)
        if (Directory.Exists(TEMP_ASSET_PATH))
        {
            FileUtil.DeleteFileOrDirectory(TEMP_ASSET_PATH);
            FileUtil.DeleteFileOrDirectory(TEMP_ASSET_PATH + ".meta");
        }
        Directory.CreateDirectory(TEMP_ASSET_PATH);

        // 5. XỬ LÝ Ở FOLDER ĐÍCH (QUAN TRỌNG)
        if (Directory.Exists(outputPath))
        {
            Log($"Xóa sạch folder đích để build mới: {outputPath}");
            Directory.Delete(outputPath, true);
            if (File.Exists(outputPath + ".meta")) File.Delete(outputPath + ".meta");  
        }
        
        // Ensure parent directories exist
        string directoryPath = Path.GetDirectoryName(outputPath);
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
        Directory.CreateDirectory(outputPath);

        Log("Đã tạo folder tạm. Dọn dẹp folder tạm");
        AssetDatabase.Refresh();

        // 3. COPY FILE TỪ CACHE VÀO TEMP
        CopyDirectory(cachePath, TEMP_ASSET_PATH);

        AssetDatabase.Refresh();

        // 4. TẠO BUILD MAP
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
            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                outputPath,
                buildMap.ToArray(),
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle,
                buildTarget
            );

            if (manifest != null)
            {
                Log("<color=green>=== THÀNH CÔNG ===</color>");

                if (Directory.Exists(TEMP_ASSET_PATH))
                {
                    FileUtil.DeleteFileOrDirectory(TEMP_ASSET_PATH);
                    FileUtil.DeleteFileOrDirectory(TEMP_ASSET_PATH + ".meta");
                }

                AssetDatabase.Refresh();
                EditorUtility.RevealInFinder(outputPath);
                EditorUtility.DisplayDialog("Thành công", $"Đã tạo {buildMap.Count} bundles!", "OK");
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

            string fullPath = fileInfo.FullName;
            int assetsIndex = fullPath.IndexOf("Assets", System.StringComparison.OrdinalIgnoreCase);
            if (assetsIndex < 0) continue;

            string unityPath = fullPath.Substring(assetsIndex).Replace("\\", "/");       

            TextureImporter importer = AssetImporter.GetAtPath(unityPath) as TextureImporter;

            if (importer == null)
            {
                AssetDatabase.ImportAsset(unityPath, ImportAssetOptions.ForceUpdate);    
                importer = AssetImporter.GetAtPath(unityPath) as TextureImporter;        
            }

            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
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

            Log($"Added Bundle: {bundleName} ({validAssetPaths.Count} files)");
        }

        foreach (var subDir in currentDir.GetDirectories())
        {
            ProcessFolderForBuild(subDir, rootPath, buildMap);
        }
    }

    static void CopyDirectory(string sourceDir, string destinationDir)
    {
        DirectoryInfo dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists) return;

        Directory.CreateDirectory(destinationDir);

        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath, true);
        }

        foreach (DirectoryInfo subDir in dir.GetDirectories())
        {
            string newDestDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestDir);
        }
    }
}
#endif
