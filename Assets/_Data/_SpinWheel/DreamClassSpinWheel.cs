using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DreamClass.Network;
using com.cyborgAssets.inspectorButtonPro;

namespace DreamClass.SpinWheel
{
    /// <summary>
    /// Custom Spin Wheel tích hợp với API backend
    /// Adapted from JSG.FortuneSpinWheel
    /// </summary>
    public class DreamClassSpinWheel : MonoBehaviour
    {
        [Header("Spin Configuration")]
        [SerializeField] private string wheelId; // ID của wheel từ API
        [SerializeField] private string spinEndpoint = "/api/spin-wheels/spin";
        
        [Header("Wheel Components")]
        [SerializeField] private Image circleBase;
        [SerializeField] private Image[] rewardPictures; // Array 6 Images như FortuneSpinWheel
        
        [Header("UI Components")]
        [SerializeField] private Button spinButton;
        [SerializeField] private GameObject rewardPanel;
        [SerializeField] private Text rewardFinalText;
        [SerializeField] private Image rewardFinalImage;
        [SerializeField] private Text goldText; // Hiển thị số gold hiện tại
        
        [Header("Spin Settings")]
        [SerializeField] private float minSpinSpeed = 4f;
        [SerializeField] private float maxSpinSpeed = 14f;
        [SerializeField] private float deceleration = 4f;
        [SerializeField] private float slowDeceleration = 0.3f;
        [SerializeField] private float slowSpeedThreshold = 2f;
        
        // Events
        public event Action<SpinResult> OnSpinSuccess;
        public event Action<string> OnSpinFailed;
        
        // Runtime data
        private ApiClient apiClient;
        private SpinWheelData currentWheelData;
        
        private bool isSpinning = false;
        private float spinSpeed = 0;
        private float currentRotation = 0;
        private int resultItemIndex = -1;
        private int currentGold = 0;

        private void Awake()
        {
            apiClient = FindFirstObjectByType<ApiClient>();
            
            if (spinButton != null)
            {
                spinButton.onClick.AddListener(OnSpinButtonClicked);
            }
        }

        private void Update()
        {
            if (isSpinning)
            {
                // Hide reward panel while spinning
                if (rewardPanel != null)
                    rewardPanel.SetActive(false);
                
                // Deceleration
                if (spinSpeed > slowSpeedThreshold)
                {
                    spinSpeed -= deceleration * Time.deltaTime;
                }
                else
                {
                    spinSpeed -= slowDeceleration * Time.deltaTime;
                }
                
                // Rotate wheel
                currentRotation += 100 * Time.deltaTime * spinSpeed;
                if (circleBase != null)
                {
                    circleBase.transform.localRotation = Quaternion.Euler(0, 0, currentRotation);
                }
                
                // Keep item icons upright
                if (rewardPictures != null)
                {
                    for (int i = 0; i < rewardPictures.Length; i++)
                    {
                        if (rewardPictures[i] != null)
                        {
                            rewardPictures[i].transform.rotation = Quaternion.identity;
                        }
                    }
                }
                
                // Stop spinning
                if (spinSpeed <= 0)
                {
                    spinSpeed = 0;
                    isSpinning = false;
                    
                    // Calculate result (which slice we landed on)
                    if (currentWheelData != null && currentWheelData.items.Count == 6)
                    {
                        float anglePerItem = 360f / 6;
                        resultItemIndex = (int)((currentRotation % 360) / anglePerItem);
                        
                        StartCoroutine(ShowRewardPanel(1f));
                    }
                }
            }
            else if (resultItemIndex != -1 && rewardPictures != null && resultItemIndex < rewardPictures.Length)
            {
                // Animate the winning item
                rewardPictures[resultItemIndex].transform.localScale = 
                    (1 + 0.2f * Mathf.Sin(10 * Time.time)) * Vector3.one;
            }
        }

        /// <summary>
        /// Setup wheel với data từ API - Nhận sprites đã load từ Manager
        /// </summary>
        public void SetupWheel(SpinWheelData wheelData, Sprite[] itemSprites)
        {
            currentWheelData = wheelData;
            wheelId = wheelData._id;
            
            if (wheelData.items == null || wheelData.items.Count < 2)
            {
                Debug.LogError($"[DreamClassSpinWheel] Wheel '{wheelData.name}' must have at least 2 items!");
                return;
            }
            
            if (rewardPictures == null || rewardPictures.Length < 6)
            {
                Debug.LogError($"[DreamClassSpinWheel] rewardPictures array must have at least 6 Image elements!");
                return;
            }
            
            if (itemSprites == null || itemSprites.Length < wheelData.items.Count)
            {
                Debug.LogError($"[DreamClassSpinWheel] itemSprites must have {wheelData.items.Count} sprites!");
                return;
            }
            
            // Set sprites vào từng Image trong array
            for (int i = 0; i < 6; i++)
            {
                if (rewardPictures[i] != null && itemSprites[i] != null)
                {
                    rewardPictures[i].sprite = itemSprites[i];
                }
            }
            
            Debug.Log($"[DreamClassSpinWheel] Wheel setup complete: {wheelData.name} with 6 items");
        }

