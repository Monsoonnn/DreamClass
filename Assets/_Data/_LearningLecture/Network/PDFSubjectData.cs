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
        public string path;
        public string grade;
        public string title;
        public string description;
        public string note;
        public string category;
        public int pages;
        public List<string> images;
        public string pdf_url;

        /// <summary>
        /// Tạo hash để check version
        /// </summary>
        public string GetVersionHash()
        {
            return $"{name}_{pages}_{(images != null ? images.Count : 0)}";
        }
    }

    /// <summary>
    /// Lưu trữ cache local của subject
    /// </summary>
    [Serializable]
    public class LocalSubjectCacheData
    {
        public string subjectName;
        public string versionHash;
        public long lastUpdated;
        public List<string> cachedImagePaths;
        public bool isFullyCached;

        public LocalSubjectCacheData()
        {
            cachedImagePaths = new List<string>();
        }

        public LocalSubjectCacheData(string name, string hash)
        {
            subjectName = name;
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
        public string path;  // Path for matching with local SubjectDatabase
        public string title;
        public string description;
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
            path = pdfInfo.path;  // Store path for matching
            title = pdfInfo.title;
            description = pdfInfo.description;
            grade = pdfInfo.grade;
            category = pdfInfo.category;
            pages = pdfInfo.pages;
            pdfUrl = pdfInfo.pdf_url;
            imageUrls = pdfInfo.images ?? new List<string>();
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
        /// Check if this subject matches with a path
        /// </summary>
        public bool MatchesPath(string remotePath)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(remotePath))
                return false;
            return path.Equals(remotePath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Convert to SubjectInfo for use with existing system
        /// </summary>
        public SubjectInfo ToSubjectInfo()
        {
            return new SubjectInfo
            {
                name = this.name,
                path = this.path,
                description = this.description,
                title = this.title,
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
