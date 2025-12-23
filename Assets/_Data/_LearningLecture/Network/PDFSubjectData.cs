using UnityEngine;
using System;
using System.Collections.Generic;

namespace DreamClass.Subjects
{
    /// <summary>
    /// Response từ API /api/pdfs/list
    /// </summary>
    [Serializable]
    public class PDFListResponse
    {
        public string message;
        public int count;
        public List<PDFSubjectInfo> data;
    }

    /// <summary>
    /// Thông tin môn học từ PDF API
    /// </summary>
    [Serializable]
    public class PDFSubjectInfo
    {
        public string name;
        public string fileName;  // Display name từ API (e.g., "SGK TOAN11 - TAP 2 KNTT")
        public string cloudinaryFolder;
        public string grade;
        public string title;
        public string description;
        public string note;
        public string category;
        public int pages;
        public List<string> pageImages;
        public string pdf_url;

        /// <summary>
        /// Tạo hash để check version
        /// </summary>
        public string GetVersionHash()
        {
            return $"{name}_{pages}_{(pageImages != null ? pageImages.Count : 0)}";
        }
    }

    /// <summary>
    /// Lưu trữ cache local của subject
    /// </summary>
    [Serializable]
    public class LocalSubjectCacheData
    {
        public string subjectName;
        public string cloudinaryFolder;  // Added for matching
        public string versionHash;
        public long lastUpdated;
        public List<string> cachedImagePaths;
        public bool isFullyCached;

        public LocalSubjectCacheData()
        {
            cachedImagePaths = new List<string>();
        }

        public LocalSubjectCacheData(string name, string folder, string hash)
        {
            subjectName = name;
            cloudinaryFolder = folder;
            versionHash = hash;
            lastUpdated = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            cachedImagePaths = new List<string>();
            isFullyCached = false;
        }
    }

    /// <summary>
    /// Toàn bộ cache metadata
    /// </summary>
    [Serializable]
    public class SubjectCacheManifest
    {
        public List<LocalSubjectCacheData> subjects;
        public long manifestVersion;

        public SubjectCacheManifest()
        {
            subjects = new List<LocalSubjectCacheData>();
            manifestVersion = 1;
        }

        public LocalSubjectCacheData GetSubjectCache(string subjectName)
        {
            return subjects.Find(s => s.subjectName == subjectName);
        }

        public LocalSubjectCacheData GetSubjectCacheByFolder(string cloudinaryFolder)
        {
            return subjects.Find(s => !string.IsNullOrEmpty(s.cloudinaryFolder) &&
                s.cloudinaryFolder.Equals(cloudinaryFolder, StringComparison.OrdinalIgnoreCase));
        }

        public void AddOrUpdateSubject(LocalSubjectCacheData cacheData)
        {
            // Match by folder first (more reliable), then by name
            var existing = !string.IsNullOrEmpty(cacheData.cloudinaryFolder) ?
                GetSubjectCacheByFolder(cacheData.cloudinaryFolder) :
                GetSubjectCache(cacheData.subjectName);

            if (existing != null)
            {
                subjects.Remove(existing);
                Log($"[CACHE MANIFEST] Updated existing subject: {cacheData.cloudinaryFolder ?? cacheData.subjectName}");
            }
            else
            {
                Log($"[CACHE MANIFEST] Added new subject: {cacheData.cloudinaryFolder ?? cacheData.subjectName}");
            }

            subjects.Add(cacheData);
        }

        private void Log(string msg)
        {
            Debug.Log($"[SubjectCacheManifest] {msg}");
        }
    }

    /// <summary>
    /// Extended SubjectInfo để hỗ trợ cả local CSV và remote PDF
    /// </summary>
    [Serializable]
    public class RemoteSubjectInfo
    {
        public string name;
        public string fileName;  // Display name from API
        public string cloudinaryFolder;  // CloudinaryFolder for matching with local SubjectDatabase
        public string title;
        public string description;
        public string note;
        public string grade;
        public string category;
        public int pages;
        public string pdfUrl;
        public List<string> imageUrls;
        public List<string> localImagePaths;
        public bool isCached;

