using System.Collections.Generic;
using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;

namespace DreamClass.QuestSystem
{
    [System.Serializable]
    public class QuestStateInfo
    {
        public string questId;
        public QuestState state;
    }

    [System.Serializable]
    public class ActiveQuestInfo
    {
        public string questId;
        public QuestCtrl questInstance;
    }

    [System.Serializable]
    public class QuestDataInfo
    {
        public string questId;
        public string questName;
        public string state;
        public int stepCount;
    }

    public class QuestManager : SingletonCtrl<QuestManager>
    {
        [Header("Quest Database")]
        [SerializeField] private QuestDatabase questDatabase;

        [Header("Runtime Data (Debug Only)")]

        // Active quests currently running in scene
        public Dictionary<string, QuestCtrl> activeQuests = new();

        // Cached state of all quests (synced from server/mock)
        public Dictionary<string, QuestState> questStates = new();

        // Cached full quest data from server
        public Dictionary<string, QuestDataJson> questDataCache = new();

        // List of quest IDs that should be spawned (initialized but not yet instantiated)
        public List<string> questsToSpawn = new();

        #region === INITIALIZATION ===

        [ProButton]
        public void InitializeFromServerMock()
        {
            StartCoroutine(QuestServerMock.Instance.FetchQuestStates(OnServerDataReceived));
        }

        private void OnServerDataReceived(PlayerQuestJson playerData)
        {
            questStates.Clear();
            questDataCache.Clear();
            questsToSpawn.Clear();

            foreach (var quest in playerData.quests)
            {
                // Cache quest data
                questDataCache[quest.questId] = quest;

                // Parse and cache state
                if (System.Enum.TryParse(quest.state, out QuestState parsedState))
                    questStates[quest.questId] = parsedState;
                else
                    questStates[quest.questId] = QuestState.NOT_PREMISE;

                // Add to spawn list if quest is NOT_START or IN_PROGRESS
                if (parsedState == QuestState.NOT_START || parsedState == QuestState.IN_PROGRESS)
                {
                    questsToSpawn.Add(quest.questId);
                }
            }

            Debug.Log($"[QuestManager] Loaded {playerData.quests.Count} quests from JSON.");
            Debug.Log($"[QuestManager] {questsToSpawn.Count} quests ready to spawn.");
            
            // Sync database with server data
            questDatabase.SyncWithServer(playerData);
        }

        #endregion

        #region === SPAWN QUEST BY ID ===

        /// <summary>
        /// Spawn a quest by its ID. Quest must be in questsToSpawn list.
        /// </summary>
        [ProButton]
        public void SpawnQuestById(string questId, Transform spawnParent)
        {
            // Check if quest is already active
            if (activeQuests.ContainsKey(questId))
            {
                Debug.LogWarning($"[QuestManager] Quest {questId} already active.");
                return;
            }

            // Check if quest is in spawn list
            if (!questsToSpawn.Contains(questId))
            {
                Debug.LogWarning($"[QuestManager] Quest {questId} not in spawn list. State: {GetQuestState(questId)}");
                return;
            }

            // Get prefab from database
            QuestCtrl prefab = questDatabase.GetQuestPrefabById(questId);
            if (prefab == null)
            {
                Debug.LogError($"[QuestManager] Quest prefab not found for {questId}");
                return;
            }

            // Get quest data from cache
            if (!questDataCache.TryGetValue(questId, out QuestDataJson questData))
            {
                Debug.LogError($"[QuestManager] Quest data not found for {questId}");
                return;
            }

            // Instantiate quest
            QuestCtrl quest = Instantiate(prefab, spawnParent);
            quest.gameObject.SetActive(true);
            quest.name = $"[Quest] {prefab.QuestName}";

            // Parse and set state
            if (System.Enum.TryParse(questData.state, out QuestState state))
            {
                quest.SetState(state);
            }

            // Sync steps from server
            if (questData.steps != null && quest.steps != null)
            {
                foreach (var stepData in questData.steps)
                {
                    var step = quest.steps.Find(s => s.StepId == stepData.stepId);
                    if (step != null)
                    {
                        step.IsComplete = stepData.isComplete;
                        Debug.Log($"[QuestManager] Synced step {stepData.stepId}: IsComplete = {stepData.isComplete}");
                    }
                    else
                    {
                        Debug.LogWarning($"[QuestManager] Step {stepData.stepId} not found in quest {questId}");
                    }
                }
            }

            activeQuests[questId] = quest;

            // Start quest if IN_PROGRESS
            if (quest.State == QuestState.IN_PROGRESS)
            {
                quest.StartQuest();
            }

            Debug.Log($"[QuestManager] Quest spawned: {prefab.QuestName} ({quest.State})");
        }

