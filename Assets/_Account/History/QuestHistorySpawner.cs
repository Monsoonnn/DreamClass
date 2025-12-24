using System.Collections.Generic;
using UnityEngine;
using DreamClass.Network;
using com.cyborgAssets.inspectorButtonPro;
using DreamClass.LoginManager;

namespace DreamClass.HistoryProfile
{
    public class QuestHistorySpawner : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private string endpoint = "/api/quests/history/my";
        [SerializeField] private int limit = 10;
        [SerializeField] private int page = 1;

        [Header("References")]
        [SerializeField] private ApiClient apiClient;
        [SerializeField] private QuestPrefabHolder prefab;
        [SerializeField] private Transform defaultSpawnParent;

        private void Start()
        {
            if (apiClient == null)
            {
                apiClient = FindObjectOfType<ApiClient>();
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
            Debug.Log($"[QuestHistorySpawner] Fetching: {url}");

            ApiRequest req = new ApiRequest(url, "GET");
            StartCoroutine(apiClient.SendRequest(req, OnHistoryResponse));
        }

        private void OnHistoryResponse(ApiResponse response)
        {
            if (response.IsSuccess)
            {
                try 
                {
                    QuestHistoryResponse data = JsonUtility.FromJson<QuestHistoryResponse>(response.Text);

                    if (data != null && data.success)
                    {
                        if (data.data != null)
                        {
                            SpawnItems(data.data);
                        }
                        else
                        {
                            Debug.LogWarning("[QuestHistorySpawner] Data list is null.");
                        }
                    }
                    else
                    {
                        Debug.LogError($"[QuestHistorySpawner] API returned success=false. Response: {response.Text}");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[QuestHistorySpawner] JSON Parse Error: {ex.Message}");
                }
            }
            else
            {
                Debug.LogError($"[QuestHistorySpawner] HTTP Request Failed: {response.Error}");
            }
        }

        private void SpawnItems(List<QuestHistoryData> items)
        {
            ClearSpawnedItems();

            if (defaultSpawnParent == null) return;

            foreach (var itemData in items)
            {
                QuestPrefabHolder item = Instantiate(prefab, defaultSpawnParent);
                item.SetData(itemData);
                item.gameObject.SetActive(true);
            }
        }

        [ProButton]
        public void ClearSpawnedItems()
        {
            if (defaultSpawnParent)
            {
                foreach (Transform child in defaultSpawnParent)
                {
                    Destroy(child.gameObject);
                }
            }
        }
    }
}
