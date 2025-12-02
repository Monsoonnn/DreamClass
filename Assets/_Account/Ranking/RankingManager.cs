using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DreamClass.Network;
using DreamClass.LoginManager;

namespace DreamClass.Ranking
{
    /// <summary>
    /// Manager để fetch và hiển thị ranking từ API
    /// Subscribe vào LoginManager.OnLoginSuccess để tự động fetch ranking sau khi đăng nhập
    /// Hỗ trợ 2 loại ranking: theo class và theo grade
    /// </summary>
    public class RankingManager : MonoBehaviour
    {
        [Header("Auto Fetch Settings")]
        [SerializeField] private bool autoFetchOnLogin = true;
        [SerializeField] private bool fetchBothTypes = true; // Fetch cả class và grade
        [SerializeField] private string targetClassName = "10A";
        [SerializeField] private string targetGrade = "10";

        [Header("API Configuration")]
        [SerializeField] private string classRankingEndpoint = "/api/ranking/class/";
        [SerializeField] private string gradeRankingEndpoint = "/api/ranking/grade/";

        [Header("UI Configuration")]
        [SerializeField] private RankingHolder rankingHolderPrefab;
        [SerializeField] private Transform classRankingContainer;
        [SerializeField] private Transform gradeRankingContainer;
        [SerializeField] private bool clearOnFetch = true;

        [Header("Debug")]
        [SerializeField] private bool enableLogs = true;

        // Events
        public event Action<List<RankingStudentData>, RankingFetchType> OnRankingLoaded;
        public event Action<string> OnRankingError;

        private ApiClient apiClient;
        private List<RankingStudentData> currentClassRankingData = new List<RankingStudentData>();
        private List<RankingStudentData> currentGradeRankingData = new List<RankingStudentData>();
        private List<RankingHolder> spawnedClassHolders = new List<RankingHolder>();
        private List<RankingHolder> spawnedGradeHolders = new List<RankingHolder>();
        
        // Avatar cache: URL -> Sprite
        private System.Collections.Generic.Dictionary<string, Sprite> avatarCache = new System.Collections.Generic.Dictionary<string, Sprite>();

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
            // Subscribe to login event
            LoginManager.LoginManager.OnLoginSuccess += OnLoginSuccess;
            
