using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using DreamClass.Network;
using DreamClass.LoginManager;
using com.cyborgAssets.inspectorButtonPro;

namespace HMStudio.EasyQuiz
{
    /// <summary>
    /// Service để fetch Quiz data từ API
    /// Sử dụng ApiClient có sẵn để xác thực
    /// Tự động fetch khi login thành công và gán vào QuizDatabase
    /// </summary>
    public class QuizAPIService : MonoBehaviour
    {
        [Header("API Configuration")]
        [SerializeField] private string baseURL = "http://localhost:3000";
        [SerializeField] private string quizzesEndpoint = "/api/quizzes";

        [Header("References")]
        [SerializeField] private ApiClient apiClient;
        [SerializeField] private QuizDatabase quizDatabase;

        [Header("Auto Fetch")]
        [SerializeField] private bool autoFetchOnLogin = true;
        [SerializeField] private bool autoSyncToDatabase = true;

        [Header("Cache")]
        [SerializeField] private float cacheExpirationSeconds = 300f; // 5 phút
        
        [Header("Debug Info")]
        [SerializeField] private bool isLoggedIn = false;
        [SerializeField] private int cachedSubjectCount = 0;
        [SerializeField] private string lastSyncTime = "Never";
        [SerializeField] private string lastCompareResult = "";
        
        private List<APISubject> cachedSubjects = new List<APISubject>();
        private float lastFetchTime = -999f;
        private bool isFetching = false;

        // Events
        public event Action<bool, string> OnQuizDataFetched;
        public event Action<QuizDataCompareResult> OnQuizDataChanged;

        // Singleton pattern
        private static QuizAPIService _instance;
        public static QuizAPIService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<QuizAPIService>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("QuizAPIService");
                        _instance = go.AddComponent<QuizAPIService>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Tìm ApiClient nếu chưa assign
            if (apiClient == null)
            {
                apiClient = FindFirstObjectByType<ApiClient>();
            }

            // Tìm QuizDatabase nếu chưa assign
            if (quizDatabase == null)
            {
                quizDatabase = Resources.Load<QuizDatabase>("QuizDatabase");
                if (quizDatabase == null)
                {
                    // Tìm trong Assets
                    #if UNITY_EDITOR
                    string[] guids = UnityEditor.AssetDatabase.FindAssets("t:QuizDatabase");
                    if (guids.Length > 0)
                    {
                        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                        quizDatabase = UnityEditor.AssetDatabase.LoadAssetAtPath<QuizDatabase>(path);
                    }
                    #endif
                }
            }
        }

        private void OnEnable()
        {
            // Subscribe vào LoginManager events
            LoginManager.OnLoginSuccess += OnLoginSuccess;
            LoginManager.OnLogoutSuccess += OnLogoutSuccess;
        }

        private void OnDisable()
        {
            // Unsubscribe
            LoginManager.OnLoginSuccess -= OnLoginSuccess;
            LoginManager.OnLogoutSuccess -= OnLogoutSuccess;
        }

        /// <summary>
        /// Set QuizDatabase reference
        /// </summary>
        public void SetQuizDatabase(QuizDatabase database)
        {
            quizDatabase = database;
        }

        /// <summary>
        /// Callback khi login thành công - tự động fetch quiz data
        /// </summary>
        private void OnLoginSuccess()
        {
            isLoggedIn = true;
            Debug.Log("[QuizAPIService] Login success detected!");
            
            if (autoFetchOnLogin)
            {
                Debug.Log("[QuizAPIService] Auto-fetching quiz data...");
                FetchQuizzesAndSync((success, message) =>
                {
                    if (success)
                    {
                        Debug.Log($"[QuizAPIService] Auto-fetch completed: {cachedSubjects.Count} subjects loaded");
                    }
                    else
                    {
                        Debug.LogWarning($"[QuizAPIService] Auto-fetch failed: {message}");
                    }
                });
            }
        }

        /// <summary>
        /// Callback khi logout - clear cache
        /// </summary>
        private void OnLogoutSuccess()
        {
            isLoggedIn = false;
            Debug.Log("[QuizAPIService] Logout detected, clearing cache...");
            ClearCache();
        }

        /// <summary>
        /// Cấu hình API URL
        /// </summary>
        public void Configure(string baseUrl, string endpoint = "/api/quizzes")
        {
            baseURL = baseUrl;
            quizzesEndpoint = endpoint;
        }

