using UnityEngine;
using System.Collections.Generic;

namespace Gameplay.Exam
{
    /// <summary>
    /// Loại phần thi trong bài kiểm tra
    /// </summary>
    public enum ExamSectionType
    {
        Quiz,           // Trắc nghiệm từ EasyQuiz
        Experiment      // Thực hành gameplay
    }

    /// <summary>
    /// Cấu hình một phần thi (Quiz hoặc Experiment)
    /// </summary>
    [System.Serializable]
    public class ExamSection
    {
        public string sectionId;
        public string sectionName;
        public ExamSectionType sectionType;
        
        [Header("Điểm số")]
        [Tooltip("Điểm tối đa của phần này")]
        public float maxScore = 5f;
        [Tooltip("Trọng số (weight) khi tính tổng điểm")]
        [Range(0f, 1f)] public float weight = 0.5f;

        [Header("Quiz Settings (nếu type = Quiz)")]
        public int subjectIndex = 0;
        public int chapterIndex = 0;
        public int questionCount = 10;
        public bool shuffleQuestions = true;

        [Header("Experiment Settings (nếu type = Experiment)")]
        [Tooltip("Tên experiment (VD: NHIET_DUNG_NUOC, VAN_CHUYEN_NUOC...)")]
        public string experimentName;
        [Tooltip("Danh sách các step cần hoàn thành")]
        public List<string> requiredStepIds = new List<string>();
        [Tooltip("Điểm cho mỗi step hoàn thành")]
        public float pointPerStep = 1f;
    }

    /// <summary>
    /// Cấu hình cho một bài kiểm tra hoàn chỉnh (Quiz + Experiment)
    /// </summary>
    [CreateAssetMenu(fileName = "ExamData", menuName = "Gameplay/Exam/ExamData", order = 1)]
    public class ExamData : ScriptableObject
    {
        [Header("Thông tin bài kiểm tra")]
        public string examId;
        public string examName;
        [TextArea] public string description;

        [Header("Cấu hình thời gian")]
        [Tooltip("Thời gian làm bài (phút)")]
        public float examDurationMinutes = 30f;
        [Tooltip("Có cho phép quay lại phần trước không")]
        public bool allowGoBack = true;

        [Header("Cấu hình điểm")]
        [Tooltip("Điểm tối đa tổng")]
        public float maxScore = 10f;
        [Tooltip("Điểm tối thiểu để đạt")]
        public float passScore = 5f;
        [Tooltip("Có trừ điểm khi sai không (Quiz)")]
        public bool penaltyForWrong = false;
        [Tooltip("Phần trăm trừ khi sai (0-1)")]
        [Range(0f, 1f)] public float penaltyPercent = 0.25f;

        [Header("Các phần thi")]
        public List<ExamSection> sections = new List<ExamSection>();

        /// <summary>
        /// Lấy tổng điểm tối đa từ tất cả sections
        /// </summary>
        public float GetTotalMaxScore()
        {
            float total = 0f;
            foreach (var section in sections)
                total += section.maxScore;
            return total;
        }
    }

    // ==================== RESULT CLASSES ====================

    /// <summary>
    /// Kết quả trả lời một câu hỏi Quiz
    /// </summary>
    [System.Serializable]
    public class QuestionResult
    {
        public int questionId;
        public string questionText;
        public string selectedAnswer;
        public string correctAnswer;
        public bool isCorrect;
        public float timeSpent;

        public QuestionResult(int id, string question, string selected, string correct, float time)
        {
            questionId = id;
            questionText = question;
            selectedAnswer = selected;
            correctAnswer = correct;
            isCorrect = string.Equals(selected, correct, System.StringComparison.OrdinalIgnoreCase);
            timeSpent = time;
        }
    }

    /// <summary>
    /// Kết quả một step trong Experiment
    /// </summary>
    [System.Serializable]
    public class ExperimentStepResult
    {
        public string stepId;
        public string stepName;
        public bool isCompleted;
        public float timeToComplete;
        public int errorCount;

        public ExperimentStepResult(string id, string name)
        {
            stepId = id;
            stepName = name;
            isCompleted = false;
            timeToComplete = 0f;
            errorCount = 0;
        }
    }

    /// <summary>
    /// Kết quả một phần thi (Quiz hoặc Experiment)
    /// </summary>
    [System.Serializable]
    public class SectionResult
    {
        public string sectionId;
        public string sectionName;
        public ExamSectionType sectionType;
        
        public float score;
        public float maxScore;
        public float percentage;
        public float timeSpent;
        public bool isCompleted;

        // Quiz specific
        public int totalQuestions;
        public int correctCount;
        public int wrongCount;
        public int skippedCount;
        public List<QuestionResult> questionResults = new List<QuestionResult>();

