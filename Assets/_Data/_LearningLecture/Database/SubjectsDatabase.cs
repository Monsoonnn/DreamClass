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
    }

    [System.Serializable]
    public class CSVLectureInfo  // Tên hoàn toàn mới
    {
        public int chapter;
        public string groupName;
        public string lectureName;
        public int page;
    }

    [System.Serializable]
    public class SubjectInfo  // Đổi tên Subject -> SubjectInfo
    {
        public string name;
        public string description;
        public List<CSVLectureInfo> lectures = new List<CSVLectureInfo>();
    }

}