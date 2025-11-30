using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using com.cyborgAssets.inspectorButtonPro;
using DreamClass.Network;

namespace DreamClass.Subjects
{
    /// <summary>
    /// Singleton Service để fetch PDF subjects từ API và quản lý cache
    /// Auto-initializes on game start (before first scene load)
    /// </summary>
    public class PDFSubjectService : SingletonCtrl<PDFSubjectService>
    {
        // Static event for when subjects are ready
        public static event Action OnReady;
        private static bool isReady = false;
        public static bool IsReady => isReady;
        [Header("API Settings")]
        [SerializeField] private ApiClient apiClient;
        [SerializeField] private string listEndpoint = "/api/pdfs/list";

        [Header("Subject Database")]
        [SerializeField] private SubjectDatabase subjectDatabase;
        [Tooltip("Only load subjects that have matching path in SubjectDatabase")]
        [SerializeField] private bool onlyLoadMatchingSubjects = true;

        [Header("Cache Settings")]
        [SerializeField] private string cacheFolder = "PDFSubjectsCache";
        [SerializeField] private string manifestFileName = "cache_manifest.json";
        [Tooltip("Automatically start caching subjects after fetch")]
        [SerializeField] private bool autoCacheAfterFetch = true;
        [Tooltip("Preload cached sprites on Start")]
        [SerializeField] private bool preloadCachedOnStart = true;
        [Tooltip("Cache as PNG instead of WebP for faster loading (3-5x faster load, ~30-50% larger file size)")]
        [SerializeField] private bool cacheAsPNG = true;
        [Tooltip("Auto fetch from API on Start")]
        [SerializeField] private bool autoFetchOnStart = true;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLog = true;

        // Events
        public event Action<List<RemoteSubjectInfo>> OnSubjectsLoaded;
        public event Action<RemoteSubjectInfo, float> OnSubjectCacheProgress;
        public event Action<RemoteSubjectInfo> OnSubjectCacheComplete;
        public event Action<string> OnError;
        
        /// <summary>
        /// Overall loading progress (0-1) for all subjects
        /// </summary>
        public static event Action<float> OnOverallProgress;
        private static float overallProgress = 0f;
        public static float OverallProgress => overallProgress;

        // Cache data
        private SubjectCacheManifest cacheManifest;
        private List<RemoteSubjectInfo> remoteSubjects = new List<RemoteSubjectInfo>();

        public List<RemoteSubjectInfo> RemoteSubjects => remoteSubjects;

        private string CachePath => Path.Combine(Application.persistentDataPath, cacheFolder);
        private string ManifestPath => Path.Combine(CachePath, manifestFileName);

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.LoadApiClient();
        }

        private void LoadApiClient()
        {
            if(apiClient != null) return;
            apiClient = GameObject.FindAnyObjectByType<ApiClient>();
        }

        private bool hasFetched = false;
        private bool isPreloading = false;
        private bool preloadComplete = false;

        protected override void Start()
        {
            base.Start();
            LoadCacheManifest();

            // Preload cached sprites first
            if (preloadCachedOnStart && cacheManifest != null && cacheManifest.subjects.Count > 0)
            {
                Log($"Found {cacheManifest.subjects.Count} cached subjects, preloading sprites...");
                isPreloading = true;
                StartCoroutine(PreloadCachedSpritesCoroutine());
            }
            else
            {
                preloadComplete = true; // No preload needed
            }

            // Auto fetch from API on start (no login required)
            if (autoFetchOnStart && !hasFetched)
            {
                Log("Auto-fetching subjects from API...");
                FetchSubjects();
            }
            else if (!autoFetchOnStart && preloadComplete)
            {
                // No fetch needed and preload done, mark ready
                TryMarkAsReady();
            }
        }

        /// <summary>
        /// Try to mark service as ready (only if both preload and fetch are complete)
        /// </summary>
        private void TryMarkAsReady()
        {
            if (isReady) return;
            
            if (isPreloading && !preloadComplete)
            {
                Log("[TryMarkAsReady] Preload still in progress, waiting...");
                return;
            }
            
            Log("[TryMarkAsReady] All loading complete, marking as ready");
            overallProgress = 1f;
            OnOverallProgress?.Invoke(1f);
            isReady = true;
            OnReady?.Invoke();
        }

        /// <summary>
        /// Manual initialize if autoFetchOnStart is disabled
        /// </summary>
        public void Initialize()
        {
            if (hasFetched)
            {
                Log("Already fetched subjects, skipping...");
                return;
            }

            Log("Initializing PDFSubjectService...");
            FetchSubjects();
        }

        #region API Fetch

        /// <summary>
        /// Fetch danh sách PDF subjects từ API
        /// No login required - public API endpoint
        /// </summary>
        [ProButton]
        public void FetchSubjects()
        {
            if (apiClient == null)
            {
                LogError("ApiClient not assigned!");
                OnError?.Invoke("ApiClient not assigned!");
                return;
            }

            StartCoroutine(FetchSubjectsCoroutine());
        }

