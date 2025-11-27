using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using com.cyborgAssets.inspectorButtonPro;
using HMStudio.EasyQuiz;

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

        [Header("Experiment References")]
        [SerializeField] private List<GameController> experimentControllers = new List<GameController>();

        [Header("Trạng thái hiện tại")]
        [SerializeField] private bool isExamRunning = false;
        [SerializeField] private int currentSectionIndex = 0;
        [SerializeField] private float remainingTimeSeconds;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // ==================== EVENTS ====================
        public event Action OnExamStarted;
        public event Action OnExamFinished;
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

        // Quiz state
        private Dictionary<int, QuestionResult> answeredQuestions = new Dictionary<int, QuestionResult>();

        // Experiment state
        private GameController currentExperiment;
        private float experimentStartTime;
        private Dictionary<string, float> stepStartTimes = new Dictionary<string, float>();

        #region Properties

        public bool IsExamRunning => isExamRunning;
        public int CurrentSectionIndex => currentSectionIndex;
        public int TotalSections => examData?.sections?.Count ?? 0;
        public float RemainingTime => remainingTimeSeconds;
        public ExamData CurrentExamData => examData;
        public ExamSection CurrentSection => examData?.sections?[currentSectionIndex];

        #endregion

        #region Lifecycle

        private void OnDestroy()
        {
            StopAllCoroutines();
            UnsubscribeQuizEvents();
            UnsubscribeExperimentEvents();
        }

        #endregion

        #region Public Methods - Exam Control

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
            if (!isExamRunning) return;

            isExamRunning = false;

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

            OnExamFinished?.Invoke();
            OnExamResultReady?.Invoke(currentResult);

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

            if (remainingTimeSeconds <= 0 && isExamRunning)
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
    }
}
