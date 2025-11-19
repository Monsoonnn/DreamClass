using System;
using System.Collections.Generic;
using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using LoginMgrNS = DreamClass.LoginManager;

namespace DreamClass.QuestSystem
{
    [System.Serializable]
    public class QuestStateInfo
    {
        public string questId;
        public QuestState state;
    }

    public class QuestManager : SingletonCtrl<QuestManager>
    {
        [Header("Quest Database")]
        [SerializeField] private QuestDatabase questDatabase;
        public QuestDatabase QuestDatabase => questDatabase;

        [Header("Quest Source")]
        [SerializeField] private bool useServerAPI = true;  // true = Server API, false = Local JSON
        [SerializeField] private string apiEndpoint = "/api/quests/my-quests";

        [Header("Daily Quests (Optional)")]
        [SerializeField] private bool useDailyQuests = false;
        [SerializeField] private string dailyQuestEndpoint = "/api/quests/daily";

        [Header("Runtime State (Debug Only)")]
        public Dictionary<string, QuestState> questStates = new();
        public Dictionary<string, QuestDataJson> questDataCache = new();

        public static event Action OnReady;

        protected override void Start()
        {
            base.Start();
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;

            // Don't auto-init - wait for LoginManager to call InitializeQuests after login
            Debug.Log("[QuestManager] Waiting for login before initializing quests...");
        }

        #region === INITIALIZATION ===

        /// <summary>
        /// Public method for LoginManager to call after successful login
        /// </summary>
        public void InitializeQuests()
        {
            if (useServerAPI)
            {
                InitializeFromServer();
            }
            else
            {
                InitializeFromLocalJSON();
            }
        }

        [ProButton]
        public void InitializeFromServer()
        {
            // Check if user is logged in
            if (LoginMgrNS.LoginManager.Instance == null || !LoginMgrNS.LoginManager.Instance.IsLoggedIn())
            {
                Debug.LogWarning("[QuestManager] Not logged in. Cannot fetch from server.");
                return;
            }

            Debug.Log("[QuestManager] Fetching quest states from Server API...");
            if (useDailyQuests)
            {
                StartCoroutine(FetchQuestsFromBothAPIs(OnServerDataReceived));
            }
            else
            {
                StartCoroutine(FetchQuestsFromAPI(apiEndpoint, OnServerDataReceived));
            }
        }

        [ProButton]
        public void InitializeFromLocalJSON()
        {
            Debug.Log("[QuestManager] Fetching quest states from Local JSON...");
            StartCoroutine(QuestServerMock.Instance.FetchQuestStates(OnServerDataReceived));
        }

        [ProButton]
        public void InitializeFromServerMock()
        {
            // Keep for backward compatibility
            InitializeFromLocalJSON();
        }

