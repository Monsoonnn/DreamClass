using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DreamClass.Network;
using EasyUI.PickerWheelUI;

namespace DreamClass.SpinWheel
{
    /// <summary>
    /// Adapter kết hợp PickerWheel animation với API backend
    /// Server quyết định kết quả, PickerWheel animation hiển thị
    /// </summary>
    public class DreamClassPickerWheel : MonoBehaviour
    {
        [Header("Spin Configuration")]
        [SerializeField] private string wheelId; // ID của wheel từ API
        [SerializeField] private string spinEndpoint = "/api/spin-wheels/spin";
        
        [Header("Wheel Components")]
        [SerializeField] private PickerWheel pickerWheel;
        
        [Header("UI Components")]
        [SerializeField] private Button spinButton;
        [SerializeField] private GameObject rewardPanel;
        [SerializeField] private Text rewardFinalText;
        [SerializeField] private Image rewardFinalImage;
        [SerializeField] private TMPro.TextMeshProUGUI goldText;
        
        [Header("Settings")]
        [SerializeField] private bool showRewardPanelOnComplete = true;
        [SerializeField] private float rewardPanelDelay = 1f;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugMode = false;
        
        // Events
        public event Action<SpinResult> OnSpinSuccess;
        public event Action<string> OnSpinFailed;
        
        // Runtime data
        private ApiClient apiClient;
        private SpinWheelData currentWheelData;
        private int currentGold = 0;
        private SpinResult pendingResult; // Kết quả từ server đang chờ animation
        
        private void Awake()
        {
            apiClient = FindFirstObjectByType<ApiClient>();
            
            if (spinButton != null)
            {
                spinButton.onClick.AddListener(OnSpinButtonClicked);
            }
            
            if (pickerWheel == null)
            {
                Debug.LogError("[DreamClassPickerWheel] PickerWheel component not assigned!");
            }
        }
        
        /// <summary>
        /// Setup wheel với data từ API và sprites đã load
        /// </summary>
        public void SetupWheel(SpinWheelData wheelData, Sprite[] itemSprites)
        {
            currentWheelData = wheelData;
            wheelId = wheelData._id;
            
            if (wheelData.items == null || wheelData.items.Count < 2)
            {
                Debug.LogError($"[DreamClassPickerWheel] Wheel '{wheelData.name}' must have at least 2 items!");
                return;
            }
            
            if (itemSprites == null || itemSprites.Length < wheelData.items.Count)
            {
                Debug.LogError($"[DreamClassPickerWheel] itemSprites must have {wheelData.items.Count} sprites!");
                return;
            }
            
            if (pickerWheel == null)
            {
                Debug.LogError("[DreamClassPickerWheel] PickerWheel not assigned!");
                return;
            }
            
            // Setup wheel pieces cho PickerWheel
            int itemCount = wheelData.items.Count;
            pickerWheel.wheelPieces = new WheelPiece[itemCount];
            
            for (int i = 0; i < itemCount; i++)
            {
                SpinWheelItem item = wheelData.items[i];
                
                pickerWheel.wheelPieces[i] = new WheelPiece
                {
                    Icon = itemSprites[i],
                    Label = item.itemDetails?.name ?? "Item",
                    Amount = 0, // Không hiển thị amount vì server quyết định
                    Chance = item.rate * 100f // Convert rate to percentage (chỉ để tham khảo, không dùng random)
                };
            }
            
            // IMPORTANT: Khởi tạo wheel SAU KHI set wheelPieces
            pickerWheel.InitializeWheel();
            
            Debug.Log($"[DreamClassPickerWheel] Wheel setup complete: {wheelData.name} with {itemCount} items");
        }
        
        /// <summary>
        /// Xử lý khi nhấn nút spin
        /// </summary>
        private void OnSpinButtonClicked()
        {
            if (string.IsNullOrEmpty(wheelId))
            {
                Debug.LogError("[DreamClassPickerWheel] Wheel ID not set!");
                return;
            }
            
            if (apiClient == null)
            {
                Debug.LogError("[DreamClassPickerWheel] ApiClient not found!");
                return;
            }
            
            if (pickerWheel.IsSpinning)
            {
                Debug.LogWarning("[DreamClassPickerWheel] Wheel is already spinning!");
                return;
            }
            
            RequestSpin();
        }
        
        /// <summary>
        /// Gửi request spin tới server
        /// </summary>
        private void RequestSpin()
        {
            StartCoroutine(RequestSpinCoroutine());
        }
        
