using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using com.cyborgAssets.inspectorButtonPro;
using HMStudio.EasyQuiz;
using DreamClass.Network;
using DreamClass.Account;
using DreamClass.LoginManager;

namespace Gameplay.Exam
{
    /// <summary>
    /// Controller chính điều khiển bài kiểm tra
    /// Tích hợp Quiz (EasyQuiz) + Experiment (Gameplay)
    /// </summary>
    public class ExamController : MonoBehaviour
    {
        [Header("Cấu hình bài kiểm tra")]
        [SerializeField] private ExamData examData;
        [SerializeField] private QuizDatabase quizDatabase;

        [Header("Quiz References")]
        [SerializeField] private QuestionViewer questionViewer;
        [SerializeField] private QuestionManager questionManager;
        [Tooltip("GameObject chứa UI Quiz, sẽ tắt khi hoàn thành phần Quiz")]
        [SerializeField] private GameObject quizUIContainer;

        [Header("Finish Button")]
        [Tooltip("GameObject chứa button Hoàn thành bài thi")]
        [SerializeField] private GameObject finishButtonGameObject;

        [Header("Result Notification")]
        [Tooltip("Canvas hiển thị kết quả thi - sẽ active và tìm TMP để hiển thị")]
        [SerializeField] private Canvas resultCanvas;
        [SerializeField] private float resultDisplayDuration = 25f;

        [Header("Experiment References")]
        [SerializeField] private List<GameController> experimentControllers = new List<GameController>();

        [Header("Score Scaling & API")]
        [Tooltip("Tỷ lệ chuyển đổi điểm thành gold (1 exam score = X gold)")]
        [SerializeField] private float goldScaleRatio = 10f;
        [Tooltip("Tỷ lệ chuyển đổi điểm thành points (1 exam score = X points)")]
        [SerializeField] private float pointsScaleRatio = 5f;
        [Tooltip("API key để gửi request")]
        [SerializeField] private string apiKey = "quanganhancut";
        [Tooltip("ApiClient reference")]
        [SerializeField] private ApiClient apiClient;
        [Tooltip("UserProfile reference")]
        [SerializeField] private UserProfileSO userProfile;

        [Header("Trạng thái hiện tại")]
        [SerializeField] private bool isExamRunning = false;
        [SerializeField] private int currentSectionIndex = 0;
        [SerializeField] private float remainingTimeSeconds;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // ==================== EVENTS ====================
        public event Action OnExamStarted;
        public event Action OnExamFinished;
        public event Action OnFinishExamButtonPressed; // Thông báo khi nhấn button Finish
        public event Action<int, int> OnSectionChanged; // (currentIndex, totalCount)
        public event Action<float> OnTimeUpdated;
        public event Action<ExamResult> OnExamResultReady;

        // Quiz events
        public event Action<int, int> OnQuestionChanged;
        public event Action<bool> OnAnswerSubmitted;

        // Experiment events
        public event Action<string> OnExperimentStarted;
        public event Action<string, bool> OnExperimentStepCompleted;
        public event Action<string> OnExperimentFinished;

        // Unity Events
        [Header("Unity Events")]
        public UnityEvent<string> OnTimerTextUpdated;
        public UnityEvent<string> OnSectionNameUpdated;
        public UnityEvent<string> OnProgressTextUpdated;

        // ==================== INTERNAL STATE ====================
        private ExamResult currentResult;
        private SectionResult currentSectionResult;
        private Coroutine timerCoroutine;
        private bool isFinishingExam = false; // Chặn multiple FinishExam() calls
        private bool isSubmittingExam = false; // Track submission status

        // Quiz state
        private Dictionary<int, QuestionResult> answeredQuestions = new Dictionary<int, QuestionResult>();

        // Experiment state
        private GameController currentExperiment;
        private float experimentStartTime;
        private Dictionary<string, float> stepStartTimes = new Dictionary<string, float>();

        #region Properties

        public bool IsExamRunning => isExamRunning;
        public bool IsSubmittingExam => isSubmittingExam;
        public int CurrentSectionIndex => currentSectionIndex;
        public int TotalSections => examData?.sections?.Count ?? 0;
        public float RemainingTime => remainingTimeSeconds;
        public ExamData CurrentExamData => examData;
        public ExamSection CurrentSection => examData?.sections?[currentSectionIndex];

        #endregion

        #region Lifecycle

