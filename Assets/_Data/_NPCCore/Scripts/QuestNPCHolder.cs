using System.Collections.Generic;
using com.cyborgAssets.inspectorButtonPro;
using DreamClass.QuestSystem;
using UnityEngine;

namespace DreamClass.NPCCore
{
    public class QuestNPCHolder : NewMonobehavior
    {

        [Header("Quest Groups")]
        public List<QuestSpawnGroup> questGroups = new List<QuestSpawnGroup>();

        // HashSet để lưu questId đã spawn
        private HashSet<string> spawnedQuestIds = new HashSet<string>();

        protected override void Start()
        {
            base.Start();

            QuestManager.OnReady += OnQuestManagerReady;

            if (QuestManager.Instance != null && QuestManager.Instance.questStates.Count > 0)
            {
                OnQuestManagerReady();
            }
        }

        private void OnDestroy()
        {
            QuestManager.OnReady -= OnQuestManagerReady;
        }

        private void OnQuestManagerReady()
        {
            SpawnQuests();
        }

        [ProButton]
        public void SpawnQuests()
        {

            if (QuestManager.Instance == null)
            {
                Debug.LogWarning($"[{name}] QuestManager not ready yet.");
                return;
            }

            foreach (var group in questGroups)
            {
                if (group.spawnParent == null)
                {
                    Debug.LogWarning($"[{name}] Group {group.groupName} has no spawnParent.");
                    continue;
                }

                foreach (string questId in group.questIds)
                {
                    var state = QuestManager.Instance.GetQuestState(questId);

                    if (state == QuestState.NOT_START || state == QuestState.IN_PROGRESS)
                    {
                        SpawnQuestObject(group.spawnParent, questId);
                    }
                    else
                    {
                        Debug.Log($"<color=green>[QuestNPCHolder] Skip {questId}, state: {state}</color>");
                    }
                }
            }
        }

        private void SpawnQuestObject(Transform parent, string questId)
        {
            // Nếu đã spawn questId này rồi thì bỏ qua
            if (spawnedQuestIds.Contains(questId))
            {
                Debug.LogWarning($"[{name}] Quest '{questId}' already spawned.");
                return;
            }

            QuestCtrl prefab = QuestManager.Instance.QuestDatabase.GetQuestPrefabById(questId);
            if (prefab == null)
            {
                Debug.LogWarning($"[{name}] Prefab not found for quest {questId}");
                return;
            }

            QuestCtrl quest = Instantiate(prefab, parent);
            quest.name = $"[Quest] {prefab.QuestName}";
            quest.gameObject.SetActive(true);

            if (quest is QuestType1 s001)
            {
                s001.npcCtrl = GetComponent<NPCManager>();
            }

            // Đánh dấu đã spawn questId này
            spawnedQuestIds.Add(questId);

            Debug.Log($"[{name}] Spawned quest '{quest.QuestName}' ({questId})");
        }
    }
    [System.Serializable]
    public class QuestSpawnGroup
    {
        public string groupName;
        public Transform spawnParent;
        public List<string> questIds = new List<string>();
    }

}