        private System.Collections.IEnumerator FetchQuestsFromAPI(string endpoint, System.Action<PlayerQuestJson> onComplete)
        {
            DreamClass.Network.ApiClient apiClient = FindFirstObjectByType<DreamClass.Network.ApiClient>();
            if (apiClient == null)
            {
                Debug.LogError("[QuestManager] ApiClient not found in scene. Falling back to Local JSON.");
                yield return QuestServerMock.Instance.FetchQuestStates(onComplete);
                yield break;
            }

            DreamClass.Network.ApiRequest request = new DreamClass.Network.ApiRequest(endpoint, "GET");

            DreamClass.Network.ApiResponse response = null;
            yield return apiClient.StartCoroutine(apiClient.SendRequest(request, r =>
            {
                response = r;
            }));

            // Handle response outside of try-catch to allow yield
            bool shouldFallback = false;
            if (response != null && response.IsSuccess && !string.IsNullOrEmpty(response.Text))
            {
                try
                {
                    // Parse array of quests from API
                    QuestDataJson[] questArray = JsonUtility.FromJson<ApiQuestArrayWrapper>(response.Text).data;

                    if (questArray != null && questArray.Length > 0)
                    {
                        // Wrap in PlayerQuestJson
                        PlayerQuestJson playerData = new PlayerQuestJson();
                        playerData.quests = new List<QuestDataJson>(questArray);

                        Debug.Log($"[QuestManager] Loaded {playerData.quests.Count} quests from Server API");
                        onComplete?.Invoke(playerData);
                        yield break;
                    }
                    else
                    {
                        Debug.LogWarning("[QuestManager] No quests returned from API. Falling back to Local JSON.");
                        shouldFallback = true;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[QuestManager] Failed to parse API response: {e.Message}. Falling back to Local JSON.");
                    shouldFallback = true;
                }
            }
            else
            {
                Debug.LogError("[QuestManager] API request failed. Falling back to Local JSON.");
                shouldFallback = true;
            }

            // Fallback to Local JSON if needed
            if (shouldFallback)
            {
                yield return QuestServerMock.Instance.FetchQuestStates(onComplete);
            }
        }

        private System.Collections.IEnumerator FetchQuestsFromBothAPIs(System.Action<PlayerQuestJson> onComplete)
        {
            DreamClass.Network.ApiClient apiClient = FindFirstObjectByType<DreamClass.Network.ApiClient>();
            if (apiClient == null)
            {
                Debug.LogError("[QuestManager] ApiClient not found in scene. Falling back to Local JSON.");
                yield return QuestServerMock.Instance.FetchQuestStates(onComplete);
                yield break;
            }

            // Fetch normal quests
            PlayerQuestJson normalQuests = null;
            yield return FetchQuestArrayFromAPI(apiClient, apiEndpoint, (data) => normalQuests = data);

            // Fetch daily quests
            PlayerQuestJson dailyQuests = null;
            yield return FetchQuestArrayFromAPI(apiClient, dailyQuestEndpoint, (data) => dailyQuests = data);

            // Merge quests
            if (normalQuests != null && dailyQuests != null)
            {
                normalQuests.quests.AddRange(dailyQuests.quests);
                Debug.Log($"[QuestManager] Merged {normalQuests.quests.Count} quests (normal + daily)");
                onComplete?.Invoke(normalQuests);
            }
            else if (normalQuests != null)
            {
                Debug.LogWarning("[QuestManager] Daily quests failed, using normal quests only");
                onComplete?.Invoke(normalQuests);
            }
            else
            {
                Debug.LogError("[QuestManager] Failed to fetch quests from API. Falling back to Local JSON.");
                yield return QuestServerMock.Instance.FetchQuestStates(onComplete);
            }
        }

        private System.Collections.IEnumerator FetchQuestArrayFromAPI(DreamClass.Network.ApiClient apiClient, string endpoint, System.Action<PlayerQuestJson> onComplete)
        {
            DreamClass.Network.ApiRequest request = new DreamClass.Network.ApiRequest(endpoint, "GET");

            DreamClass.Network.ApiResponse response = null;
            yield return apiClient.StartCoroutine(apiClient.SendRequest(request, r =>
            {
                response = r;
            }));

            if (response != null && response.IsSuccess && !string.IsNullOrEmpty(response.Text))
            {
                try
                {
                    QuestDataJson[] questArray = JsonUtility.FromJson<ApiQuestArrayWrapper>(response.Text).data;

                    if (questArray != null && questArray.Length > 0)
                    {
                        PlayerQuestJson playerData = new PlayerQuestJson();
                        playerData.quests = new List<QuestDataJson>(questArray);
                        onComplete?.Invoke(playerData);
                    }
                    else
                    {
                        onComplete?.Invoke(null);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[QuestManager] Failed to parse response from {endpoint}: {e.Message}");
                    onComplete?.Invoke(null);
                }
            }
            else
            {
                onComplete?.Invoke(null);
            }
        }

        [System.Serializable]
        private class ApiQuestArrayWrapper
        {
            public string message;
            public int count;
            public QuestDataJson[] data;
        }

        private void OnServerDataReceived(PlayerQuestJson playerData)
        {
            questStates.Clear();
            questDataCache.Clear();

            foreach (var quest in playerData.quests)
            {
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
        public void StartQuest(string questId)
        {
            QuestCtrl prefab = questDatabase.GetQuestPrefabById(questId);
            if (prefab == null)
            {
                Debug.LogWarning($"[QuestManager] Quest {questId} not found in database.");
                return;
            }

            Transform parent = FindQuestHolder();
            if (parent == null)
            {
                Debug.LogWarning("[QuestManager] No QuestHolder found in scene.");
                return;
            }

            QuestCtrl quest = Instantiate(prefab, parent);
            quest.name = $"[Quest] {prefab.QuestName}";
            quest.StartQuest();

            SetQuestState(questId, QuestState.IN_PROGRESS);

            // Start quest on server if using Server API
            if (useServerAPI && LoginMgrNS.LoginManager.Instance != null && LoginMgrNS.LoginManager.Instance.IsLoggedIn())
            {
                StartCoroutine(StartQuestOnServer(questId));
            }

            Debug.Log($"[QuestManager] Quest started: {quest.QuestName}");
        }

        private System.Collections.IEnumerator StartQuestOnServer(string questId)
        {
            DreamClass.Network.ApiClient apiClient = FindFirstObjectByType<DreamClass.Network.ApiClient>();
            if (apiClient == null)
            {
                Debug.LogError("[QuestManager] ApiClient not found.");
                yield break;
            }

            string endpoint = $"/api/quests/my-quests/{questId}/start";
            DreamClass.Network.ApiRequest request = new DreamClass.Network.ApiRequest(endpoint, "POST");

            DreamClass.Network.ApiResponse response = null;
            yield return apiClient.StartCoroutine(apiClient.SendRequest(request, r =>
            {
                response = r;
            }));

            if (response != null && response.IsSuccess && !string.IsNullOrEmpty(response.Text))
            {
                try
                {
                    StartQuestResponse startResponse = JsonUtility.FromJson<StartQuestResponse>(response.Text);
                    if (startResponse.data != null)
                    {
                        questDataCache[questId] = startResponse.data;
                        Debug.Log($"[QuestManager] Quest {questId} started on server");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[QuestManager] Failed to parse start quest response: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"[QuestManager] Failed to start quest on server: {response?.Error}");
            }
        }

        public void CompleteQuest(string questId, System.Action<bool, QuestDataJson> onComplete = null)
        {
            SetQuestState(questId, QuestState.FINISHED);

            if (useServerAPI && LoginMgrNS.LoginManager.Instance != null && LoginMgrNS.LoginManager.Instance.IsLoggedIn())
            {
                StartCoroutine(CompleteQuestOnServer(questId, onComplete));
            }
            else
            {
                // Local mock
                StartCoroutine(QuestServerMock.Instance.SendComplete(questId));
                onComplete?.Invoke(true, GetQuestData(questId));
            }
            Debug.Log($"[QuestManager] Quest '{questId}' marked as complete.");
        }

        private System.Collections.IEnumerator CompleteQuestOnServer(string questId, System.Action<bool, QuestDataJson> onComplete)
        {
            DreamClass.Network.ApiClient apiClient = FindFirstObjectByType<DreamClass.Network.ApiClient>();
            if (apiClient == null)
            {
                Debug.LogError("[QuestManager] ApiClient not found.");
                onComplete?.Invoke(false, null);
                yield break;
            }

            string endpoint = $"/api/quests/{questId}/complete";
            Debug.Log($"[QuestManager] CompleteQuestOnServer Request:{endpoint}\n");

            DreamClass.Network.ApiRequest request = new DreamClass.Network.ApiRequest(endpoint, "POST");

            DreamClass.Network.ApiResponse response = null;
            yield return apiClient.StartCoroutine(apiClient.SendRequest(request, r =>
            {
                response = r;
            }));

            if (response != null && response.IsSuccess && !string.IsNullOrEmpty(response.Text))
            {
                try
                {
                    CompleteQuestResponse completeResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<CompleteQuestResponse>(response.Text);
                    if (completeResponse.data != null && completeResponse.data.quest != null)
                    {
                        //  Update cache with server data
                        questDataCache[questId] = completeResponse.data.quest;

                        //  Return updated quest data
                        onComplete?.Invoke(true, new QuestDataJson
                        {
                            rewardGold = completeResponse.data.reward.gold,
                            completedAt = completeResponse.data.quest.completedAt
                        });
                    }
                    else
                    {
                        Debug.LogError("[QuestManager] Invalid response data for complete quest.");
                        onComplete?.Invoke(false, null);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[QuestManager] Failed to parse complete quest response: {e.Message}");
                    onComplete?.Invoke(false, null);
                }
            }
            else
            {
                Debug.LogError($"[QuestManager] Failed to complete quest: {response?.Error}");
                onComplete?.Invoke(false, null);
            }
        }


        public void UpdateStepOnServer(string questId, string stepId, System.Action<QuestDataJson> onComplete = null)
        {
            if (!useServerAPI || LoginMgrNS.LoginManager.Instance == null || !LoginMgrNS.LoginManager.Instance.IsLoggedIn())
            {
                Debug.LogWarning("[QuestManager] Not using Server API or not logged in. Cannot update step.");
                return;
            }

            StartCoroutine(UpdateStepCoroutine(questId, stepId, onComplete));
        }

        private System.Collections.IEnumerator UpdateStepCoroutine(string questId, string stepId, System.Action<QuestDataJson> onComplete)
        {
            DreamClass.Network.ApiClient apiClient = FindFirstObjectByType<DreamClass.Network.ApiClient>();
            if (apiClient == null)
            {
                Debug.LogError("[QuestManager] ApiClient not found.");
                yield break;
            }

            string endpoint = $"/api/quests/{questId}/steps/{stepId}";
            string body = JsonUtility.ToJson(new StepUpdateRequest { isComplete = true });

            DreamClass.Network.ApiRequest request = new DreamClass.Network.ApiRequest(endpoint, "PUT", body);

            DreamClass.Network.ApiResponse response = null;
            yield return apiClient.StartCoroutine(apiClient.SendRequest(request, r =>
            {
                response = r;
            }));

            if (response != null && response.IsSuccess && !string.IsNullOrEmpty(response.Text))
            {
                try
                {
                    StepUpdateResponse stepResponse = JsonUtility.FromJson<StepUpdateResponse>(response.Text);
                    if (stepResponse.data != null)
                    {
                        questDataCache[questId] = stepResponse.data;
                        Debug.Log($"[QuestManager] Step {stepId} updated successfully");
                        onComplete?.Invoke(stepResponse.data);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[QuestManager] Failed to parse step update response: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"[QuestManager] Failed to update step: {response?.Error}");
            }
        }

        private System.Collections.IEnumerator CompleteQuestOnServer(string questId, System.Action<bool> onComplete)
        {
            DreamClass.Network.ApiClient apiClient = FindFirstObjectByType<DreamClass.Network.ApiClient>();
            if (apiClient == null)
            {
                Debug.LogError("[QuestManager] ApiClient not found.");
                onComplete?.Invoke(false);
                yield break;
            }

            string endpoint = $"/api/quests/{questId}/complete";

            Debug.Log($"[QuestManager] CompleteQuestOnServer Request:{endpoint}\n");

            DreamClass.Network.ApiRequest request = new DreamClass.Network.ApiRequest(endpoint, "POST");

            DreamClass.Network.ApiResponse response = null;
            yield return apiClient.StartCoroutine(apiClient.SendRequest(request, r =>
            {
                response = r;
            }));

            if (response != null && response.IsSuccess && !string.IsNullOrEmpty(response.Text))
            {
                try
                {
                    CompleteQuestResponse completeResponse = JsonUtility.FromJson<CompleteQuestResponse>(response.Text);
                    if (completeResponse.data != null && completeResponse.data.quest != null)
                    {
                        questDataCache[questId] = completeResponse.data.quest;
                        Debug.Log($"[QuestManager] Quest {questId} completed! Reward: {completeResponse.data.reward.gold} gold");
                        onComplete?.Invoke(true);
                    }
                    else
                    {
                        Debug.LogError("[QuestManager] Invalid response data for complete quest.");
                        onComplete?.Invoke(false);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[QuestManager] Failed to parse complete quest response: {e.Message}");
                    onComplete?.Invoke(false);
                }
            }
            else
            {
                Debug.LogError($"[QuestManager] Failed to complete quest: {response?.Error}");
                onComplete?.Invoke(false);
            }
        }

        #region === ABANDON & RESTART ===

        public void AbandonQuest(string questId, System.Action<bool> onComplete = null)
        {
            if (!useServerAPI || LoginMgrNS.LoginManager.Instance == null || !LoginMgrNS.LoginManager.Instance.IsLoggedIn())
            {
                Debug.LogWarning("[QuestManager] Not using Server API or not logged in. Cannot abandon quest.");
                onComplete?.Invoke(false);
                return;
            }

            SetQuestState(questId, QuestState.NOT_START);
            StartCoroutine(AbandonQuestOnServer(questId, onComplete));
        }

        private System.Collections.IEnumerator AbandonQuestOnServer(string questId, System.Action<bool> onComplete)
        {
            DreamClass.Network.ApiClient apiClient = FindFirstObjectByType<DreamClass.Network.ApiClient>();
            if (apiClient == null)
            {
                Debug.LogError("[QuestManager] ApiClient not found.");
                onComplete?.Invoke(false);
                yield break;
            }

            string playerId = LoginMgrNS.LoginManager.Instance.GetPlayerId();
            string endpoint = "/api/quests/admin/reset";
            string body = JsonUtility.ToJson(new AbandonQuestRequest { playerId = playerId, questId = questId });

            DreamClass.Network.ApiRequest request = new DreamClass.Network.ApiRequest(endpoint, "POST", body);

            DreamClass.Network.ApiResponse response = null;
            yield return apiClient.StartCoroutine(apiClient.SendRequest(request, r =>
            {
                response = r;
            }));

            if (response != null && response.IsSuccess)
            {
                Debug.Log($"[QuestManager] Quest {questId} abandoned successfully");
                onComplete?.Invoke(true);
            }
            else
            {
                Debug.LogError($"[QuestManager] Failed to abandon quest: {response?.Error}");
                onComplete?.Invoke(false);
            }
        }

        public void RestartQuestStep(string questId, string stepId, System.Action<bool> onComplete = null)
        {
            if (!useServerAPI || LoginMgrNS.LoginManager.Instance == null || !LoginMgrNS.LoginManager.Instance.IsLoggedIn())
            {
                Debug.LogWarning("[QuestManager] Not using Server API or not logged in. Cannot restart step.");
                onComplete?.Invoke(false);
                return;
            }

            StartCoroutine(RestartQuestStepOnServer(questId, stepId, onComplete));
        }

        private System.Collections.IEnumerator RestartQuestStepOnServer(string questId, string stepId, System.Action<bool> onComplete)
        {
            DreamClass.Network.ApiClient apiClient = FindFirstObjectByType<DreamClass.Network.ApiClient>();
            if (apiClient == null)
            {
                Debug.LogError("[QuestManager] ApiClient not found.");
                onComplete?.Invoke(false);
                yield break;
            }

            string endpoint = $"/api/quests/{questId}/steps/{stepId}";
            string body = JsonUtility.ToJson(new StepUpdateRequest { isComplete = false });

            //Debug.Log($"[QuestManager] RestartQuestStepOnServer Request:{endpoint}\nBody: {body}");

            DreamClass.Network.ApiRequest request = new DreamClass.Network.ApiRequest(endpoint, "PUT", body);

            //Debug.Log($"[QuestManager] RestartQuestStepOnServer Request:{request.endpoint}\nBody: {request.body}");

            DreamClass.Network.ApiResponse response = null;
            yield return apiClient.StartCoroutine(apiClient.SendRequest(request, r =>
            {
                response = r;
                // Debug.Log($"[QuestManager] RestartQuestStepOnServer Response: Success={response.IsSuccess}, Body={response.Text}, Error={response.Error}");
            }));

            if (response != null && response.IsSuccess && !string.IsNullOrEmpty(response.Text))
            {
                try
                {
                    StepUpdateResponse stepResponse = JsonUtility.FromJson<StepUpdateResponse>(response.Text);
                    if (stepResponse.data != null)
                    {
                        questDataCache[questId] = stepResponse.data;
                        Debug.Log($"[QuestManager] Step {stepId} restarted successfully");
                        onComplete?.Invoke(true);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[QuestManager] Failed to parse restart step response: {e.Message}");
                    onComplete?.Invoke(false);
                }
            }
            else
            {
                Debug.LogError($"[QuestManager] Failed to restart step: {response?.Error}");
                onComplete?.Invoke(false);
            }
        }

        #endregion

        [System.Serializable]
        private class StepUpdateRequest
        {
            public bool isComplete;
        }

        [System.Serializable]
        private class AbandonQuestRequest
        {
            public string playerId;
            public string questId;
        }

        [System.Serializable]
        private class StepUpdateResponse
        {
            public string message;
            public QuestDataJson data;
        }

        [System.Serializable]
        private class StartQuestResponse
        {
            public string message;
            public QuestDataJson data;
        }

        [System.Serializable]
        private class CompleteQuestResponse
        {
            public string message;
            public CompleteQuestData data;

            [System.Serializable]
            public class CompleteQuestData
            {
                public QuestDataJson quest;
                public RewardData reward;

                [System.Serializable]
                public class RewardData
                {
                    public int gold;
                    public int totalGold;
                }
            }
        }

        #endregion

        #region === QUEST STATE & INFO ===

        public bool IsUsingServerAPI => useServerAPI;

        public QuestState GetQuestState(string questId)
        {
            return questStates.TryGetValue(questId, out var state)
                ? state
                : QuestState.NOT_PREMISE;
        }

        public QuestDataJson GetQuestData(string questId)
        {
            return questDataCache.TryGetValue(questId, out var data) ? data : null;
        }

        public List<string> GetQuestNames(List<string> questIds)
        {
            List<string> questNames = new List<string>();
            if (questIds == null || questIds.Count == 0) return questNames;

            foreach (var id in questIds)
            {
                // Lấy quest prefab hoặc data từ database
                var questPrefab = questDatabase.GetQuestPrefabById(id);
                if (questPrefab != null && !string.IsNullOrEmpty(questPrefab.QuestName))
                {
                    questNames.Add(questPrefab.QuestName);
                }
                else
                {
                    questNames.Add($"Unknown Quest ({id})");
                }
            }

            return questNames;
        }

        public void SetQuestState(string questId, QuestState state)
        {
            questStates[questId] = state;
        }

        #endregion

        #region === SCENE LOAD HANDLING ===

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log($"[QuestManager] Scene loaded: {scene.name} -> clearing scene-specific quest objects");

            // Không giữ reference quest nào => chỉ cần dọn object tồn tại trong scene
            var existingQuests = GameObject.FindObjectsByType<QuestCtrl>(FindObjectsSortMode.None);
            foreach (var quest in existingQuests)
                Destroy(quest.gameObject);

            // OnReady?.Invoke();
        }

        private Transform FindQuestHolder()
        {
            GameObject holder = GameObject.Find("QuestHolder");
            return holder ? holder.transform : null;
        }

        #endregion

        #region === DEBUG ===

        [ProButton]
        public void LogQuestStatus()
        {
            Debug.Log("=== QUEST STATUS ===");
            foreach (var kvp in questStates)
                Debug.Log($"- {kvp.Key}: {kvp.Value}");
        }

        #endregion
    }
}
