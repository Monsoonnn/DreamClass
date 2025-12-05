using System;
using System.Collections;
using System.Collections.Generic;
using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;

namespace HMStudio.EasyQuiz
{
    public class QuestionManager : MonoBehaviour
    {
        [Header("Reference")]
        [Tooltip("Reference to the QuestionViewer in the scene")]
        public QuestionViewer questionViewer;

        [Header("UI")]
        [Tooltip("GameObject chứa UI Quiz, sẽ tắt khi hoàn thành")]
        public GameObject quizUIContainer;

        // Variable to count the number of correct answers
        private int correctAnswersCount = 0;
        private HashSet<int> answeredQuestions = new HashSet<int>();  // Track câu đã trả lời

        // API Submit: Lưu câu trả lời để submit
        private List<QuizAnswerItem> submittedAnswers = new List<QuizAnswerItem>();

        public event Action<float> OnQuizComplete;

        /// <summary>
        /// Event khi submit API quiz hoàn thành (success, result)
        /// </summary>
        public event Action<bool, QuizSubmitResult> OnAPIQuizSubmitted;

        // ==================== EXAM MODE ====================
        [Header("Exam Mode")]
        [SerializeField] private bool isExamMode = false;
        public bool IsExamMode => isExamMode;

        /// <summary>
        /// Event khi trả lời câu hỏi trong ExamMode
        /// (questionId, questionText, selectedAnswer, correctAnswer, timeSpent, isCorrect)
        /// </summary>
        public event Action<int, string, string, string, float, bool> OnExamAnswerSubmitted;

        /// <summary>
        /// Event khi chuyển câu hỏi trong ExamMode
        /// (currentIndex, totalCount)
        /// </summary>
        public event Action<int, int> OnExamQuestionChanged;

        /// <summary>
        /// Event khi hoàn thành quiz trong ExamMode
        /// </summary>
        public event Action OnExamQuizCompleted;

        private float currentQuestionStartTime;

        /// <summary>
        /// Bật ExamMode - quiz sẽ gửi kết quả qua event thay vì tự xử lý
        /// </summary>
        public void EnableExamMode()
        {
            isExamMode = true;
            Debug.Log("[QuestionManager] EXAM MODE ENABLED");
        }

        /// <summary>
        /// Tắt ExamMode - quay về chế độ bình thường
        /// </summary>
        public void DisableExamMode()
        {
            isExamMode = false;
            Debug.Log("[QuestionManager] EXAM MODE DISABLED");
        }

