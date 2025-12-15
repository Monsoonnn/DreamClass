using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DreamClass.Network;
using DreamClass.LoginManager;
using EasyUI.PickerWheelUI;
using DreamClass.Account;
using System;

namespace DreamClass.SpinWheel
{
    /// <summary>
    /// Manager để fetch spin wheels từ API và spawn UI tabs
    /// </summary>
    public class SpinWheelManager : MonoBehaviour
    {
        [Header("Data References")]
        [SerializeField] private UserProfileSO userProfile; // Reference to the User Profile

        [Header("API Configuration")]
        [SerializeField] private string spinWheelsEndpoint = "/api/spin-wheels?activeOnly=true";

        [Header("UI Prefabs")]
        [SerializeField] private Toggle tabToggleButtonPrefab;
        [SerializeField] private GameObject tabWindowPrefab;

        [Header("UI Components")]
        [SerializeField] private TMPro.TextMeshProUGUI goldText;

        [Header("UI Containers")]
        [SerializeField] private Transform toggleContainer;
        [SerializeField] private Transform windowContainer;
        [SerializeField] private ToggleGroup toggleGroup;

        [Header("Settings")]
        [SerializeField] private bool autoFetchOnLogin = true;
        [SerializeField] private bool clearOnFetch = true;

        [Header("Debug")]
        [SerializeField] private bool enableLogs = true;
        
        [Header("Loading Progress (Inspector Only)")]
        [SerializeField] [Range(0f, 1f)] private float loadingProgress = 0f;
        [SerializeField] private string loadingStatus = "Idle";
        
        [Header("Loading State")]
        [SerializeField] private bool isLoadingComplete = false;

        // Events
        public event Action<List<SpinWheelData>> OnWheelsLoaded;
        public event Action<string> OnError;
        public event Action OnLoadingComplete; // Event khi loading hoàn tất 100%

        private ApiClient apiClient;
        private List<SpinWheelData> currentWheels = new List<SpinWheelData>();
        private List<Toggle> spawnedToggles = new List<Toggle>();
        private List<GameObject> spawnedWindows = new List<GameObject>();
        private int currentGold = 0;
        
        // Image cache: URL -> Sprite
        private Dictionary<string, Sprite> imageCache = new Dictionary<string, Sprite>();
        
        /// <summary>
        /// Kiểm tra loading đã hoàn tất chưa
        /// </summary>
        public bool IsLoadingComplete => isLoadingComplete;

        private void Awake()
        {
            apiClient = FindFirstObjectByType<ApiClient>();

            if (apiClient == null)
            {
                LogError("ApiClient not found in scene!");
            }
        }

        private void Start()
        {
            if (autoFetchOnLogin)
            {
                LoginManager.LoginManager.OnLoginSuccess += OnLoginSuccess;
                
                // Check if already logged in (khi manager spawn sau khi login)
                var loginManager = LoginManager.LoginManager.Instance;
                if (loginManager != null && loginManager.IsLoggedIn())
                {
                    OnLoginSuccess();
                }
            }
        }

        private void OnLoginSuccess()
        {
            // Set initial gold from profile
            if (userProfile != null && userProfile.HasProfile)
            {
                SetGold(userProfile.gold);
            }

            // Check if this object is still valid before starting coroutine
            if (this == null || !gameObject.activeInHierarchy)
            {
                LogError("Cannot fetch spin wheels: Manager is destroyed or inactive");
                return;
            }
            
            Log("Login success detected, fetching spin wheels...");
            FetchSpinWheels();
        }

        /// <summary>
        /// Fetch danh sách spin wheels từ API
        /// </summary>
        public void FetchSpinWheels()
        {
            if (this == null || !gameObject.activeInHierarchy)
            {
                LogError("Cannot fetch: Manager is destroyed or inactive");
                OnError?.Invoke("Manager not available");
                return;
            }
            
            if (apiClient == null)
            {
                LogError("ApiClient is null!");
                OnError?.Invoke("ApiClient not available");
                return;
            }

            // Reset loading state khi bắt đầu fetch mới
            isLoadingComplete = false;
            StartCoroutine(FetchSpinWheelsCoroutine());
        }

