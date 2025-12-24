using TMPro;
using UnityEngine;
using System;
using System.Globalization;

namespace DreamClass.HistoryProfile
{

    public class QuestPrefabHolder : MonoBehaviour
    {
        public TextMeshProUGUI QuestName; 
        public TextMeshProUGUI typeQuest;
        public TextMeshProUGUI RewardGold;
        public TextMeshProUGUI RewardPoints;
        public TextMeshProUGUI TimeStamp;

        public void SetData(QuestHistoryData data)
        {
            if (QuestName) QuestName.text = data.questName;
            
            if (typeQuest)
            {
                // Logic: isDaily string "true" -> "Hằng ngày", else "Thông thường"
                bool isDaily = data.isDaily == "true"; 
                typeQuest.text = isDaily ? "Hằng ngày" : "Thông thường";
                typeQuest.color = isDaily ? Color.cyan : Color.white; // Optional visual distinction
            }

            if (data.rewards != null)
            {
                if (RewardGold) RewardGold.text = $"+{data.rewards.gold}" + "golds";
                if (RewardPoints) RewardPoints.text = $"+{data.rewards.points}" + "points";
            }
            else
            {
                if (RewardGold) RewardGold.text = "0" + "golds";
                if (RewardPoints) RewardPoints.text = "0" + "points";
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