        private void Start()
        {
            // Kiểm tra login trước khi khởi tạo exam
            if (!IsUserLoggedIn())
            {
                Debug.LogWarning("[ExamController] User not logged in - Exam will not be available");
                
                // Ẩn exam UI elements nếu chưa login
                if (finishButtonGameObject != null)
                    finishButtonGameObject.SetActive(false);
                if (quizUIContainer != null)
                    quizUIContainer.SetActive(false);
                
                return;
            }

            LoadApiComponents();
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
            UnsubscribeQuizEvents();
            UnsubscribeExperimentEvents();
        }

        #endregion

        #region Public Methods - Exam Control

        /// <summary>
        /// Set ExamData và bắt đầu bài kiểm tra
        /// </summary>
        public void StartExam(ExamData newExamData)
        {
            if (newExamData == null)
            {
                Debug.LogError("[ExamController] Cannot start exam: ExamData is null!");
                return;
            }
            
            examData = newExamData;
            StartExam();
        }

        [ProButton]
        public void StartExam()
        {
            if (isExamRunning)
            {
                Debug.LogWarning("[ExamController] Exam is already running!");
                return;
            }

            if (examData == null || examData.sections.Count == 0)
            {
                Debug.LogError("[ExamController] ExamData not assigned or has no sections!");
                return;
            }

            // Reset finish flag
            isFinishingExam = false;
            isSubmittingExam = false;

            // Initialize
            currentResult = new ExamResult
            {
                examId = examData.examId,
                examName = examData.examName,
                startTime = DateTime.Now,
                maxScore = examData.maxScore
            };

            currentSectionIndex = 0;
            remainingTimeSeconds = examData.examDurationMinutes * 60f;
            isExamRunning = true;

            // Active finish button khi bắt đầu bài thi
            if (finishButtonGameObject != null)
            {
                finishButtonGameObject.SetActive(true);
                if (debugMode)
                    Debug.Log("[ExamController] Finish button activated");
            }

            // Hiện UI Container khi start
            if (quizUIContainer != null)
            {
                quizUIContainer.SetActive(true);
            }

            // Start timer
            if (timerCoroutine != null)
                StopCoroutine(timerCoroutine);
            timerCoroutine = StartCoroutine(TimerCoroutine());

            // Start first section
            StartCurrentSection();

            OnExamStarted?.Invoke();

            if (debugMode)
                Debug.Log($"[ExamController] Exam started: {examData.examName} with {examData.sections.Count} sections");
        }

        [ProButton]
        public void FinishExam()
        {
            // Kiểm tra login trước khi nộp bài
            if (!IsUserLoggedIn())
            {
                Debug.LogWarning("[ExamController] User must be logged in to finish exam!");
                ShowLoginRequiredMessage();
                return;
            }

            // Chặn multiple calls
            if (isFinishingExam) {
                if (debugMode)
                    Debug.LogWarning("[ExamController] FinishExam() already in progress - ignoring duplicate call");
                return;
            }

            if (!isExamRunning) return;

            // Log "Đang nộp"
            if (debugMode)
                Debug.Log("[ExamController] Đang nộp...");

            isFinishingExam = true;
            isSubmittingExam = true;
            isExamRunning = false;

            // Inactive finish button ngay lập tức
            if (finishButtonGameObject != null)
            {
                finishButtonGameObject.SetActive(false);
                if (debugMode)
                    Debug.Log("[ExamController] Finish button deactivated");
            }

            // Phát event để Announcer/UI xử lý
            OnFinishExamButtonPressed?.Invoke();

            // Stop timer
            if (timerCoroutine != null)
            {
                StopCoroutine(timerCoroutine);
                timerCoroutine = null;
            }

            // Finish current section
            FinishCurrentSection();

            // Calculate final result
            currentResult.Calculate(examData);

            // Log "Nộp thành công"
            if (debugMode)
                Debug.Log("[ExamController] Nộp thành công!");

            OnExamFinished?.Invoke();
            OnExamResultReady?.Invoke(currentResult);

            // Gửi điểm lên API
            StartCoroutine(SendScoreToAPI(currentResult));

            // Reset submission flag
            isSubmittingExam = false;

            if (debugMode)
                Debug.Log($"[ExamController] Exam finished!\n{currentResult.GetSummary()}");
        }

        [ProButton]
        public void NextSection()
        {
            if (!isExamRunning) return;

            FinishCurrentSection();

            currentSectionIndex++;
            if (currentSectionIndex >= examData.sections.Count)
            {
                FinishExam();
                return;
            }

            StartCurrentSection();
        }