        private IEnumerator FetchSpinWheelsCoroutine()
        {
            Log("Fetching spin wheels...");
            
            UpdateLoadingProgress(0f, "Fetching wheels...");

            ApiRequest request = new ApiRequest(spinWheelsEndpoint, "GET");
            ApiResponse response = null;

            yield return apiClient.SendRequest(request, (res) => response = res);

            if (response == null || !response.IsSuccess)
            {
                string error = response?.Error ?? "Unknown error";
                LogError($"Failed to fetch spin wheels: {error}");
                OnError?.Invoke(error);
                yield break;
            }

            try
            {
                SpinWheelResponse wheelResponse = JsonUtility.FromJson<SpinWheelResponse>(response.Text);

                if (wheelResponse == null || wheelResponse.data == null)
                {
                    LogError("Invalid spin wheel response data");
                    OnError?.Invoke("Invalid response format");
                    yield break;
                }

                // Filter: chỉ lấy wheels có ít nhất 2 items
                currentWheels = wheelResponse.data.FindAll(wheel => wheel.items != null && wheel.items.Count >= 2);
                Log($"Successfully fetched {currentWheels.Count} spin wheels (filtered for 6 items only)");
                
                UpdateLoadingProgress(0.2f, $"Found {currentWheels.Count} wheels...");

                OnWheelsLoaded?.Invoke(currentWheels);
            }
            catch (Exception e)
            {
                LogError($"Failed to parse spin wheels response: {e.Message}");
                OnError?.Invoke("Failed to parse response");
                yield break;
            }
            
            // Load ALL wheel images trước khi spawn UI (NGOÀI try-catch)
            yield return StartCoroutine(LoadAllWheelImages());
        }
        
        /// <summary>
        /// Load tất cả wheel images trước khi spawn UI
        /// </summary>
        private IEnumerator LoadAllWheelImages()
        {
            if (currentWheels == null || currentWheels.Count == 0)
            {
                Log("No wheels to load images for");
                yield break;
            }
            
            float baseProgress = 0.2f;
            float loadingRange = 0.7f; // 70% cho loading images
            int totalWheels = currentWheels.Count;
            
            for (int w = 0; w < totalWheels; w++)
            {
                SpinWheelData wheel = currentWheels[w];
                
                if (wheel.items == null || wheel.items.Count < 2)
                {
                    LogError($"Wheel '{wheel.name}' must have at least 2 items! Skipping.");
                    continue;
                }
                
                int itemCount = wheel.items.Count;
                
                for (int i = 0; i < itemCount; i++)
                {
                    string imageUrl = wheel.items[i].itemDetails?.image;
                    
                    if (string.IsNullOrEmpty(imageUrl))
                    {
                        continue;
                    }
                    
                    // Check cache
                    if (imageCache.ContainsKey(imageUrl))
                    {
                        continue;
                    }
                    
                    // Calculate progress
                    float wheelProgress = (float)w / totalWheels;
                    float itemProgress = (float)i / itemCount / totalWheels;
                    float totalProgress = baseProgress + loadingRange * (wheelProgress + itemProgress);
                    UpdateLoadingProgress(totalProgress, $"Loading {wheel.name}: {i+1}/{itemCount} images...");
                    
                    // Download image
                    using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(imageUrl))
                    {
                        yield return request.SendWebRequest();
                        
                        if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                        {
                            byte[] imageData = request.downloadHandler.data;
                            Texture2D texture = null;
                            
                            // Check WebP format
                            if (imageData.Length >= 12 && 
                                imageData[0] == 'R' && imageData[1] == 'I' && 
                                imageData[2] == 'F' && imageData[3] == 'F' &&
                                imageData[8] == 'W' && imageData[9] == 'E' && 
                                imageData[10] == 'B' && imageData[11] == 'P')
                            {
                                WebP.Error error;
                                texture = WebP.Texture2DExt.CreateTexture2DFromWebP(imageData, false, false, out error);
                                
                                if (error != WebP.Error.Success)
                                {
                                    LogError($"Failed to decode WebP: {error}");
                                    continue;
                                }
                            }
                            else
                            {
                                texture = new Texture2D(2, 2);
                                if (!texture.LoadImage(imageData))
                                {
                                    LogError("Failed to load image");
                                    Destroy(texture);
                                    continue;
                                }
                            }
                            
                            if (texture != null)
                            {
                                Sprite sprite = Sprite.Create(
                                    texture,
                                    new Rect(0, 0, texture.width, texture.height),
                                    new Vector2(0.5f, 0.5f)
                                );
                                
                                imageCache[imageUrl] = sprite;
                                Log($"Cached image: {imageUrl}");
                            }
                        }
                        else
                        {
                            LogError($"Failed to download image: {request.error}");
                        }
                    }
                }
            }
            
            UpdateLoadingProgress(0.9f, "Setting up UI...");
            
