using System;
using System.Collections.Generic;
using UnityEngine;

namespace DreamClass.Ranking
{
    [Serializable]
    public class RankingStudentData
    {
        public int rank;
        public string _id;
        public string name;
        public string role;
        public string avatar;
        public string playerId;
        public string className;
        public string grade;
        public int gold;
        public int points;
        public int totalExercisesCompleted;
        public string rating;
    }

    [Serializable]
    public class RankingResponse
    {
        public string message;
        public string grade;
        public string className;
        public List<RankingStudentData> data;
    }
}