        [ProButton]
        public void PreviousSection()
        {
            if (!isExamRunning) return;
            if (!examData.allowGoBack) return;

            FinishCurrentSection();

            currentSectionIndex--;
            if (currentSectionIndex < 0)
                currentSectionIndex = 0;

            StartCurrentSection();
        }

        #endregion

        #region Section Management

        private void StartCurrentSection()
        {
            var section = examData.sections[currentSectionIndex];

            currentSectionResult = new SectionResult
            {
                sectionId = section.sectionId,
                sectionName = section.sectionName,
                sectionType = section.sectionType,
                maxScore = section.maxScore
            };

            OnSectionChanged?.Invoke(currentSectionIndex, examData.sections.Count);
            OnSectionNameUpdated?.Invoke(section.sectionName);

            if (section.sectionType == ExamSectionType.Quiz)
            {
                StartQuizSection(section);
            }
            else
            {
                StartExperimentSection(section);
            }

            if (debugMode)
                Debug.Log($"[ExamController] Started section {currentSectionIndex + 1}: {section.sectionName} ({section.sectionType})");
        }

        private void FinishCurrentSection()
        {
            if (currentSectionResult == null) return;

            var section = examData.sections[currentSectionIndex];

            if (section.sectionType == ExamSectionType.Quiz)
            {
                FinishQuizSection(section);
            }
            else
            {
                FinishExperimentSection(section);
            }

            currentResult.sectionResults.Add(currentSectionResult);
            currentSectionResult = null;

            if (debugMode)
                Debug.Log($"[ExamController] Finished section: {section.sectionName}");
        }

        #endregion

        #region Quiz Section

        private void StartQuizSection(ExamSection section)
        {
            if (questionManager == null)
            {
                Debug.LogError("[ExamController] QuestionManager not assigned for Quiz section!");
                return;
            }

            // Clear previous data
            answeredQuestions.Clear();

            // Enable ExamMode on QuestionManager
            questionManager.EnableExamMode();
            SubscribeQuizEvents();

            // Start quiz with config from section
            questionManager.StartQuizWithConfig(
                quizDatabase,
                section.subjectIndex,
                section.chapterIndex,
                section.questionCount,
                section.shuffleQuestions
            );

            if (debugMode)
                Debug.Log($"[ExamController] Started Quiz section via QuestionManager [EXAM MODE]");
        }

        private void FinishQuizSection(ExamSection section)
        {
            // Unsubscribe events
            UnsubscribeQuizEvents();

            // Disable ExamMode
            if (questionManager != null)
            {
                questionManager.DisableExamMode();
            }

            // Tắt UI Quiz khi hoàn thành
            if (quizUIContainer != null)
            {
                quizUIContainer.SetActive(false);
                if (debugMode)
                    Debug.Log("[ExamController] Quiz UI disabled");
            }

            // Build results from answered questions
            currentSectionResult.questionResults.Clear();
            foreach (var kvp in answeredQuestions)
            {
                currentSectionResult.questionResults.Add(kvp.Value);
            }

            currentSectionResult.CalculateQuizScore(section, examData.penaltyForWrong, examData.penaltyPercent);
        }

        private void SubscribeQuizEvents()
        {
            if (questionManager == null) return;

            questionManager.OnExamAnswerSubmitted += HandleQuizAnswerSubmitted;
            questionManager.OnExamQuestionChanged += HandleQuizQuestionChanged;
            questionManager.OnExamQuizCompleted += HandleQuizCompleted;
        }

        private void UnsubscribeQuizEvents()
        {
            if (questionManager == null) return;

            questionManager.OnExamAnswerSubmitted -= HandleQuizAnswerSubmitted;
            questionManager.OnExamQuestionChanged -= HandleQuizQuestionChanged;
            questionManager.OnExamQuizCompleted -= HandleQuizCompleted;
        }

        private void HandleQuizAnswerSubmitted(int questionId, string questionText, string selectedAnswer, string correctAnswer, float timeSpent, bool isCorrect)
        {
            // Lưu kết quả vào dictionary
            var result = new QuestionResult(questionId, questionText, selectedAnswer, correctAnswer, timeSpent);
            answeredQuestions[questionId] = result;

            OnAnswerSubmitted?.Invoke(isCorrect);

            if (debugMode)
                Debug.Log($"[ExamController] Quiz answer received: Q{questionId} - {selectedAnswer} ({(isCorrect ? "Correct" : "Wrong")})");
        }

