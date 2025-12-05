using System;
using System.Collections.Generic;
using UnityEngine;

namespace HMStudio.EasyQuiz
{
    /// <summary>
    /// Mode nguồn dữ liệu Quiz
    /// </summary>
    public enum QuizDataMode
    {
        Excel,  // Đọc từ file Excel local
        API     // Đọc từ API server
    }

    // ==================== API Response Models ====================

    /// <summary>
    /// Response từ API /api/quizzes
    /// </summary>
    [Serializable]
    public class QuizAPIResponse
    {
        public string message;
        public int count;
        public List<QuizData> data;
    }

    /// <summary>
    /// Dữ liệu một Quiz từ API
    /// </summary>
    [Serializable]
    public class QuizData
    {
        public string _id;
        public string name;
        public string subject;
        public string grade;
        public int star;
        public List<QuizChapter> chapters;
        public string createdBy;
        public string createdAt;
        public string updatedAt;
    }

    /// <summary>
    /// Chapter trong Quiz
    /// </summary>
    [Serializable]
    public class QuizChapter
    {
        public string _id;
        public string name;
        public List<QuizQuestion> questions;
    }

    /// <summary>
    /// Câu hỏi trong Chapter
    /// </summary>
    [Serializable]
    public class QuizQuestion
    {
        public string _id;
        public string questionText;
        public QuizOptions options;
    }

    /// <summary>
    /// Các đáp án A, B, C, D
    /// </summary>
    [Serializable]
    public class QuizOptions
    {
        public string A;
        public string B;
        public string C;
        public string D;

        /// <summary>
        /// Chuyển options thành List
        /// </summary>
        public List<string> ToList()
        {
            var list = new List<string>();
            if (!string.IsNullOrEmpty(A)) list.Add(A);
            if (!string.IsNullOrEmpty(B)) list.Add(B);
            if (!string.IsNullOrEmpty(C)) list.Add(C);
            if (!string.IsNullOrEmpty(D)) list.Add(D);
            return list;
        }

        /// <summary>
        /// Lấy đáp án theo key (A, B, C, D)
        /// </summary>
        public string GetByKey(string key)
        {
            return key switch
            {
                "A" => A,
                "B" => B,
                "C" => C,
                "D" => D,
                _ => null
            };
        }
    }

    // ==================== Cached Quiz Data ====================

    /// <summary>
    /// Subject được cache từ API
    /// </summary>
    [Serializable]
    public class APISubject
    {
        public string Id;           // Quiz _id
        public string Name;         // Subject name
        public string Grade;        // Lớp
        public List<APIChapter> Chapters = new List<APIChapter>();
    }

    /// <summary>
    /// Chapter được cache từ API  
    /// </summary>
    [Serializable]
    public class APIChapter
    {
        public string Id;           // Chapter _id
        public string Name;         // Chapter name
        public List<APIQuestion> Questions = new List<APIQuestion>();
    }

    /// <summary>
    /// Question được cache từ API
    /// </summary>
    [Serializable]
    public class APIQuestion
    {
        public string Id;           // Question _id
        public string QuestionText;
        public List<string> Options = new List<string>();
        public string CorrectAnswer; // Đáp án đúng (A, B, C, D hoặc text)
        public int LocalId;         // ID local để tương thích với Excel mode
    }

    // ==================== Compare Result ====================

    /// <summary>
    /// Kết quả so sánh giữa dữ liệu cũ và mới
    /// </summary>
    [Serializable]
    public class QuizDataCompareResult
    {
        public bool HasChanges => NewSubjects.Count > 0 || 
                                  RemovedSubjects.Count > 0 || 
                                  UpdatedSubjects.Count > 0;
        
        // Subjects mới được thêm
        public List<string> NewSubjects = new List<string>();
        
        // Subjects bị xóa
        public List<string> RemovedSubjects = new List<string>();
        
        // Subjects có thay đổi (chapters hoặc questions thay đổi)
        public List<SubjectChanges> UpdatedSubjects = new List<SubjectChanges>();
        
        // Tổng số
        public int TotalNewChapters = 0;
        public int TotalRemovedChapters = 0;
        public int TotalNewQuestions = 0;
        public int TotalRemovedQuestions = 0;
        public int TotalModifiedQuestions = 0;

