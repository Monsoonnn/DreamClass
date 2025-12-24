using System.Collections.Generic;
using UnityEngine;
using DreamClass.Network;
using com.cyborgAssets.inspectorButtonPro;
using DreamClass.LoginManager;

namespace DreamClass.HistoryProfile
{
    public class QuizHistorySpawner : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private string endpoint = "/api/quizzes/history/my";
        [SerializeField] private int limit = 10;
        [SerializeField] private int page = 1;

        [Header("References")]
        [SerializeField] private ApiClient apiClient;
        [SerializeField] private QuizPrefabHolder prefab;
        [SerializeField] private Transform defaultSpawnParent;

        private void Start()
        {
            if (apiClient == null)
            {
                apiClient = FindAnyObjectByType<ApiClient>();
            }

            // Listen to login/logout events
            LoginManager.LoginManager.OnLoginSuccess += FetchHistory;
            LoginManager.LoginManager.OnLogoutSuccess += ClearSpawnedItems;

            // If already logged in, fetch immediately
            if (LoginManager.LoginManager.Instance != null && LoginManager.LoginManager.Instance.IsLoggedIn())
            {
                FetchHistory();
            }
        }

        private void OnDestroy()
        {
            LoginManager.LoginManager.OnLoginSuccess -= FetchHistory;
            LoginManager.LoginManager.OnLogoutSuccess -= ClearSpawnedItems;
        }

        [ProButton]
        public void FetchHistory()
        {
            if (apiClient == null)
            {
                Debug.LogError("ApiClient is missing!");
                return;
            }

            string url = $"{endpoint}?page={page}&limit={limit}";
            Debug.Log($"[QuizHistorySpawner] Fetching: {url}");

            ApiRequest req = new ApiRequest(url, "GET");
            StartCoroutine(apiClient.SendRequest(req, OnHistoryResponse));
        }

        private void OnHistoryResponse(ApiResponse response)
        {
            if (response.IsSuccess)
            {
                // Note: JsonUtility requires the root object to match structure. 
                // The provided API response has { "success": true, "data": [...], ... }
                // which matches QuizHistoryResponse structure.
                try 
                {
                    QuizHistoryResponse data = JsonUtility.FromJson<QuizHistoryResponse>(response.Text);

                    if (data != null && data.success)
                    {
                        if (data.data != null)
                        {
                            SpawnItems(data.data);
                        }
                        else
                        {
                            Debug.LogWarning("[QuizHistorySpawner] Data list is null.");
                        }
                    }
                    else
                    {
                        Debug.LogError($"[QuizHistorySpawner] API returned success=false. Response: {response.Text}");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[QuizHistorySpawner] JSON Parse Error: {ex.Message}");
                }
            }
            else
            {
                Debug.LogError($"[QuizHistorySpawner] HTTP Request Failed: {response.Error}");
            }
        }

        private void SpawnItems(List<QuizAttemptData> attempts)
        {
            ClearSpawnedItems();

            foreach (var attempt in attempts)
            {
                Transform parent = defaultSpawnParent;

                if (parent == null)
                {
                    Debug.LogWarning($"[QuizHistorySpawner] No parent found for {attempt.quizName} ({attempt.subject})");
                    continue;
                }

                QuizPrefabHolder item = Instantiate(prefab, parent);
                item.SetData(attempt);
                item.gameObject.SetActive(true);
            }
        }

        [ProButton]
        public void ClearSpawnedItems()
        {
            if (defaultSpawnParent)
            {
                // Destroy children of default parent
                // Iterate backwards or use a list to avoid issues when modifying collection
                foreach (Transform child in defaultSpawnParent)
                {
                    Destroy(child.gameObject);
                }
            }
        }
    }

    [System.Serializable]
    public class QuizSpawnGroup
    {
        public string groupName;
        public Transform spawnParent;
        public List<string> subjects = new List<string>();
    }
}