            // Spawn UI SAU KHI load xong tất cả images
            if (tabToggleButtonPrefab != null && tabWindowPrefab != null)
            {
                SpawnWheelTabs();
            }
        }

        /// <summary>
        /// Spawn TabToggleButton và TabWindow cho từng wheel
        /// </summary>
        public void SpawnWheelTabs()
        {
            StartCoroutine(SpawnWheelTabsCoroutine());
        }
        
        private IEnumerator SpawnWheelTabsCoroutine()
        {
            if (tabToggleButtonPrefab == null || tabWindowPrefab == null)
            {
                LogError("Prefabs not assigned!");
                yield break;
            }

            if (toggleContainer == null || windowContainer == null)
            {
                LogError("Containers not assigned!");
                yield break;
            }

            if (toggleGroup == null)
            {
                LogError("ToggleGroup not assigned!");
                yield break;
            }

            // Clear existing
            if (clearOnFetch)
            {
                ClearSpawnedUI();
            }

            Log($"Spawning {currentWheels.Count} wheel tabs...");

            for (int i = 0; i < currentWheels.Count; i++)
            {
                SpinWheelData wheel = currentWheels[i];

                // Spawn Toggle Button
                Toggle toggle = Instantiate(tabToggleButtonPrefab, toggleContainer);
                toggle.name = $"Toggle_{wheel.name}";
                toggle.gameObject.SetActive(true);
                toggle.group = toggleGroup;

                // Set toggle name (tìm TextMeshPro hoặc Text component)
                TMPro.TextMeshProUGUI toggleText = toggle.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (toggleText != null)
                {
                    toggleText.text = wheel.name;
                }
                else
                {
                    Text toggleTextLegacy = toggle.GetComponentInChildren<Text>();
                    if (toggleTextLegacy != null)
                    {
                        toggleTextLegacy.text = wheel.name;
                    }
                }

                // Spawn Tab Window (INACTIVE để tránh trigger Start() của PickerWheel)
                GameObject window = Instantiate(tabWindowPrefab, windowContainer);
                window.name = $"Window_{wheel.name}";
                window.SetActive(false); // Keep inactive initially
                
                // Set window title
                Transform titleWindowTransform = window.transform.Find("TitleWindow");
                if (titleWindowTransform != null)
                {
                    TMPro.TextMeshProUGUI titleText = titleWindowTransform.GetComponent<TMPro.TextMeshProUGUI>();
                    if (titleText != null)
                    {
                        titleText.text = wheel.name;
                    }
                    else
                    {
                        Text titleTextLegacy = titleWindowTransform.GetComponent<Text>();
                        if (titleTextLegacy != null)
                        {
                            titleTextLegacy.text = wheel.name;
                        }
                    }
                }
                
                // Setup DreamClassPickerWheel trong window (window vẫn inactive)
                DreamClassPickerWheel spinWheel = window.GetComponentInChildren<DreamClassPickerWheel>(true);
                if (spinWheel != null)
                {
                    // Setup với sprites đã có trong cache
                    SetupWheelFromCache(spinWheel, wheel, window, i == 0);
                    
                    // Subscribe to spin events
                    spinWheel.OnSpinSuccess += (result) => OnWheelSpinSuccess(wheel, result);
                    spinWheel.OnSpinFailed += (error) => OnWheelSpinFailed(wheel, error);
                    
                    Log($"Setup spin wheel for: {wheel.name}");
                }
                else
                {
                    LogError($"DreamClassPickerWheel component not found in TabWindow prefab for wheel: {wheel.name}");
                }
                
                // Setup toggle listener để control window visibility
                bool isFirstTab = (i == 0);
                toggle.onValueChanged.AddListener((isOn) => 
                {
                    // Chỉ cho phép active window khi loading đã hoàn tất
                    if (isLoadingComplete)
                    {
                        window.SetActive(isOn);
                        if (isOn)
                        {
                            Log($"Switched to wheel: {wheel.name}");
                        }
                    }
                    else
                    {
                        // Nếu chưa loading xong, không cho active
                        window.SetActive(false);
                        Log($"Cannot switch to wheel: {wheel.name} - Loading not complete");
                    }
                });

                // Toggle và window đều disable cho đến khi loading hoàn tất
                toggle.interactable = false;
                toggle.isOn = isFirstTab; // Set toggle state nhưng không active window
                window.SetActive(false); // Tất cả window đều inactive ban đầu

                spawnedToggles.Add(toggle);
                spawnedWindows.Add(window);

                Log($"Spawned tab for wheel: {wheel.name}");
            }

            Log($"Successfully spawned {spawnedToggles.Count} tabs");
            
            // Hoàn tất loading
            UpdateLoadingProgress(1f, "Complete!");
            
            // Đánh dấu loading hoàn tất và enable tất cả UI
            isLoadingComplete = true;
            EnableAllSpawnedUI();
            
            // Fire event loading complete
            OnLoadingComplete?.Invoke();
        }
        