        private IEnumerator FetchSubjectsCoroutine()
        {
            Log("Fetching PDF subjects from API...");

            ApiRequest request = new ApiRequest(listEndpoint, "GET");
            ApiResponse response = null;

            yield return apiClient.SendRequest(request, (res) => response = res);

            if (response == null || !response.IsSuccess)
            {
                string error = response?.Error ?? "Unknown error";
                LogError($"Failed to fetch subjects: {error}");
                OnError?.Invoke(error);
                yield break;
            }

            try
            {
                PDFListResponse listResponse = JsonUtility.FromJson<PDFListResponse>(response.Text);

                if (listResponse != null && listResponse.data != null)
                {
                    remoteSubjects.Clear();
                    int skippedCount = 0;

                    foreach (var pdfInfo in listResponse.data)
                    {
                        // Check if path matches any local subject
                        if (onlyLoadMatchingSubjects && subjectDatabase != null)
                        {
                            bool hasMatch = subjectDatabase.subjects.Exists(s => s.MatchesPath(pdfInfo.path));
                            if (!hasMatch)
                            {
                                Log($"Skipping '{pdfInfo.name}' - no matching path in SubjectDatabase (path: {pdfInfo.path})");
                                skippedCount++;
                                continue;
                            }
                        }

                        RemoteSubjectInfo remoteSubject = new RemoteSubjectInfo(pdfInfo);
                        
                        // Check cache status
                        var cachedData = cacheManifest.GetSubjectCache(pdfInfo.name);
                        if (cachedData != null)
                        {
                            string currentHash = pdfInfo.GetVersionHash();
                            remoteSubject.isCached = cachedData.isFullyCached && 
                                                     cachedData.versionHash == currentHash;
                            
                            if (remoteSubject.isCached)
                            {
                                remoteSubject.localImagePaths = new List<string>(cachedData.cachedImagePaths);
                                Log($"Subject '{pdfInfo.name}' is cached and up-to-date");
                            }
                            else
                            {
                                Log($"Subject '{pdfInfo.name}' cache outdated. Hash: {cachedData.versionHash} -> {currentHash}");
                            }
                        }

                        remoteSubjects.Add(remoteSubject);
                    }

                    Log($"Loaded {remoteSubjects.Count} subjects from API (skipped {skippedCount} without matching path)");
                    hasFetched = true;
                    
                    // Update SubjectDatabase with remote data immediately
                    UpdateSubjectDatabaseWithRemoteData();
                    
                    // Validate existing cache
                    ValidateAllCacheStatus();
                    
                    // Count cached subjects
                    int cachedCount = 0;
                    foreach (var s in remoteSubjects)
                    {
                        if (s.isCached) cachedCount++;
                    }
                    Log($"Cached subjects: {cachedCount}/{remoteSubjects.Count}");

                    // Auto-cache uncached subjects
                    if (autoCacheAfterFetch)
                    {
                        StartCoroutine(AutoCacheAllSubjects());
                    }
                    else
                    {
                        // If not auto-caching, mark as ready immediately
                        isReady = true;
                        OnSubjectsLoaded?.Invoke(remoteSubjects);
                        OnReady?.Invoke();
                    }
                }
                else
                {
                    LogError("Failed to parse PDF list response");
                    OnError?.Invoke("Failed to parse response");
                }
            }
            catch (Exception ex)
            {
                LogError($"Exception parsing response: {ex.Message}");
                OnError?.Invoke(ex.Message);
            }
        }

        /// <summary>
        /// Reset fetch state (useful for re-login scenarios)
        /// </summary>
        public void ResetFetchState()
        {
            hasFetched = false;
            isReady = false;
            remoteSubjects.Clear();
            Log("Fetch state reset");
        }

        /// <summary>
        /// Update SubjectDatabase với remote data ngay sau khi fetch
        /// Lưu trực tiếp vào ScriptableObject để LearningModeManager không phải merge
        /// </summary>
        private void UpdateSubjectDatabaseWithRemoteData()
        {
            if (subjectDatabase == null)
            {
                LogError("SubjectDatabase not assigned!");
                return;
            }

            int updatedCount = 0;

            foreach (var remote in remoteSubjects)
            {
                // Find matching local subject by path
                var localSubject = subjectDatabase.subjects.Find(s => s.MatchesPath(remote.path));
                
                if (localSubject != null)
                {
                    // Update local subject with API data (keep lectures!)
                    localSubject.title = remote.title;
                    localSubject.grade = remote.grade;
                    localSubject.category = remote.category;
                    localSubject.pages = remote.pages;
                    localSubject.pdfUrl = remote.pdfUrl;
                    localSubject.imageUrls = remote.imageUrls != null ? new List<string>(remote.imageUrls) : new List<string>();
                    localSubject.localImagePaths = remote.localImagePaths != null ? new List<string>(remote.localImagePaths) : new List<string>();
                    localSubject.isCached = remote.isCached;
                    
                    updatedCount++;
                    Log($"Updated SubjectDatabase: {localSubject.name} (cached: {remote.isCached})");
                }
            }

            Log($"Updated {updatedCount} subjects in SubjectDatabase");

            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(subjectDatabase);
            #endif
        }

