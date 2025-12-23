using System.Collections.Generic;
using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;
using DreamClass.NPCCore;
using TMPro;

namespace DreamClass.QuestSystem
{
    public class DailyQuestNPCHolder : NewMonobehavior
    {
        [Header("Settings")]
        public Transform spawnParent;
        public GameObject dailyQuestPrefab;

        [Header("UI Panels")]
        public GameObject notLoginPanel;
        public GameObject spawnedPanel;

        // HashSet để lưu questId đã spawn
        private HashSet<string> spawnedQuestIds = new HashSet<string>();

        protected override void Start()
        {
            base.Start();

            if (spawnParent == null) spawnParent = transform;

            // Trạng thái ban đầu: Hiện panel chưa login, ẩn panel quest
            if (notLoginPanel != null) notLoginPanel.SetActive(true);
            if (spawnedPanel != null) spawnedPanel.SetActive(false);

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

            // Chuyển đổi panel sau khi QuestManager sẵn sàng
            if (notLoginPanel != null) notLoginPanel.SetActive(false);
            if (spawnedPanel != null) spawnedPanel.SetActive(true);
        }

        [ProButton]
        public void SpawnQuests()
        {
            if (QuestManager.Instance == null)
            {
                Debug.LogWarning($"[{name}] QuestManager not ready yet.");
                return;
            }

            // Tự động spawn tất cả daily quests được QuestManager ghi nhận
            foreach (string questId in QuestManager.Instance.DailyQuestIds)
            {
                var state = QuestManager.Instance.GetQuestState(questId);

                // Daily quests typically reset, so we check if they are available to be started or in progress
                if (state == QuestState.NOT_START || state == QuestState.IN_PROGRESS)
                {
                    SpawnQuestObject(spawnParent, questId);
                }
                else
                {
                    Debug.Log($"<color=cyan>[DailyQuestNPCHolder] Skip {questId}, state: {state}</color>");
                }
            }
        }

        private void SpawnQuestObject(Transform parent, string questId)
        {
            if (spawnedQuestIds.Contains(questId))
            {
                Debug.LogWarning($"[{name}] Daily Quest '{questId}' already spawned.");
                return;
            }

            if (dailyQuestPrefab == null)
            {
                Debug.LogWarning($"[{name}] Daily Quest Prefab not assigned!");
                return;
            }

            GameObject questObj = Instantiate(dailyQuestPrefab, parent);

            // Get quest data from manager (API/Cache)
            var data = QuestManager.Instance.GetQuestData(questId);
            string questName = data != null ? data.name : questId;

            questObj.name = $"[DailyQuest] {questName}";

            // Update TMP Text
            var tmp = questObj.transform.Find("Text")?.GetComponent<TMP_Text>();
            if (tmp != null)
            {
                tmp.text = questName;
            }
            else
            {
                // Fallback: Try GetComponentInChildren if not found directly under "Text"
                tmp = questObj.GetComponentInChildren<TMP_Text>();
                if (tmp != null) tmp.text = questName;
            }

            questObj.gameObject.SetActive(true);


            spawnedQuestIds.Add(questId);
            Debug.Log($"[{name}] Spawned daily quest '{questName}' ({questId})");
        }

        [ProButton]
        private void ClearHasSpawnedQuests()
        {
            spawnedQuestIds.Clear();
        }
    }
}