        /// <summary>
        /// Enable tất cả UI sau khi loading hoàn tất
        /// </summary>
        private void EnableAllSpawnedUI()
        {
            Log("Loading complete - Enabling all spawned UI...");
            
            // Enable tất cả toggles
            foreach (var toggle in spawnedToggles)
            {
                if (toggle != null)
                {
                    toggle.interactable = true;
                }
            }
            
            // Active window của toggle đang được chọn (toggle đầu tiên)
            for (int i = 0; i < spawnedToggles.Count; i++)
            {
                if (spawnedToggles[i] != null && spawnedToggles[i].isOn)
                {
                    if (i < spawnedWindows.Count && spawnedWindows[i] != null)
                    {
                        spawnedWindows[i].SetActive(true);
                        Log($"Activated first window: {spawnedWindows[i].name}");
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Xóa tất cả UI đã spawn
        /// </summary>
        public void ClearSpawnedUI()
        {
            // Reset loading state khi clear UI
            isLoadingComplete = false;
            
            foreach (var toggle in spawnedToggles)
            {
                if (toggle != null)
                {
                    Destroy(toggle.gameObject);
                }
            }
            spawnedToggles.Clear();

            foreach (var window in spawnedWindows)
            {
                if (window != null)
                {
                    Destroy(window);
                }
            }
            spawnedWindows.Clear();

            Log("Cleared all spawned UI");
        }
        
        /// <summary>
        /// Callback khi spin thành công
        /// </summary>
        private void OnWheelSpinSuccess(SpinWheelData wheel, DreamClassPickerWheel.SpinResult result)
        {
            Log($"Spin successful on '{wheel.name}': Got {result.item.name}, Remaining gold: {result.remainingGold}");
            SetGold(result.remainingGold);
        }
        
        /// <summary>
        /// Callback khi spin thất bại
        /// </summary>
        private void OnWheelSpinFailed(SpinWheelData wheel, string error)
        {
            LogError($"Spin failed on '{wheel.name}': {error}");
        }
        
        /// <summary>
        /// Set current gold and update display
        /// </summary>
        public void SetGold(int amount)
        {
            currentGold = amount;
            UpdateGoldDisplay();
        }

        /// <summary>
        /// Update gold display text
        /// </summary>
        private void UpdateGoldDisplay()
        {
            if (goldText != null)
            {
                goldText.text = currentGold.ToString();
            }
            else
            {
                LogError("goldText is not assigned in the inspector!");
            }
        }
        
        /// <summary>
        /// Setup wheel với sprites từ cache (không cần load lại)
        /// </summary>
        private void SetupWheelFromCache(DreamClassPickerWheel spinWheel, SpinWheelData wheel, GameObject window, bool isFirstTab)
        {
            int itemCount = wheel.items.Count;
            Sprite[] itemSprites = new Sprite[itemCount];
            
            // Lấy sprites từ cache
            for (int i = 0; i < itemCount; i++)
            {
                string imageUrl = wheel.items[i].itemDetails?.image;
                
                if (!string.IsNullOrEmpty(imageUrl) && imageCache.ContainsKey(imageUrl))
                {
                    itemSprites[i] = imageCache[imageUrl];
                }
                else
                {
                    LogError($"Image not found in cache for item {i}: {imageUrl}");
                }
            }
            
            // Setup wheel
            spinWheel.SetupWheel(wheel, itemSprites);
            
            // Activate window
            if (window != null)
            {
                window.SetActive(isFirstTab);
                Log($"Activated window for wheel: {wheel.name} (isFirstTab: {isFirstTab})");
            }
        }

        /// <summary>
        /// Load images cho wheel items và setup spin wheel (DEPRECATED - giờ dùng LoadAllWheelImages)
        /// </summary>
        private IEnumerator LoadWheelImagesAndSetup(DreamClassPickerWheel spinWheel, SpinWheelData wheel, GameObject window, bool isFirstTab)
        {
            if (wheel.items == null || wheel.items.Count < 2)
            {
                LogError($"Wheel '{wheel.name}' must have at least 2 items! Skipping image loading.");
                yield break;
            }
            
            // Load sprites theo số lượng items
            int itemCount = wheel.items.Count;
            Sprite[] itemSprites = new Sprite[itemCount];
            
            float baseProgress = 0.2f; // 20% sau khi fetch
            float loadingRange = 0.6f; // 60% cho việc load sprites
            
            for (int i = 0; i < itemCount; i++)
            {
                float currentItemProgress = baseProgress + (loadingRange * (float)i / itemCount);
                UpdateLoadingProgress(currentItemProgress, $"Loading {wheel.name}: {i+1}/{itemCount} images...");
                string imageUrl = wheel.items[i].itemDetails?.image;
                
                if (string.IsNullOrEmpty(imageUrl))
                {
                    Log($"Item {i} has no image URL");
                    continue;
                }
                
                // Check cache
                if (imageCache.ContainsKey(imageUrl))
                {
                    itemSprites[i] = imageCache[imageUrl];
                    continue;
                }
                
                // Download image
                using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(imageUrl))
                {
                    yield return request.SendWebRequest();
                    
                    if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                    {
                        byte[] imageData = request.downloadHandler.data;
                        Texture2D texture = null;
                        
                        // Check WebP format
                        if (imageData.Length >= 12 && 
                            imageData[0] == 'R' && imageData[1] == 'I' && 
                            imageData[2] == 'F' && imageData[3] == 'F' &&
                            imageData[8] == 'W' && imageData[9] == 'E' && 
                            imageData[10] == 'B' && imageData[11] == 'P')
                        {
                            WebP.Error error;
                            texture = WebP.Texture2DExt.CreateTexture2DFromWebP(imageData, false, false, out error);
                            
                            if (error != WebP.Error.Success)
                            {
                                LogError($"Failed to decode WebP: {error}");
                                continue;
                            }
                        }
                        else
                        {
                            texture = new Texture2D(2, 2);
                            if (!texture.LoadImage(imageData))
                            {
                                LogError("Failed to load image");
                                Destroy(texture);
                                continue;
                            }
                        }
                        
                        if (texture != null)
                        {
                            Sprite sprite = Sprite.Create(
                                texture,
                                new Rect(0, 0, texture.width, texture.height),
                                new Vector2(0.5f, 0.5f)
                            );
                            
                            itemSprites[i] = sprite;
                            imageCache[imageUrl] = sprite;
                            Log($"Loaded and cached image {i}: {imageUrl}");
                            
                            // Update progress
                            float completedProgress = baseProgress + (loadingRange * (float)(i+1) / itemCount);
                            UpdateLoadingProgress(completedProgress, $"Loading {wheel.name}: {i+1}/{itemCount} images...");
                        }
                    }
                    else
                    {
                        LogError($"Failed to download image {i}: {request.error}");
                    }
                }
            }
            
            UpdateLoadingProgress(0.8f, $"Setting up {wheel.name}...");
            
            // Setup wheel với sprites đã load
            if (spinWheel != null)
            {
                spinWheel.SetupWheel(wheel, itemSprites);
                
                // IMPORTANT: Activate window SAU KHI setup xong
                // Điều này trigger Start() của PickerWheel với wheelPieces đã có sprites
                if (window != null)
                {
                    window.SetActive(isFirstTab); // Chỉ active tab đầu tiên
                    Log($"Activated window for wheel: {wheel.name} (isFirstTab: {isFirstTab})");
                }
            }
        }

        /// <summary>
        /// Lấy danh sách wheels hiện tại
        /// </summary>
        public List<SpinWheelData> GetCurrentWheels()
        {
            return new List<SpinWheelData>(currentWheels);
        }

        /// <summary>
        /// Lấy wheel theo ID
        /// </summary>
        public SpinWheelData GetWheelById(string wheelId)
        {
            return currentWheels.Find(w => w._id == wheelId);
        }

        #region Logging
        
        /// <summary>
        /// Cập nhật loading progress hiển thị trong Inspector
        /// </summary>
        private void UpdateLoadingProgress(float progress, string message)
        {
            loadingProgress = progress;
            loadingStatus = $"{message} ({Mathf.RoundToInt(progress * 100)}%)";
            
            Log($"Loading: {message} ({Mathf.RoundToInt(progress * 100)}%)");
        }
        
        private void Log(string message)
        {
            if (enableLogs)
                Debug.Log($"<color=cyan>[SpinWheelManager]</color> {message}");
        }

        private void LogError(string message)
        {
            if (enableLogs)
                Debug.LogError($"<color=red>[SpinWheelManager]</color> {message}");
        }
        
        #endregion
    }
}