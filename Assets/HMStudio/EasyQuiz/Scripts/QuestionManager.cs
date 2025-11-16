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

        // Variable to count the number of correct answers
        private int correctAnswersCount = 0;
        private HashSet<int> answeredQuestions = new HashSet<int>();  // Track câu đã trả lời

        public event Action<float> OnQuizComplete;

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
            if (isCorrect)
            {
                correctAnswersCount++;
            }

            // Add ID hiện tại vào answered
            answeredQuestions.Add(questionViewer.questionID);

            // Check nếu hoàn thành (answered == total)
            int total = GetTotalQuestions();
            if (answeredQuestions.Count == total)
            {
                OnQuizComplete?.Invoke(GetPoint());  // Gửi score
            }

            return isCorrect;
        }

        [SerializeField] private QuizDatabase quizDatabase;  // Assign
        public int subjectID = 0;
        public int chapterID = 0;

        private List<int> shuffledQuestionIDs = new List<int>();
        private int currentQuestionIndex = 0;
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

            Debug.Log("Quiz started with shuffled questions!");
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
            if (currentQuestionIndex >= shuffledQuestionIDs.Count)
                currentQuestionIndex = 0;

            questionViewer.questionID = shuffledQuestionIDs[currentQuestionIndex];
            questionViewer.LoadQuestionFromExcel();

            Debug.Log($"Loaded next shuffled question ID: {questionViewer.questionID}");
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
                currentQuestionIndex = shuffledQuestionIDs.Count - 1;

            questionViewer.questionID = shuffledQuestionIDs[currentQuestionIndex];
            questionViewer.LoadQuestionFromExcel();

            Debug.Log($"Loaded previous shuffled question ID: {questionViewer.questionID}");
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