using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HMStudio.EasyQuiz
{
    [CreateAssetMenu(fileName = "QuizDatabase", menuName = "EasyQuiz/QuizDatabase", order = 1)]
    public class QuizDatabase : ScriptableObject
    {
        [Header("Data Mode")]
        [Tooltip("Chọn nguồn dữ liệu Quiz")]
        [SerializeField] private QuizDataMode dataMode = QuizDataMode.Excel;

        [Header("=== EXCEL DATA ===")]
        [Tooltip("Danh sách Subjects cho mode Excel")]
        [SerializeField] private List<Subject> excelSubjects = new List<Subject>();

        [Header("=== API DATA ===")]
        [Tooltip("Base URL của API server")]
        [SerializeField] private string apiBaseURL = "http://localhost:3000";
        [Tooltip("Endpoint để lấy quizzes")]
        [SerializeField] private string apiEndpoint = "/api/quizzes";
        [Tooltip("Danh sách Subjects được cache từ API (Read-only trong Inspector)")]
        [SerializeField] private List<APISubjectData> apiSubjects = new List<APISubjectData>();

        // Properties
        public QuizDataMode DataMode => dataMode;
        public string APIBaseURL => apiBaseURL;
        public string APIEndpoint => apiEndpoint;
        public List<Subject> ExcelSubjects => excelSubjects;
        public List<APISubjectData> APISubjects => apiSubjects;
        
        // Backward compatibility
        public List<Subject> Subjects => excelSubjects;

        /// <summary>
        /// Lấy path Excel theo ID (chỉ dùng cho mode Excel)
        /// </summary>
        public string GetExcelPath(int subjectIndex, int chapterIndex)
        {
            if (dataMode != QuizDataMode.Excel) return null;
            if (subjectIndex < 0 || subjectIndex >= excelSubjects.Count) return null;
            var subject = excelSubjects[subjectIndex];
            if (chapterIndex < 0 || chapterIndex >= subject.Chapters.Count) return null;
            return subject.Chapters[chapterIndex].ExcelPath;
        }

        /// <summary>
        /// Khởi tạo API Service (nếu mode = API)
        /// </summary>
        public void InitializeAPIService()
        {
            if (dataMode == QuizDataMode.API)
            {
                QuizAPIService.Instance.Configure(apiBaseURL, apiEndpoint);
            }
        }

        /// <summary>
        /// Fetch data từ API (async)
        /// </summary>
        public void FetchAPIData(Action<bool, string> onComplete)
        {
            if (dataMode != QuizDataMode.API)
            {
                onComplete?.Invoke(false, "Not in API mode");
                return;
            }

            InitializeAPIService();
            QuizAPIService.Instance.FetchQuizzes((success, message) =>
            {
                if (success)
                {
                    // Sync API data to local cache
                    SyncAPIDataToLocal();
                }
                onComplete?.Invoke(success, message);
            });
        }

        /// <summary>
        /// Đồng bộ dữ liệu từ QuizAPIService vào local cache
        /// </summary>
        public void SyncAPIDataToLocal()
        {
            apiSubjects.Clear();
            var cachedSubjects = QuizAPIService.Instance.GetCachedSubjects();
            
            foreach (var apiSubject in cachedSubjects)
            {
                var subjectData = new APISubjectData
                {
                    Id = apiSubject.Id,
                    Name = apiSubject.Name,
                    Grade = apiSubject.Grade,
                    Chapters = new List<APIChapterData>()
                };

                foreach (var chapter in apiSubject.Chapters)
                {
                    var chapterData = new APIChapterData
                    {
                        Id = chapter.Id,
                        Name = chapter.Name,
                        QuestionCount = chapter.Questions.Count
                    };
                    subjectData.Chapters.Add(chapterData);
                }

                apiSubjects.Add(subjectData);
            }

            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }

        /// <summary>
        /// So sánh và đồng bộ dữ liệu API mới với cache hiện tại
        /// Returns: QuizDataCompareResult chứa thông tin thay đổi
        /// </summary>
        public QuizDataCompareResult CompareAndSyncAPIData(List<APISubject> newSubjects)
        {
            var result = new QuizDataCompareResult();
            
            // Tạo dictionary cho dữ liệu cũ (by Id)
            var oldSubjectsDict = new Dictionary<string, APISubjectData>();
            foreach (var oldSubject in apiSubjects)
            {
                if (!string.IsNullOrEmpty(oldSubject.Id))
                    oldSubjectsDict[oldSubject.Id] = oldSubject;
            }
            
            // Tạo dictionary cho dữ liệu mới (by Id)
            var newSubjectsDict = new Dictionary<string, APISubject>();
            foreach (var newSubject in newSubjects)
            {
                if (!string.IsNullOrEmpty(newSubject.Id))
                    newSubjectsDict[newSubject.Id] = newSubject;
            }
            
            // Tìm subjects mới (có trong new, không có trong old)
            foreach (var newSubject in newSubjects)
            {
                if (!oldSubjectsDict.ContainsKey(newSubject.Id))
                {
                    result.NewSubjects.Add($"{newSubject.Name} (Lớp {newSubject.Grade})");
                    result.TotalNewChapters += newSubject.Chapters.Count;
                    foreach (var chapter in newSubject.Chapters)
                    {
                        result.TotalNewQuestions += chapter.Questions.Count;
                    }
                }
            }
            
            // Tìm subjects bị xóa (có trong old, không có trong new)
            foreach (var oldSubject in apiSubjects)
            {
                if (!newSubjectsDict.ContainsKey(oldSubject.Id))
                {
                    result.RemovedSubjects.Add($"{oldSubject.Name} (Lớp {oldSubject.Grade})");
                    result.TotalRemovedChapters += oldSubject.Chapters.Count;
                    foreach (var chapter in oldSubject.Chapters)
                    {
                        result.TotalRemovedQuestions += chapter.QuestionCount;
                    }
                }
            }
            
            // Tìm subjects có thay đổi
            foreach (var newSubject in newSubjects)
            {
                if (oldSubjectsDict.TryGetValue(newSubject.Id, out var oldSubject))
                {
                    var subjectChanges = CompareSubject(oldSubject, newSubject);
                    if (subjectChanges != null)
                    {
                        result.UpdatedSubjects.Add(subjectChanges);
                        result.TotalNewChapters += subjectChanges.NewChapters.Count;
                        result.TotalRemovedChapters += subjectChanges.RemovedChapters.Count;
                        result.TotalNewQuestions += subjectChanges.NewQuestionsCount;
                        result.TotalRemovedQuestions += subjectChanges.RemovedQuestionsCount;
                        result.TotalModifiedQuestions += subjectChanges.ModifiedQuestionsCount;
                    }
                }
            }
            
            // Cập nhật local cache với dữ liệu mới
            UpdateLocalCache(newSubjects);
            
            return result;
        }
        
        /// <summary>
        /// So sánh 2 subjects để tìm thay đổi
        /// </summary>
        private SubjectChanges CompareSubject(APISubjectData oldSubject, APISubject newSubject)
        {
            var changes = new SubjectChanges
            {
                SubjectName = $"{newSubject.Name} (Lớp {newSubject.Grade})",
                SubjectId = newSubject.Id
            };
            
            // Tạo dictionary chapters cũ
            var oldChaptersDict = new Dictionary<string, APIChapterData>();
            foreach (var chapter in oldSubject.Chapters)
            {
                if (!string.IsNullOrEmpty(chapter.Id))
                    oldChaptersDict[chapter.Id] = chapter;
            }
            
            // Kiểm tra chapters mới
            foreach (var newChapter in newSubject.Chapters)
            {
                if (!oldChaptersDict.TryGetValue(newChapter.Id, out var oldChapter))
                {
                    // Chapter mới
                    changes.NewChapters.Add(newChapter.Name);
                    changes.NewQuestionsCount += newChapter.Questions.Count;
                }
                else
                {
                    // So sánh số lượng questions
                    int oldCount = oldChapter.QuestionCount;
                    int newCount = newChapter.Questions.Count;
                    
                    if (newCount > oldCount)
                    {
                        changes.NewQuestionsCount += (newCount - oldCount);
                    }
                    else if (newCount < oldCount)
                    {
                        changes.RemovedQuestionsCount += (oldCount - newCount);
                    }
                    // TODO: có thể thêm logic so sánh chi tiết từng question nếu cần
                }
            }
            
            // Kiểm tra chapters bị xóa
            var newChaptersDict = new Dictionary<string, APIChapter>();
            foreach (var chapter in newSubject.Chapters)
            {
                if (!string.IsNullOrEmpty(chapter.Id))
                    newChaptersDict[chapter.Id] = chapter;
            }
            
            foreach (var oldChapter in oldSubject.Chapters)
            {
                if (!newChaptersDict.ContainsKey(oldChapter.Id))
                {
                    changes.RemovedChapters.Add(oldChapter.Name);
                    changes.RemovedQuestionsCount += oldChapter.QuestionCount;
                }
            }
            
            // Trả về null nếu không có thay đổi
            if (changes.NewChapters.Count == 0 && 
                changes.RemovedChapters.Count == 0 &&
                changes.NewQuestionsCount == 0 &&
                changes.RemovedQuestionsCount == 0 &&
                changes.ModifiedQuestionsCount == 0)
            {
                return null;
            }
            
            return changes;
        }
        
        /// <summary>
        /// Cập nhật local cache với dữ liệu mới
        /// </summary>
        private void UpdateLocalCache(List<APISubject> newSubjects)
        {
            apiSubjects.Clear();
            
            foreach (var apiSubject in newSubjects)
            {
                var subjectData = new APISubjectData
                {
                    Id = apiSubject.Id,
                    Name = apiSubject.Name,
                    Grade = apiSubject.Grade,
                    Chapters = new List<APIChapterData>()
                };

                foreach (var chapter in apiSubject.Chapters)
                {
                    var chapterData = new APIChapterData
                    {
                        Id = chapter.Id,
                        Name = chapter.Name,
                        QuestionCount = chapter.Questions.Count
                    };
                    subjectData.Chapters.Add(chapterData);
                }

                apiSubjects.Add(subjectData);
            }
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }

        /// <summary>
        /// Clear all API data cache
        /// </summary>
        public void ClearAPIDataCache()
        {
            apiSubjects.Clear();
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }

        /// <summary>
        /// Lấy tổng số subjects dựa trên mode
        /// </summary>
        public int GetSubjectCount()
        {
            if (dataMode == QuizDataMode.Excel)
                return excelSubjects.Count;
            else
                return QuizAPIService.Instance.GetCachedSubjects().Count;
        }

        /// <summary>
        /// Lấy tên subject theo index
        /// </summary>
        public string GetSubjectName(int index)
        {
            if (dataMode == QuizDataMode.Excel)
            {
                if (index < 0 || index >= excelSubjects.Count) return null;
                return excelSubjects[index].Name;
            }
            else
            {
                var apiSubject = QuizAPIService.Instance.GetSubject(index);
                return apiSubject != null ? $"{apiSubject.Name} - Lớp {apiSubject.Grade}" : null;
            }
        }

        /// <summary>
        /// Lấy số chapters của một subject
        /// </summary>
        public int GetChapterCount(int subjectIndex)
        {
            if (dataMode == QuizDataMode.Excel)
            {
                if (subjectIndex < 0 || subjectIndex >= excelSubjects.Count) return 0;
                return excelSubjects[subjectIndex].Chapters.Count;
            }
            else
            {
                var apiSubject = QuizAPIService.Instance.GetSubject(subjectIndex);
                return apiSubject?.Chapters.Count ?? 0;
            }
        }

        /// <summary>
        /// Lấy tên chapter
        /// </summary>
        public string GetChapterName(int subjectIndex, int chapterIndex)
        {
            if (dataMode == QuizDataMode.Excel)
            {
                if (subjectIndex < 0 || subjectIndex >= excelSubjects.Count) return null;
                var subject = excelSubjects[subjectIndex];
                if (chapterIndex < 0 || chapterIndex >= subject.Chapters.Count) return null;
                return subject.Chapters[chapterIndex].Name;
            }
            else
            {
                var apiChapter = QuizAPIService.Instance.GetChapter(subjectIndex, chapterIndex);
                return apiChapter?.Name;
            }
        }

        /// <summary>
        /// Lấy số câu hỏi trong chapter (chỉ cho API mode, Excel cần đọc file)
        /// </summary>
        public int GetQuestionCount(int subjectIndex, int chapterIndex)
        {
            if (dataMode == QuizDataMode.API)
            {
                return QuizAPIService.Instance.GetQuestionCount(subjectIndex, chapterIndex);
            }
            return -1; // Excel mode cần đọc từ file
        }
    }

    // ==================== EXCEL DATA CLASSES ====================

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

    // ==================== API DATA CLASSES (Local Cache) ====================

    [System.Serializable]
    public class APISubjectData
    {
        public string Id;
        public string Name;
        public string Grade;
        public List<APIChapterData> Chapters = new List<APIChapterData>();
    }

    [System.Serializable]
    public class APIChapterData
    {
        public string Id;
        public string Name;
        public int QuestionCount;
    }
}