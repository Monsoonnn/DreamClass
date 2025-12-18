using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;
using com.cyborgAssets.inspectorButtonPro;

namespace DreamClass.Subjects
{
    [CreateAssetMenu(fileName = "SubjectDatabase", menuName = "DreamClass/Subjects Database")]
    public class SubjectDatabase : ScriptableObject
    {
        [Header("CSV Source")]
        public TextAsset csvFile;
        public string filePath;

        [Header("Subjects List")]
        public List<SubjectInfo> subjects = new List<SubjectInfo>();

        [ProButton]
        public void LoadCSVAsSubject()
        {
            if (csvFile == null && string.IsNullOrEmpty(filePath))
            {
                Debug.LogError("No CSV source provided!");
                return;
            }

            string csvName = csvFile != null ? Path.GetFileNameWithoutExtension(csvFile.name) :
                                               Path.GetFileNameWithoutExtension(filePath);

            SubjectInfo newSubject = new SubjectInfo { name = csvName };

            string[] lines;

            if (csvFile != null)
            {
                lines = csvFile.text.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
            }
            else
            {
                string fullPath = Path.Combine(Application.dataPath, filePath);
                if (!File.Exists(fullPath))
                {
                    Debug.LogError("CSV file not found: " + fullPath);
                    return;
                }
                lines = File.ReadAllLines(fullPath, Encoding.UTF8);
            }

            for (int i = 1; i < lines.Length; i++)
            {
                var values = lines[i].Split(',');

                if (values.Length < 4) continue;

                if (!int.TryParse(values[0], out int chapter)) continue;
                string groupName = values[1].Trim();
                string lectureName = values[2].Trim();
                if (!int.TryParse(values[3], out int page)) continue;

                newSubject.lectures.Add(new CSVLectureInfo
                {
                    chapter = chapter,
                    groupName = groupName,
                    lectureName = lectureName,
                    page = page
                });
            }

            subjects.Add(newSubject);

            Debug.Log($"Loaded Subject '{newSubject.name}' with {newSubject.lectures.Count} lectures.");
        }
        [ProButton]
        public void LogJson()
        {
            string json = JsonUtility.ToJson(this, true); // true = pretty print
            Debug.Log(json);
        }

        /// <summary>
        /// Clear all remote data from all subjects (keep local lectures)
        /// </summary>
        [ProButton]
        public void ClearAllRemoteData()
        {
            int clearedCount = 0;
            foreach (var subject in subjects)
            {
                if (subject.isCached || !string.IsNullOrEmpty(subject.pdfUrl))
                {
                    subject.ClearRemoteData();
                    clearedCount++;
                }
            }
            Debug.Log($"[SubjectDatabase] Cleared remote data from {clearedCount} subjects");

            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }

        /// <summary>
        /// Clear all cache sprites from all subjects (free memory)
        /// </summary>
        [ProButton]
        public void ClearAllSprites()
        {
            foreach (var subject in subjects)
            {
                subject.ClearCacheData();
            }
            Debug.Log($"[SubjectDatabase] Cleared sprites from all {subjects.Count} subjects");
        }
    }

    [System.Serializable]
    public class CSVLectureInfo  // Tên hoàn toàn mới
    {
        public int chapter;
        public string groupName;
        public string lectureName;
        public int page;

        public CSVLectureInfo Clone()
        {
            return (CSVLectureInfo)this.MemberwiseClone();
        }
    }

    [System.Serializable]
    public class SubjectInfo  
    {
        public string name;
        public string description;
        public List<CSVLectureInfo> lectures = new List<CSVLectureInfo>();
        
        [Header("CloudinaryFolder for matching with API")]
        [Tooltip("CloudinaryFolder để match với API response, ví dụ: pdf-pages/SGK VL 11 KNTT (1)")]
        public string cloudinaryFolder;

        // Extended fields for PDF subjects from API
        [Header("API Subject Data")]
        public string title;
        public string note;
        public string grade;
        public string category;
        public int pages;
        public string pdfUrl;
        public List<string> imageUrls;
        public List<string> localImagePaths;
        public bool isCached;