        private void HandleQuizQuestionChanged(int currentIndex, int totalCount)
        {
            OnQuestionChanged?.Invoke(currentIndex, totalCount);
            OnProgressTextUpdated?.Invoke($"Câu {currentIndex + 1}/{totalCount}");
        }

        private void HandleQuizCompleted()
        {
            if (debugMode)
                Debug.Log("[ExamController] Quiz section completed!");

            // Auto move to next section
            NextSection();
        }

        /// <summary>
        /// Submit answer từ UI (gọi qua QuestionManager)
        /// </summary>
        public void SubmitQuizAnswer(string answer)
        {
            if (!isExamRunning) return;
            if (CurrentSection?.sectionType != ExamSectionType.Quiz) return;

            if (questionManager != null)
            {
                questionManager.AnswerQuestion(answer);
            }
        }

        [ProButton]
        public void NextQuestion()
        {
            if (!isExamRunning) return;
            if (CurrentSection?.sectionType != ExamSectionType.Quiz) return;

            if (questionManager != null)
            {
                questionManager.NextQuestion();
            }
        }

        [ProButton]
        public void PreviousQuestion()
        {
            if (!isExamRunning) return;
            if (CurrentSection?.sectionType != ExamSectionType.Quiz) return;
            if (!examData.allowGoBack) return;

            if (questionManager != null)
            {
                questionManager.PrevQuestion();
            }
        }

        #endregion

        #region Experiment Section

        private void StartExperimentSection(ExamSection section)
        {
            // Find experiment controller
            currentExperiment = FindExperimentController(section.experimentName);

            if (currentExperiment == null)
            {
                Debug.LogError($"[ExamController] Experiment not found: {section.experimentName}");
                return;
            }

            // Initialize step results
            currentSectionResult.experimentName = section.experimentName;
            currentSectionResult.stepResults.Clear();
            stepStartTimes.Clear();

            foreach (string stepId in section.requiredStepIds)
            {
                var stepResult = new ExperimentStepResult(stepId, stepId);
                currentSectionResult.stepResults.Add(stepResult);
                stepStartTimes[stepId] = Time.time;
            }

            // === ENABLE EXAM MODE in GuideStepManager ===
            if (GuideStepManager.Instance != null)
            {
                // Load guide cho experiment này TRƯỚC khi enable exam mode
                GuideStepManager.Instance.SetCurrentGuide(section.experimentName);
                
                GuideStepManager.Instance.EnableExamMode();
                SubscribeGuideStepEvents();
            }

            // Subscribe to experiment events
            SubscribeExperimentEvents();

            // Setup and start experiment (OnActiveGame đã được gọi qua SetCurrentGuide)
            currentExperiment.SetupExperiment();
            currentExperiment.StartExperiment();
            experimentStartTime = Time.time;

            OnExperimentStarted?.Invoke(section.experimentName);
            OnProgressTextUpdated?.Invoke($"Thực hành: {section.sectionName}");

            if (debugMode)
                Debug.Log($"[ExamController] Started experiment: {section.experimentName} [EXAM MODE]");
        }

        private void FinishExperimentSection(ExamSection section)
        {
            // === DISABLE EXAM MODE in GuideStepManager ===
            if (GuideStepManager.Instance != null)
            {
                // Lấy summary từ GuideStepManager
                var examSummary = GuideStepManager.Instance.GetExamSummary();
                
                // Cập nhật error count từ GuideStepManager
                currentSectionResult.totalErrors = examSummary.totalErrors;
                
                UnsubscribeGuideStepEvents();
                GuideStepManager.Instance.DisableExamMode();
            }

            UnsubscribeExperimentEvents();

            if (currentExperiment != null)
            {
                currentExperiment.StopExperiment();
                currentExperiment = null;
            }

            currentSectionResult.CalculateExperimentScore(section);
            OnExperimentFinished?.Invoke(section.experimentName);
        }

        private void SubscribeGuideStepEvents()
        {
            if (GuideStepManager.Instance == null || GuideStepManager.Instance.examTracker == null) return;

            GuideStepManager.Instance.examTracker.OnExamError += HandleGuideStepError;
            GuideStepManager.Instance.examTracker.OnExamRollback += HandleGuideStepRollback;
            GuideStepManager.Instance.examTracker.OnExamStepCompleted += HandleGuideStepCompleted;
        }