            // Check if already logged in (khi manager spawn sau khi login)
            if (autoFetchOnLogin)
            {
                var loginManager = LoginManager.LoginManager.Instance;
                if (loginManager != null && loginManager.IsLoggedIn())
                {
                    Log("Already logged in, fetching ranking...");
                    
                    if (fetchBothTypes)
                    {
                        FetchClassRanking(targetClassName);
                        FetchGradeRanking(targetGrade);
                    }
                    else
                    {
                        FetchClassRanking(targetClassName);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from login event
            LoginManager.LoginManager.OnLoginSuccess -= OnLoginSuccess;
        }

        /// <summary>
        /// Called when user successfully logs in
        /// </summary>
        private void OnLoginSuccess()
        {
            if (!autoFetchOnLogin)
            {
                Log("Auto fetch disabled, skipping ranking fetch");
                return;
            }

            Log("Login success detected, fetching ranking...");
            
            if (fetchBothTypes)
            {
                // Fetch cả 2 loại ranking
                FetchClassRanking(targetClassName);
                FetchGradeRanking(targetGrade);
            }
            else
            {
                // Chỉ fetch 1 loại (backward compatibility)
                FetchClassRanking(targetClassName);
            }
        }

        /// <summary>
        /// Fetch ranking theo className
        /// </summary>
        public void FetchClassRanking(string className)
        {
            if (string.IsNullOrEmpty(className))
            {
                LogError("Class name cannot be empty!");
                OnRankingError?.Invoke("Class name is empty");
                return;
            }

            StartCoroutine(FetchRankingCoroutine(classRankingEndpoint + className, RankingFetchType.Class));
        }

        /// <summary>
        /// Fetch ranking theo grade
        /// </summary>
        public void FetchGradeRanking(string grade)
        {
            if (string.IsNullOrEmpty(grade))
            {
                LogError("Grade cannot be empty!");
                OnRankingError?.Invoke("Grade is empty");
                return;
            }

            StartCoroutine(FetchRankingCoroutine(gradeRankingEndpoint + grade, RankingFetchType.Grade));
        }

        /// <summary>
        /// Coroutine để fetch ranking từ API
        /// </summary>
        private IEnumerator FetchRankingCoroutine(string endpoint, RankingFetchType type)
        {
            if (apiClient == null)
            {
                LogError("ApiClient is null!");
                OnRankingError?.Invoke("ApiClient not available");
                yield break;
            }

            Log($"Fetching {type} ranking from: {endpoint}");

            ApiRequest request = new ApiRequest(endpoint, "GET");
            ApiResponse response = null;

            yield return apiClient.SendRequest(request, (res) => response = res);

            if (response == null || !response.IsSuccess)
            {
                string error = response?.Error ?? "Unknown error";
                LogError($"Failed to fetch {type} ranking: {error}");
                OnRankingError?.Invoke(error);
                yield break;
            }

            // Parse response
            try
            {
                RankingResponse rankingResponse = JsonUtility.FromJson<RankingResponse>(response.Text);

                if (rankingResponse == null || rankingResponse.data == null)
                {
                    LogError("Invalid ranking response data");
                    OnRankingError?.Invoke("Invalid response format");
                    yield break;
                }

                // Store data based on type
                if (type == RankingFetchType.Class)
                {
                    currentClassRankingData = rankingResponse.data;
                    Log($"Successfully fetched {currentClassRankingData.Count} class ranking entries");
                }
                else
                {
                    currentGradeRankingData = rankingResponse.data;
                    Log($"Successfully fetched {currentGradeRankingData.Count} grade ranking entries");
                }

                // Trigger event
                OnRankingLoaded?.Invoke(rankingResponse.data, type);

                // Auto spawn holders if container is set
                if (rankingHolderPrefab != null)
                {
                    if (type == RankingFetchType.Class && classRankingContainer != null)
                    {
                        SpawnRankingHolders(RankingFetchType.Class);
                    }
                    else if (type == RankingFetchType.Grade && gradeRankingContainer != null)
                    {
                        SpawnRankingHolders(RankingFetchType.Grade);
                    }
                }
            }
            catch (Exception e)
            {
                LogError($"Failed to parse ranking response: {e.Message}");
                OnRankingError?.Invoke("Failed to parse response");
            }
        }

        /// <summary>
        /// Sinh các RankingHolder từ prefab và hiển thị dữ liệu
        /// </summary>
        public void SpawnRankingHolders(RankingFetchType type)
        {
            if (rankingHolderPrefab == null)
            {
                LogError("RankingHolder prefab is not assigned!");
                return;
            }

            Transform container = type == RankingFetchType.Class ? classRankingContainer : gradeRankingContainer;
            List<RankingStudentData> data = type == RankingFetchType.Class ? currentClassRankingData : currentGradeRankingData;
            List<RankingHolder> holders = type == RankingFetchType.Class ? spawnedClassHolders : spawnedGradeHolders;

            if (container == null)
            {
                LogError($"{type} ranking container is not assigned!");
                return;
            }

            // Clear existing holders if needed
            if (clearOnFetch)
            {
                ClearRankingHolders(type);
            }

            Log($"Spawning {data.Count} {type} ranking holders...");

            // Spawn holders cho từng entry
            foreach (var studentData in data)
            {
                RankingHolder holder = Instantiate(rankingHolderPrefab, container);
                holder.gameObject.SetActive(true);
                
                // Set data (text only)
                holder.SetData(studentData);
                
                // Load avatar từ Manager (cached)
                if (!string.IsNullOrEmpty(studentData.avatar))
                {
                    StartCoroutine(LoadAndSetAvatar(holder, studentData.avatar));
                }
                
                holders.Add(holder);
            }

            Log($"Successfully spawned {holders.Count} {type} holders");
        }

        /// <summary>
        /// Load avatar và set vào holder (with caching)
        /// </summary>
        private IEnumerator LoadAndSetAvatar(RankingHolder holder, string avatarUrl)
        {
            // Check cache first
            if (avatarCache.ContainsKey(avatarUrl))
            {
                holder.SetAvatar(avatarCache[avatarUrl]);
                yield break;
            }

            // Download avatar
            using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(avatarUrl))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    byte[] imageData = request.downloadHandler.data;
                    Texture2D texture = null;

                    // Check if it's WebP format
                    if (imageData.Length >= 12 && 
                        imageData[0] == 'R' && imageData[1] == 'I' && 
                        imageData[2] == 'F' && imageData[3] == 'F' &&
                        imageData[8] == 'W' && imageData[9] == 'E' && 
                        imageData[10] == 'B' && imageData[11] == 'P')
                    {
                        // Decode WebP
                        WebP.Error error;
                        texture = WebP.Texture2DExt.CreateTexture2DFromWebP(imageData, false, false, out error);
                        
                        if (error != WebP.Error.Success)
                        {
                            LogError($"Failed to decode WebP avatar: {error}");
                            yield break;
                        }
                    }
                    else
                    {
                        // Standard PNG/JPG
                        texture = new Texture2D(2, 2);
                        if (!texture.LoadImage(imageData))
                        {
                            LogError("Failed to load avatar image");
                            Destroy(texture);
                            yield break;
                        }
                    }

                    if (texture != null)
                    {
                        Sprite sprite = Sprite.Create(
                            texture,
                            new Rect(0, 0, texture.width, texture.height),
                            new Vector2(0.5f, 0.5f)
                        );
                        
                        // Cache sprite
                        avatarCache[avatarUrl] = sprite;
                        
                        // Set to holder
                        if (holder != null)
                        {
                            holder.SetAvatar(sprite);
                        }
                        
                        Log($"Avatar loaded and cached: {avatarUrl}");
                    }
                }
                else
                {
                    LogError($"Failed to download avatar: {request.error}");
                }
            }
        }