        /// <summary>
        /// Spawn multiple quests by their IDs
        /// </summary>
        [ProButton]
        public void SpawnQuestsByIds(Transform spawnParent, params string[] questIds)
        {
            foreach (string questId in questIds)
            {
                SpawnQuestById(questId, spawnParent);
            }
        }

        /// <summary>
        /// Spawn all quests in the spawn list
        /// </summary>
        [ProButton]
        public void SpawnAllQuests()
        {
            Debug.Log($"[QuestManager] Spawning {questsToSpawn.Count} quests...");
            
            List<string> toSpawn = new List<string>(questsToSpawn);
            foreach (string questId in toSpawn)
            {
                SpawnQuestById(questId, transform);
            }
        }

        #endregion

        #region === QUEST CONTROL ===

        [ProButton]
        public void StartQuest(string questId)
        {
            // If quest is already active, just start it
            if (activeQuests.TryGetValue(questId, out QuestCtrl existingQuest))
            {
                if (existingQuest.State == QuestState.NOT_START)
                {
                    existingQuest.StartQuest();
                    SetQuestState(questId, QuestState.IN_PROGRESS);
                    Debug.Log($"[QuestManager] Quest started: {existingQuest.QuestName}");
                }
                else
                {
                    Debug.LogWarning($"[QuestManager] Quest {questId} already in state: {existingQuest.State}");
                }
                return;
            }

            // If not active, spawn and start it
            QuestCtrl prefab = questDatabase.GetQuestPrefabById(questId);
            if (prefab == null)
            {
                Debug.LogWarning($"[QuestManager] Quest {questId} not found in database.");
                return;
            }

            QuestCtrl quest = Instantiate(prefab, transform);
            quest.name = $"[Quest] {prefab.QuestName}";
            quest.StartQuest();

            activeQuests[questId] = quest;
            SetQuestState(questId, QuestState.IN_PROGRESS);

            Debug.Log($"[QuestManager] Quest started: {prefab.QuestName}");
        }

        public void UpdateQuestProgress(string questId, string stepId, int progress)
        {
            if (activeQuests.TryGetValue(questId, out QuestCtrl quest))
            {
                quest.UpdateProgress(progress);

                // TODO: Replace with real network sync later
                StartCoroutine(QuestServerMock.Instance.SendProgress(questId, stepId, progress));
            }
        }

        public void CompleteQuest(string questId)
        {
            if (!activeQuests.TryGetValue(questId, out QuestCtrl quest))
                return;

            quest.SetState(QuestState.FINISHED);
            SetQuestState(questId, QuestState.FINISHED);

            // Remove from spawn list if still there
            questsToSpawn.Remove(questId);

            // TODO: Replace with real network sync later
            StartCoroutine(QuestServerMock.Instance.SendComplete(questId));

            Debug.Log($"[QuestManager] Quest '{quest.QuestName}' completed!");
            Destroy(quest.gameObject);
            activeQuests.Remove(questId);
        }

        #endregion

        #region === QUEST STATE & INFO ===

        public QuestState GetQuestState(string questId)
        {
            return questStates.TryGetValue(questId, out var state)
                ? state
                : QuestState.NOT_PREMISE;
        }

        public void SetQuestState(string questId, QuestState state)
        {
            questStates[questId] = state;
        }

        public bool IsQuestActive(string questId)
        {
            return activeQuests.ContainsKey(questId);
        }

        public QuestCtrl GetActiveQuest(string questId)
        {
            return activeQuests.TryGetValue(questId, out var quest) ? quest : null;
        }

        public List<string> GetQuestsToSpawn()
        {
            return new List<string>(questsToSpawn);
        }

        public int GetActiveQuestCount()
        {
            return activeQuests.Count;
        }

        public int GetSpawnableQuestCount()
        {
            return questsToSpawn.Count;
        }

        #endregion

        #region === DEBUG ===

        [ProButton]
        public void LogQuestStatus()
        {
            Debug.Log($"=== QUEST STATUS ===");
            Debug.Log($"Active Quests: {activeQuests.Count}");
            Debug.Log($"Quests to Spawn: {questsToSpawn.Count}");
            Debug.Log($"Total Quest States: {questStates.Count}");
            
            Debug.Log("\n--- Quests to Spawn ---");
            foreach (string questId in questsToSpawn)
            {
                Debug.Log($"- {questId} ({GetQuestState(questId)})");
            }
            
            Debug.Log("\n--- Active Quests ---");
            foreach (var kvp in activeQuests)
            {
                Debug.Log($"- {kvp.Key}: {kvp.Value.QuestName} ({kvp.Value.State})");
            }
        }

        #endregion
    }
}