        /// <summary>
        /// Selects an answer for the current question.
        /// Compares the answer with the correctAnswer of the QuestionViewer (case-insensitive).
        /// If the answer is correct, increments the number of correct answers.
        /// Then, automatically proceeds to the next question.
        /// Returns true if the answer is correct, false otherwise.
        /// </summary>
        /// <param name="answer">The selected answer</param>
        /// <returns>true if correct, false if incorrect</returns>
        public bool AnswerQuestion(string answer)
        {
            bool isCorrect = string.Equals(answer, questionViewer.correctAnswer, StringComparison.OrdinalIgnoreCase);
            float timeSpent = Time.time - currentQuestionStartTime;

            // === API MODE: Lưu câu trả lời để submit ===
            if (quizDatabase != null && quizDatabase.DataMode == QuizDataMode.API)
            {
                string apiQuestionId = questionViewer.CurrentAPIQuestionId;
                if (!string.IsNullOrEmpty(apiQuestionId))
                {
                    // Convert answer text thành key (A, B, C, D) nếu cần
                    string answerKey = ConvertAnswerToKey(answer);
                    
                    // Kiểm tra xem đã có answer cho question này chưa, nếu có thì update
                    int existingIndex = submittedAnswers.FindIndex(a => a.questionId == apiQuestionId);
                    if (existingIndex >= 0)
                    {
                        // Update answer cũ
                        submittedAnswers[existingIndex] = new QuizAnswerItem(apiQuestionId, answerKey);
                        Debug.Log($"[QuestionManager] API: Updated answer - {apiQuestionId}: {answerKey}");
                    }
                    else
                    {
                        // Thêm answer mới
                        submittedAnswers.Add(new QuizAnswerItem(apiQuestionId, answerKey));
                        Debug.Log($"[QuestionManager] API: Saved answer - {apiQuestionId}: {answerKey}");
                    }
                }
            }

            // === EXAM MODE: Gửi kết quả qua event ===
            if (isExamMode)
            {
                OnExamAnswerSubmitted?.Invoke(
                    questionViewer.questionID,
                    questionViewer.questionText,
                    answer,
                    questionViewer.correctAnswer,
                    timeSpent,
                    isCorrect
                );

                Debug.Log($"[QuestionManager] EXAM: Answer submitted - Q{questionViewer.questionID}: {answer} ({(isCorrect ? "Correct" : "Wrong")}) in {timeSpent:F2}s");
            }

            // Chế độ bình thường hoặc cả hai
            if (isCorrect)
            {
                correctAnswersCount++;
            }

            // Add ID hiện tại vào answered
            answeredQuestions.Add(questionViewer.questionID);

            // Check nếu hoàn thành (answered == total)
            int total = shuffledQuestionIDs.Count > 0 ? shuffledQuestionIDs.Count : GetTotalQuestions();
            if (answeredQuestions.Count >= total)
            {
                // Tắt UI Quiz khi hoàn thành
                if (quizUIContainer != null)
                {
                    quizUIContainer.SetActive(false);
                    Debug.Log("[QuestionManager] Quiz UI disabled");
                }

                // === API MODE: Tự động submit khi hoàn thành ===
                if (quizDatabase != null && quizDatabase.DataMode == QuizDataMode.API && submittedAnswers.Count > 0)
                {
                    Debug.Log("[QuestionManager] API Mode - Auto submitting quiz...");
                    SubmitQuizToAPI((success, result) =>
                    {
                        // Sau khi submit, fire event tương ứng
                        if (isExamMode)
                        {
                            OnExamQuizCompleted?.Invoke();
                        }
                        else
                        {
                            // Nếu submit thành công, dùng score từ API, nếu không dùng local score
                            float finalScore = (success && result != null) ? result.GetPercentage() : GetPoint();
                            OnQuizComplete?.Invoke(finalScore);
                        }
                    });
                }
                else
                {
                    // Excel mode hoặc không có answers để submit
                    if (isExamMode)
                    {
                        OnExamQuizCompleted?.Invoke();
                        Debug.Log("[QuestionManager] EXAM: Quiz completed!");
                    }
                    else
                    {
                        OnQuizComplete?.Invoke(GetPoint());
                    }
                }
            }

            return isCorrect;
        }

        [SerializeField] private QuizDatabase quizDatabase;  // Assign
        public int subjectID = 0;
        public int chapterID = 0;

        private List<int> shuffledQuestionIDs = new List<int>();
        private int currentQuestionIndex = 0;

        // API Fetch state
        private bool isAPIDataReady = false;
        private bool isFetchingAPI = false;

        /// <summary>
        /// Event khi fetch API hoàn thành
        /// </summary>
        public event Action<bool, string> OnAPIFetchComplete;

        /// <summary>
        /// Fetch dữ liệu từ API (nếu mode = API và đã login)
        /// </summary>
        public void FetchAPIData(Action<bool, string> onComplete = null)
        {
            if (quizDatabase == null)
            {
                Debug.LogError("[QuestionManager] QuizDatabase not assigned!");
                onComplete?.Invoke(false, "QuizDatabase not assigned");
                return;
            }

            if (quizDatabase.DataMode != QuizDataMode.API)
            {
                Debug.Log("[QuestionManager] Not in API mode, skipping fetch");
                isAPIDataReady = true;
                onComplete?.Invoke(true, "Excel mode - no fetch needed");
                return;
            }

            // Kiểm tra đã login chưa
            if (!QuizAPIService.Instance.IsAuthenticated())
            {
                Debug.LogWarning("[QuestionManager] Not authenticated! Falling back to Excel mode.");
                isAPIDataReady = false;
                onComplete?.Invoke(false, "Not authenticated - fallback to Excel");
                return;
            }

            if (isFetchingAPI)
            {
                Debug.LogWarning("[QuestionManager] Already fetching API data...");
                return;
            }

            isFetchingAPI = true;
            Debug.Log("[QuestionManager] Fetching API data...");

            quizDatabase.FetchAPIData((success, message) =>
            {
                isFetchingAPI = false;
                isAPIDataReady = success;
                
                if (success)
                {
                    Debug.Log($"[QuestionManager] API fetch successful: {message}");
                }
                else
                {
                    Debug.LogError($"[QuestionManager] API fetch failed: {message}");
                }

                onComplete?.Invoke(success, message);
                OnAPIFetchComplete?.Invoke(success, message);
            });
        }

