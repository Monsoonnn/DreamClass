using System.Collections.Generic;
using UnityEngine;

namespace HMStudio.EasyQuiz
{
    [CreateAssetMenu(fileName = "QuizDatabase", menuName = "EasyQuiz/QuizDatabase", order = 1)]
    public class QuizDatabase : ScriptableObject
    {
        [SerializeField] private List<Subject> subjects = new List<Subject>();

        public List<Subject> Subjects => subjects;

        // Method để lấy path Excel theo ID
        public string GetExcelPath(int subjectIndex, int chapterIndex)
        {
            if (subjectIndex < 0 || subjectIndex >= subjects.Count) return null;
            var subject = subjects[subjectIndex];
            if (chapterIndex < 0 || chapterIndex >= subject.Chapters.Count) return null;
            return subject.Chapters[chapterIndex].ExcelPath;
        }
    }

    [System.Serializable]
    public class Subject
    {
        public string Name;  
        public List<Chapter> Chapters = new List<Chapter>();
    }

    [System.Serializable]
    public class Chapter
    {
        public string Name;  // e.g., "Chương 1: Dao động"
        public string ExcelPath;  // e.g., "Assets/Excel/Physics11/Chapter1.xlsx"
    }
}