        private void UnsubscribeGuideStepEvents()
        {
            if (GuideStepManager.Instance == null || GuideStepManager.Instance.examTracker == null) return;

            GuideStepManager.Instance.examTracker.OnExamError -= HandleGuideStepError;
            GuideStepManager.Instance.examTracker.OnExamRollback -= HandleGuideStepRollback;
            GuideStepManager.Instance.examTracker.OnExamStepCompleted -= HandleGuideStepCompleted;
        }

        private void HandleGuideStepError(string stepId, int totalErrors)
        {
            // Cập nhật error count trong section result
            var stepResult = currentSectionResult?.stepResults.Find(s => s.stepId == stepId);
            if (stepResult != null)
            {
                stepResult.errorCount++;
            }

            if (debugMode)
                Debug.Log($"[ExamController] Guide step error: {stepId} (Total: {totalErrors})");
        }

        private void HandleGuideStepRollback(string fromStepId)
        {
            if (debugMode)
                Debug.Log($"[ExamController] Guide step rollback from: {fromStepId}");
        }

        private void HandleGuideStepCompleted(string stepId)
        {
            // Cập nhật step result
            var stepResult = currentSectionResult?.stepResults.Find(s => s.stepId == stepId);
            if (stepResult != null && !stepResult.isCompleted)
            {
                stepResult.isCompleted = true;
                stepResult.timeToComplete = GuideStepManager.Instance.GetStepCompletionTime(stepId);

                OnExperimentStepCompleted?.Invoke(stepId, true);

                // Update progress
                int completed = 0;
                foreach (var s in currentSectionResult.stepResults)
                    if (s.isCompleted) completed++;
                OnProgressTextUpdated?.Invoke($"Hoàn thành: {completed}/{currentSectionResult.stepResults.Count} bước");

                if (debugMode)
                    Debug.Log($"[ExamController] Exam step completed: {stepId} in {stepResult.timeToComplete:F2}s");
            }
        }

        private GameController FindExperimentController(string experimentName)
        {
            foreach (var controller in experimentControllers)
            {
                if (controller != null && controller.GetExperimentName() == experimentName)
                    return controller;
            }

            // Fallback: find in scene
            var allControllers = FindObjectsByType<GameController>(FindObjectsSortMode.None);
            foreach (var controller in allControllers)
            {
                if (controller.GetExperimentName() == experimentName)
                    return controller;
            }

            return null;
        }

        private void SubscribeExperimentEvents()
        {
            if (currentExperiment == null) return;

            currentExperiment.OnGuideStepStatusChanged += HandleExperimentStepChanged;
            currentExperiment.OnExperimentCompleted += HandleExperimentCompleted;
        }

        private void UnsubscribeExperimentEvents()
        {
            if (currentExperiment == null) return;

            currentExperiment.OnGuideStepStatusChanged -= HandleExperimentStepChanged;
            currentExperiment.OnExperimentCompleted -= HandleExperimentCompleted;
        }

        private void HandleExperimentStepChanged(string stepId, bool isCompleted)
        {
            if (!isCompleted) return;

            // Find and update step result
            var stepResult = currentSectionResult.stepResults.Find(s => s.stepId == stepId);
            if (stepResult != null && !stepResult.isCompleted)
            {
                stepResult.isCompleted = true;
                stepResult.timeToComplete = Time.time - (stepStartTimes.ContainsKey(stepId) ? stepStartTimes[stepId] : experimentStartTime);

                OnExperimentStepCompleted?.Invoke(stepId, true);

                // Update progress
                int completed = 0;
                foreach (var s in currentSectionResult.stepResults)
                    if (s.isCompleted) completed++;

                OnProgressTextUpdated?.Invoke($"Hoàn thành: {completed}/{currentSectionResult.stepResults.Count} bước");

                if (debugMode)
                    Debug.Log($"[ExamController] Experiment step completed: {stepId}");
            }
        }

        private void HandleExperimentCompleted()
        {
            if (debugMode)
                Debug.Log("[ExamController] Experiment completed!");

            // Auto move to next section (optional)
            // NextSection();
        }

        /// <summary>
        /// Ghi nhận lỗi trong experiment
        /// </summary>
        public void RecordExperimentError(string stepId)
        {
            var stepResult = currentSectionResult?.stepResults.Find(s => s.stepId == stepId);
            if (stepResult != null)
            {
                stepResult.errorCount++;
                if (debugMode)
                    Debug.Log($"[ExamController] Error recorded for step: {stepId} (total: {stepResult.errorCount})");
            }
        }

        #endregion

        #region Timer