        private IEnumerator RequestSpinCoroutine()
        {
            Debug.Log($"[DreamClassPickerWheel] Requesting spin for wheel: {wheelId}");
            
            // Disable spin button
            if (spinButton != null)
                spinButton.interactable = false;
            
            // Create request body
            string jsonBody = JsonUtility.ToJson(new SpinRequest { wheelId = wheelId });
            ApiRequest request = new ApiRequest(spinEndpoint, "POST", jsonBody);
            
            ApiResponse response = null;
            yield return apiClient.SendRequest(request, (res) => response = res);
            
            if (response == null || !response.IsSuccess)
            {
                // Handle error
                string errorMessage = "Spin failed";
                
                try
                {
                    SpinErrorResponse errorData = JsonUtility.FromJson<SpinErrorResponse>(response?.Text);
                    if (errorData != null && !string.IsNullOrEmpty(errorData.message))
                    {
                        errorMessage = errorData.message;
                    }
                }
                catch { }
                
                Debug.LogError($"[DreamClassPickerWheel] {errorMessage}");
                OnSpinFailed?.Invoke(errorMessage);
                
                // Show error UI
                if (rewardFinalText != null && rewardPanel != null)
                {
                    rewardFinalText.text = errorMessage;
                    rewardPanel.SetActive(true);
                }
                
                // Re-enable button
                if (spinButton != null)
                    spinButton.interactable = true;
                
                yield break;
            }
            
            // Parse success response
            try
            {
                SpinSuccessResponse spinResult = JsonUtility.FromJson<SpinSuccessResponse>(response.Text);
                
                if (spinResult != null && spinResult.data != null)
                {
                    Debug.Log($"[DreamClassPickerWheel] Spin successful! Item: {spinResult.data.item.name}");
                    
                    // Update gold
                    currentGold = spinResult.data.remainingGold;
                    UpdateGoldDisplay();
                    
                    // Store result và start animation
                    pendingResult = spinResult.data;
                    
                    // Tìm index của item trong wheel
                    int targetIndex = FindItemIndexById(spinResult.data.item.itemId);
                    
                    if (targetIndex != -1)
                    {
                        StartSpinAnimation(targetIndex);
                    }
                    else
                    {
                        Debug.LogError($"[DreamClassPickerWheel] Cannot find item {spinResult.data.item.itemId} in wheel!");
                        
                        // Re-enable button
                        if (spinButton != null)
                            spinButton.interactable = true;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[DreamClassPickerWheel] Failed to parse spin response: {e.Message}");
                OnSpinFailed?.Invoke("Failed to parse response");
                
                // Re-enable button
                if (spinButton != null)
                    spinButton.interactable = true;
            }
        }
        
        /// <summary>
        /// Tìm index của item trong wheel data theo itemId
        /// </summary>
        private int FindItemIndexById(string itemId)
        {
            if (currentWheelData == null || currentWheelData.items == null)
                return -1;
            
            for (int i = 0; i < currentWheelData.items.Count; i++)
            {
                if (currentWheelData.items[i].itemId == itemId)
                    return i;
            }
            
            return -1;
        }
        
        /// <summary>
        /// Bắt đầu animation spin đến target index
        /// </summary>
        private void StartSpinAnimation(int targetIndex)
        {
            if (pickerWheel == null)
                return;
            
            // Setup callbacks cho PickerWheel
            pickerWheel.OnSpinStart(() => {
                Debug.Log("[DreamClassPickerWheel] Spin animation started");
                
                // Hide reward panel
                if (rewardPanel != null)
                    rewardPanel.SetActive(false);
            });
            
            pickerWheel.OnSpinEnd((piece) => {
                Debug.Log($"[DreamClassPickerWheel] Spin animation ended on piece: {piece.Label}");
                
                // Show reward panel after delay
                if (showRewardPanelOnComplete && pendingResult != null)
                {
                    StartCoroutine(ShowRewardPanelDelayed());
                }
                
                // Trigger success event
                if (pendingResult != null)
                {
                    OnSpinSuccess?.Invoke(pendingResult);
                }
                
                // Re-enable button
                if (spinButton != null)
                    spinButton.interactable = true;
            });
            
            // NOTE: PickerWheel sẽ spin đến targetIndex được server quyết định
            // Với fix mới, PickerWheel.Spin(targetIndex) sẽ adjust rotation để land chính xác
            pickerWheel.Spin(targetIndex);
            
            Debug.Log($"[DreamClassPickerWheel] Starting spin animation to index {targetIndex} (server result)");
        }
        
        /// <summary>
        /// Hiển thị reward panel sau delay
        /// </summary>
        private IEnumerator ShowRewardPanelDelayed()
        {
            yield return new WaitForSeconds(rewardPanelDelay);
            
            if (pendingResult == null)
                yield break;
            
            // Show reward panel cho mọi loại item
            if (rewardPanel != null)
                rewardPanel.SetActive(true);
            
            if (rewardFinalText != null)
                rewardFinalText.text = pendingResult.item.name;
            
            if (rewardFinalImage != null && !string.IsNullOrEmpty(pendingResult.item.image))
            {
                // Tìm sprite từ wheelPieces (sprites đã load từ Manager)
                int index = FindItemIndexById(pendingResult.item.itemId);
                if (index != -1 && index < pickerWheel.wheelPieces.Length && pickerWheel.wheelPieces[index] != null)
                {
                    rewardFinalImage.sprite = pickerWheel.wheelPieces[index].Icon;
                    Debug.Log($"[DreamClassPickerWheel] Set reward image from wheelPieces[{index}]");
                }
                else
                {
                    Debug.LogWarning($"[DreamClassPickerWheel] Cannot find sprite for item index {index}");
                }
            }
            
            // Clear pending result
            pendingResult = null;
        }
        
        /// <summary>
        /// Update gold display
        /// </summary>
        private void UpdateGoldDisplay()
        {
            if (goldText != null)
            {
                goldText.text = currentGold.ToString();
            }
        }
        
        /// <summary>
        /// Set current gold amount
        /// </summary>
        public void SetGold(int gold)
        {
            currentGold = gold;
            UpdateGoldDisplay();
        }
        
        /// <summary>
        /// Reset wheel về trạng thái ban đầu
        /// </summary>
        public void ResetWheel()
        {
            if (rewardPanel != null)
                rewardPanel.SetActive(false);
            
            if (spinButton != null)
                spinButton.interactable = true;
            
            pendingResult = null;
        }
        
        #region Debug Methods
        
        [ContextMenu("Debug: Spin (No API)")]
        private void DebugSpin()
        {
            if (!enableDebugMode)
            {
                Debug.LogWarning("[DreamClassPickerWheel] Debug mode is disabled. Enable it in inspector.");
                return;
            }
            
            if (pickerWheel == null)
            {
                Debug.LogError("[DreamClassPickerWheel] PickerWheel not assigned!");
                return;
            }
            
            if (pickerWheel.IsSpinning)
            {
                Debug.LogWarning("[DreamClassPickerWheel] Wheel is already spinning!");
                return;
            }
            
            Debug.Log("[DreamClassPickerWheel] DEBUG: Starting spin animation without API call");
            
            // Create fake result
            if (currentWheelData != null && currentWheelData.items.Count > 0)
            {
                // Pick random item
                int randomIndex = UnityEngine.Random.Range(0, currentWheelData.items.Count);
                SpinWheelItem randomItem = currentWheelData.items[randomIndex];
                
                pendingResult = new SpinResult
                {
                    item = randomItem.itemDetails,
                    remainingGold = currentGold - 100 // Fake gold deduction
                };
                
                currentGold = pendingResult.remainingGold;
                UpdateGoldDisplay();
                
                Debug.Log($"[DreamClassPickerWheel] DEBUG: Fake result - Item: {randomItem.itemDetails?.name}, Index: {randomIndex}");
            }
            else
            {
                Debug.LogWarning("[DreamClassPickerWheel] DEBUG: No wheel data available for fake result");
            }
            
            // Start animation
            StartSpinAnimation(-1); // -1 means random
        }
        
        [ContextMenu("Debug: Test Reward Panel")]
        private void DebugShowRewardPanel()
        {
            if (!enableDebugMode)
            {
                Debug.LogWarning("[DreamClassPickerWheel] Debug mode is disabled. Enable it in inspector.");
                return;
            }
            
            if (rewardPanel != null)
            {
                rewardPanel.SetActive(!rewardPanel.activeSelf);
                Debug.Log($"[DreamClassPickerWheel] DEBUG: Reward panel toggled - Active: {rewardPanel.activeSelf}");
            }
        }
        
        [ContextMenu("Debug: Add Test Gold")]
        private void DebugAddGold()
        {
            if (!enableDebugMode)
            {
                Debug.LogWarning("[DreamClassPickerWheel] Debug mode is disabled. Enable it in inspector.");
                return;
            }
            
            SetGold(currentGold + 500);
            Debug.Log($"[DreamClassPickerWheel] DEBUG: Added 500 gold. Current: {currentGold}");
        }
        
        /// <summary>
        /// Public method để test spin từ button trong scene
        /// </summary>
        public void DebugSpinPublic()
        {
            if (enableDebugMode)
            {
                DebugSpin();
            }
        }
        
        #endregion
        
        #region API Data Models
        
        [Serializable]
        private class SpinRequest
        {
            public string wheelId;
        }
        
        [Serializable]
        public class SpinSuccessResponse
        {
            public string message;
            public SpinResult data;
        }
        
        [Serializable]
        public class SpinResult
        {
            public ItemDetails item;
            public int remainingGold;
        }
        
        [Serializable]
        private class SpinErrorResponse
        {
            public string message;
        }
        
        #endregion
    }
}
