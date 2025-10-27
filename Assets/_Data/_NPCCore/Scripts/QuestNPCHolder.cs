using System.Collections.Generic;
using com.cyborgAssets.inspectorButtonPro;
using DreamClass.QuestSystem;
using UnityEngine;

namespace DreamClass.NPCCore {
    public class QuestNPCHolder : NewMonobehavior {
        [Header("Quest IDs Linked to This NPC")]
        public List<string> questIds = new List<string>();

        [Header("Spawn Parent")]
        public Transform spawnParent;

        protected override void Start() {
            base.Start();

            // Wait until QuestManager is ready (data loaded)
            QuestManager.OnReady += OnQuestManagerReady;
        }

        private void OnDestroy() {
            QuestManager.OnReady -= OnQuestManagerReady;
        }

        private void OnQuestManagerReady() {
            SpawnQuests();
        }

        [ProButton]
        public void SpawnQuests() {
            if (spawnParent == null) {
                Debug.LogWarning($"[{name}] Missing spawnParent.");
                return;
            }

            if (QuestManager.Instance == null) {
                Debug.LogWarning($"[{name}] QuestManager not ready yet.");
                return;
            }

            foreach (string questId in questIds) {
                var state = QuestManager.Instance.GetQuestState(questId);

                // Only spawn if quest is not started or currently in progress
                if (state == QuestState.NOT_START || state == QuestState.IN_PROGRESS) {
                    SpawnQuestObject(questId);
                }
            }
        }

        private void SpawnQuestObject( string questId ) {
            QuestCtrl prefab = QuestManager.Instance.QuestDatabase.GetQuestPrefabById(questId);
            if (prefab == null) {
                Debug.LogWarning($"[{name}] Quest prefab not found for {questId}.");
                return;
            }

            QuestCtrl quest = Instantiate(prefab, spawnParent);
            quest.name = $"[Quest] {prefab.QuestName}";
            quest.gameObject.SetActive(true);

            Debug.Log($"[{name}] Spawned quest '{quest.QuestName}' ({questId})");
        }
    }
}
