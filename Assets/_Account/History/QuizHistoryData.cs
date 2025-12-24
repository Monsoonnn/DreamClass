using System;
using System.Collections.Generic;

namespace DreamClass.HistoryProfile
{
    [System.Serializable]
    public class QuizHistoryResponse
    {
        public bool success;
        public List<QuizAttemptData> data;
        public PaginationData pagination;
    }

    [System.Serializable]
    public class QuizAttemptData
    {
        public string attemptId;
        public QuizIdData quizId;
        public string quizName;
        public string subject;
        public bool isPassed;
        public int totalQuestions;
        public int correctAnswersCount;
        public string startedAt;
        public string completedAt;
    }

    [System.Serializable]
    public class QuizIdData
    {
        public string _id;
        public string name;
    }

    [System.Serializable]
    public class PaginationData
    {
        public int current;
        public int total;
        public int totalRecords;
    }
}
