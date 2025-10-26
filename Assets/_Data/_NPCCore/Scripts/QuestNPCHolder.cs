using System;
using System.Collections.Generic;
using com.cyborgAssets.inspectorButtonPro;
using DreamClass.QuestSystem;
using UnityEngine;

namespace DreamClass.NPCCore
{
    public class QuestNPCHolder : NewMonobehavior
    {
        public List<string> questIds = new List<string>();
        public Transform spawnParent;

        protected override void Start()
        {
            base.Start();
            SpawnQuests();
        }

        [ProButton]
        public void SpawnQuests()
        {
            if (spawnParent == null ) return;

            foreach (string questId in questIds)
            {
                QuestManager.Instance.SpawnQuestById(questId, spawnParent);
            }
        }
    }
}
