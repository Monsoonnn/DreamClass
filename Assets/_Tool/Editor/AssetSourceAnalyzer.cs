using UnityEditor;
using UnityEngine;
using DreamClass.Subjects;
using System.IO;

namespace DreamClass.Tools.Editor
{
    /// <summary>
    /// Asset Source Analyzer - Analyzes and reports asset source configuration
    /// Right-click on PDFSubjectService > Asset Source Analyzer
    /// </summary>
    public class AssetSourceAnalyzer
    {
        [MenuItem("Assets/DreamClass/Asset Source Analyzer")]
        public static void AnalyzeAssetSource()
        {
            var pdfService = Object.FindAnyObjectByType<PDFSubjectService>();
            if (pdfService == null)
            {
                EditorUtility.DisplayDialog("Error", "PDFSubjectService not found in scene", "OK");
                return;
            }

            PrintAnalysis(pdfService);
        }

        [MenuItem("Window/DreamClass/Asset Source Analyzer")]
        public static void ShowAnalyzerWindow()
        {
            var pdfService = Object.FindAnyObjectByType<PDFSubjectService>();
            if (pdfService == null)
            {
                EditorUtility.DisplayDialog("Error", "PDFSubjectService not found in scene", "OK");
                return;
            }

            PrintAnalysis(pdfService);
        }

        private static void PrintAnalysis(PDFSubjectService pdfService)
        {
            string analysisReport = BuildAnalysisReport(pdfService);
            EditorUtility.DisplayDialog("Asset Source Analysis", analysisReport, "OK");
        }

        private static string BuildAnalysisReport(PDFSubjectService pdfService)
        {
            string report = "ASSET SOURCE LOADING ANALYSIS REPORT\n";
            report += "=====================================\n\n";
            report += BuildConfiguration(pdfService);
            report += "\n";
            report += BuildBundleAnalysis(pdfService);
            report += "\n";
            report += BuildCacheAnalysis(pdfService);
            report += "\n";
            report += BuildAssetPriority(pdfService);
            report += "\n";
            report += BuildRecommendations(pdfService);
            report += "\nANALYSIS COMPLETE";
            return report;
        }

        private static string BuildConfiguration(PDFSubjectService pdfService)
        {
            string config = "CONFIGURATION:\n";
            config += $"  Bundle Check Enabled: {(pdfService.CheckLocalBundleFirst ? "YES" : "NO")}\n";
            config += $"  Preload Cache at Start: {(pdfService.PreloadCachedOnStart ? "YES" : "NO")}\n";
            config += $"  Bundle Store Path: {pdfService.BundleStorePath}\n";
            config += $"  Cache Folder Name: {pdfService.CacheFolderName}";
            return config;
        }

        private static string BuildBundleAnalysis(PDFSubjectService pdfService)
        {
            string analysis = "BUNDLE ANALYSIS:\n";
            
            if (!pdfService.CheckLocalBundleFirst)
            {
                analysis += "  Bundle check DISABLED\n";
                analysis += "  Bundle files will NOT be checked";
                return analysis;
            }

            string bundlePath = Path.Combine(Application.streamingAssetsPath, pdfService.BundleStorePath);
            bool bundlePathExists = Directory.Exists(bundlePath);

            analysis += $"  Bundle Path: {bundlePath}\n";
            analysis += $"  Directory Exists: {(bundlePathExists ? "YES" : "NO")}\n";

            if (bundlePathExists)
            {
                string[] files = Directory.GetFiles(bundlePath);
                analysis += $"  Bundle Files Found: {files.Length}\n";
                
                foreach (var file in files)
                {
                    var info = new FileInfo(file);
                    analysis += $"    {info.Name} ({FormatBytes(info.Length)})\n";
                }
                analysis += $"  Total Bundle Size: {FormatBytes(GetDirectorySize(bundlePath))}";
            }
            else
            {
                analysis += "  WARNING: Bundle directory NOT found - will fallback to CACHE";
            }
            
            return analysis;
        }

