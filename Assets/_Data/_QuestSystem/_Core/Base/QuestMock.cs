using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DreamClass.QuestSystem
{
    [DefaultExecutionOrder(-50)]
    public class QuestServerMock : SingletonCtrl<QuestServerMock>
    {
        [Header("Mock JSON File Path")]
        [SerializeField] private string jsonPath = "Assets/_Data/_QuestSystem/Mock/QuestMock.json";

        private PlayerQuestJson playerData;
        private FileSystemWatcher watcher;

        #region === FETCH & SAVE ===

        public IEnumerator FetchQuestStates(System.Action<PlayerQuestJson> onComplete)
        {
            if (!File.Exists(jsonPath))
            {
                Debug.LogError($"[QuestServerMock] JSON file not found: {jsonPath}");
                yield break;
            }

            string json = File.ReadAllText(jsonPath);
            playerData = JsonUtility.FromJson<PlayerQuestJson>(json);

            yield return new WaitForSeconds(0.3f);
            onComplete?.Invoke(playerData);
        }

        private void SaveToJson()
        {
            string json = JsonUtility.ToJson(playerData, true);
            File.WriteAllText(jsonPath, json);
            Debug.Log("[QuestServerMock] JSON updated.");
        }

        #endregion

        #region === CRUD OPERATIONS ===

        public void AddQuest(QuestDataJson quest)
        {
            if (playerData == null || playerData.quests == null)
                return;

            playerData.quests.Add(quest);
            SaveToJson();
        }

        public QuestDataJson GetQuest(string questId)
        {
            return playerData?.quests?.Find(q => q.questId == questId);
        }

        public void UpdateQuest(string questId, string newState = null, bool? isStepComplete = null)
        {
            var quest = GetQuest(questId);
            if (quest == null)
            {
                Debug.LogWarning($"[QuestServerMock] Quest {questId} not found.");
                return;
            }

            if (newState != null)
                quest.state = newState;

            if (isStepComplete.HasValue && quest.steps != null && quest.steps.Count > 0)
                quest.steps[0].isComplete = isStepComplete.Value;

            SaveToJson();
        }

        public void DeleteQuest(string questId)
        {
            playerData?.quests?.RemoveAll(q => q.questId == questId);
            SaveToJson();
        }

        #endregion

        #region === NETWORK SIMULATION ===

        public IEnumerator SendProgress(string questId, string stepId, int progress)
        {
            Debug.Log($"[QuestServerMock] Progress → Quest:{questId}, Step:{stepId}, Value:{progress}");
            yield return new WaitForSeconds(0.2f);

            // Simulate step completion
            UpdateQuest(questId, null, progress >= 1);
        }

        public IEnumerator SendComplete(string questId)
        {
            Debug.Log($"[QuestServerMock] Completing quest: {questId}");
            UpdateQuest(questId, "FINISHED");

            yield return new WaitForSeconds(0.2f);
            Debug.Log($"[QuestServerMock] Quest {questId} marked as FINISHED.");
        }

        #endregion

        #region === AUTO RELOAD JSON ===

        protected override void Start()
        {
            string fullPath = Path.GetFullPath(jsonPath);
            string dir = Path.GetDirectoryName(fullPath);
            string file = Path.GetFileName(fullPath);

            watcher = new FileSystemWatcher(dir, file)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            watcher.Changed += (s, e) =>
            {
                Debug.Log("[QuestServerMock] JSON changed — reloading from disk...");
                QuestManager.Instance.InitializeFromServerMock();
            };
        }

        private void OnDestroy()
        {
            watcher?.Dispose();
        }

        #endregion
    }

    [System.Serializable]
    public class QuestStepJson
    {
        public string stepId;
        public bool isComplete;
    }

    [System.Serializable]
    public class QuestDataJson
    {
        public string questId;
        public string name;
        public string description;
        public int rewardGold;
        public string state;
        public List<QuestStepJson> steps;
    }

    [System.Serializable]
    public class PlayerQuestJson
    {
        public string playerId;
        public string playerName;
        public int gold;
        public List<QuestDataJson> quests;
    }
}