        /// <summary>
        /// Kiểm tra đã đăng nhập chưa
        /// </summary>
        [ProButton]
        public bool IsAuthenticated()
        {
            if (apiClient == null)
            {
                apiClient = FindFirstObjectByType<ApiClient>();
            }
            bool result = apiClient != null && apiClient.IsAuthenticated();
            isLoggedIn = result;
            Debug.Log($"[QuizAPIService] IsAuthenticated: {result}");
            return result;
        }

        /// <summary>
        /// Kiểm tra cache còn hợp lệ không
        /// </summary>
        [ProButton]
        public bool IsCacheValid()
        {
            bool result = cachedSubjects.Count > 0 && 
                   (Time.realtimeSinceStartup - lastFetchTime) < cacheExpirationSeconds;
            Debug.Log($"[QuizAPIService] IsCacheValid: {result} (Subjects: {cachedSubjects.Count})");
            return result;
        }

        /// <summary>
        /// Lấy danh sách subjects đã cache
        /// </summary>
        public List<APISubject> GetCachedSubjects() => cachedSubjects;

        /// <summary>
        /// Xóa cache
        /// </summary>
        [ProButton]
        public void ClearCache()
        {
            cachedSubjects.Clear();
            cachedSubjectCount = 0;
            lastFetchTime = -999f;
            Debug.Log("[QuizAPIService] Cache cleared!");
        }

        /// <summary>
        /// Fetch và sync vào QuizDatabase - Button test
        /// </summary>
        [ProButton]
        public void TestFetchAndSync()
        {
            FetchQuizzesAndSync((success, message) =>
            {
                Debug.Log($"[QuizAPIService] TestFetchAndSync result: {(success ? "SUCCESS" : "FAILED")} - {message}");
            });
        }

        /// <summary>
        /// Fetch quizzes từ API (sử dụng ApiClient) - Button test
        /// </summary>
        [ProButton]
        public void TestFetchQuizzes()
        {
            FetchQuizzes((success, message) =>
            {
                Debug.Log($"[QuizAPIService] TestFetch result: {(success ? "SUCCESS" : "FAILED")} - {message}");
            });
        }

        /// <summary>
        /// Fetch quizzes và tự động sync vào QuizDatabase
        /// </summary>
        public void FetchQuizzesAndSync(Action<bool, string> onComplete)
        {
            StartCoroutine(FetchQuizzesAndSyncCoroutine(onComplete));
        }

        /// <summary>
        /// Coroutine fetch và sync
        /// </summary>
        private IEnumerator FetchQuizzesAndSyncCoroutine(Action<bool, string> onComplete)
        {
            // Fetch data
            bool fetchSuccess = false;
            string fetchMessage = "";
            
            yield return FetchQuizzesCoroutine((success, msg) =>
            {
                fetchSuccess = success;
                fetchMessage = msg;
            });

            if (!fetchSuccess)
            {
                onComplete?.Invoke(false, fetchMessage);
                yield break;
            }

            // Sync vào QuizDatabase nếu được bật
            if (autoSyncToDatabase && quizDatabase != null)
            {
                // So sánh và sync
                var compareResult = quizDatabase.CompareAndSyncAPIData(cachedSubjects);
                
                lastSyncTime = DateTime.Now.ToString("HH:mm:ss");
                lastCompareResult = compareResult.HasChanges ? 
                    $"+{compareResult.NewSubjects.Count} subjects, ~{compareResult.UpdatedSubjects.Count} updated" : 
                    "No changes";
                
                if (compareResult.HasChanges)
                {
                    Debug.Log($"[QuizAPIService] {compareResult}");
                    OnQuizDataChanged?.Invoke(compareResult);
                }
                else
                {
                    Debug.Log("[QuizAPIService] No changes detected in quiz data");
                }
                
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(quizDatabase);
                #endif
            }

            onComplete?.Invoke(true, "Fetch and sync completed");
        }

        /// <summary>
        /// Force sync cached data vào QuizDatabase
        /// </summary>
        [ProButton]
        public void ForceSyncToDatabase()
        {
            if (quizDatabase == null)
            {
                Debug.LogError("[QuizAPIService] QuizDatabase not assigned!");
                return;
            }

            if (cachedSubjects.Count == 0)
            {
                Debug.LogWarning("[QuizAPIService] No cached data to sync!");
                return;
            }

            var compareResult = quizDatabase.CompareAndSyncAPIData(cachedSubjects);
            
            lastSyncTime = DateTime.Now.ToString("HH:mm:ss");
            lastCompareResult = compareResult.HasChanges ? 
                $"+{compareResult.NewSubjects.Count} subjects, ~{compareResult.UpdatedSubjects.Count} updated" : 
                "No changes";
            
            Debug.Log($"[QuizAPIService] Force sync completed: {compareResult}");
            
            if (compareResult.HasChanges)
            {
                OnQuizDataChanged?.Invoke(compareResult);
            }
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(quizDatabase);
            #endif
        }

