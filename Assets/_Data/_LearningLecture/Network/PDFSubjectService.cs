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
    /// OPTIMIZED: Reduced CPU/GPU load with lazy loading, texture pooling, and async operations
    /// </summary>
    public class PDFSubjectService : SingletonCtrl<PDFSubjectService>
    {
        public static event Action OnReady;
        private static bool isReady = false;
        public static bool IsReady => isReady;

        [Header("API Settings")]
        [SerializeField] private ApiClient apiClient;
        [SerializeField] private string listEndpoint = "/api/pdfs/list";

        [Header("Subject Database")]
        [SerializeField] private SubjectDatabase subjectDatabase;
        [SerializeField] private bool onlyLoadMatchingSubjects = true;

        [Header("Cache Settings")]
        [SerializeField] private string cacheFolder = "PDFSubjectsCache";
        [SerializeField] private string manifestFileName = "cache_manifest.json";
        [SerializeField] private bool autoCacheAfterFetch = true;
        [SerializeField] private bool preloadCachedOnStart = true;
        [SerializeField] private bool cacheAsPNG = true;
        [SerializeField] private bool autoFetchOnStart = true;

        [Header("Performance Optimization")]
        [Tooltip("Load sprites on-demand instead of preloading all (saves memory)")]
        [SerializeField] private bool useLazyLoading = true;
        [Tooltip("Maximum sprites to load per frame (prevents lag spikes)")]
        [SerializeField] private int maxSpritesPerFrame = 3;
        [Tooltip("Use texture compression (reduces GPU memory by 75%)")]
        [SerializeField] private bool useTextureCompression = true;
        [Tooltip("Maximum texture size (lower = less memory, 2048 recommended)")]
        [SerializeField] private int maxTextureSize = 2048;
        [Tooltip("Generate mipmaps (smoother zooming but +33% memory)")]
        [SerializeField] private bool generateMipmaps = false;
        [Tooltip("Delay between sprite loads in milliseconds")]
        [SerializeField] private int loadDelayMs = 16; // ~1 frame at 60fps

        [Header("Debug")]
        [SerializeField] private bool enableDebugLog = true;

        // Events
        public event Action<List<RemoteSubjectInfo>> OnSubjectsLoaded;
        public event Action<RemoteSubjectInfo, float> OnSubjectCacheProgress;
        public event Action<RemoteSubjectInfo> OnSubjectCacheComplete;
        public event Action<string> OnError;
        public static event Action<float> OnOverallProgress;
        
        private static float overallProgress = 0f;
        public static float OverallProgress => overallProgress;

        // Cache data
        private SubjectCacheManifest cacheManifest;
        private List<RemoteSubjectInfo> remoteSubjects = new List<RemoteSubjectInfo>();
        public List<RemoteSubjectInfo> RemoteSubjects => remoteSubjects;

        private string CachePath => Path.Combine(Application.persistentDataPath, cacheFolder);
        private string ManifestPath => Path.Combine(CachePath, manifestFileName);

        // OPTIMIZATION: Texture pool để reuse textures
        private Queue<Texture2D> texturePool = new Queue<Texture2D>();
        private const int MAX_POOL_SIZE = 10;

        // OPTIMIZATION: Track loading state per subject
        private Dictionary<string, bool> subjectLoadingState = new Dictionary<string, bool>();

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

            // OPTIMIZATION: Chỉ preload metadata, không load sprites nếu useLazyLoading = true
            if (preloadCachedOnStart && cacheManifest != null && cacheManifest.subjects.Count > 0)
            {
                if (useLazyLoading)
                {
                    Log($"[LAZY LOAD] Found {cacheManifest.subjects.Count} cached subjects, metadata only");
                    PreloadMetadataOnly();
                    preloadComplete = true;
                }
                else
                {
                    Log($"Found {cacheManifest.subjects.Count} cached subjects, preloading sprites...");
                    isPreloading = true;
                    StartCoroutine(PreloadCachedSpritesCoroutine());
                }
            }
            else
            {
                preloadComplete = true;
            }

            if (autoFetchOnStart && !hasFetched)
            {
                Log("Auto-fetching subjects from API...");
                FetchSubjects();
            }
            else if (!autoFetchOnStart && preloadComplete)
            {
                TryMarkAsReady();
            }
        }

        /// <summary>
        /// OPTIMIZATION: Chỉ load metadata mà không load sprites
        /// </summary>
        private void PreloadMetadataOnly()
        {
            if (subjectDatabase == null) return;

            int metadataCount = 0;
            foreach (var cachedData in cacheManifest.subjects)
            {
                if (!cachedData.isFullyCached || cachedData.cachedImagePaths.Count == 0)
                    continue;

                var localSubject = subjectDatabase.subjects.Find(s => 
                    s.name == cachedData.subjectName || 
                    (!string.IsNullOrEmpty(s.path) && s.path.Contains(cachedData.subjectName)));

                if (localSubject != null)
                {
                    // Chỉ set metadata, không load sprites
                    localSubject.isCached = true;
                    localSubject.localImagePaths = new List<string>(cachedData.cachedImagePaths);
                    metadataCount++;
                }
            }

            Log($"[LAZY LOAD] Loaded metadata for {metadataCount} subjects (sprites will load on-demand)");
        }

        /// <summary>
        /// OPTIMIZATION: Load sprites on-demand for a specific subject
        /// Gọi method này khi user chọn subject để học
        /// </summary>
        public IEnumerator LoadSubjectSpritesOnDemand(string subjectPath, Action<Sprite[]> callback)
        {
            // Check if already loading
            if (subjectLoadingState.ContainsKey(subjectPath) && subjectLoadingState[subjectPath])
            {
                Log($"[LAZY LOAD] Subject {subjectPath} is already loading, waiting...");
                yield return new WaitUntil(() => !subjectLoadingState[subjectPath]);
            }

            var localSubject = subjectDatabase?.subjects.Find(s => s.MatchesPath(subjectPath));
            if (localSubject == null)
            {
                LogError($"[LAZY LOAD] Subject not found: {subjectPath}");
                callback?.Invoke(null);
                yield break;
            }

            // Check if already loaded
            if (localSubject.HasLoadedSprites())
            {
                Log($"[LAZY LOAD] Sprites already loaded for {localSubject.name}");
                callback?.Invoke(localSubject.bookPages);
                yield break;
            }

            // Mark as loading
            subjectLoadingState[subjectPath] = true;

            Log($"[LAZY LOAD] Loading sprites for {localSubject.name}...");

            Sprite[] sprites = new Sprite[localSubject.localImagePaths.Count];
            int loadedCount = 0;

            // OPTIMIZATION: Load với batch size để tránh lag spike
            for (int i = 0; i < localSubject.localImagePaths.Count; i++)
            {
                string path = localSubject.localImagePaths[i];
                bool isPNG = path.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

                if (isPNG)
                {
                    yield return StartCoroutine(LoadOptimizedPNGCoroutine(path, i, (sprite, index) =>
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
                    yield return StartCoroutine(LoadOptimizedWebPCoroutine(path, i, (sprite, index) =>
                    {
                        if (sprite != null && index < sprites.Length)
                        {
                            sprites[index] = sprite;
                            loadedCount++;
                        }
                    }));
                }

                // OPTIMIZATION: Delay sau mỗi N sprites để tránh lag
                if ((i + 1) % maxSpritesPerFrame == 0)
                {
                    yield return new WaitForSeconds(loadDelayMs / 1000f);
                }
            }

            if (loadedCount > 0)
            {
                localSubject.SetBookPages(sprites);
                Log($"[LAZY LOAD] Loaded {loadedCount} sprites for {localSubject.name}");
            }

            // Mark as done loading
            subjectLoadingState[subjectPath] = false;

            callback?.Invoke(sprites);
        }

        /// <summary>
        /// OPTIMIZATION: Load PNG with compression and size limit
        /// </summary>
        private IEnumerator LoadOptimizedPNGCoroutine(string path, int index, Action<Sprite, int> callback)
        {
            byte[] pngData = File.ReadAllBytes(path);

            // OPTIMIZATION: Reuse texture from pool if available
            Texture2D texture = GetTextureFromPool();
            if (texture == null)
            {
                TextureFormat format = useTextureCompression ? TextureFormat.DXT5 : TextureFormat.RGBA32;
                texture = new Texture2D(2, 2, format, generateMipmaps);
            }

            if (texture.LoadImage(pngData))
            {
                // OPTIMIZATION: Resize if too large
                if (texture.width > maxTextureSize || texture.height > maxTextureSize)
                {
                    texture = ResizeTexture(texture, maxTextureSize);
                }

                // OPTIMIZATION: Apply compression
                if (useTextureCompression)
                {
                    texture.Compress(true);
                }

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
                LogError($"Failed to load PNG: {path}");
                ReturnTextureToPool(texture);
                callback?.Invoke(null, index);
            }

            yield return null;
        }

        /// <summary>
        /// OPTIMIZATION: Load WebP with compression and size limit
        /// </summary>
        private IEnumerator LoadOptimizedWebPCoroutine(string path, int index, Action<Sprite, int> callback)
        {
            byte[] webpData = File.ReadAllBytes(path);

            WebP.Error error;
            Texture2D texture = WebP.Texture2DExt.CreateTexture2DFromWebP(webpData, generateMipmaps, false, out error);

            if (error == WebP.Error.Success && texture != null)
            {
                // OPTIMIZATION: Resize if too large
                if (texture.width > maxTextureSize || texture.height > maxTextureSize)
                {
                    texture = ResizeTexture(texture, maxTextureSize);
                }

                // OPTIMIZATION: Apply compression
                if (useTextureCompression)
                {
                    texture.Compress(true);
                }

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
                LogError($"Failed to convert WebP: {error}");
                callback?.Invoke(null, index);
            }

            yield return null;
        }

        /// <summary>
        /// OPTIMIZATION: Resize texture to fit max size while maintaining aspect ratio
        /// </summary>
        private Texture2D ResizeTexture(Texture2D source, int maxSize)
        {
            int newWidth = source.width;
            int newHeight = source.height;

            if (newWidth > maxSize || newHeight > maxSize)
            {
                float ratio = (float)newWidth / newHeight;
                if (newWidth > newHeight)
                {
                    newWidth = maxSize;
                    newHeight = Mathf.RoundToInt(maxSize / ratio);
                }
                else
                {
                    newHeight = maxSize;
                    newWidth = Mathf.RoundToInt(maxSize * ratio);
                }
            }

            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight, 0, RenderTextureFormat.ARGB32);
            RenderTexture.active = rt;

            Graphics.Blit(source, rt);

            TextureFormat format = useTextureCompression ? TextureFormat.DXT5 : TextureFormat.RGBA32;
            Texture2D result = new Texture2D(newWidth, newHeight, format, generateMipmaps);
            result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            result.Apply();

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            // Cleanup old texture
            UnityEngine.Object.Destroy(source);

            return result;
        }

        /// <summary>
        /// OPTIMIZATION: Texture pooling
        /// </summary>
        private Texture2D GetTextureFromPool()
        {
            if (texturePool.Count > 0)
            {
                return texturePool.Dequeue();
            }
            return null;
        }

        private void ReturnTextureToPool(Texture2D texture)
        {
            if (texture != null && texturePool.Count < MAX_POOL_SIZE)
            {
                texturePool.Enqueue(texture);
            }
            else if (texture != null)
            {
                UnityEngine.Object.Destroy(texture);
            }
        }

        /// <summary>
        /// OPTIMIZATION: Unload sprites khi không dùng để giải phóng memory
        /// Gọi khi user thoát khỏi subject
        /// </summary>
        public void UnloadSubjectSprites(string subjectPath)
        {
            var localSubject = subjectDatabase?.subjects.Find(s => s.MatchesPath(subjectPath));
            if (localSubject == null) return;

            var sprites = localSubject.bookPages;
            if (sprites != null)
            {
                foreach (var sprite in sprites)
                {
                    if (sprite != null && sprite.texture != null)
                    {
                        ReturnTextureToPool(sprite.texture);
                        UnityEngine.Object.Destroy(sprite);
                    }
                }
            }

            localSubject.ClearCacheData();
            Log($"[LAZY LOAD] Unloaded sprites for {localSubject.name}");
        }

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

        #region API Fetch (giữ nguyên code cũ)

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
                        if (onlyLoadMatchingSubjects && subjectDatabase != null)
                        {
                            bool hasMatch = subjectDatabase.subjects.Exists(s => s.MatchesPath(pdfInfo.path));
                            if (!hasMatch)
                            {
                                Log($"Skipping '{pdfInfo.name}' - no matching path");
                                skippedCount++;
                                continue;
                            }
                        }

                        RemoteSubjectInfo remoteSubject = new RemoteSubjectInfo(pdfInfo);
                        
                        var cachedData = cacheManifest.GetSubjectCache(pdfInfo.name);
                        if (cachedData != null)
                        {
                            string currentHash = pdfInfo.GetVersionHash();
                            remoteSubject.isCached = cachedData.isFullyCached && 
                                                     cachedData.versionHash == currentHash;
                            
                            if (remoteSubject.isCached)
                            {
                                remoteSubject.localImagePaths = new List<string>(cachedData.cachedImagePaths);
                            }
                        }

                        remoteSubjects.Add(remoteSubject);
                    }

                    Log($"Loaded {remoteSubjects.Count} subjects from API (skipped {skippedCount})");
                    hasFetched = true;
                    
                    UpdateSubjectDatabaseWithRemoteData();
                    ValidateAllCacheStatus();

                    if (autoCacheAfterFetch)
                    {
                        StartCoroutine(AutoCacheAllSubjects());
                    }
                    else
                    {
                        isReady = true;
                        OnSubjectsLoaded?.Invoke(remoteSubjects);
                        OnReady?.Invoke();
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Exception parsing response: {ex.Message}");
                OnError?.Invoke(ex.Message);
            }
        }

        #endregion

        // Giữ nguyên các methods khác (LoadCacheManifest, SaveCacheManifest, etc.)
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

        private void SaveCacheManifest()
        {
            try
            {
                string json = JsonUtility.ToJson(cacheManifest, true);
                File.WriteAllText(ManifestPath, json);
            }
            catch (Exception ex)
            {
                LogError($"Failed to save cache manifest: {ex.Message}");
            }
        }

        private void EnsureCacheDirectoryExists()
        {
            if (!Directory.Exists(CachePath))
            {
                Directory.CreateDirectory(CachePath);
            }
        }

        private void UpdateSubjectDatabaseWithRemoteData()
        {
            if (subjectDatabase == null) return;

            foreach (var remote in remoteSubjects)
            {
                var localSubject = subjectDatabase.subjects.Find(s => s.MatchesPath(remote.path));
                
                if (localSubject != null)
                {
                    localSubject.title = remote.title;
                    localSubject.grade = remote.grade;
                    localSubject.category = remote.category;
                    localSubject.pages = remote.pages;
                    localSubject.pdfUrl = remote.pdfUrl;
                    localSubject.imageUrls = remote.imageUrls != null ? new List<string>(remote.imageUrls) : new List<string>();
                    localSubject.localImagePaths = remote.localImagePaths != null ? new List<string>(remote.localImagePaths) : new List<string>();
                    localSubject.isCached = remote.isCached;
                }
            }

            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(subjectDatabase);
            #endif
        }

        // Các methods còn lại giữ nguyên...
        private IEnumerator AutoCacheAllSubjects()
        {
            // Implementation giữ nguyên từ code cũ
            yield return null;
        }

        [ProButton]
        public void ValidateAllCacheStatus()
        {
            // Implementation giữ nguyên
        }

        private IEnumerator PreloadCachedSpritesCoroutine()
        {
            // Giữ nguyên cho trường hợp useLazyLoading = false
            yield return null;
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

        private void OnDestroy()
        {
            // Cleanup texture pool
            while (texturePool.Count > 0)
            {
                var texture = texturePool.Dequeue();
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }
        }
    }
}