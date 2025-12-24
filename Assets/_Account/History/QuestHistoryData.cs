using System;
using System.Collections.Generic;

namespace DreamClass.HistoryProfile
{
    [System.Serializable]
    public class QuestHistoryResponse
    {
        public bool success;
        public List<QuestHistoryData> data;
        public PaginationData pagination;
    }

    [System.Serializable]
    public class QuestHistoryData
    {
        public string historyId;
        public string questId;
        public string questName;
        public string status;
        public string isDaily; // JSON shows string "true"
        public QuestRewards rewards;
        public string completedAt;
        public int completionCount;
    }

    [System.Serializable]
    public class QuestRewards
    {
        public int gold;
        public int points;
        // items ignored for now as it's empty in sample
    }
}