        [Header("Loaded Sprites (Runtime)")]
        [Tooltip("Sprites loaded from cache - used by BookSpriteManager. This is runtime data and will be cleared on game restart.")]
        [System.NonSerialized]
        public Sprite[] bookPages;

        public SubjectInfo Clone()
        {
            SubjectInfo clone = (SubjectInfo)this.MemberwiseClone();
            clone.lectures = new List<CSVLectureInfo>();
            if (this.lectures != null)
            {
                foreach (var lec in this.lectures)
                {
                    clone.lectures.Add(lec.Clone());
                }
            }
            // Lists of strings (imageUrls, localImagePaths) need new instances too if they are modified
            if (this.imageUrls != null) clone.imageUrls = new List<string>(this.imageUrls);
            if (this.localImagePaths != null) clone.localImagePaths = new List<string>(this.localImagePaths);
            
            return clone;
        }

        /// <summary>
        /// Get display name (ưu tiên title > description > name)
        /// </summary>
        public string GetDisplayName()
        {
            if (!string.IsNullOrEmpty(title))
                return title;
            if (!string.IsNullOrEmpty(description))
                return description;
            return name;
        }

        /// <summary>
        /// Check if this subject matches with a remote PDF by cloudinaryFolder
        /// </summary>
        public bool MatchesCloudinaryFolder(string remoteFolder)
        {
            if (string.IsNullOrEmpty(cloudinaryFolder) || string.IsNullOrEmpty(remoteFolder))
                return false;
            return cloudinaryFolder.Equals(remoteFolder, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Check if sprites are loaded
        /// </summary>
        public bool HasLoadedSprites()
        {
            return bookPages != null && bookPages.Length > 0;
        }

        /// <summary>
        /// Update data from PDFSubjectInfo while keeping local lectures
        /// </summary>
        public void UpdateFromRemote(PDFSubjectInfo pdfInfo, bool cached, List<string> cachedPaths)
        {
            // Update API fields
            title = pdfInfo.title;
            note = pdfInfo.note;
            grade = pdfInfo.grade;
            category = pdfInfo.category;
            pages = pdfInfo.pages;
            pdfUrl = pdfInfo.pdf_url;
            imageUrls = pdfInfo.pageImages != null ? new List<string>(pdfInfo.pageImages) : new List<string>();
            
            // Cache status
            isCached = cached;
            localImagePaths = cachedPaths ?? new List<string>();

            // Keep existing lectures - don't overwrite!
            // lectures remains unchanged
        }

        /// <summary>
        /// Set loaded sprites
        /// </summary>
        public void SetBookPages(Sprite[] sprites)
        {
            bookPages = sprites;
            Debug.Log($"[SubjectInfo] Set {sprites?.Length ?? 0} sprites for subject: {name}");
        }

        /// <summary>
        /// Clear loaded sprites (to free memory)
        /// </summary>
        [ProButton]
        public void ClearCacheData()
        {
            if (bookPages != null)
            {
                foreach (var sprite in bookPages)
                {
                    if (sprite != null && sprite.texture != null)
                    {
                        UnityEngine.Object.Destroy(sprite.texture);
                        UnityEngine.Object.Destroy(sprite);
                    }
                }
                bookPages = null;
            }
        }

        /// <summary>
        /// Clear all API data (keep local lectures and path)
        /// </summary>
        [ProButton]
        public void ClearRemoteData()
        {
            // Clear sprites first
            ClearCacheData();

            // Reset API fields
            title = string.Empty;
            grade = string.Empty;
            category = string.Empty;
            pages = 0;
            pdfUrl = string.Empty;
            imageUrls = new List<string>();
            localImagePaths = new List<string>();
            isCached = false;

            // Keep: name, description, lectures, path
            Debug.Log($"[SubjectInfo] Cleared API data for: {name}");
        }
    }

}