#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class CacheToBundleConverter : EditorWindow
{
    // CẤU HÌNH ĐƯỜNG DẪN
    const string TEMP_ASSET_PATH = "Assets/TempBundleBuild";
    const string OUTPUT_PATH = "Assets/StreamingAssets/PDFAssetBundles"; 

    private Vector2 scrollPosition;
    private string logMessages = "";

    [MenuItem("Tools/DreamClass/Bundles/Convert PDFCache to Bundles")]
    public static void OpenWindow()
    {
        GetWindow<CacheToBundleConverter>("Cache to Bundle Converter");
    }

    private void OnGUI()
    {
        GUILayout.Label("Convert Cached PDFs to AssetBundles", EditorStyles.boldLabel);
        
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

        if (GUILayout.Button("Open StreamingAssets Folder", GUILayout.Height(30)))
        {
            EditorUtility.RevealInFinder(OUTPUT_PATH);
        }
        
        // ... (phần log giữ nguyên)
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
        Log("=== BẮT ĐẦU CONVERT (FULL FIX) ===");

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

        // Đảm bảo folder đích tồn tại
        if (!Directory.Exists(OUTPUT_PATH)) Directory.CreateDirectory(OUTPUT_PATH);

        Log("Đã tạo folder tạm. Đang copy file...");
        AssetDatabase.Refresh(); // Refresh để Unity nhận folder mới

        // 3. COPY FILE TỪ CACHE VÀO TEMP
        string[] subjectDirs = Directory.GetDirectories(cachePath);
        if (subjectDirs.Length == 0)
        {
            LogError("Folder Cache rỗng!");
            return;
        }

        foreach (var dir in subjectDirs)
        {
            string folderName = new DirectoryInfo(dir).Name;
            // Bỏ qua folder ẩn
            if (folderName.StartsWith(".")) continue;

            string destDir = Path.Combine(TEMP_ASSET_PATH, folderName);
            CopyDirectory(dir, destDir);
        }

        AssetDatabase.Refresh(); // Refresh lần 2 để nhận file ảnh

        // 4. TẠO BUILD MAP (VÀ FIX IMPORTER)
        List<AssetBundleBuild> buildMap = new List<AssetBundleBuild>();
        DirectoryInfo tempRootInfo = new DirectoryInfo(TEMP_ASSET_PATH);
        DirectoryInfo[] tempFolders = tempRootInfo.GetDirectories();

        foreach (var folderInfo in tempFolders)
        {
            string folderName = folderInfo.Name;
            FileInfo[] files = folderInfo.GetFiles();
            List<string> validAssetPaths = new List<string>();

            Log($"Đang xử lý: {folderName} ({files.Length} files)");

            foreach (var fileInfo in files)
            {
                if (fileInfo.Name.EndsWith(".meta")) continue;

                // === FIX PATH: Lắp ghép đường dẫn thủ công ===
                string safeRelativePath = $"{TEMP_ASSET_PATH}/{folderName}/{fileInfo.Name}";
                safeRelativePath = safeRelativePath.Replace("\\", "/"); // Chuẩn hóa cho Unity

                // Lấy Importer
                TextureImporter importer = AssetImporter.GetAtPath(safeRelativePath) as TextureImporter;

                // Nếu Unity chưa kịp nhận, ép Import lại
                if (importer == null)
                {
                    AssetDatabase.ImportAsset(safeRelativePath, ImportAssetOptions.ForceUpdate);
                    importer = AssetImporter.GetAtPath(safeRelativePath) as TextureImporter;
                }

                if (importer != null)
                {
                    // Setup ảnh
                    importer.textureType = TextureImporterType.Sprite;
                    importer.mipmapEnabled = false; // Tối ưu dung lượng
                    importer.SaveAndReimport();
                    validAssetPaths.Add(safeRelativePath);
                }
                else
                {
                    LogError($"Lỗi NULL Importer: {safeRelativePath}");
                }
            }

            if (validAssetPaths.Count > 0)
            {
                AssetBundleBuild build = new AssetBundleBuild();
                build.assetBundleName = folderName.ToLower(); // Tên bundle viết thường
                build.assetNames = validAssetPaths.ToArray();
                buildMap.Add(build);
            }
        }

        // 5. XỬ LÝ XUNG ĐỘT Ở FOLDER ĐÍCH (QUAN TRỌNG)
        // Bước này xóa các Folder cũ trùng tên với Bundle sắp tạo để tránh lỗi Build
        if (buildMap.Count > 0)
        {
            Log("Đang dọn dẹp folder đích...");
            foreach (var build in buildMap)
            {
                string targetPath = Path.Combine(OUTPUT_PATH, build.assetBundleName);
                
                // Nếu có Folder trùng tên -> Xóa
                if (Directory.Exists(targetPath))
                {
                    Log($"Xóa folder cũ trùng tên: {build.assetBundleName}");
                    Directory.Delete(targetPath, true);
                    if(File.Exists(targetPath + ".meta")) File.Delete(targetPath + ".meta");
                }
                
                // Nếu có File cũ -> Xóa luôn để build mới
                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                    if(File.Exists(targetPath + ".meta")) File.Delete(targetPath + ".meta");
                }
            }
            AssetDatabase.Refresh();
        }

        // 6. THỰC HIỆN BUILD
        if (buildMap.Count == 0)
        {
            LogError("Không tìm thấy ảnh hợp lệ để build.");
            return;
        }

        Log($"Đang build {buildMap.Count} bundles...");

        try
        {
            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                OUTPUT_PATH,
                buildMap.ToArray(),
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle,
                EditorUserBuildSettings.activeBuildTarget
            );

            if (manifest != null)
            {
                Log("<color=green>=== THÀNH CÔNG ===</color>");

                // Dọn dẹp folder tạm
                if (Directory.Exists(TEMP_ASSET_PATH))
                {
                    FileUtil.DeleteFileOrDirectory(TEMP_ASSET_PATH);
                    FileUtil.DeleteFileOrDirectory(TEMP_ASSET_PATH + ".meta");
                }
                
                AssetDatabase.Refresh();
                EditorUtility.RevealInFinder(OUTPUT_PATH);
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
    }
}
#endif