        /// <summary>
        /// Fetch quizzes từ API (sử dụng ApiClient)
        /// </summary>
        public void FetchQuizzes(Action<bool, string> onComplete)
        {
            StartCoroutine(FetchQuizzesCoroutine(onComplete));
        }

        /// <summary>
        /// Fetch quizzes và chờ kết quả (async/await style)
        /// </summary>
        public IEnumerator FetchQuizzesCoroutine(Action<bool, string> onComplete = null)
        {
            if (isFetching)
            {
                Debug.LogWarning("[QuizAPIService] Already fetching...");
                onComplete?.Invoke(false, "Already fetching");
                yield break;
            }

            // Nếu cache còn valid, không cần fetch lại
            if (IsCacheValid())
            {
                Debug.Log("[QuizAPIService] Using cached data");
                onComplete?.Invoke(true, "Using cached data");
                yield break;
            }

            // Kiểm tra ApiClient
            if (apiClient == null)
            {
                apiClient = FindFirstObjectByType<ApiClient>();
            }

            if (apiClient == null)
            {
                Debug.LogError("[QuizAPIService] ApiClient not found!");
                onComplete?.Invoke(false, "ApiClient not found");
                yield break;
            }

            // Kiểm tra đã login chưa
            if (!apiClient.IsAuthenticated())
            {
                Debug.LogWarning("[QuizAPIService] Not authenticated! Please login first. Falling back to Excel mode.");
                onComplete?.Invoke(false, "Not authenticated - use Excel mode");
                yield break;
            }

            isFetching = true;
            Debug.Log($"[QuizAPIService] Fetching quizzes using ApiClient...");

            // Sử dụng ApiClient để gửi request (có auth header)
            ApiRequest request = new ApiRequest(quizzesEndpoint, "GET");
            ApiResponse response = null;

            yield return apiClient.SendRequest(request, (res) => response = res);

            isFetching = false;

            if (response != null && response.IsSuccess)
            {
                try
                {
                    string json = response.Text;
                    ProcessAPIResponse(json);
                    lastFetchTime = Time.realtimeSinceStartup;
                    cachedSubjectCount = cachedSubjects.Count;
                    Debug.Log($"[QuizAPIService] Successfully fetched {cachedSubjects.Count} subjects");
                    onComplete?.Invoke(true, "Success");
                    OnQuizDataFetched?.Invoke(true, "Success");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[QuizAPIService] Parse error: {ex.Message}");
                    onComplete?.Invoke(false, ex.Message);
                    OnQuizDataFetched?.Invoke(false, ex.Message);
                }
            }
            else
            {
                string error = response != null ? $"Status: {response.StatusCode}" : "No response";
                Debug.LogError($"[QuizAPIService] Request failed: {error}");
                onComplete?.Invoke(false, error);
                OnQuizDataFetched?.Invoke(false, error);
            }
        }

        /// <summary>
        /// In ra thông tin debug
        /// </summary>
        [ProButton]
        public void PrintDebugInfo()
        {
            Debug.Log("========== QuizAPIService Debug Info ==========");
            Debug.Log($"Is Authenticated: {IsAuthenticated()}");
            Debug.Log($"Is Cache Valid: {IsCacheValid()}");
            Debug.Log($"Cached Subjects: {cachedSubjects.Count}");
            Debug.Log($"Auto Fetch On Login: {autoFetchOnLogin}");
            Debug.Log($"Base URL: {baseURL}");
            Debug.Log($"Endpoint: {quizzesEndpoint}");
            
            if (cachedSubjects.Count > 0)
            {
                Debug.Log("--- Cached Subjects ---");
                for (int i = 0; i < cachedSubjects.Count; i++)
                {
                    var subject = cachedSubjects[i];
                    Debug.Log($"  [{i}] {subject.Name} (Grade {subject.Grade}) - {subject.Chapters.Count} chapters");
                    foreach (var chapter in subject.Chapters)
                    {
                        Debug.Log($"      - {chapter.Name}: {chapter.Questions.Count} questions");
                    }
                }
            }
            Debug.Log("================================================");
        }

