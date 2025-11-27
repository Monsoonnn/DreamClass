using System;
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

        public event Action<float> OnQuizComplete;

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

            return isCorrect;
        }

        [SerializeField] private QuizDatabase quizDatabase;  // Assign
        public int subjectID = 0;
        public int chapterID = 0;

        private List<int> shuffledQuestionIDs = new List<int>();
        private int currentQuestionIndex = 0;

        /// <summary>
        /// Start quiz với cấu hình từ ExamController
        /// </summary>
        public void StartQuizWithConfig(QuizDatabase database, int subject, int chapter, int questionCount, bool shuffle)
        {
            quizDatabase = database;
            subjectID = subject;
            chapterID = chapter;

            correctAnswersCount = 0;
            answeredQuestions.Clear();

            if (questionViewer == null)
            {
                Debug.LogError("QuestionViewer not assigned!");
                return;
            }

            questionViewer.quizDatabase = database;
            questionViewer.subjectID = subject;
            questionViewer.chapterID = chapter;
            questionViewer.UpdateExcelPath();

            if (string.IsNullOrEmpty(questionViewer.excelFilePath))
            {
                Debug.LogError("Excel path is empty! Check QuizDatabase or fallback.");
                return;
            }

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
            questionViewer.LoadQuestionFromExcel();
            currentQuestionStartTime = Time.time;

            // Fire event cho ExamMode
            if (isExamMode)
            {
                OnExamQuestionChanged?.Invoke(currentQuestionIndex, shuffledQuestionIDs.Count);
            }

            Debug.Log($"Quiz started with {shuffledQuestionIDs.Count} questions (shuffle={shuffle}) [ExamMode={isExamMode}]");
        }

        [ProButton]
        public void StartQuiz()
        {
            Debug.Log("StartQuiz called! SubjectID: " + subjectID + ", ChapterID: " + chapterID);

            correctAnswersCount = 0;
            answeredQuestions.Clear();

            if (questionViewer == null)
            {
                Debug.LogError("QuestionViewer not assigned!");
                return;
            }

            questionViewer.quizDatabase = quizDatabase;
            questionViewer.subjectID = subjectID;
            questionViewer.chapterID = chapterID;
            questionViewer.UpdateExcelPath();

            if (string.IsNullOrEmpty(questionViewer.excelFilePath))
            {
                Debug.LogError("Excel path is empty! Check QuizDatabase or fallback.");
                return;
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
            questionViewer.LoadQuestionFromExcel();
            currentQuestionStartTime = Time.time;

            // Fire event cho ExamMode
            if (isExamMode)
            {
                OnExamQuestionChanged?.Invoke(currentQuestionIndex, shuffledQuestionIDs.Count);
            }

            Debug.Log("Quiz started with shuffled questions!" + (isExamMode ? " [EXAM MODE]" : ""));
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
            questionViewer.LoadQuestionFromExcel();
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
            questionViewer.LoadQuestionFromExcel();
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
        /// Gets the current question information in the format: "Question {questionID} / {totalQuestions}"
        /// </summary>
        /// <returns>The information string</returns>
        public string GetInfo()
        {
            int total = GetTotalQuestions();
            return $"Question {questionViewer.questionID} / {total}";
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

    }
}