        /// <summary>
        /// Kiểm tra có thể dùng API mode không (đã login và mode = API)
        /// </summary>
        public bool CanUseAPIMode()
        {
            return quizDatabase != null && 
                   quizDatabase.DataMode == QuizDataMode.API && 
                   QuizAPIService.Instance.IsAuthenticated();
        }

        /// <summary>
        /// Kiểm tra API data đã sẵn sàng chưa
        /// </summary>
        public bool IsAPIDataReady => isAPIDataReady || (quizDatabase != null && quizDatabase.DataMode == QuizDataMode.Excel);

        /// <summary>
        /// Start quiz với cấu hình từ ExamController
        /// </summary>
        public void StartQuizWithConfig(QuizDatabase database, int subject, int chapter, int questionCount, bool shuffle)
        {
            quizDatabase = database;
            subjectID = subject;
            chapterID = chapter;

            // Nếu mode API, kiểm tra đã login chưa
            if (database.DataMode == QuizDataMode.API)
            {
                if (!QuizAPIService.Instance.IsAuthenticated())
                {
                    Debug.LogWarning("[QuestionManager] API mode but not authenticated! Falling back to Excel mode.");
                    // Tự động chuyển sang dùng Excel data
                    StartQuizInternalWithExcelFallback(database, subject, chapter, questionCount, shuffle);
                    return;
                }

                // Đã login, fetch nếu chưa có cache
                if (!QuizAPIService.Instance.IsCacheValid())
                {
                    Debug.Log("[QuestionManager] API mode detected, fetching data first...");
                    FetchAPIData((success, message) =>
                    {
                        if (success)
                        {
                            StartQuizInternal(database, subject, chapter, questionCount, shuffle);
                        }
                        else
                        {
                            Debug.LogWarning($"[QuestionManager] API fetch failed: {message}. Falling back to Excel.");
                            StartQuizInternalWithExcelFallback(database, subject, chapter, questionCount, shuffle);
                        }
                    });
                    return;
                }
            }

            StartQuizInternal(database, subject, chapter, questionCount, shuffle);
        }

        /// <summary>
        /// Start quiz với Excel data khi API không khả dụng
        /// </summary>
        private void StartQuizInternalWithExcelFallback(QuizDatabase database, int subject, int chapter, int questionCount, bool shuffle)
        {
            Debug.Log("[QuestionManager] Starting quiz with Excel fallback...");
            
            correctAnswersCount = 0;
            answeredQuestions.Clear();
            submittedAnswers.Clear(); // Clear answers khi bắt đầu quiz mới

            if (questionViewer == null)
            {
                Debug.LogError("QuestionViewer not assigned!");
                return;
            }

            questionViewer.quizDatabase = database;
            questionViewer.subjectID = subject;
            questionViewer.chapterID = chapter;
            
            // Force Excel mode
            questionViewer.excelFilePath = database.GetExcelPath(subject, chapter);
            
            if (string.IsNullOrEmpty(questionViewer.excelFilePath))
            {
                Debug.LogError("Excel fallback failed - no Excel path configured!");
                return;
            }

            Debug.Log($"[QuestionManager] Using Excel file: {questionViewer.excelFilePath}");

            // Tạo danh sách ID từ Excel
            int total = questionViewer.GetTotalQuestions();
            shuffledQuestionIDs.Clear();
            for (int i = 1; i <= total; i++)
                shuffledQuestionIDs.Add(i);

            if (shuffle)
                ShuffleList(shuffledQuestionIDs);

            if (questionCount > 0 && questionCount < shuffledQuestionIDs.Count)
            {
                shuffledQuestionIDs = shuffledQuestionIDs.GetRange(0, questionCount);
            }

            currentQuestionIndex = 0;
            questionViewer.questionID = shuffledQuestionIDs[currentQuestionIndex];
            questionViewer.LoadQuestionFromExcel(); // Force Excel load
            currentQuestionStartTime = Time.time;

            if (isExamMode)
            {
                OnExamQuestionChanged?.Invoke(currentQuestionIndex, shuffledQuestionIDs.Count);
            }

            Debug.Log($"Quiz started with Excel fallback: {shuffledQuestionIDs.Count} questions");
        }