        private static string BuildCacheAnalysis(PDFSubjectService pdfService)
        {
            string analysis = "CACHE ANALYSIS:\n";
            
            string cachePath = Path.Combine(Application.persistentDataPath, pdfService.CacheFolderName);
            bool cachePathExists = Directory.Exists(cachePath);

            analysis += $"  Cache Path: {cachePath}\n";
            analysis += $"  Directory Exists: {(cachePathExists ? "YES" : "NO")}\n";

            if (cachePathExists)
            {
                string[] files = Directory.GetFiles(cachePath, "*", SearchOption.AllDirectories);
                long cacheSize = GetDirectorySize(cachePath);
                
                analysis += $"  Files Cached: {files.Length}\n";
                analysis += $"  Cache Size: {FormatBytes(cacheSize)}\n";
                analysis += $"  Subdirectories: {Directory.GetDirectories(cachePath).Length}";
            }
            else
            {
                analysis += "  NOTE: Cache directory will be created on first API fetch";
            }

            return analysis;
        }

        private static string BuildAssetPriority(PDFSubjectService pdfService)
        {
            string priority = "ASSET LOADING PRIORITY:\n";
            priority += "  [1] AssetBundle\n";
            priority += "       Enabled: " + (pdfService.CheckLocalBundleFirst ? "YES" : "NO") + "\n";
            priority += "       Speed: Very Fast (< 1 sec)\n";
            priority += "       Path: " + pdfService.BundleStorePath + "\n\n";
            
            priority += "  [2] Local Cache\n";
            priority += "       Enabled: YES (always)\n";
            priority += "       Speed: Medium (1-3 sec)\n";
            priority += "       Path: " + pdfService.CacheFolderName + "\n\n";
            
            priority += "  [3] API Fetch\n";
            priority += "       Enabled: YES (if cache empty)\n";
            priority += "       Speed: Slow (5-10+ sec)\n";
            priority += "       Network dependent";
            
            return priority;
        }

        private static string BuildRecommendations(PDFSubjectService pdfService)
        {
            string recommendations = "RECOMMENDATIONS:\n";

            string bundlePath = Path.Combine(Application.streamingAssetsPath, pdfService.BundleStorePath);
            bool bundlePathExists = Directory.Exists(bundlePath);
            string cachePath = Path.Combine(Application.persistentDataPath, pdfService.CacheFolderName);
            bool cachePathExists = Directory.Exists(cachePath);

            // Check Bundle
            if (pdfService.CheckLocalBundleFirst)
            {
                if (!bundlePathExists)
                {
                    recommendations += "  Bundle directory not found!\n";
                    recommendations += $"      Create: {bundlePath}\n";
                    recommendations += "      Or set checkLocalBundleFirst = false\n";
                }
                else
                {
                    string[] files = Directory.GetFiles(bundlePath);
                    if (files.Length == 0)
                    {
                        recommendations += "  Bundle directory exists but is EMPTY\n";
                        recommendations += "      Use CacheToBundleConverter to create bundles\n";
                    }
                    else
                    {
                        recommendations += "  Bundle setup looks good!\n";
                    }
                }
            }
            else
            {
                recommendations += "  Bundle check is disabled\n";
                recommendations += "      Consider enabling for better performance\n";
            }

            // Check Cache
            if (cachePathExists)
            {
                long cacheSize = GetDirectorySize(cachePath);
                if (cacheSize > 500 * 1024 * 1024) // 500 MB
                {
                    recommendations += $"  Cache is large ({FormatBytes(cacheSize)})\n";
                    recommendations += "      Consider clearing old cache";
                }
                else
                {
                    recommendations += $"  Cache size is acceptable ({FormatBytes(cacheSize)})";
                }
            }
            else
            {
                recommendations += "  Cache directory will be created on first use";
            }

            return recommendations;
        }

        private static long GetDirectorySize(string dirPath)
        {
            long size = 0;
            try
            {
                var dir = new DirectoryInfo(dirPath);
                foreach (var file in dir.GetFiles("*", SearchOption.AllDirectories))
                {
                    size += file.Length;
                }
            }
            catch { }
            return size;
        }

        private static string FormatBytes(long bytes)
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