        /// <summary>
        /// Tự động cache tất cả subjects chưa cached
        /// </summary>
        private IEnumerator AutoCacheAllSubjects()
        {
            Log("Starting auto-cache for all uncached subjects...");

            int totalUncached = 0;
            int cachedNow = 0;

            // Count uncached subjects
            foreach (var subject in remoteSubjects)
            {
                if (!subject.isCached)
                {
                    totalUncached++;
                }
            }

            if (totalUncached == 0)
            {
                Log("All subjects already cached!");
                OnSubjectsLoaded?.Invoke(remoteSubjects);
                
                // Wait for preload to complete before marking ready
                while (isPreloading && !preloadComplete)
                {
//                    Log("[AUTO-CACHE] Waiting for preload to complete...");
                    yield return new WaitForSeconds(0.5f);
                }
                
                TryMarkAsReady();
                yield break;
            }
            
            // Reset progress
            overallProgress = 0f;
            OnOverallProgress?.Invoke(0f);

            Log($"Found {totalUncached} uncached subjects, starting download...");

            // Cache each uncached subject
            foreach (var subject in remoteSubjects)
            {
                // Skip if already cached AND sprites already loaded (from preload)
                var localSubject = subjectDatabase?.subjects.Find(s => s.MatchesPath(subject.path));
                if (subject.isCached && localSubject != null && localSubject.HasLoadedSprites())
                {
                    Log($"[AUTO-CACHE] Skipping {subject.name} - already cached and loaded from preload");
                    continue;
                }

                if (!subject.isCached)
                {
                    int overallPercent = totalUncached > 0 ? (cachedNow * 100 / totalUncached) : 0;
                    Log($"[AUTO-CACHE] Subject {cachedNow + 1}/{totalUncached} ({overallPercent}%): {subject.name}");
                    
                    bool cacheComplete = false;
                    
                    // Subscribe to complete event for this subject
                    Action<RemoteSubjectInfo> onComplete = null;
                    onComplete = (s) =>
                    {
                        if (s.name == subject.name)
                        {
                            cacheComplete = true;
                            OnSubjectCacheComplete -= onComplete;
                        }
                    };
                    OnSubjectCacheComplete += onComplete;

                    // Start caching
                    yield return StartCoroutine(CacheSubjectCoroutine(subject));

                    // Wait for completion
                    while (!cacheComplete)
                    {
                        yield return null;
                    }

                    cachedNow++;
                    overallProgress = totalUncached > 0 ? (float)cachedNow / totalUncached : 1f;
                    OnOverallProgress?.Invoke(overallProgress);
                    int completedPercent = (int)(overallProgress * 100);
                    Log($"[AUTO-CACHE] Overall progress: {completedPercent}% ({cachedNow}/{totalUncached} subjects)");

                    // Small delay between subjects
                    yield return new WaitForSeconds(0.5f);
                }
            }

            Log($"[AUTO-CACHE COMPLETE] 100% - Cached {cachedNow} subjects");
            
            OnSubjectsLoaded?.Invoke(remoteSubjects);
            
            // Wait for preload to complete before marking ready
            while (isPreloading && !preloadComplete)
            {
                Log("[AUTO-CACHE] Waiting for preload to complete...");
                yield return new WaitForSeconds(0.5f);
            }
            
            TryMarkAsReady();
        }

        /// <summary>
        /// Cache một subject cụ thể theo path
        /// </summary>
        public void CacheSubjectByPath(string path)
        {
            var subject = remoteSubjects.Find(s => s.path == path);
            if (subject == null)
            {
                LogError($"Subject not found for path: {path}");
                return;
            }

            if (subject.isCached && ValidateCacheFiles(subject))
            {
                Log($"Subject already cached: {subject.name}");
                return;
            }

            StartCoroutine(CacheSubjectCoroutine(subject));
        }

        #endregion

        #region Cache Management

        /// <summary>
        /// Load cache manifest từ disk
        /// </summary>
        private void LoadCacheManifest()
        {
            EnsureCacheDirectoryExists();

            if (File.Exists(ManifestPath))
            {
                try
                {
                    string json = File.ReadAllText(ManifestPath);
                    cacheManifest = JsonUtility.FromJson<SubjectCacheManifest>(json);
                    Log($"Loaded cache manifest with {cacheManifest.subjects.Count} subjects");
                }
                catch (Exception ex)
                {
                    LogError($"Failed to load cache manifest: {ex.Message}");
                    cacheManifest = new SubjectCacheManifest();
                }
            }
            else
            {
                cacheManifest = new SubjectCacheManifest();
                Log("Created new cache manifest");
            }
        }