        /// <summary>
        /// Xử lý response từ API và convert sang format internal
        /// </summary>
        private void ProcessAPIResponse(string json)
        {
            var response = JsonUtility.FromJson<QuizAPIResponse>(json);
            
            cachedSubjects.Clear();

            if (response?.data == null) return;

            // Group by subject name
            var subjectDict = new Dictionary<string, APISubject>();

            foreach (var quiz in response.data)
            {
                // Tạo key unique cho subject
                string subjectKey = $"{quiz.subject}_{quiz.grade}";
                
                if (!subjectDict.ContainsKey(subjectKey))
                {
                    subjectDict[subjectKey] = new APISubject
                    {
                        Id = quiz._id,
                        Name = quiz.subject,
                        Grade = quiz.grade,
                        Chapters = new List<APIChapter>()
                    };
                }

                var subject = subjectDict[subjectKey];

                // Add chapters từ quiz
                foreach (var chapter in quiz.chapters)
                {
                    var apiChapter = new APIChapter
                    {
                        Id = chapter._id,
                        Name = chapter.name,
                        Questions = new List<APIQuestion>()
                    };

                    int localId = 1;
                    foreach (var question in chapter.questions)
                    {
                        var apiQuestion = new APIQuestion
                        {
                            Id = question._id,
                            QuestionText = question.questionText,
                            Options = question.options.ToList(),
                            CorrectAnswer = "A", // Mặc định đáp án A (có thể cập nhật sau từ API)
                            LocalId = localId++
                        };
                        apiChapter.Questions.Add(apiQuestion);
                    }

                    subject.Chapters.Add(apiChapter);
                }
            }

            cachedSubjects = new List<APISubject>(subjectDict.Values);
        }

        /// <summary>
        /// Lấy subject theo index
        /// </summary>
        public APISubject GetSubject(int index)
        {
            if (index < 0 || index >= cachedSubjects.Count) return null;
            return cachedSubjects[index];
        }

        /// <summary>
        /// Lấy chapter theo subject và chapter index
        /// </summary>
        public APIChapter GetChapter(int subjectIndex, int chapterIndex)
        {
            var subject = GetSubject(subjectIndex);
            if (subject == null || chapterIndex < 0 || chapterIndex >= subject.Chapters.Count) 
                return null;
            return subject.Chapters[chapterIndex];
        }

        /// <summary>
        /// Lấy question theo subject, chapter và question index
        /// </summary>
        public APIQuestion GetQuestion(int subjectIndex, int chapterIndex, int questionLocalId)
        {
            var chapter = GetChapter(subjectIndex, chapterIndex);
            if (chapter == null) return null;
            
            return chapter.Questions.Find(q => q.LocalId == questionLocalId);
        }

        /// <summary>
        /// Lấy tổng số câu hỏi trong chapter
        /// </summary>
        public int GetQuestionCount(int subjectIndex, int chapterIndex)
        {
            var chapter = GetChapter(subjectIndex, chapterIndex);
            return chapter?.Questions.Count ?? 0;
        }

        /// <summary>
        /// Lấy danh sách tên subjects
        /// </summary>
        public List<string> GetSubjectNames()
        {
            var names = new List<string>();
            foreach (var subject in cachedSubjects)
            {
                names.Add($"{subject.Name} - Lớp {subject.Grade}");
            }
            return names;
        }

        /// <summary>
        /// Lấy danh sách tên chapters của một subject
        /// </summary>
        public List<string> GetChapterNames(int subjectIndex)
        {
            var names = new List<string>();
            var subject = GetSubject(subjectIndex);
            if (subject == null) return names;

            foreach (var chapter in subject.Chapters)
            {
                names.Add(chapter.Name);
            }
            return names;
        }

        // ==================== QUIZ SUBMIT ====================

        /// <summary>
        /// Event khi submit quiz hoàn thành
        /// </summary>
        public event Action<bool, QuizSubmitResult> OnQuizSubmitted;

        /// <summary>
        /// Submit quiz answers to API
        /// </summary>
        /// <param name="quizId">MongoDB _id của quiz (subject)</param>
        /// <param name="answers">Danh sách câu trả lời</param>
        /// <param name="onComplete">Callback với kết quả</param>
        public void SubmitQuiz(string quizId, List<QuizAnswerItem> answers, Action<bool, QuizSubmitResult> onComplete = null)
        {
            StartCoroutine(SubmitQuizCoroutine(quizId, answers, onComplete));
        }