        /// <summary>
        /// Internal method to start quiz after data is ready
        /// </summary>
        private void StartQuizInternal(QuizDatabase database, int subject, int chapter, int questionCount, bool shuffle)
        {
            correctAnswersCount = 0;
            answeredQuestions.Clear();
            submittedAnswers.Clear(); // Clear answers khi bắt đầu quiz mới

            if (questionViewer == null)
            {
                Debug.LogError("QuestionViewer not assigned!");
                return;
            }

            questionViewer.quizDatabase = database;
            questionViewer.subjectID = subject;
            questionViewer.chapterID = chapter;
            questionViewer.UpdateExcelPath();

            // Kiểm tra dữ liệu dựa trên mode
            if (database.DataMode == QuizDataMode.Excel)
            {
                if (string.IsNullOrEmpty(questionViewer.excelFilePath))
                {
                    Debug.LogError("Excel path is empty! Check QuizDatabase or fallback.");
                    return;
                }
            }
            // API mode không cần check excel path

            // Tạo danh sách ID
            int total = GetTotalQuestions();
            shuffledQuestionIDs.Clear();
            for (int i = 1; i <= total; i++)
                shuffledQuestionIDs.Add(i);

            // Trộn danh sách câu hỏi nếu cần
            if (shuffle)
                ShuffleList(shuffledQuestionIDs);

            // Giới hạn số câu hỏi nếu questionCount > 0
            if (questionCount > 0 && questionCount < shuffledQuestionIDs.Count)
            {
                shuffledQuestionIDs = shuffledQuestionIDs.GetRange(0, questionCount);
            }

            // Bắt đầu từ câu đầu tiên
            currentQuestionIndex = 0;
            questionViewer.questionID = shuffledQuestionIDs[currentQuestionIndex];
            questionViewer.LoadQuestion(); // Sử dụng LoadQuestion() thay vì LoadQuestionFromExcel()
            currentQuestionStartTime = Time.time;

            // Fire event cho ExamMode
            if (isExamMode)
            {
                OnExamQuestionChanged?.Invoke(currentQuestionIndex, shuffledQuestionIDs.Count);
            }

            Debug.Log($"Quiz started with {shuffledQuestionIDs.Count} questions (shuffle={shuffle}) [ExamMode={isExamMode}] [Mode={database.DataMode}]");
        }

        [ProButton]
        public void StartQuiz()
        {
            Debug.Log("StartQuiz called! SubjectID: " + subjectID + ", ChapterID: " + chapterID);

            // Nếu mode API, kiểm tra login
            if (quizDatabase != null && quizDatabase.DataMode == QuizDataMode.API)
            {
                if (!QuizAPIService.Instance.IsAuthenticated())
                {
                    Debug.LogWarning("[QuestionManager] API mode but not authenticated! Falling back to Excel.");
                    StartQuizInternalWithExcelFallback(quizDatabase, subjectID, chapterID, 0, true);
                    return;
                }

                // Đã login, fetch nếu chưa có cache
                if (!QuizAPIService.Instance.IsCacheValid())
                {
                    Debug.Log("[QuestionManager] API mode detected, fetching data first...");
                    FetchAPIData((success, message) =>
                    {
                        if (success)
                        {
                            StartQuizInternal();
                        }
                        else
                        {
                            Debug.LogWarning($"[QuestionManager] API fetch failed: {message}. Falling back to Excel.");
                            StartQuizInternalWithExcelFallback(quizDatabase, subjectID, chapterID, 0, true);
                        }
                    });
                    return;
                }
            }

            StartQuizInternal();
        }