        private IEnumerator TimerCoroutine()
        {
            while (remainingTimeSeconds > 0 && isExamRunning)
            {
                yield return new WaitForSeconds(1f);
                remainingTimeSeconds -= 1f;

                OnTimeUpdated?.Invoke(remainingTimeSeconds);
                OnTimerTextUpdated?.Invoke(GetTimerText());

                if (remainingTimeSeconds <= 60 && debugMode)
                {
                    Debug.LogWarning($"[ExamController] Warning: {remainingTimeSeconds}s remaining!");
                }
            }

            // Chỉ tự động finish nếu chưa có finish call nào
            if (remainingTimeSeconds <= 0 && isExamRunning && !isFinishingExam)
            {
                if (debugMode)
                    Debug.Log("[ExamController] Time's up!");
                FinishExam();
            }
        }

        public string GetTimerText()
        {
            int mins = (int)(remainingTimeSeconds / 60);
            int secs = (int)(remainingTimeSeconds % 60);
            return $"{mins:D2}:{secs:D2}";
        }

        #endregion

        #region Utilities

        private void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public string GetCurrentScoreText()
        {
            if (CurrentSection?.sectionType == ExamSectionType.Quiz)
            {
                int correct = 0;
                foreach (var kv in answeredQuestions)
                    if (kv.Value.isCorrect) correct++;
                return $"Đúng: {correct}/{answeredQuestions.Count}";
            }
            else
            {
                int completed = 0;
                if (currentSectionResult?.stepResults != null)
                {
                    foreach (var s in currentSectionResult.stepResults)
                        if (s.isCompleted) completed++;
                    return $"Hoàn thành: {completed}/{currentSectionResult.stepResults.Count}";
                }
                return "";
            }
        }

        #endregion

        #region API Score Submission

        /// <summary>
        /// Data structure để gửi điểm lên API
        /// </summary>
        [System.Serializable]
        public class ScoreSubmissionData
        {
            public string key;
            public int gold;
            public int points;
        }

        /// <summary>
        /// Gửi điểm lên API sau khi hoàn thành bài thi với thông báo hoàn thành
        /// Similar to Quest system's ShowNotification pattern
        /// </summary>
        private IEnumerator SendScoreToAPI(ExamResult examResult)
        {
            // Kiểm tra dependencies
            if (apiClient == null)
            {
                Debug.LogWarning("[ExamController] ApiClient not assigned - Score submission skipped");
                ShowExamCompletionNotification(examResult, false);
                yield break;
            }

            if (userProfile == null || !userProfile.HasProfile)
            {
                Debug.LogWarning("[ExamController] UserProfile not available - Score submission skipped");
                ShowExamCompletionNotification(examResult, false);
                yield break;
            }

            string playerId = userProfile.playerId;
            if (string.IsNullOrEmpty(playerId))
            {
                Debug.LogWarning("[ExamController] Player ID is empty - Score submission skipped");
                ShowExamCompletionNotification(examResult, false);
                yield break;
            }

            // Tính toán điểm scaled (ưu tiên từ examData, fallback về settings)
            float goldRatio = examData?.goldScaleRatio ?? goldScaleRatio;
            float pointsRatio = examData?.pointsScaleRatio ?? pointsScaleRatio;
            
            int scaledGold = Mathf.RoundToInt(examResult.totalScore * goldRatio);
            int scaledPoints = Mathf.RoundToInt(examResult.totalScore * pointsRatio);

            // Tạo data để gửi
            ScoreSubmissionData scoreData = new ScoreSubmissionData
            {
                key = apiKey,
                gold = scaledGold,
                points = scaledPoints
            };

            string jsonData = JsonUtility.ToJson(scoreData);
            string endpoint = $"/api/players/test/currency/{playerId}";

            if (debugMode)
                Debug.Log($"[ExamController] Sending score to API: {endpoint}\n" +
                         $"Player ID: {playerId}\n" +
                         $"Exam Score: {examResult.totalScore:F1}\n" +
                         $"Gold: {scaledGold}, Points: {scaledPoints}\n" +
                         $"Data: {jsonData}");

            // Gửi request qua ApiClient
            var request = new ApiRequest(
                endpoint,
                "PUT",
                jsonData,
                new Dictionary<string, string> { { "Content-Type", "application/json" } }
            );

            bool requestCompleted = false;
            string responseText = "";
            bool requestSuccess = false;

            yield return StartCoroutine(apiClient.SendRequest(request, (response) =>
            {
                requestCompleted = true;
                responseText = response.Text;
                requestSuccess = response.IsSuccess;
                
                if (response.IsSuccess)
                {
                    if (debugMode)
                        Debug.Log($"[ExamController] Score submitted successfully! Response: {response.Text}");
                }
                else
                {
                    Debug.LogError($"[ExamController] Failed to submit score. Status: {response.StatusCode}, Error: {response.Error}");
                }
            }));

            // Wait for request completion
            while (!requestCompleted)
            {
                yield return null;
            }

            // Show completion notification similar to Quest system
            StartCoroutine(ShowExamCompletionNotificationCoroutine(examResult, requestSuccess, scaledGold, scaledPoints));
        }