        /// <summary>
        /// Xử lý khi nhấn nút spin
        /// </summary>
        private void OnSpinButtonClicked()
        {
            if (string.IsNullOrEmpty(wheelId))
            {
                Debug.LogError("[DreamClassSpinWheel] Wheel ID not set!");
                return;
            }
            
            if (apiClient == null)
            {
                Debug.LogError("[DreamClassSpinWheel] ApiClient not found!");
                return;
            }
            
            RequestSpin();
        }

        /// <summary>
        /// Gửi request spin tới server
        /// </summary>
        [ProButton]
        public void RequestSpin()
        {
            if (isSpinning)
                return;
            Debug.Log("[DreamClassSpinWheel] Sending spin request to server...");
            StartCoroutine(RequestSpinCoroutine());
        }

        private IEnumerator RequestSpinCoroutine()
        {
            Debug.Log($"[DreamClassSpinWheel] Requesting spin for wheel: {wheelId}");
            
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
                
                Debug.LogError($"[DreamClassSpinWheel] {errorMessage}");
                OnSpinFailed?.Invoke(errorMessage);
                
                // Show error UI (implement as needed)
                if (rewardFinalText != null)
                {
                    rewardFinalText.text = errorMessage;
                    if (rewardPanel != null)
                        rewardPanel.SetActive(true);
                }
                
                yield break;
            }
            
            // Parse success response
            try
            {
                SpinSuccessResponse spinResult = JsonUtility.FromJson<SpinSuccessResponse>(response.Text);
                
                if (spinResult != null && spinResult.data != null)
                {
                    Debug.Log($"[DreamClassSpinWheel] Spin successful! Item: {spinResult.data.item.name}");
                    
                    // Update gold
                    currentGold = spinResult.data.remainingGold;
                    UpdateGoldDisplay();
                    
                    // Calculate which item we got and start spinning
                    int targetIndex = FindItemIndexById(spinResult.data.item.itemId);
                    if (targetIndex != -1)
                    {
                        StartSpinToTarget(targetIndex);
                    }
                    
                    OnSpinSuccess?.Invoke(spinResult.data);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[DreamClassSpinWheel] Failed to parse spin response: {e.Message}");
                OnSpinFailed?.Invoke("Failed to parse response");
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
        /// Bắt đầu spin với target item đã biết từ server
        /// </summary>
        private void StartSpinToTarget(int targetIndex)
        {
            if (isSpinning)
                return;
            
            resultItemIndex = targetIndex;
            
            // Calculate target rotation
            float anglePerItem = 360f / currentWheelData.items.Count;
            float targetAngle = targetIndex * anglePerItem;
            
            // Add multiple full rotations for effect
            int extraRotations = UnityEngine.Random.Range(3, 6);
            float targetRotation = currentRotation + (360 * extraRotations) + targetAngle;
            
            // Start spinning
            spinSpeed = UnityEngine.Random.Range(minSpinSpeed, maxSpinSpeed);
            isSpinning = true;
            
            if (spinButton != null)
                spinButton.gameObject.SetActive(false);
            
            Debug.Log($"[DreamClassSpinWheel] Spinning to item index {targetIndex} at angle {targetAngle}");
        }

        /// <summary>
        /// Hiển thị panel kết quả
        /// </summary>
        private IEnumerator ShowRewardPanel(float delay)
        {
            yield return new WaitForSeconds(delay);
            
            if (resultItemIndex >= 0 && resultItemIndex < currentWheelData.items.Count)
            {
                SpinWheelItem resultItem = currentWheelData.items[resultItemIndex];
                
                // Don't show panel for "empty" type
                if (resultItem.itemDetails?.type != "empty")
                {
                    if (rewardPanel != null)
                        rewardPanel.SetActive(true);
                    
                    if (rewardFinalText != null)
                        rewardFinalText.text = resultItem.itemDetails?.name ?? "Reward!";
                    
                    if (rewardFinalImage != null && !string.IsNullOrEmpty(resultItem.itemDetails?.image))
                    {
                        StartCoroutine(LoadItemImage(rewardFinalImage, resultItem.itemDetails.image));
                    }
                    
                    yield return new WaitForSeconds(2f);
                }
            }
            
            yield return new WaitForSeconds(0.1f);
            ResetWheel();
        }

        /// <summary>
        /// Reset wheel về trạng thái ban đầu
        /// </summary>
        public void ResetWheel()
        {
            currentRotation = 0;
            if (circleBase != null)
                circleBase.transform.localRotation = Quaternion.identity;
            
            isSpinning = false;
            resultItemIndex = -1;
            
            if (spinButton != null)
                spinButton.gameObject.SetActive(true);
            
            if (rewardPanel != null)
                rewardPanel.SetActive(false);
            
            // Reset item scales
            if (rewardPictures != null)
            {
                for (int i = 0; i < rewardPictures.Length; i++)
                {
                    if (rewardPictures[i] != null)
                        rewardPictures[i].transform.localScale = Vector3.one;
                }
            }
        }

        /// <summary>
        /// Load image từ URL (implement with WebP or UnityWebRequest)
        /// </summary>
        private IEnumerator LoadItemImage(Image target, string imageUrl)
        {
            // TODO: Implement image loading
            // You can reuse the avatar loading logic from RankingManager
            Debug.Log($"[DreamClassSpinWheel] Loading image: {imageUrl}");
            yield return null;
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