        /// <summary>
        /// Preload cached sprites vào SubjectDatabase NGAY KHI START (không cần login)
        /// Điều này giúp sprites sẵn sàng ngay khi user mở app lại
        /// </summary>
        private IEnumerator PreloadCachedSpritesCoroutine()
        {
            if (subjectDatabase == null)
            {
                LogError("SubjectDatabase not assigned, cannot preload sprites");
                yield break;
            }

            Log("[PRELOAD] Starting to preload cached sprites...");
            int preloadedCount = 0;
            int lastLoggedPercent = 0;

            foreach (var cachedData in cacheManifest.subjects)
            {
                if (!cachedData.isFullyCached || cachedData.cachedImagePaths.Count == 0)
                    continue;

                // Find matching subject in database by name
                var localSubject = subjectDatabase.subjects.Find(s => 
                    s.name == cachedData.subjectName || 
                    (!string.IsNullOrEmpty(s.path) && s.path.Contains(cachedData.subjectName)));

                if (localSubject == null)
                {
                    Log($"[PRELOAD] No matching subject for cache: {cachedData.subjectName}");
                    continue;
                }

                // Skip if already loaded
                if (localSubject.HasLoadedSprites())
                {
                    Log($"[PRELOAD] Sprites already loaded for: {localSubject.name}");
                    preloadedCount++;
                    continue;
                }

                // Validate cache files exist
                bool allFilesExist = true;
                foreach (var path in cachedData.cachedImagePaths)
                {
                    if (!File.Exists(path))
                    {
                        allFilesExist = false;
                        break;
                    }
                }

                if (!allFilesExist)
                {
                    Log($"[PRELOAD] Cache files missing for: {cachedData.subjectName}");
                    continue;
                }

                Log($"[PRELOAD] Loading sprites for: {localSubject.name} ({cachedData.cachedImagePaths.Count} images)");

                // Load sprites from cache
                Sprite[] sprites = new Sprite[cachedData.cachedImagePaths.Count];
                int loadedCount = 0;

                for (int i = 0; i < cachedData.cachedImagePaths.Count; i++)
                {
                    string path = cachedData.cachedImagePaths[i];

                    // Detect format by extension and use appropriate loader
                    bool isPNG = path.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
                    
                    if (isPNG)
                    {
                        yield return StartCoroutine(LoadPNGFromFileCoroutine(path, i, (sprite, index) =>
                        {
                            if (sprite != null && index < sprites.Length)
                            {
                                sprites[index] = sprite;
                                loadedCount++;
                            }
                        }));
                    }
                    else
                    {
                        yield return StartCoroutine(LoadWebPFromFileCoroutine(path, i, (sprite, index) =>
                        {
                            if (sprite != null && index < sprites.Length)
                            {
                                sprites[index] = sprite;
                                loadedCount++;
                            }
                        }));
                    }

                    // Log progress every 10%
                    int percent = (i + 1) * 100 / cachedData.cachedImagePaths.Count;
                    if (percent >= lastLoggedPercent + 10)
                    {
                        lastLoggedPercent = (percent / 10) * 10;
                        Log($"[PRELOAD] {localSubject.name}: {lastLoggedPercent}%");
                    }
                }

                if (loadedCount > 0)
                {
                    localSubject.SetBookPages(sprites);
                    localSubject.isCached = true;
                    localSubject.localImagePaths = new List<string>(cachedData.cachedImagePaths);
                    preloadedCount++;
                    Log($"[PRELOAD COMPLETE] {localSubject.name}: {loadedCount} sprites loaded");
                }

                lastLoggedPercent = 0; // Reset for next subject
                yield return null; // Give UI a frame to breathe
            }

            Log($"[PRELOAD COMPLETE] Preloaded {preloadedCount} subjects from cache");

            // Only mark as ready here if autoFetchOnStart is disabled
            // If autoFetchOnStart is enabled, let FetchSubjects/AutoCacheAllSubjects set IsReady
            if (!autoFetchOnStart && preloadedCount > 0 && !isReady)
            {
                // Check if we have any subjects that need API fetch
                bool allCached = true;
                foreach (var subject in subjectDatabase.subjects)
                {
                    if (!string.IsNullOrEmpty(subject.path) && !subject.isCached)
                    {
                        allCached = false;
                        break;
                    }
                }

                if (allCached)
                {
                    Log("[PRELOAD] All subjects cached and autoFetch disabled, marking as ready");
                    isReady = true;
                    OnReady?.Invoke();
                }
            }
            else if (autoFetchOnStart)
            {
                Log("[PRELOAD] autoFetchOnStart enabled, waiting for fetch to complete before marking ready");
            }

            // Mark preload as complete
            preloadComplete = true;
            isPreloading = false;
            Log("[PRELOAD] Preload phase finished");
            
            // Try to mark ready if fetch is also done
            if (!autoFetchOnStart || hasFetched)
            {
                TryMarkAsReady();
            }

            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(subjectDatabase);
            #endif
        }