        /// <summary>
        /// Internal StartQuiz after API data ready
        /// </summary>
        private void StartQuizInternal()
        {
            correctAnswersCount = 0;
            answeredQuestions.Clear();
            submittedAnswers.Clear(); // Clear answers khi bắt đầu quiz mới

            if (questionViewer == null)
            {
                Debug.LogError("QuestionViewer not assigned!");
                return;
            }

            questionViewer.quizDatabase = quizDatabase;
            questionViewer.subjectID = subjectID;
            questionViewer.chapterID = chapterID;
            questionViewer.UpdateExcelPath();

            // Kiểm tra dữ liệu dựa trên mode
            if (quizDatabase != null && quizDatabase.DataMode == QuizDataMode.Excel)
            {
                if (string.IsNullOrEmpty(questionViewer.excelFilePath))
                {
                    Debug.LogError("Excel path is empty! Check QuizDatabase or fallback.");
                    return;
                }
            }

            // Tạo danh sách ID
            int total = GetTotalQuestions();
            shuffledQuestionIDs.Clear();
            for (int i = 1; i <= total; i++)
                shuffledQuestionIDs.Add(i);

            // Trộn danh sách câu hỏi
            ShuffleList(shuffledQuestionIDs);

            // Bắt đầu từ câu đầu tiên trong danh sách trộn
            currentQuestionIndex = 0;
            questionViewer.questionID = shuffledQuestionIDs[currentQuestionIndex];
            questionViewer.LoadQuestion(); // Sử dụng LoadQuestion() thay vì LoadQuestionFromExcel()
            currentQuestionStartTime = Time.time;

            // Fire event cho ExamMode
            if (isExamMode)
            {
                OnExamQuestionChanged?.Invoke(currentQuestionIndex, shuffledQuestionIDs.Count);
            }

            string modeStr = quizDatabase != null ? quizDatabase.DataMode.ToString() : "Unknown";
            Debug.Log($"Quiz started with shuffled questions! [Mode={modeStr}]" + (isExamMode ? " [EXAM MODE]" : ""));
        }

        /// <summary>
        /// Proceeds to the next question.
        /// If it exceeds the total number of questions, returns to the first question.
        /// Then loads the data of the new question.
        /// </summary>
        [ProButton]
        public void NextQuestion()
        {
            if (shuffledQuestionIDs.Count == 0)
            {
                Debug.LogError("Question list not shuffled! Call StartQuiz first.");
                return;
            }

            currentQuestionIndex++;
            
            // Trong ExamMode: không quay lại đầu, báo hoàn thành
            if (currentQuestionIndex >= shuffledQuestionIDs.Count)
            {
                if (isExamMode)
                {
                    currentQuestionIndex = shuffledQuestionIDs.Count - 1; // Giữ ở câu cuối
                    OnExamQuizCompleted?.Invoke();
                    Debug.Log("[QuestionManager] EXAM: All questions completed!");
                    return;
                }
                currentQuestionIndex = 0; // Chế độ bình thường: quay lại đầu
            }

            questionViewer.questionID = shuffledQuestionIDs[currentQuestionIndex];
            questionViewer.LoadQuestion(); // Sử dụng LoadQuestion()
            currentQuestionStartTime = Time.time;

            if (isExamMode)
            {
                OnExamQuestionChanged?.Invoke(currentQuestionIndex, shuffledQuestionIDs.Count);
            }

            Debug.Log($"Loaded next shuffled question ID: {questionViewer.questionID}" + (isExamMode ? " [EXAM MODE]" : ""));
        }

        [ProButton]
        public void PrevQuestion()
        {
            if (shuffledQuestionIDs.Count == 0)
            {
                Debug.LogError("Question list not shuffled! Call StartQuiz first.");
                return;
            }

            currentQuestionIndex--;
            if (currentQuestionIndex < 0)
            {
                if (isExamMode)
                {
                    currentQuestionIndex = 0; // Trong ExamMode: giữ ở câu đầu
                    return;
                }
                currentQuestionIndex = shuffledQuestionIDs.Count - 1; // Chế độ bình thường: quay lại cuối
            }

            questionViewer.questionID = shuffledQuestionIDs[currentQuestionIndex];
            questionViewer.LoadQuestion(); // Sử dụng LoadQuestion()
            currentQuestionStartTime = Time.time;

            if (isExamMode)
            {
                OnExamQuestionChanged?.Invoke(currentQuestionIndex, shuffledQuestionIDs.Count);
            }

            Debug.Log($"Loaded previous shuffled question ID: {questionViewer.questionID}" + (isExamMode ? " [EXAM MODE]" : ""));
        }

        /// <summary>
        /// Lấy index câu hỏi hiện tại
        /// </summary>
        public int GetCurrentQuestionIndex() => currentQuestionIndex;

        /// <summary>
        /// Lấy tổng số câu hỏi trong quiz hiện tại
        /// </summary>
        public int GetCurrentQuizQuestionCount() => shuffledQuestionIDs.Count;