        /// <summary>
        /// Xóa ranking holders đã spawn theo type
        /// </summary>
        public void ClearRankingHolders(RankingFetchType type)
        {
            List<RankingHolder> holders = type == RankingFetchType.Class ? spawnedClassHolders : spawnedGradeHolders;

            foreach (var holder in holders)
            {
                if (holder != null)
                {
                    Destroy(holder.gameObject);
                }
            }
            holders.Clear();
            Log($"Cleared all {type} ranking holders");
        }

        /// <summary>
        /// Xóa tất cả ranking holders (cả class và grade)
        /// </summary>
        public void ClearAllRankingHolders()
        {
            ClearRankingHolders(RankingFetchType.Class);
            ClearRankingHolders(RankingFetchType.Grade);
            Log("Cleared all ranking holders (both types)");
        }

        /// <summary>
        /// Lấy danh sách ranking hiện tại theo type
        /// </summary>
        public List<RankingStudentData> GetCurrentRanking(RankingFetchType type)
        {
            return type == RankingFetchType.Class 
                ? new List<RankingStudentData>(currentClassRankingData) 
                : new List<RankingStudentData>(currentGradeRankingData);
        }

        /// <summary>
        /// Lấy top N students theo type
        /// </summary>
        public List<RankingStudentData> GetTopStudents(int count, RankingFetchType type)
        {
            var data = type == RankingFetchType.Class ? currentClassRankingData : currentGradeRankingData;
            int takeCount = Mathf.Min(count, data.Count);
            return data.GetRange(0, takeCount);
        }

        /// <summary>
        /// Tìm student theo playerId trong type cụ thể
        /// </summary>
        public RankingStudentData FindStudentByPlayerId(string playerId, RankingFetchType type)
        {
            var data = type == RankingFetchType.Class ? currentClassRankingData : currentGradeRankingData;
            return data.Find(s => s.playerId == playerId);
        }

        /// <summary>
        /// Tìm student theo name trong type cụ thể
        /// </summary>
        public RankingStudentData FindStudentByName(string name, RankingFetchType type)
        {
            var data = type == RankingFetchType.Class ? currentClassRankingData : currentGradeRankingData;
            return data.Find(s => s.name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        #region Logging
        private void Log(string message)
        {
            if (enableLogs)
                Debug.Log($"<color=cyan>[RankingManager]</color> {message}");
        }

        private void LogError(string message)
        {
            if (enableLogs)
                Debug.LogError($"<color=red>[RankingManager]</color> {message}");
        }
        #endregion
    }

    public enum RankingFetchType
    {
        Class,
        Grade
    }
}