        public override string ToString()
        {
            if (!HasChanges) return "No changes detected";
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== Quiz Data Changes ===");
            
            if (NewSubjects.Count > 0)
                sb.AppendLine($"+ New Subjects: {string.Join(", ", NewSubjects)}");
            if (RemovedSubjects.Count > 0)
                sb.AppendLine($"- Removed Subjects: {string.Join(", ", RemovedSubjects)}");
            
            foreach (var update in UpdatedSubjects)
            {
                sb.AppendLine($"~ {update.SubjectName}:");
                if (update.NewChapters.Count > 0)
                    sb.AppendLine($"    + New Chapters: {string.Join(", ", update.NewChapters)}");
                if (update.RemovedChapters.Count > 0)
                    sb.AppendLine($"    - Removed Chapters: {string.Join(", ", update.RemovedChapters)}");
                if (update.NewQuestionsCount > 0)
                    sb.AppendLine($"    + New Questions: {update.NewQuestionsCount}");
                if (update.RemovedQuestionsCount > 0)
                    sb.AppendLine($"    - Removed Questions: {update.RemovedQuestionsCount}");
                if (update.ModifiedQuestionsCount > 0)
                    sb.AppendLine($"    ~ Modified Questions: {update.ModifiedQuestionsCount}");
            }
            
            sb.AppendLine($"Summary: +{TotalNewChapters} chapters, -{TotalRemovedChapters} chapters, " +
                         $"+{TotalNewQuestions} questions, -{TotalRemovedQuestions} questions, ~{TotalModifiedQuestions} modified");
            
            return sb.ToString();
        }
    }

    /// <summary>
    /// Chi tiết thay đổi của một Subject
    /// </summary>
    [Serializable]
    public class SubjectChanges
    {
        public string SubjectName;
        public string SubjectId;
        
        public List<string> NewChapters = new List<string>();
        public List<string> RemovedChapters = new List<string>();
        
        public int NewQuestionsCount = 0;
        public int RemovedQuestionsCount = 0;
        public int ModifiedQuestionsCount = 0;
    }

    // ==================== Quiz Submit Models ====================

    /// <summary>
    /// Request body để submit quiz
    /// </summary>
    [Serializable]
    public class QuizSubmitRequest
    {
        public List<QuizAnswerItem> answers;

        public QuizSubmitRequest()
        {
            answers = new List<QuizAnswerItem>();
        }
    }

    /// <summary>
    /// Một câu trả lời trong submit request
    /// </summary>
    [Serializable]
    public class QuizAnswerItem
    {
        public string questionId;      // MongoDB _id của question
        public string selectedOption;  // Đáp án user chọn (A, B, C, D)

        public QuizAnswerItem(string qId, string ans)
        {
            questionId = qId;
            selectedOption = ans;
        }
    }

    /// <summary>
    /// Response từ API /api/quizzes/:id/submit
    /// </summary>
    [Serializable]
    public class QuizSubmitResponse
    {
        public string message;
        public QuizSubmitResult data;
    }

    /// <summary>
    /// Kết quả submit quiz
    /// </summary>
    [Serializable]
    public class QuizSubmitResult
    {
        public float score;           // Điểm (0-10 hoặc %)
        public int correctCount;      // Số câu đúng
        public int totalQuestions;    // Tổng số câu
        public List<QuizAnswerDetail> details;  // Chi tiết từng câu

        /// <summary>
        /// Tính % đúng
        /// </summary>
        public float GetPercentage()
        {
            if (totalQuestions == 0) return 0f;
            return (float)correctCount / totalQuestions;
        }

        /// <summary>
        /// Tính điểm trên thang 10
        /// </summary>
        public float GetScoreOutOf10()
        {
            return GetPercentage() * 10f;
        }
    }

    /// <summary>
    /// Chi tiết một câu trả lời
    /// </summary>
    [Serializable]
    public class QuizAnswerDetail
    {
        public string questionId;     // MongoDB _id
        public bool isCorrect;        // Đúng/sai
        public string userAnswer;     // Đáp án user chọn
        public string correctAnswer;  // Đáp án đúng
    }
}