        /// <summary>
        /// Kiểm tra đã trả lời câu hỏi hiện tại chưa
        /// </summary>
        public bool IsCurrentQuestionAnswered()
        {
            if (shuffledQuestionIDs.Count == 0) return false;
            return answeredQuestions.Contains(shuffledQuestionIDs[currentQuestionIndex]);
        }


        /// <summary>
        /// Returns the current score (number of correct answers).
        /// </summary>
        /// <returns>The score as a float</returns>
        public float GetPoint()
        {
            return (float)correctAnswersCount / GetTotalQuestions();
        }

        /// <summary>
        /// Gets the current question information in the format: "Question {currentIndex + 1} / {totalQuestions}"
        /// </summary>
        /// <returns>The information string</returns>
        public string GetInfo()
        {
            int total = shuffledQuestionIDs.Count > 0 ? shuffledQuestionIDs.Count : GetTotalQuestions();
            int current = currentQuestionIndex + 1;
            return $"Câu hỏi {current} / {total}";
        }

        /// <summary>
        /// Gets the statistics in the format "number of correct answers / total number of questions".
        /// </summary>
        /// <returns>The statistics string</returns>
        public string GetStatistic()
        {
            int total = GetTotalQuestions();
            return $"Tỉ lệ đúng: {correctAnswersCount} / {total}";
        }

        /// <summary>
        /// Reads the Excel file to count the total number of questions.
        /// Assumes the Excel file has a header in row 0 and data starting from row 1.
        /// </summary>
        /// <returns>The total number of questions</returns>

        public int GetTotalQuestions()
        {
            return questionViewer.GetTotalQuestions();
        }

        private void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ==================== API SUBMIT HELPERS ====================

        /// <summary>
        /// Convert answer text thành key (A, B, C, D) dựa trên options
        /// </summary>
        private string ConvertAnswerToKey(string answerText)
        {
            // Nếu answer đã là key (1 ký tự A-D), return luôn
            if (answerText.Length == 1 && answerText[0] >= 'A' && answerText[0] <= 'D')
            {
                return answerText;
            }

            // Tìm index của answer trong options
            for (int i = 0; i < questionViewer.options.Count; i++)
            {
                if (string.Equals(questionViewer.options[i], answerText, StringComparison.OrdinalIgnoreCase))
                {
                    return ((char)('A' + i)).ToString();
                }
            }

            // Fallback: return answer gốc
            return answerText;
        }

        /// <summary>
        /// Submit quiz answers lên API
        /// </summary>
        public void SubmitQuizToAPI(System.Action<bool, QuizSubmitResult> onComplete = null)
        {
            if (quizDatabase == null || quizDatabase.DataMode != QuizDataMode.API)
            {
                Debug.LogWarning("[QuestionManager] Not in API mode, cannot submit to API");
                onComplete?.Invoke(false, null);
                return;
            }

            if (submittedAnswers.Count == 0)
            {
                Debug.LogWarning("[QuestionManager] No answers to submit");
                onComplete?.Invoke(false, null);
                return;
            }

            // Lấy quiz ID từ subject
            string quizId = QuizAPIService.Instance.GetQuizId(subjectID);
            if (string.IsNullOrEmpty(quizId))
            {
                Debug.LogError($"[QuestionManager] Cannot get Quiz ID for subject {subjectID}");
                onComplete?.Invoke(false, null);
                return;
            }

            Debug.Log($"[QuestionManager] Submitting {submittedAnswers.Count} answers to quiz {quizId}...");

            QuizAPIService.Instance.SubmitQuiz(quizId, submittedAnswers, (success, result) =>
            {
                if (success && result != null)
                {
                    Debug.Log($"[QuestionManager] API Submit Success! Score: {result.correctCount}/{result.totalQuestions} ({result.GetPercentage():P0})");
                }
                else
                {
                    Debug.LogError("[QuestionManager] API Submit Failed!");
                }

                onComplete?.Invoke(success, result);
                OnAPIQuizSubmitted?.Invoke(success, result);
            });
        }

        /// <summary>
        /// Lấy danh sách câu trả lời đã lưu
        /// </summary>
        public List<QuizAnswerItem> GetSubmittedAnswers() => new List<QuizAnswerItem>(submittedAnswers);

        /// <summary>
        /// Clear submitted answers
        /// </summary>
        public void ClearSubmittedAnswers()
        {
            submittedAnswers.Clear();
        }

    }
}