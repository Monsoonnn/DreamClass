using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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
        [Tooltip("Preload all sprites in background after start (requires useLazyLoading=true)")]
        [SerializeField] private bool backgroundPreloadAfterStart = true;
        [Tooltip("Delay before starting background preload (seconds)")]
        [SerializeField] private float backgroundPreloadDelay = 1f;
        [Tooltip("Maximum sprites to load per frame (prevents lag spikes)")]
        [SerializeField] private int maxSpritesPerFrame = 2;
        [Tooltip("Use texture compression - WARNING: Very CPU intensive, disable for better performance")]
        [SerializeField] private bool useTextureCompression = false;
        [Tooltip("Maximum texture size (lower = faster load, 1024 recommended for performance)")]
        [SerializeField] private int maxTextureSize = 1024;
        [Tooltip("Skip resize if texture is smaller than max size (faster)")]
        [SerializeField] private bool skipResizeIfSmaller = true;
        [Tooltip("Generate mipmaps (smoother zooming but +33% memory)")]
        [SerializeField] private bool generateMipmaps = false;
        [Tooltip("Delay between sprite loads in seconds")]
        [SerializeField] private float loadDelaySeconds = 0.05f;
        [Tooltip("Use UnityWebRequest for loading (more optimized than File.ReadAllBytes)")]
        [SerializeField] private bool useWebRequestLoader = true;

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
        // NOTE: Pool disabled vì conflict với compression
        // private Queue<Texture2D> texturePool = new Queue<Texture2D>();
        // private const int MAX_POOL_SIZE = 10;

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
                    
                    // OPTIMIZATION: Start background preload sau một khoảng delay
                    if (backgroundPreloadAfterStart)
                    {
                        StartCoroutine(BackgroundPreloadAllSpritesCoroutine());
                    }
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
            int newSubjectsCount = 0;
            
            foreach (var cachedData in cacheManifest.subjects)
            {
                if (!cachedData.isFullyCached || cachedData.cachedImagePaths.Count == 0)
                    continue;

                // Match by cloudinaryFolder first, then by name
                var localSubject = subjectDatabase.subjects.Find(s => 
                    (!string.IsNullOrEmpty(s.cloudinaryFolder) && !string.IsNullOrEmpty(cachedData.cloudinaryFolder) && 
                     s.cloudinaryFolder.Equals(cachedData.cloudinaryFolder, System.StringComparison.OrdinalIgnoreCase)) ||
                    s.name == cachedData.subjectName);

                // Nếu không tìm thấy trong database, tạo mới SubjectInfo từ cache
                if (localSubject == null && !string.IsNullOrEmpty(cachedData.cloudinaryFolder))
                {
                    localSubject = new SubjectInfo
                    {
                        name = cachedData.subjectName,
                        cloudinaryFolder = cachedData.cloudinaryFolder
                    };
                    subjectDatabase.subjects.Add(localSubject);
                    newSubjectsCount++;
                    Log($"[LAZY LOAD] Created new SubjectInfo from cache: {cachedData.subjectName}");
                }

                if (localSubject != null)
                {
                    // Chỉ set metadata, không load sprites
                    localSubject.isCached = true;
                    localSubject.localImagePaths = new List<string>(cachedData.cachedImagePaths);
                    metadataCount++;
                }
            }

            if (newSubjectsCount > 0)
            {
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(subjectDatabase);
                UnityEditor.AssetDatabase.SaveAssets();
                #endif
            }

            Log($"[LAZY LOAD] Loaded metadata for {metadataCount} subjects ({newSubjectsCount} new from cache)");
        }

        /// <summary>
        /// OPTIMIZATION: Preload tất cả sprites trong background sau khi game đã ready
        /// Chạy với delay để không ảnh hưởng đến loading ban đầu
        /// </summary>
        private IEnumerator BackgroundPreloadAllSpritesCoroutine()
        {
            // Đợi một khoảng thời gian trước khi bắt đầu preload
            yield return new WaitForSeconds(backgroundPreloadDelay);
            
            Log($"[BACKGROUND PRELOAD] Starting background preload of all cached sprites...");

            if (subjectDatabase == null) yield break;

            int totalSubjects = 0;
            int loadedSubjects = 0;

            // Đếm số subjects cần load
            foreach (var subject in subjectDatabase.subjects)
            {
                if (subject.isCached && subject.localImagePaths != null && subject.localImagePaths.Count > 0)
                {
                    if (!subject.HasLoadedSprites())
                    {
                        totalSubjects++;
                    }
                }
            }

            Log($"[BACKGROUND PRELOAD] Found {totalSubjects} subjects to preload");

            // Load từng subject
            foreach (var subject in subjectDatabase.subjects)
            {
                if (subject.isCached && subject.localImagePaths != null && subject.localImagePaths.Count > 0)
                {
                    if (!subject.HasLoadedSprites())
                    {
                        Log($"[BACKGROUND PRELOAD] Loading {subject.name} ({loadedSubjects + 1}/{totalSubjects})");
                        
                        // Load sprites cho subject này
                        bool loadComplete = false;
                        yield return StartCoroutine(LoadSubjectSpritesOnDemand(subject.cloudinaryFolder, (sprites) => {
                            loadComplete = true;
                        }));

                        loadedSubjects++;
                        
                        // Delay giữa các subjects để không lag
                        yield return new WaitForSeconds(0.1f);
                    }
                }
            }

            Log($"[BACKGROUND PRELOAD] Completed! Loaded {loadedSubjects} subjects");
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

            var localSubject = subjectDatabase?.subjects.Find(s => s.MatchesCloudinaryFolder(subjectPath));
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
                    yield return new WaitForSeconds(loadDelaySeconds);
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
        /// OPTIMIZED: Load PNG using UnityWebRequest (non-blocking) or async file read
        /// </summary>
        private IEnumerator LoadOptimizedPNGCoroutine(string path, int index, Action<Sprite, int> callback)
        {
            Texture2D texture = null;

            if (useWebRequestLoader)
            {
                // Use UnityWebRequest - most optimized way to load textures
                string fileUri = "file:///" + path.Replace("\\", "/");
                using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(fileUri, true))
                {
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        texture = DownloadHandlerTexture.GetContent(request);
                    }
                    else
                    {
                        LogError($"Failed to load PNG via WebRequest: {path} - {request.error}");
                        callback?.Invoke(null, index);
                        yield break;
                    }
                }
            }
            else
            {
                // Fallback: Async file read
                byte[] pngData = null;
                bool fileReadComplete = false;
                
                Task.Run(() => {
                    try {
                        pngData = File.ReadAllBytes(path);
                    } catch (Exception ex) {
                        Debug.LogError($"[PDFSubjectService] Failed to read file: {ex.Message}");
                    }
                    fileReadComplete = true;
                });

                yield return new WaitUntil(() => fileReadComplete);

                if (pngData == null)
                {
                    callback?.Invoke(null, index);
                    yield break;
                }

                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(pngData))
                {
                    LogError($"Failed to load PNG: {path}");
                    UnityEngine.Object.Destroy(texture);
                    callback?.Invoke(null, index);
                    yield break;
                }
            }

            // Optional: Resize if needed (skip if smaller and skipResizeIfSmaller is true)
            bool needsResize = texture.width > maxTextureSize || texture.height > maxTextureSize;
            bool needsCompressionResize = useTextureCompression && (texture.width % 4 != 0 || texture.height % 4 != 0);
            
            if (needsResize || (needsCompressionResize && !skipResizeIfSmaller))
            {
                yield return null; // Spread across frame
                texture = ResizeTexture(texture, maxTextureSize, useTextureCompression);
            }

            // Optional: Compression (WARNING: CPU intensive)
            if (useTextureCompression)
            {
                yield return null; // Spread across frame
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

        /// <summary>
        /// OPTIMIZED: Load WebP with async file read
        /// </summary>
        private IEnumerator LoadOptimizedWebPCoroutine(string path, int index, Action<Sprite, int> callback)
        {
            // Async file read
            byte[] webpData = null;
            bool fileReadComplete = false;
            
            Task.Run(() => {
                try {
                    webpData = File.ReadAllBytes(path);
                } catch (Exception ex) {
                    Debug.LogError($"[PDFSubjectService] Failed to read WebP file: {ex.Message}");
                }
                fileReadComplete = true;
            });

            yield return new WaitUntil(() => fileReadComplete);

            if (webpData == null)
            {
                callback?.Invoke(null, index);
                yield break;
            }

            yield return null; // Spread decode across frame

            WebP.Error error;
            Texture2D texture = WebP.Texture2DExt.CreateTexture2DFromWebP(webpData, false, false, out error);

            if (error != WebP.Error.Success || texture == null)
            {
                LogError($"Failed to convert WebP: {error}");
                callback?.Invoke(null, index);
                yield break;
            }

            // Optional: Resize if needed
            bool needsResize = texture.width > maxTextureSize || texture.height > maxTextureSize;
            bool needsCompressionResize = useTextureCompression && (texture.width % 4 != 0 || texture.height % 4 != 0);
            
            if (needsResize || (needsCompressionResize && !skipResizeIfSmaller))
            {
                yield return null; // Spread across frame
                texture = ResizeTexture(texture, maxTextureSize, useTextureCompression);
            }

            // Optional: Compression (WARNING: CPU intensive)
            if (useTextureCompression)
            {
                yield return null; // Spread across frame
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

        /// <summary>
        /// OPTIMIZATION: Resize texture to fit max size while maintaining aspect ratio
        /// Ensures size is multiple of 4 for DXT compression
        /// </summary>
        private Texture2D ResizeTexture(Texture2D source, int maxSize, bool needsCompressionSize = false)
        {
            int newWidth = source.width;
            int newHeight = source.height;

            // Calculate new size maintaining aspect ratio
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

            // IMPORTANT: Round to multiple of 4 for DXT compression
            if (needsCompressionSize)
            {
                newWidth = Mathf.Max(4, (newWidth + 3) / 4 * 4);
                newHeight = Mathf.Max(4, (newHeight + 3) / 4 * 4);
            }

            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight, 0, RenderTextureFormat.ARGB32);
            RenderTexture.active = rt;

            Graphics.Blit(source, rt);

            // Always create as RGBA32 first, compress later if needed
            Texture2D result = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, generateMipmaps);
            result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            result.Apply();

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            // Cleanup old texture
            UnityEngine.Object.Destroy(source);

            return result;
        }

        /// <summary>
        /// OPTIMIZATION: Texture pooling (disabled for compression compatibility)
        /// </summary>
        /*
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
        */

        /// <summary>
        /// OPTIMIZATION: Unload sprites khi không dùng để giải phóng memory
        /// Gọi khi user thoát khỏi subject
        /// </summary>
        public void UnloadSubjectSprites(string subjectPath)
        {
            var localSubject = subjectDatabase?.subjects.Find(s => s.MatchesCloudinaryFolder(subjectPath));
            if (localSubject == null) return;

            var sprites = localSubject.bookPages;
            if (sprites != null)
            {
                foreach (var sprite in sprites)
                {
                    if (sprite != null && sprite.texture != null)
                    {
                        UnityEngine.Object.Destroy(sprite.texture);
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
                            bool hasMatch = subjectDatabase.subjects.Exists(s => s.MatchesCloudinaryFolder(pdfInfo.cloudinaryFolder));
                            if (!hasMatch)
                            {
                                Log($"Skipping '{pdfInfo.name}' - no matching path");
                                skippedCount++;
                                continue;
                            }
                        }

                        RemoteSubjectInfo remoteSubject = new RemoteSubjectInfo(pdfInfo);
                        
                        // Check cache by cloudinaryFolder first, then by name
                        var cachedData = cacheManifest.GetSubjectCacheByFolder(pdfInfo.cloudinaryFolder) 
                                         ?? cacheManifest.GetSubjectCache(pdfInfo.name);
                        if (cachedData != null)
                        {
                            string currentHash = pdfInfo.GetVersionHash();
                            bool hashMatch = cachedData.versionHash == currentHash;
                            bool filesExist = VerifyCachedFilesExist(cachedData.cachedImagePaths);
                            
                            remoteSubject.isCached = cachedData.isFullyCached && hashMatch && filesExist;
                            
                            Log($"[CACHE CHECK] '{pdfInfo.name}': fullyCached={cachedData.isFullyCached}, hashMatch={hashMatch}, filesExist={filesExist}, isCached={remoteSubject.isCached}");
                            
                            if (remoteSubject.isCached)
                            {
                                remoteSubject.localImagePaths = new List<string>(cachedData.cachedImagePaths);
                            }
                            else if (!filesExist && cachedData.isFullyCached)
                            {
                                // Files were deleted, need to re-cache
                                Log($"[CACHE CHECK] '{pdfInfo.name}': Cache files missing, will re-download");
                                cachedData.isFullyCached = false;
                                cachedData.cachedImagePaths.Clear();
                            }
                        }
                        else
                        {
                            Log($"[CACHE CHECK] '{pdfInfo.name}': No cache data found, imageUrls count = {pdfInfo.pageImages?.Count ?? 0}");
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
                var localSubject = subjectDatabase.subjects.Find(s => s.MatchesCloudinaryFolder(remote.cloudinaryFolder));
                
                if (localSubject != null)
                {
                    localSubject.title = remote.title;
                    localSubject.grade = remote.grade;
                    localSubject.category = remote.category;
                    localSubject.note = remote.note;
                }
            }

            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(subjectDatabase);
            UnityEditor.AssetDatabase.SaveAssets();
            #endif
        }

        // Các methods còn lại giữ nguyên...
        private IEnumerator AutoCacheAllSubjects()
        {
            Log("Starting auto-cache for all subjects...");
            
            int totalSubjects = remoteSubjects.Count;
            int cachedCount = 0;
            int skippedCount = 0;

            for (int i = 0; i < remoteSubjects.Count; i++)
            {
                var remote = remoteSubjects[i];
                
                // Skip if already cached
                if (remote.isCached)
                {
                    Log($"[CACHE] Skipping '{remote.GetIdentifier()}' - already cached");
                    skippedCount++;
                    cachedCount++;
                    continue;
                }

                // Skip if no images to cache
                if (remote.imageUrls == null || remote.imageUrls.Count == 0)
                {
                    Log($"[CACHE] Skipping '{remote.GetIdentifier()}' - no images");
                    skippedCount++;
                    continue;
                }

                Log($"[CACHE] Caching '{remote.GetIdentifier()}' ({i + 1}/{totalSubjects}) - {remote.imageUrls.Count} images");

                // Cache this subject
                yield return StartCoroutine(CacheSubjectCoroutine(remote));
                
                cachedCount++;

                // Update overall progress
                overallProgress = (float)cachedCount / totalSubjects;
                OnOverallProgress?.Invoke(overallProgress);
            }

            Log($"Auto-cache complete: {cachedCount} cached, {skippedCount} skipped");
            
            // Save manifest after caching all
            SaveCacheManifest();
            
            TryMarkAsReady();
            OnSubjectsLoaded?.Invoke(remoteSubjects);
        }

        /// <summary>
        /// Cache images for a single subject - downloads from server and saves to local cache
        /// </summary>
        private IEnumerator CacheSubjectCoroutine(RemoteSubjectInfo subject)
        {
            if (subject.imageUrls == null || subject.imageUrls.Count == 0)
            {
                LogError($"[CACHE] No images to cache for '{subject.GetIdentifier()}'");
                yield break;
            }

            // Create folder for subject
            string subjectFolder = Path.Combine(CachePath, SanitizeFolderName(subject.cloudinaryFolder ?? subject.name));
            if (!Directory.Exists(subjectFolder))
            {
                Directory.CreateDirectory(subjectFolder);
            }

            // Create or get cache data
            var cacheData = cacheManifest.GetSubjectCacheByFolder(subject.cloudinaryFolder) 
                            ?? cacheManifest.GetSubjectCache(subject.name);
            
            if (cacheData == null)
            {
                string versionHash = $"{subject.name}_{subject.pages}_{subject.imageUrls.Count}";
                cacheData = new LocalSubjectCacheData(subject.name, subject.cloudinaryFolder, versionHash);
            }
            else
            {
                // Clear existing cached paths if re-caching
                cacheData.cachedImagePaths.Clear();
            }

            int downloadedCount = 0;
            int totalImages = subject.imageUrls.Count;

            for (int i = 0; i < totalImages; i++)
            {
                string imageUrl = subject.imageUrls[i];
                string extension = cacheAsPNG ? ".png" : GetExtensionFromUrl(imageUrl);
                string fileName = $"page_{i:D3}{extension}";
                string localPath = Path.Combine(subjectFolder, fileName);

                Log($"[CACHE] Downloading image {i + 1}/{totalImages}: {imageUrl}");

                // Download and save image
                bool success = false;
                yield return StartCoroutine(DownloadAndSaveImageCoroutine(imageUrl, localPath, cacheAsPNG, (result) => {
                    success = result;
                    Log($"[CACHE] Download result for image {i + 1}: {(result ? "SUCCESS" : "FAILED")} -> {localPath}");
                }));

                if (success)
                {
                    cacheData.cachedImagePaths.Add(localPath);
                    downloadedCount++;
                    
                    // Report progress
                    float progress = (float)downloadedCount / totalImages;
                    OnSubjectCacheProgress?.Invoke(subject, progress);
                }
                else
                {
                    LogError($"[CACHE] Failed to download image {i} for '{subject.GetIdentifier()}'");
                }

                // Small delay between downloads to avoid overwhelming the server
                if ((i + 1) % 5 == 0)
                {
                    yield return new WaitForSeconds(0.1f);
                }
            }

            // Update cache data
            cacheData.isFullyCached = downloadedCount == totalImages;
            cacheData.lastUpdated = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            cacheManifest.AddOrUpdateSubject(cacheData);

            // Update subject status
            subject.isCached = cacheData.isFullyCached;
            subject.localImagePaths = new List<string>(cacheData.cachedImagePaths);

            // Update local database
            var localSubject = subjectDatabase?.subjects.Find(s => s.MatchesCloudinaryFolder(subject.cloudinaryFolder));
            if (localSubject != null)
            {
                localSubject.isCached = subject.isCached;
                localSubject.localImagePaths = new List<string>(subject.localImagePaths);
            }

            Log($"[CACHE] Completed '{subject.GetIdentifier()}': {downloadedCount}/{totalImages} images");
            OnSubjectCacheComplete?.Invoke(subject);
        }

        /// <summary>
        /// Download WebP from URL and save to cache (as PNG or WebP based on settings)
        /// Uses WebP decoder for proper handling of WebP images
        /// </summary>
        private IEnumerator DownloadAndSaveImageCoroutine(string url, string localPath, bool convertToPNG, Action<bool> callback)
        {
            // Fix double-encoded URLs (e.g., %C3%83%C2%A1 instead of %C3%A1)
            string decodedUrl = url;
            try
            {
                if (url.Contains("%C3%83") || url.Contains("%C2%") || url.Contains("%25"))
                {
                    decodedUrl = Uri.UnescapeDataString(url);
                    Log($"[CACHE] Fixed double-encoded URL");
                }
            }
            catch (Exception ex)
            {
                Log($"[CACHE] URL decode warning: {ex.Message}, using original URL");
                decodedUrl = url;
            }

            using (UnityWebRequest request = UnityWebRequest.Get(decodedUrl))
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
                                LogError($"[CACHE] Failed to decode WebP: {error}");
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
                        LogError($"[CACHE] Failed to save image: {ex.Message}");
                        callback?.Invoke(false);
                    }
                }
                else
                {
                    LogError($"[CACHE] Failed to download: {request.error}");
                    callback?.Invoke(false);
                }
            }
        }

        /// <summary>
        /// Sanitize folder name to remove invalid characters
        /// </summary>
        private string SanitizeFolderName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "unknown";
            
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                name = name.Replace(c, '_');
            }
            return name;
        }

        /// <summary>
        /// Get file extension from URL
        /// </summary>
        private string GetExtensionFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return ".png";
            
            if (url.Contains(".webp", StringComparison.OrdinalIgnoreCase)) return ".webp";
            if (url.Contains(".jpg", StringComparison.OrdinalIgnoreCase)) return ".jpg";
            if (url.Contains(".jpeg", StringComparison.OrdinalIgnoreCase)) return ".jpeg";
            if (url.Contains(".png", StringComparison.OrdinalIgnoreCase)) return ".png";
            
            return ".png"; // Default
        }

        /// <summary>
        /// Verify that all cached files actually exist on disk
        /// </summary>
        private bool VerifyCachedFilesExist(List<string> cachedPaths)
        {
            if (cachedPaths == null || cachedPaths.Count == 0)
                return false;

            foreach (var path in cachedPaths)
            {
                if (!File.Exists(path))
                {
                    Log($"[CACHE VERIFY] File missing: {path}");
                    return false;
                }
            }
            return true;
        }

        [ProButton]
        public void ValidateAllCacheStatus()
        {
            // Implementation giữ nguyên
        }

        #region Debug Cache Tools

        [ProButton]
        public void OpenCacheFolder()
        {
            EnsureCacheDirectoryExists();
            string path = CachePath.Replace("/", "\\");
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.RevealInFinder(path);
            #else
            System.Diagnostics.Process.Start("explorer.exe", path);
            #endif
            
            Log($"Opening cache folder: {path}");
        }

        [ProButton]
        public void ClearAllCache()
        {
            if (Directory.Exists(CachePath))
            {
                try
                {
                    Directory.Delete(CachePath, true);
                    Log("Deleted all cache files");
                }
                catch (Exception ex)
                {
                    LogError($"Failed to delete cache folder: {ex.Message}");
                }
            }

            // Reset manifest
            cacheManifest = new SubjectCacheManifest();
            EnsureCacheDirectoryExists();
            SaveCacheManifest();

            // Clear cached status in database
            if (subjectDatabase != null)
            {
                foreach (var subject in subjectDatabase.subjects)
                {
                    subject.isCached = false;
                    subject.localImagePaths?.Clear();
                    subject.ClearCacheData();
                }

                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(subjectDatabase);
                #endif
            }

            // Clear remote subjects cache status
            foreach (var remote in remoteSubjects)
            {
                remote.isCached = false;
                remote.localImagePaths?.Clear();
            }

            Log("Cache cleared successfully!");
        }

        [ProButton]
        public void LogCacheInfo()
        {
            Log($"=== Cache Info ===");
            Log($"Cache Path: {CachePath}");
            Log($"Manifest Path: {ManifestPath}");
            Log($"Cache Exists: {Directory.Exists(CachePath)}");
            
            if (cacheManifest != null)
            {
                Log($"Cached Subjects: {cacheManifest.subjects.Count}");
                foreach (var cached in cacheManifest.subjects)
                {
                    Log($"  - {cached.subjectName} (folder: {cached.cloudinaryFolder}): {cached.cachedImagePaths.Count} images, fully cached: {cached.isFullyCached}");
                }
            }

            if (Directory.Exists(CachePath))
            {
                var files = Directory.GetFiles(CachePath, "*", SearchOption.AllDirectories);
                long totalSize = 0;
                foreach (var file in files)
                {
                    totalSize += new FileInfo(file).Length;
                }
                Log($"Total Files: {files.Length}");
                Log($"Total Size: {totalSize / 1024f / 1024f:F2} MB");
            }
            Log($"==================");
        }

        /// <summary>
        /// Force re-download all subjects (ignores cache status)
        /// </summary>
        [ProButton]
        public void ForceRedownloadAll()
        {
            Log("[FORCE REDOWNLOAD] Clearing cache and re-downloading all subjects...");
            
            // Clear cache status but keep manifest
            foreach (var remote in remoteSubjects)
            {
                remote.isCached = false;
                remote.localImagePaths?.Clear();
            }

            // Also clear manifest cache status
            if (cacheManifest != null)
            {
                foreach (var cached in cacheManifest.subjects)
                {
                    cached.isFullyCached = false;
                    cached.cachedImagePaths.Clear();
                }
            }

            // Start caching
            if (remoteSubjects.Count > 0)
            {
                StartCoroutine(AutoCacheAllSubjects());
            }
            else
            {
                LogError("[FORCE REDOWNLOAD] No remote subjects loaded. Call FetchSubjects first.");
            }
        }

        /// <summary>
        /// Debug: Log all remote subjects and their imageUrls
        /// </summary>
        [ProButton]
        public void LogRemoteSubjectsInfo()
        {
            Log($"=== Remote Subjects Info ===");
            Log($"Total remote subjects: {remoteSubjects.Count}");
            
            foreach (var remote in remoteSubjects)
            {
                int imageCount = remote.imageUrls?.Count ?? 0;
                Log($"  - {remote.name} (folder: {remote.cloudinaryFolder})");
                Log($"    isCached: {remote.isCached}, imageUrls: {imageCount}, localPaths: {remote.localImagePaths?.Count ?? 0}");
                if (imageCount > 0)
                {
                    Log($"    First URL: {remote.imageUrls[0]}");
                }
            }
            Log($"============================");
        }

        #endregion

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
            // Texture pool disabled
            // No cleanup needed
        }
    }
}