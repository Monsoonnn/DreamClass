using System.Collections.Generic;
using UnityEngine;
using DreamClass.Network;
using com.cyborgAssets.inspectorButtonPro;
using DreamClass.LoginManager;
using System.Collections;

namespace DreamClass.ItemsAchivement
{
    public class InventorySpawner : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private string endpoint = "/api/items/inventory/my";

        [Header("References")]
        [SerializeField] private ApiClient apiClient;
        [SerializeField] private ItemPrefabHolder itemPrefab;
        [SerializeField] private GameObject rowPrefab; // Prefab containing Horizontal Layout Group
        [SerializeField] private Transform contentParent;

        private void Start()
        {
            if (apiClient == null)
            {
                apiClient = FindObjectOfType<ApiClient>();
            }

            LoginManager.LoginManager.OnLoginSuccess += FetchInventory;
            LoginManager.LoginManager.OnLogoutSuccess += ClearItems;

            if (LoginManager.LoginManager.Instance != null && LoginManager.LoginManager.Instance.IsLoggedIn())
            {
                FetchInventory();
            }
        }

        private void OnDestroy()
        {
            LoginManager.LoginManager.OnLoginSuccess -= FetchInventory;
            LoginManager.LoginManager.OnLogoutSuccess -= ClearItems;
        }

        [ProButton]
        public void FetchInventory()
        {
            if (apiClient == null)
            {
                Debug.LogError("ApiClient is missing!");
                return;
            }

            Debug.Log($"[InventorySpawner] Fetching: {endpoint}");
            ApiRequest req = new ApiRequest(endpoint, "GET");
            StartCoroutine(apiClient.SendRequest(req, OnInventoryResponse));
        }

        private void OnInventoryResponse(ApiResponse response)
        {
            if (response.IsSuccess)
            {
                try
                {
                    // Response format: { "message": "...", "count": 2, "data": [...] }
                    InventoryResponse data = JsonUtility.FromJson<InventoryResponse>(response.Text);
                    
                    if (data != null && data.data != null)
                    {
                        SpawnItems(data.data);
                    }
                    else
                    {
                        Debug.LogWarning("[InventorySpawner] Data is null.");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[InventorySpawner] JSON Parse Error: {ex.Message}");
                }
            }
            else
            {
                Debug.LogError($"[InventorySpawner] Request Failed: {response.Error}");
            }
        }

        private void SpawnItems(List<InventoryItem> items)
        {
            ClearItems();

            if (contentParent == null || rowPrefab == null || itemPrefab == null)
            {
                Debug.LogError("[InventorySpawner] Missing references.");
                return;
            }

            Transform currentRow = null;

            for (int i = 0; i < items.Count; i++)
            {
                // Create new row every 2 items
                if (i % 2 == 0)
                {
                    GameObject rowObj = Instantiate(rowPrefab, contentParent);
                    rowObj.SetActive(true);
                    currentRow = rowObj.transform;
                }

                if (currentRow != null)
                {
                    ItemPrefabHolder itemHolder = Instantiate(itemPrefab, currentRow);
                    itemHolder.SetData(items[i]);
                    itemHolder.gameObject.SetActive(true);
                }
            }
        }

        [ProButton]
        public void ClearItems()
        {
            if (contentParent)
            {
                foreach (Transform child in contentParent)
                {
                    Destroy(child.gameObject);
                }
            }
        }
    }
}
