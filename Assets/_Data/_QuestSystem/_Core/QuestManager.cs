using System;
using System.Collections.Generic;
using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DreamClass.QuestSystem {
    [System.Serializable]
    public class QuestStateInfo {
        public string questId;
        public QuestState state;
    }

    public class QuestManager : SingletonCtrl<QuestManager> {
        [Header("Quest Database")]
        [SerializeField] private QuestDatabase questDatabase;
        public QuestDatabase QuestDatabase => questDatabase;


        [Header("Runtime State (Debug Only)")]
        public Dictionary<string, QuestState> questStates = new();
        public Dictionary<string, QuestDataJson> questDataCache = new();

        public static event Action OnReady;

        protected override void Start() {
            base.Start();
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;

            InitializeFromServerMock();

        }

        #region === INITIALIZATION ===

        [ProButton]
        public void InitializeFromServerMock() {
            Debug.Log("[QuestManager] Fetching quest states from server...");
            StartCoroutine(QuestServerMock.Instance.FetchQuestStates(OnServerDataReceived));
        }

        private void OnServerDataReceived( PlayerQuestJson playerData ) {
            questStates.Clear();
            questDataCache.Clear();

            foreach (var quest in playerData.quests) {
                questDataCache[quest.questId] = quest;

                if (System.Enum.TryParse(quest.state, out QuestState parsedState))
                    questStates[quest.questId] = parsedState;
                else
                    questStates[quest.questId] = QuestState.NOT_PREMISE;
            }

            questDatabase.SyncWithServer(playerData);

            OnReady?.Invoke();

            Debug.Log($"[QuestManager] Loaded {playerData.quests.Count} quests from JSON.");
        }

        #endregion

        #region === QUEST CONTROL ===

        [ProButton]
        public void StartQuest( string questId ) {
            QuestCtrl prefab = questDatabase.GetQuestPrefabById(questId);
            if (prefab == null) {
                Debug.LogWarning($"[QuestManager] Quest {questId} not found in database.");
                return;
            }

            Transform parent = FindQuestHolder();
            if (parent == null) {
                Debug.LogWarning("[QuestManager] No QuestHolder found in scene.");
                return;
            }

            QuestCtrl quest = Instantiate(prefab, parent);
            quest.name = $"[Quest] {prefab.QuestName}";
            quest.StartQuest();

            SetQuestState(questId, QuestState.IN_PROGRESS);
            Debug.Log($"[QuestManager] Quest started: {quest.QuestName}");
        }

        public void CompleteQuest( string questId ) {
            SetQuestState(questId, QuestState.FINISHED);
            StartCoroutine(QuestServerMock.Instance.SendComplete(questId));
            Debug.Log($"[QuestManager] Quest '{questId}' marked as complete.");
        }

        #endregion

        #region === QUEST STATE & INFO ===

        public QuestState GetQuestState( string questId ) {
            return questStates.TryGetValue(questId, out var state)
                ? state
                : QuestState.NOT_PREMISE;
        }

        public void SetQuestState( string questId, QuestState state ) {
            questStates[questId] = state;
        }

        #endregion

        #region === SCENE LOAD HANDLING ===

        private void OnSceneLoaded( Scene scene, LoadSceneMode mode ) {
            Debug.Log($"[QuestManager] Scene loaded: {scene.name} -> clearing scene-specific quest objects");

            // Không giữ reference quest nào => chỉ cần dọn object tồn tại trong scene
            var existingQuests = GameObject.FindObjectsOfType<QuestCtrl>();
            foreach (var quest in existingQuests)
                Destroy(quest.gameObject);
        }

        private Transform FindQuestHolder() {
            GameObject holder = GameObject.Find("QuestHolder");
            return holder ? holder.transform : null;
        }

        #endregion

        #region === DEBUG ===

        [ProButton]
        public void LogQuestStatus() {
            Debug.Log("=== QUEST STATUS ===");
            foreach (var kvp in questStates)
                Debug.Log($"- {kvp.Key}: {kvp.Value}");
        }

        #endregion
    }
}