        /// <summary>
        /// Show exam completion notification using Canvas and TMP
        /// Active canvas, find TMP in children, display results for 25s, then deactivate
        /// </summary>
        private IEnumerator ShowExamCompletionNotificationCoroutine(ExamResult examResult, bool apiSuccess, int goldEarned, int pointsEarned)
        {
            // Wait a short delay for smooth transition
            yield return new WaitForSeconds(0.5f);

            try
            {
                string examName = examData?.examName ?? "Bài thi";
                string completionTime = System.DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                string scoreText = examResult.totalScore.ToString("F1");

                // Log exam completion details
                Debug.Log($"[ExamController] Showing completion notification: Exam={examName}, Score={scoreText}, Gold={goldEarned}, Points={pointsEarned}, API Success={apiSuccess}");

                // Active the result canvas
                if (resultCanvas != null)
                {
                    resultCanvas.gameObject.SetActive(true);

                    // Find TextMeshPro in children
                    TMPro.TextMeshProUGUI resultText = resultCanvas.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                    
                    if (resultText != null)
                    {
                        // Create simplified result text
                        string detailedResult = $"=== KẾT QUẢ BÀI THI ===\n\n" +
                                              $"Tên bài thi: {examName}\n" +
                                              $"Thời gian hoàn thành: {completionTime}\n";

                        // Calculate score based on examData maxScore
                        float actualMaxScore = examData?.maxScore ?? 10f;
                        float actualScore = examResult.totalScore;
                        detailedResult += $"Điểm tổng: {actualScore:F1}/{actualMaxScore:F1}\n\n";

                        // // Add section breakdown if available
                        // if (examResult.sectionResults != null && examResult.sectionResults.Count > 0)
                        // {
                        //     detailedResult += "=== CHI TIẾT THEO PHẦN ===\n";
                        //     for (int i = 0; i < examResult.sectionResults.Count; i++)
                        //     {
                        //         var section = examResult.sectionResults[i];
                        //         string sectionName = examData?.sections?[i]?.sectionName ?? $"Phần {i + 1}";
                        //         float sectionAccuracy = section.totalQuestions > 0 ? (float)section.correctCount / section.totalQuestions * 100f : 0f;
                                
                        //         detailedResult += $"{sectionName}:\n" +
                        //                         $"   Đúng: {section.correctCount}/{section.totalQuestions} ({sectionAccuracy:F1}%)\n" +
                        //                         $"   Sai: {section.wrongCount} | Bỏ qua: {section.skippedCount}\n" +
                        //                         $"   Điểm: {section.score:F1}/{section.maxScore:F1}\n";
                        //     }
                        //     detailedResult += "\n";
                        // }

                        // Add rewards section only if API is successful
                        if (apiSuccess)
                        {
                            detailedResult += "=== PHẦN THƯỞNG ===\n";
                            detailedResult += $"Vàng nhận được: +{goldEarned} Gold\n" +
                                            $"Điểm kinh nghiệm: +{pointsEarned} Points\n";
                        }

                        // Add performance evaluation based on examData maxScore
                        float accuracy = actualMaxScore > 0 ? (actualScore / actualMaxScore) * 100f : 0f;
                        string performance = "";
                        if (accuracy >= 90) performance = "XUẤT SẮC";
                        else if (accuracy >= 80) performance = "TỐT";
                        else if (accuracy >= 70) performance = "KHÁ";
                        else if (accuracy >= 60) performance = "CẦN CỐ GẮNG";
                        else performance = "CẦN ÔN TẬP THÊM";
                        
                        detailedResult += $"=== ĐÁNH GIÁ ===\n" +
                                        $"{performance}\n" +
                                        $"Độ chính xác: {accuracy:F1}% ({actualScore:F1}/{actualMaxScore:F1})\n\n";
                        
                        resultText.text = detailedResult;
                        
                        Debug.Log($"[ExamController] Detailed result displayed on canvas for {resultDisplayDuration} seconds");
                    }
                    else
                    {
                        Debug.LogWarning("[ExamController] No TextMeshProUGUI found in result canvas children");
                    }
                }
                else
                {
                    Debug.LogWarning("[ExamController] Result canvas not assigned - notification skipped");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ExamController] Error showing completion notification: {ex.Message}");
            }

            // Wait for display duration outside try-catch, then deactivate
            yield return new WaitForSeconds(resultDisplayDuration);
            
            if (resultCanvas != null)
            {
                resultCanvas.gameObject.SetActive(false);
                Debug.Log("[ExamController] Result canvas deactivated");
            }
        }

