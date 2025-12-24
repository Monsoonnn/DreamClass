using TMPro;
using UnityEngine;
using System;
using System.Globalization;

namespace DreamClass.HistoryProfile
{

    public class QuizPrefabHolder : MonoBehaviour
    {
        public TextMeshProUGUI quizName; 
        public TextMeshProUGUI subjectName;
        public TextMeshProUGUI Result;
        public TextMeshProUGUI TimeStamp;

        public void SetData(QuizAttemptData data)
        {
            if (quizName) quizName.text = data.quizName;
            if (subjectName) subjectName.text = data.subject;

            if (Result)
            {
                Result.text = data.isPassed ? "Đạt" : "Không đạt";
                Result.color = data.isPassed ? Color.green : Color.red;
            }

            if (TimeStamp)
            {
                if (DateTime.TryParse(data.completedAt, null, DateTimeStyles.RoundtripKind, out DateTime date))
                {
                    TimeStamp.text = date.ToString("dd/MM/yyyy HH:mm");
                }
                else
                {
                    TimeStamp.text = data.completedAt;
                }
            }
        }
    }

}