        // Reference to CSV lectures if available
        public List<CSVLectureInfo> lectures;

        public RemoteSubjectInfo()
        {
            imageUrls = new List<string>();
            localImagePaths = new List<string>();
            lectures = new List<CSVLectureInfo>();
            isCached = false;
        }

        public RemoteSubjectInfo(PDFSubjectInfo pdfInfo)
        {
            name = pdfInfo.name;
            fileName = pdfInfo.fileName;  // Store fileName for cache manifest
            cloudinaryFolder = pdfInfo.cloudinaryFolder;  // Store cloudinaryFolder for matching
            title = pdfInfo.title;
            description = pdfInfo.description;
            note = pdfInfo.note;
            grade = pdfInfo.grade;
            category = pdfInfo.category;
            pages = pdfInfo.pages;
            pdfUrl = pdfInfo.pdf_url;
            imageUrls = pdfInfo.pageImages != null ? new List<string>(pdfInfo.pageImages) : new List<string>();  // Deep copy!
            localImagePaths = new List<string>();
            lectures = new List<CSVLectureInfo>();
            isCached = false;
        }

        /// <summary>
        /// Lấy display name ưu tiên title > name
        /// </summary>
        public string GetDisplayName()
        {
            return !string.IsNullOrEmpty(title) ? title : name;
        }

        /// <summary>
        /// Lấy identifier cho logging (cloudinaryFolder > title > name)
        /// </summary>
        public string GetIdentifier()
        {
            if (!string.IsNullOrEmpty(cloudinaryFolder)) return cloudinaryFolder;
            if (!string.IsNullOrEmpty(title)) return title;
            if (!string.IsNullOrEmpty(name)) return name;
            return "[unknown]";
        }

        /// <summary>
        /// Check if this subject matches with a cloudinaryFolder
        /// </summary>
        public bool MatchesCloudinaryFolder(string remoteFolder)
        {
            if (string.IsNullOrEmpty(cloudinaryFolder) || string.IsNullOrEmpty(remoteFolder))
                return false;
            return cloudinaryFolder.Equals(remoteFolder, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Convert to SubjectInfo for use with existing system
        /// </summary>
        public SubjectInfo ToSubjectInfo()
        {
            return new SubjectInfo
            {
                name = this.name,
                cloudinaryFolder = this.cloudinaryFolder,
                description = this.description,
                title = this.title,
                note = this.note,
                grade = this.grade,
                category = this.category,
                pages = this.pages,
                pdfUrl = this.pdfUrl,
                imageUrls = this.imageUrls != null ? new List<string>(this.imageUrls) : new List<string>(),
                localImagePaths = this.localImagePaths != null ? new List<string>(this.localImagePaths) : new List<string>(),
                lectures = this.lectures != null ? new List<CSVLectureInfo>(this.lectures) : new List<CSVLectureInfo>(),
                isCached = this.isCached
            };
        }
    }
    public enum CloudinaryQuality
    {
        Auto,       // q_auto
        Best,       // q_auto:best
        Good,       // q_auto:good
        Eco,        // q_auto:eco
        Low,        // q_auto:low
        Fixed_80,   // q_80
        Fixed_60    // q_60
    }

    public enum CloudinaryFormat
    {
        Auto,       // f_auto
        Jpg,        // f_jpg
        Png,        // f_png
        WebP        // f_webp
    }

    [Serializable]
    public class CloudinarySettings
    {
        public int textureSize = 1024;
        public CloudinaryQuality quality = CloudinaryQuality.Auto;
        public CloudinaryFormat format = CloudinaryFormat.Auto;

        public CloudinarySettings() { }

        public CloudinarySettings(int size, CloudinaryQuality quality, CloudinaryFormat format)
        {
            this.textureSize = size;
            this.quality = quality;
            this.format = format;
        }
    }

    public class DownloadJob
    {
        public int index;
        public UnityEngine.Networking.UnityWebRequest request;
        public string localPath;
        public bool convertToPNG;
        public string url;
    }
}