        /// <summary>
        /// Show exam completion notification without API call (fallback method)
        /// </summary>
        private void ShowExamCompletionNotification(ExamResult examResult, bool success)
        {
            StartCoroutine(ShowExamCompletionNotificationCoroutine(examResult, success, 0, 0));
        }

        /// <summary>
        /// Test method để gửi điểm (dùng trong Editor)
        /// </summary>
        [ProButton]
        public void TestSendScore()
        {
            if (currentResult == null)
            {
                Debug.LogWarning("[ExamController] No exam result available for testing");
                return;
            }
            
            StartCoroutine(SendScoreToAPI(currentResult));
        }

        /// <summary>
        /// Kích hoạt exam sau khi user đã login
        /// Có thể được gọi từ LoginManager sau khi login thành công
        /// </summary>
        public void ActivateExamAfterLogin()
        {
            if (!IsUserLoggedIn())
            {
                Debug.LogWarning("[ExamController] Cannot activate exam - user not logged in");
                return;
            }

            Debug.Log("[ExamController] Activating exam after successful login");
            
            // Hiển thị lại UI elements
            if (finishButtonGameObject != null)
                finishButtonGameObject.SetActive(true);
            if (quizUIContainer != null)
                quizUIContainer.SetActive(true);
            
            // Load components if not already loaded
            LoadApiComponents();
        }

        /// <summary>
        /// Kiểm tra xem user đã login chưa
        /// </summary>
        private bool IsUserLoggedIn()
        {
            if (LoginManager.Instance == null)
            {
                Debug.LogError("[ExamController] LoginManager.Instance is null!");
                return false;
            }

            return LoginManager.Instance.IsLoggedIn();
        }

        /// <summary>
        /// Hiển thị thông báo yêu cầu login
        /// </summary>
        private void ShowLoginRequiredMessage()
        {
            // Hiển thị thông báo yêu cầu login trên resultCanvas
            if (resultCanvas != null)
            {
                resultCanvas.gameObject.SetActive(true);
                TMPro.TextMeshProUGUI resultText = resultCanvas.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                
                if (resultText != null)
                {
                    string loginMessage = "=== YÊU CẦU ĐĂNG NHẬP ===\n\n" +
                                         "Bạn cần đăng nhập để tham gia bài kiểm tra\n\n" +
                                         "Vui lòng đăng nhập và thử lại";
                    
                    resultText.text = loginMessage;
                    
                    // Tự động ẩn sau 5 giây
                    StartCoroutine(HideLoginMessageAfterDelay(5f));
                }
            }
            else
            {
                // Fallback: Log warning nếu không có resultCanvas
                Debug.LogWarning("[ExamController] Login required! Please log in first.");
            }
        }

        /// <summary>
        /// Ẩn thông báo login sau delay
        /// </summary>
        private IEnumerator HideLoginMessageAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (resultCanvas != null)
            {
                resultCanvas.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Load components automatically
        /// </summary>
        private void LoadApiComponents()
        {
            if (apiClient == null)
            {
                apiClient = FindFirstObjectByType<ApiClient>();
                if (debugMode && apiClient != null)
                    Debug.Log("[ExamController] ApiClient auto-loaded");
            }

            if (userProfile == null)
            {
                // Tìm ProfileService và lấy userProfile từ đó
                var profileService = FindFirstObjectByType<ProfileService>();
                if (profileService != null)
                {
                    // Sử dụng reflection để lấy userProfile field (nếu cần)
                    // Hoặc tạo public property trong ProfileService
                    if (debugMode)
                        Debug.Log("[ExamController] Found ProfileService - UserProfile should be assigned manually");
                }
            }
        }

        #endregion
    }
}