        /// <summary>
        /// Save cache manifest vào disk
        /// </summary>
        private void SaveCacheManifest()
        {
            try
            {
                string json = JsonUtility.ToJson(cacheManifest, true);
                File.WriteAllText(ManifestPath, json);
                Log("Cache manifest saved");
            }
            catch (Exception ex)
            {
                LogError($"Failed to save cache manifest: {ex.Message}");
            }
        }

        /// <summary>
        /// Đảm bảo thư mục cache tồn tại
        /// </summary>
        private void EnsureCacheDirectoryExists()
        {
            if (!Directory.Exists(CachePath))
            {
                Directory.CreateDirectory(CachePath);
                Log($"Created cache directory: {CachePath}");
            }
        }

        /// <summary>
        /// Lấy đường dẫn thư mục cache cho subject
        /// </summary>
        private string GetSubjectCachePath(string subjectName)
        {
            string safeName = SanitizeFileName(subjectName);
            return Path.Combine(CachePath, safeName);
        }

        /// <summary>
        /// Làm sạch tên file
        /// </summary>
        private string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }

        /// <summary>
        /// Kiểm tra subject đã được cache chưa
        /// </summary>
        public bool IsSubjectCached(string subjectName)
        {
            var subject = remoteSubjects.Find(s => s.name == subjectName);
            if (subject == null) return false;
            
            // Double check - verify cache status
            if (!subject.isCached) return false;
            
            // Validate files actually exist
            return ValidateCacheFiles(subject);
        }

        /// <summary>
        /// Kiểm tra subject theo path đã được cache chưa
        /// </summary>
        public bool IsSubjectCachedByPath(string path)
        {
            var subject = remoteSubjects.Find(s => s.path == path);
            if (subject == null) return false;
            
            if (!subject.isCached) return false;
            
            return ValidateCacheFiles(subject);
        }

        /// <summary>
        /// Validate that cached files actually exist on disk
        /// </summary>
        private bool ValidateCacheFiles(RemoteSubjectInfo subject)
        {
            if (subject == null || subject.localImagePaths == null || subject.localImagePaths.Count == 0)
                return false;

            // Check that all cached files exist
            foreach (var path in subject.localImagePaths)
            {
                if (!File.Exists(path))
                {
                    Log($"Cache file missing: {path}");
                    // Mark as not cached since files are missing
                    subject.isCached = false;
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Check và update cache status cho tất cả subjects
        /// </summary>
        [ProButton]
        public void ValidateAllCacheStatus()
        {
            Log("Validating cache status for all subjects...");
            int cachedCount = 0;
            int invalidCount = 0;

            foreach (var subject in remoteSubjects)
            {
                if (subject.isCached)
                {
                    if (ValidateCacheFiles(subject))
                    {
                        cachedCount++;
                    }
                    else
                    {
                        invalidCount++;
                        Log($"Cache invalid for: {subject.name}");
                    }
                }
            }

            Log($"Cache validation complete: {cachedCount} valid, {invalidCount} invalid");
        }

        /// <summary>
        /// Kiểm tra và cập nhật cache nếu cần
        /// </summary>
        public bool NeedsCacheUpdate(RemoteSubjectInfo subject)
        {
            if (subject == null) return false;

            var cachedData = cacheManifest.GetSubjectCache(subject.name);
            if (cachedData == null) return true;

            // Check version hash
            string currentHash = $"{subject.name}_{subject.pages}_{subject.imageUrls.Count}";
            return cachedData.versionHash != currentHash || !cachedData.isFullyCached;
        }

        #endregion

        #region Download & Cache Images

        /// <summary>
        /// Download và cache tất cả images của subject
        /// </summary>
        [ProButton]
        public void CacheSubject(int subjectIndex)
        {
            if (subjectIndex < 0 || subjectIndex >= remoteSubjects.Count)
            {
                LogError($"Invalid subject index: {subjectIndex}");
                return;
            }

            StartCoroutine(CacheSubjectCoroutine(remoteSubjects[subjectIndex]));
        }

        public void CacheSubject(RemoteSubjectInfo subject)
        {
            StartCoroutine(CacheSubjectCoroutine(subject));
        }

        private IEnumerator CacheSubjectCoroutine(RemoteSubjectInfo subject)
        {
            if (subject == null || subject.imageUrls == null || subject.imageUrls.Count == 0)
            {
                LogError("Invalid subject or no images to cache");
                yield break;
            }

            Log($"[CACHE START] {subject.name} - {subject.imageUrls.Count} images");

            string subjectCachePath = GetSubjectCachePath(subject.name);
            if (!Directory.Exists(subjectCachePath))
            {
                Directory.CreateDirectory(subjectCachePath);
            }

            LocalSubjectCacheData cacheData = new LocalSubjectCacheData(
                subject.name, 
                $"{subject.name}_{subject.pages}_{subject.imageUrls.Count}"
            );

            int successCount = 0;
            int lastLoggedPercent = 0; // Track last logged percentage

            for (int i = 0; i < subject.imageUrls.Count; i++)
            {
                string url = subject.imageUrls[i];
                string fileExt = cacheAsPNG ? "png" : "webp";
                string fileName = $"page_{i:D4}.{fileExt}";
                string localPath = Path.Combine(subjectCachePath, fileName);

                // Skip if already exists
                if (File.Exists(localPath))
                {
                    cacheData.cachedImagePaths.Add(localPath);
                    successCount++;
                    
                    float progress = (float)(i + 1) / subject.imageUrls.Count;
                    OnSubjectCacheProgress?.Invoke(subject, progress);
                    
                    // Log every 10%
                    int currentPercent = Mathf.FloorToInt(progress * 100);
                    if (currentPercent >= lastLoggedPercent + 10)
                    {
                        lastLoggedPercent = (currentPercent / 10) * 10;
                        Log($"[CACHE] {subject.name}: {lastLoggedPercent}% ({i + 1}/{subject.imageUrls.Count})");
                    }
                    
                    continue;
                }

                // Download and convert
                yield return StartCoroutine(DownloadAndCacheImageCoroutine(url, localPath, cacheAsPNG, (success) =>
                {
                    if (success)
                    {
                        cacheData.cachedImagePaths.Add(localPath);
                        successCount++;
                    }
                }));

                float currentProgress = (float)(i + 1) / subject.imageUrls.Count;
                OnSubjectCacheProgress?.Invoke(subject, currentProgress);

                // Log every 10%
                int percent = Mathf.FloorToInt(currentProgress * 100);
                if (percent >= lastLoggedPercent + 10)
                {
                    lastLoggedPercent = (percent / 10) * 10;
                    Log($"[CACHE] {subject.name}: {lastLoggedPercent}% ({i + 1}/{subject.imageUrls.Count})");
                }

                // Small delay between downloads
                yield return new WaitForSeconds(0.1f);
            }

            // Update cache status
            cacheData.isFullyCached = successCount == subject.imageUrls.Count;
            cacheManifest.AddOrUpdateSubject(cacheData);
            SaveCacheManifest();

            // Update subject
            subject.isCached = cacheData.isFullyCached;
            subject.localImagePaths = new List<string>(cacheData.cachedImagePaths);

            // Log completion with percentage
            int successRate = subject.imageUrls.Count > 0 ? (successCount * 100 / subject.imageUrls.Count) : 0;
            Log($"[CACHE COMPLETE] {subject.name}: 100% - {successCount}/{subject.imageUrls.Count} images ({successRate}% success)");

            // Load sprites and update SubjectDatabase
            if (subject.isCached)
            {
                yield return LoadSpritesAndUpdateDatabase(subject);
            }

            OnSubjectCacheComplete?.Invoke(subject);
        }

        /// <summary>
        /// Load sprites từ cache và update vào SubjectDatabase
        /// </summary>
        private IEnumerator LoadSpritesAndUpdateDatabase(RemoteSubjectInfo subject)
        {
            if (subjectDatabase == null)
            {
                LogError("SubjectDatabase not assigned, cannot update sprites");
                yield break;
            }

            // Find matching local subject
            var localSubject = subjectDatabase.subjects.Find(s => s.MatchesPath(subject.path));
            if (localSubject == null)
            {
                LogError($"No matching local subject for path: {subject.path}");
                yield break;
            }

            // Skip if sprites already loaded (from preload)
            if (localSubject.HasLoadedSprites())
            {
                Log($"Sprites already loaded for {subject.name}, skipping conversion");
                yield break;
            }

            Log($"Loading sprites for subject: {subject.name}");

            Sprite[] sprites = null;
            yield return LoadCachedSpritesCoroutine(subject, (result) => sprites = result);

            if (sprites != null && sprites.Length > 0)
            {
                // Update local subject in ScriptableObject
                localSubject.SetBookPages(sprites);
                localSubject.isCached = true;
                localSubject.localImagePaths = new List<string>(subject.localImagePaths);

                Log($"Updated SubjectDatabase with {sprites.Length} sprites for: {localSubject.name}");

                #if UNITY_EDITOR
                // Mark ScriptableObject as dirty in Editor
                UnityEditor.EditorUtility.SetDirty(subjectDatabase);
                #endif
            }
            else
            {
                LogError($"Failed to load sprites for subject: {subject.name}");
            }
        }

        /// <summary>
        /// Download WebP from URL and save to cache (as PNG or WebP based on settings)
        /// </summary>
        private IEnumerator DownloadAndCacheImageCoroutine(string url, string localPath, bool convertToPNG, Action<bool> callback)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        byte[] webpData = request.downloadHandler.data;

                        if (convertToPNG)
                        {
                            // Decode WebP → Texture2D → Encode PNG → Save
                            // Note: CreateTexture2DFromWebP with lMipmaps=false, lLinear=false
                            // We need to make texture readable for EncodeToPNG
                            WebP.Error error;
                            Texture2D texture = WebP.Texture2DExt.CreateTexture2DFromWebP(webpData, lMipmaps: false, lLinear: false, out error);

                            if (error == WebP.Error.Success && texture != null)
                            {
                                // Create a readable copy of the texture for encoding
                                RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
                                Graphics.Blit(texture, rt);
                                
                                RenderTexture previous = RenderTexture.active;
                                RenderTexture.active = rt;
                                
                                Texture2D readableTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                                readableTexture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                                readableTexture.Apply();
                                
                                RenderTexture.active = previous;
                                RenderTexture.ReleaseTemporary(rt);
                                
                                // Encode to PNG and save
                                byte[] pngData = readableTexture.EncodeToPNG();
                                File.WriteAllBytes(localPath, pngData);
                                
                                // Cleanup textures
                                UnityEngine.Object.Destroy(texture);
                                UnityEngine.Object.Destroy(readableTexture);
                                
                                callback?.Invoke(true);
                            }
                            else
                            {
                                LogError($"Failed to decode WebP for PNG conversion: {error}");
                                callback?.Invoke(false);
                            }
                        }
                        else
                        {
                            // Save WebP directly
                            File.WriteAllBytes(localPath, webpData);
                            callback?.Invoke(true);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"Failed to save image: {ex.Message}");
                        callback?.Invoke(false);
                    }
                }
                else
                {
                    LogError($"Failed to download image: {request.error}");
                    callback?.Invoke(false);
                }
            }
        }

        #endregion

        #region Load Cached Images

        /// <summary>
        /// Load cached images thành Sprite array
        /// </summary>
        public IEnumerator LoadCachedSpritesCoroutine(RemoteSubjectInfo subject, Action<Sprite[]> callback)
        {
            if (subject == null || !subject.isCached || subject.localImagePaths.Count == 0)
            {
                LogError("Subject not cached or no local paths");
                callback?.Invoke(null);
                yield break;
            }

            Sprite[] sprites = new Sprite[subject.localImagePaths.Count];

            for (int i = 0; i < subject.localImagePaths.Count; i++)
            {
                string path = subject.localImagePaths[i];
                
                if (!File.Exists(path))
                {
                    LogError($"Cached file not found: {path}");
                    continue;
                }

                // Detect format by extension and use appropriate loader
                bool isPNG = path.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
                
                if (isPNG)
                {
                    yield return StartCoroutine(LoadPNGFromFileCoroutine(path, i, (sprite, index) =>
                    {
                        if (sprite != null && index < sprites.Length)
                        {
                            sprites[index] = sprite;
                        }
                    }));
                }
                else
                {
                    yield return StartCoroutine(LoadWebPFromFileCoroutine(path, i, (sprite, index) =>
                    {
                        if (sprite != null && index < sprites.Length)
                        {
                            sprites[index] = sprite;
                        }
                    }));
                }
            }

            Log($"Loaded {sprites.Length} sprites from cache for {subject.name}");
            callback?.Invoke(sprites);
        }

        /// <summary>
        /// Load PNG from file - FAST (native Unity, no decode needed)
        /// </summary>
        private IEnumerator LoadPNGFromFileCoroutine(string path, int index, Action<Sprite, int> callback)
        {
            byte[] pngData = File.ReadAllBytes(path);

            // Texture2D.LoadImage is much faster than WebP decode!
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            
            if (texture.LoadImage(pngData))
            {
                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f
                );
                sprite.name = $"CachedPage_{index}";
                callback?.Invoke(sprite, index);
            }
            else
            {
                LogError($"Failed to load PNG from cache: {path}");
                UnityEngine.Object.Destroy(texture);
                callback?.Invoke(null, index);
            }

            yield return null;
        }

        /// <summary>
        /// Load WebP from file - SLOW (requires WebP decode)
        /// </summary>
        private IEnumerator LoadWebPFromFileCoroutine(string path, int index, Action<Sprite, int> callback)
        {
            byte[] webpData = File.ReadAllBytes(path);

            WebP.Error error;
            Texture2D texture = WebP.Texture2DExt.CreateTexture2DFromWebP(webpData, false, false, out error);

            if (error == WebP.Error.Success && texture != null)
            {
                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f
                );
                sprite.name = $"CachedPage_{index}";
                callback?.Invoke(sprite, index);
            }
            else
            {
                LogError($"Failed to convert WebP from cache: {error}");
                callback?.Invoke(null, index);
            }

            yield return null;
        }

        #endregion

        #region Utility

        /// <summary>
        /// Clear all cache
        /// </summary>
        [ProButton]
        public void ClearAllCache()
        {
            try
            {
                if (Directory.Exists(CachePath))
                {
                    Directory.Delete(CachePath, true);
                }

                cacheManifest = new SubjectCacheManifest();
                
                foreach (var subject in remoteSubjects)
                {
                    subject.isCached = false;
                    subject.localImagePaths.Clear();
                }

                if(subjectDatabase != null)
                {
                    subjectDatabase.ClearAllRemoteData();
                }

                EnsureCacheDirectoryExists();
                Log("All cache cleared");
            }
            catch (Exception ex)
            {
                LogError($"Failed to clear cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Clear cache cho subject cụ thể
        /// </summary>
        public void ClearSubjectCache(string subjectName)
        {
            try
            {
                string subjectPath = GetSubjectCachePath(subjectName);
                if (Directory.Exists(subjectPath))
                {
                    Directory.Delete(subjectPath, true);
                }

                var cachedData = cacheManifest.GetSubjectCache(subjectName);
                if (cachedData != null)
                {
                    cacheManifest.subjects.Remove(cachedData);
                    SaveCacheManifest();
                }

                var subject = remoteSubjects.Find(s => s.name == subjectName);
                if (subject != null)
                {
                    subject.isCached = false;
                    subject.localImagePaths.Clear();
                }

                Log($"Cache cleared for subject: {subjectName}");
            }
            catch (Exception ex)
            {
                LogError($"Failed to clear subject cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Get cache info for debugging
        /// </summary>
        [ProButton]
        public void DebugCacheInfo()
        {
            Debug.Log($"=== PDF Subject Cache Info ===");
            Debug.Log($"Cache Path: {CachePath}");
            Debug.Log($"Manifest Subjects: {(cacheManifest != null ? cacheManifest.subjects.Count : 0)}");

            if (cacheManifest != null)
            {
                foreach (var cached in cacheManifest.subjects)
                {
                    Debug.Log($"  - {cached.subjectName}: {cached.cachedImagePaths.Count} images, " +
                             $"Fully Cached: {cached.isFullyCached}, Hash: {cached.versionHash}");
                }
            }

            Debug.Log($"Remote Subjects: {remoteSubjects.Count}");
            foreach (var subject in remoteSubjects)
            {
                Debug.Log($"  - {subject.name}: Cached={subject.isCached}, " +
                         $"URLs={subject.imageUrls.Count}, LocalPaths={subject.localImagePaths.Count}");
            }
        }

        /// <summary>
        /// Open cache folder in File Explorer (Windows) / Finder (Mac)
        /// </summary>
        [ProButton]
        public void OpenCacheFolder()
        {
            string path = CachePath;
            
            if (!Directory.Exists(path))
            {
                Debug.LogWarning($"[PDFSubjectService] Cache folder does not exist yet: {path}");
                EnsureCacheDirectoryExists();
            }

            Debug.Log($"[PDFSubjectService] Opening cache folder: {path}");

            #if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            System.Diagnostics.Process.Start("explorer.exe", path.Replace("/", "\\"));
            #elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            System.Diagnostics.Process.Start("open", path);
            #else
            Debug.Log($"[PDFSubjectService] Cache Path: {path}");
            #endif
        }

        /// <summary>
        /// Copy cache path to clipboard
        /// </summary>
        [ProButton]
        public void CopyCachePathToClipboard()
        {
            string path = CachePath;
            GUIUtility.systemCopyBuffer = path;
            Debug.Log($"[PDFSubjectService] Cache path copied to clipboard: {path}");
        }

        /// <summary>
        /// Get cache size in MB
        /// </summary>
        [ProButton]
        public void DebugCacheSize()
        {
            if (!Directory.Exists(CachePath))
            {
                Debug.Log("[PDFSubjectService] Cache folder does not exist");
                return;
            }

            long totalSize = 0;
            int fileCount = 0;

            try
            {
                var files = Directory.GetFiles(CachePath, "*.*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var info = new FileInfo(file);
                    totalSize += info.Length;
                    fileCount++;
                }

                double sizeMB = totalSize / (1024.0 * 1024.0);
                Debug.Log($"[PDFSubjectService] Cache Size: {sizeMB:F2} MB ({fileCount} files)");

                // Per-subject breakdown
                var subjectFolders = Directory.GetDirectories(CachePath);
                foreach (var folder in subjectFolders)
                {
                    long subjectSize = 0;
                    var subjectFiles = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);
                    foreach (var file in subjectFiles)
                    {
                        subjectSize += new FileInfo(file).Length;
                    }
                    double subjectSizeMB = subjectSize / (1024.0 * 1024.0);
                    string folderName = Path.GetFileName(folder);
                    Debug.Log($"  - {folderName}: {subjectSizeMB:F2} MB ({subjectFiles.Length} files)");
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to calculate cache size: {ex.Message}");
            }
        }

        private void Log(string message)
        {
            if (enableDebugLog)
                Debug.Log($"[PDFSubjectService] {message}");
        }

        private void LogError(string message)
        {
            Debug.LogError($"[PDFSubjectService] {message}");
        }

        #endregion
    }
}