        // Experiment specific
        public string experimentName;
        public int totalSteps;
        public int completedSteps;
        public int totalErrors;
        public List<ExperimentStepResult> stepResults = new List<ExperimentStepResult>();

        public void CalculateQuizScore(ExamSection sectionConfig, bool penaltyForWrong, float penaltyPercent)
        {
            totalQuestions = questionResults.Count;
            correctCount = 0;
            wrongCount = 0;
            skippedCount = 0;

            foreach (var result in questionResults)
            {
                if (string.IsNullOrEmpty(result.selectedAnswer))
                    skippedCount++;
                else if (result.isCorrect)
                    correctCount++;
                else
                    wrongCount++;

                timeSpent += result.timeSpent;
            }

            if (totalQuestions > 0)
            {
                float pointPerQuestion = sectionConfig.maxScore / totalQuestions;
                score = correctCount * pointPerQuestion;

                if (penaltyForWrong)
                {
                    score -= wrongCount * pointPerQuestion * penaltyPercent;
                    score = Mathf.Max(0, score);
                }
            }

            maxScore = sectionConfig.maxScore;
            percentage = maxScore > 0 ? (score / maxScore) * 100f : 0f;
            isCompleted = true;
        }

        public void CalculateExperimentScore(ExamSection sectionConfig)
        {
            totalSteps = stepResults.Count;
            completedSteps = 0;
            totalErrors = 0;
            timeSpent = 0f;

            foreach (var step in stepResults)
            {
                if (step.isCompleted)
                    completedSteps++;
                totalErrors += step.errorCount;
                timeSpent += step.timeToComplete;
            }

            score = completedSteps * sectionConfig.pointPerStep;
            score = Mathf.Max(0, Mathf.Min(score, sectionConfig.maxScore));

            maxScore = sectionConfig.maxScore;
            percentage = maxScore > 0 ? (score / maxScore) * 100f : 0f;
            isCompleted = completedSteps >= totalSteps;
        }

        public string GetSummary()
        {
            if (sectionType == ExamSectionType.Quiz)
            {
                return $"[Quiz] {sectionName}: {score:F1}/{maxScore} ({percentage:F1}%)\n" +
                       $"  Đúng: {correctCount} | Sai: {wrongCount} | Bỏ qua: {skippedCount}";
            }
            else
            {
                return $"[Thực hành] {sectionName}: {score:F1}/{maxScore} ({percentage:F1}%)\n" +
                       $"  Hoàn thành: {completedSteps}/{totalSteps} bước | Lỗi: {totalErrors}";
            }
        }
    }

    /// <summary>
    /// Kết quả toàn bộ bài kiểm tra
    /// </summary>
    [System.Serializable]
    public class ExamResult
    {
        public string examId;
        public string examName;
        public System.DateTime startTime;
        public System.DateTime endTime;

        public float totalScore;
        public float maxScore;
        public float percentage;
        public bool isPassed;

        public float totalTimeSeconds;
        public List<SectionResult> sectionResults = new List<SectionResult>();

        public void Calculate(ExamData examData)
        {
            totalScore = 0f;
            maxScore = 0f;
            totalTimeSeconds = 0f;

            foreach (var section in sectionResults)
            {
                totalScore += section.score * GetSectionWeight(examData, section.sectionId);
                maxScore += section.maxScore * GetSectionWeight(examData, section.sectionId);
                totalTimeSeconds += section.timeSpent;
            }

            if (maxScore > 0)
            {
                float ratio = examData.maxScore / maxScore;
                totalScore *= ratio;
                maxScore = examData.maxScore;
            }

            percentage = maxScore > 0 ? (totalScore / maxScore) * 100f : 0f;
            isPassed = totalScore >= examData.passScore;
            endTime = System.DateTime.Now;
        }

        private float GetSectionWeight(ExamData examData, string sectionId)
        {
            foreach (var section in examData.sections)
            {
                if (section.sectionId == sectionId)
                    return section.weight;
            }
            return 1f;
        }

        public string GetSummary()
        {
            string summary = $"=== KẾT QUẢ BÀI KIỂM TRA ===\n";
            summary += $"Tên: {examName}\n";
            summary += $"Điểm: {totalScore:F1}/{maxScore} ({percentage:F1}%)\n";
            summary += $"Kết quả: {(isPassed ? "ĐẠT" : "KHÔNG ĐẠT")}\n";
            summary += $"Thời gian: {FormatTime(totalTimeSeconds)}\n\n";

            summary += "--- Chi tiết từng phần ---\n";
            foreach (var section in sectionResults)
            {
                summary += section.GetSummary() + "\n";
            }

            return summary;
        }

        private string FormatTime(float seconds)
        {
            int mins = (int)(seconds / 60);
            int secs = (int)(seconds % 60);
            return $"{mins:D2}:{secs:D2}";
        }
    }
}
