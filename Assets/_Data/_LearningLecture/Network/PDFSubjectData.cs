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
            var existing = GetSubjectCache(cacheData.subjectName);
            if (existing != null)
            {
                subjects.Remove(existing);
            }
            subjects.Add(cacheData);
        }
    }

    /// <summary>
    /// Extended SubjectInfo để hỗ trợ cả local CSV và remote PDF
    /// </summary>
    [Serializable]
    public class RemoteSubjectInfo
    {
        public string name;
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
            cloudinaryFolder = pdfInfo.cloudinaryFolder;  // Store cloudinaryFolder for matching
            title = pdfInfo.title;
            description = pdfInfo.description;
            note = pdfInfo.note;
            grade = pdfInfo.grade;
            category = pdfInfo.category;
            pages = pdfInfo.pages;
            pdfUrl = pdfInfo.pdf_url;
            imageUrls = pdfInfo.pageImages ?? new List<string>();
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
}