        /// <summary>
        /// Submit quiz với subject index và chapter index
        /// </summary>
        public void SubmitQuizByIndex(int subjectIndex, int chapterIndex, List<QuizAnswerItem> answers, Action<bool, QuizSubmitResult> onComplete = null)
        {
            var subject = GetSubject(subjectIndex);
            if (subject == null)
            {
                Debug.LogError($"[QuizAPIService] Subject not found at index {subjectIndex}");
                onComplete?.Invoke(false, null);
                return;
            }

            SubmitQuiz(subject.Id, answers, onComplete);
        }

        /// <summary>
        /// Coroutine submit quiz
        /// </summary>
        private IEnumerator SubmitQuizCoroutine(string quizId, List<QuizAnswerItem> answers, Action<bool, QuizSubmitResult> onComplete)
        {
            if (!IsAuthenticated())
            {
                Debug.LogError("[QuizAPIService] Not authenticated! Cannot submit quiz.");
                onComplete?.Invoke(false, null);
                yield break;
            }

            if (string.IsNullOrEmpty(quizId))
            {
                Debug.LogError("[QuizAPIService] Quiz ID is empty!");
                onComplete?.Invoke(false, null);
                yield break;
            }

            // Build request
            var submitRequest = new QuizSubmitRequest { answers = answers };
            string jsonBody = JsonUtility.ToJson(submitRequest);
            string endpoint = $"/api/quizzes/{quizId}/submit";

            Debug.Log($"[QuizAPIService] Submitting quiz {quizId} with {answers.Count} answers...");
            Debug.Log($"[QuizAPIService] Request body: {jsonBody}");

            // Create API request
            var request = new DreamClass.Network.ApiRequest(endpoint, "POST", jsonBody);
            DreamClass.Network.ApiResponse response = null;

            yield return apiClient.SendRequest(request, (res) => response = res);

            if (response == null || !response.IsSuccess)
            {
                string error = response?.Error ?? "Unknown error";
                Debug.LogError($"[QuizAPIService] Submit failed: {error}");
                onComplete?.Invoke(false, null);
                OnQuizSubmitted?.Invoke(false, null);
                yield break;
            }

            // Parse response
            try
            {
                Debug.Log($"[QuizAPIService] Submit response: {response.Text}");
                
                var submitResponse = JsonUtility.FromJson<QuizSubmitResponse>(response.Text);
                
                if (submitResponse != null && submitResponse.data != null)
                {
                    var result = submitResponse.data;
                    Debug.Log($"[QuizAPIService] Submit success! Score: {result.correctCount}/{result.totalQuestions} ({result.GetPercentage():P0})");
                    
                    onComplete?.Invoke(true, result);
                    OnQuizSubmitted?.Invoke(true, result);
                }
                else
                {
                    Debug.LogError("[QuizAPIService] Failed to parse submit response");
                    onComplete?.Invoke(false, null);
                    OnQuizSubmitted?.Invoke(false, null);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuizAPIService] Exception parsing submit response: {ex.Message}");
                onComplete?.Invoke(false, null);
                OnQuizSubmitted?.Invoke(false, null);
            }
        }

        /// <summary>
        /// Helper: Tạo QuizAnswerItem từ questionId và answer key (A, B, C, D)
        /// </summary>
        public QuizAnswerItem CreateAnswer(string questionId, string answerKey)
        {
            return new QuizAnswerItem(questionId, answerKey);
        }

        /// <summary>
        /// Helper: Tạo danh sách answers từ dictionary
        /// </summary>
        public List<QuizAnswerItem> CreateAnswerList(Dictionary<string, string> answersDict)
        {
            var list = new List<QuizAnswerItem>();
            foreach (var kvp in answersDict)
            {
                list.Add(new QuizAnswerItem(kvp.Key, kvp.Value));
            }
            return list;
        }

        /// <summary>
        /// Lấy Quiz ID (subject ID) từ subject index
        /// </summary>
        public string GetQuizId(int subjectIndex)
        {
            var subject = GetSubject(subjectIndex);
            return subject?.Id;
        }

        /// <summary>
        /// Lấy Question ID từ indices
        /// </summary>
        public string GetQuestionId(int subjectIndex, int chapterIndex, int questionLocalId)
        {
            var question = GetQuestion(subjectIndex, chapterIndex, questionLocalId);
            return question?.Id;
        }
    }
}
