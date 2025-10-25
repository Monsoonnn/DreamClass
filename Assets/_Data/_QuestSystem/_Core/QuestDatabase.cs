using System.Collections.Generic;
using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;

namespace DreamClass.QuestSystem
{
    [CreateAssetMenu(fileName = "QuestDatabase", menuName = "DreamClass/QuestSystem/Database")]
    public class QuestDatabase : ScriptableObject
    {
        [Header("All Quest Prefabs")]
        public List<QuestCtrl> questPrefabs = new();

        #region === BASIC GET ===

        public QuestCtrl GetQuestPrefabById(string questId)
        {
            foreach (var quest in questPrefabs)
            {
                if (quest != null && quest.QuestId == questId)
                    return quest;
            }

            Debug.LogWarning($"[QuestDatabase] Quest with ID '{questId}' not found.");
            return null;
        }

        #endregion

        #region === CRUD ===

        [ProButton]
        public void AddQuest(QuestCtrl quest)
        {
            if (quest == null)
            {
                Debug.LogWarning("[QuestDatabase] Cannot add null QuestCtrl.");
                return;
            }

            if (questPrefabs.Exists(q => q != null && q.QuestId == quest.QuestId))
            {
                Debug.LogWarning($"[QuestDatabase] Quest with ID '{quest.QuestId}' already exists.");
                return;
            }

            questPrefabs.Add(quest);
            Debug.Log($"[QuestDatabase] Added quest '{quest.QuestName}' ({quest.QuestId}).");
        }

        [ProButton]
        public void RemoveQuest(string questId)
        {
            int removed = questPrefabs.RemoveAll(q => q != null && q.QuestId == questId);
            Debug.Log(removed > 0
                ? $"[QuestDatabase] Removed quest {questId}."
                : $"[QuestDatabase] No quest found with ID {questId}.");
        }

        [ProButton]
        public void UpdateQuest(QuestCtrl updatedQuest)
        {
            if (updatedQuest == null)
            {
                Debug.LogWarning("[QuestDatabase] Cannot update null quest.");
                return;
            }

            int index = questPrefabs.FindIndex(q => q != null && q.QuestId == updatedQuest.QuestId);
            if (index >= 0)
            {
                questPrefabs[index] = updatedQuest;
                Debug.Log($"[QuestDatabase] Updated quest '{updatedQuest.QuestName}'.");
            }
            else
            {
                Debug.LogWarning($"[QuestDatabase] Quest '{updatedQuest.QuestId}' not found to update.");
            }
        }

        public void ClearAll()
        {
            questPrefabs.Clear();
            Debug.Log("[QuestDatabase] Cleared all quests.");
        }

        #endregion

        #region === SYNC WITH SERVER ===

        public void SyncWithServer(PlayerQuestJson playerData)
        {
            bool hasChanges = false;

            // --- Kiểm tra các quest từ server ---
            foreach (var q in playerData.quests)
            {
                QuestCtrl existing = questPrefabs.Find(prefab => prefab != null && prefab.QuestId == q.questId);

                if (existing == null)
                {
                    // Quest ID không có trong database
                    Debug.LogError($"<color=red>[QuestDatabase] Quest ID '{q.questId}' (Name: '{q.name}') from server NOT FOUND in database!</color>");
                    continue;
                }

                // Kiểm tra thay đổi metadata
                bool metadataChanged = existing.QuestName != q.name || existing.Description != q.description;

                // Kiểm tra thay đổi state
                bool stateChanged = existing.State.ToString() != q.state;

                // Kiểm tra thay đổi steps
                bool stepsChanged = false;
                if (q.steps != null && existing.steps != null && q.steps.Count == existing.steps.Count)
                {
                    for (int i = 0; i < q.steps.Count; i++)
                    {
                        if (existing.steps[i] != null && existing.steps[i].StepId == q.steps[i].stepId)
                        {
                            if (existing.steps[i].IsComplete != q.steps[i].isComplete)
                            {
                                stepsChanged = true;
                                break;
                            }
                        }
                    }
                }

                // Nếu có thay đổi thì update
                if (metadataChanged || stateChanged || stepsChanged)
                {
                    if (metadataChanged)
                    {
                        existing.QuestName = q.name;
                        existing.Description = q.description;
                        Debug.Log($"[QuestDatabase] Updated metadata for quest: {q.questId}");
                    }

                    if (stateChanged)
                    {
                        // Parse state từ string sang enum
                        if (System.Enum.TryParse(q.state, out QuestState newState))
                        {
                            existing.State = newState;
                            Debug.Log($"[QuestDatabase] Updated state for quest {q.questId}: {q.state}");
                        }
                    }

                    if (stepsChanged && q.steps != null && existing.steps != null)
                    {
                        for (int i = 0; i < q.steps.Count; i++)
                        {
                            if (i < existing.steps.Count && existing.steps[i] != null)
                            {
                                if (existing.steps[i].StepId == q.steps[i].stepId)
                                {
                                    // Cập nhật IsComplete cho step
                                    var stepField = existing.steps[i].GetType().GetProperty("IsComplete");
                                    if (stepField != null)
                                    {
                                        stepField.SetValue(existing.steps[i], q.steps[i].isComplete);
                                        Debug.Log($"[QuestDatabase] Updated step {q.steps[i].stepId} completion: {q.steps[i].isComplete}");
                                    }
                                }
                            }
                        }
                    }

                    hasChanges = true;
                }
            }

            // --- Kết quả sync ---
            if (hasChanges)
            {
                Debug.Log("[QuestDatabase] Sync completed and Database updated.");
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
            else
            {
                Debug.Log("[QuestDatabase] Sync check complete – no changes detected.");
                return;
            }
        }
        #endregion
    }
}
