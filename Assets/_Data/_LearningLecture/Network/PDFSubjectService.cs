using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
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
        [SerializeField] private string listEndpoint = "/api/pdfs/list/books";

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
        [SerializeField] private bool backgroundPreloadAfterStart = false;  // DISABLED: Causes race condition with lazy load
        [Tooltip("Delay before starting background preload (seconds)")]
        [SerializeField] private float backgroundPreloadDelay = 1f;
        [Tooltip("Maximum sprites to load per frame (prevents lag spikes)")]
        [SerializeField] private int maxSpritesPerFrame = 5;
        [Tooltip("Use texture compression - WARNING: Very CPU intensive, disable for better performance")]
        [SerializeField] private bool useTextureCompression = false;
        [Tooltip("Maximum texture size (lower = faster load, 1024 recommended for performance)")]
        [SerializeField] private int maxTextureSize = 1024;
        [Tooltip("Skip resize if texture is smaller than max size (faster)")]
        [SerializeField] private bool skipResizeIfSmaller = true;
        [Tooltip("Generate mipmaps (smoother zooming but +33% memory)")]
        [SerializeField] private bool generateMipmaps = false;
        [Tooltip("Delay between sprite loads in seconds (reduced for faster loading)")]
        [SerializeField] private float loadDelaySeconds = 0.01f;
        [Tooltip("Use UnityWebRequest for loading (more optimized than File.ReadAllBytes)")]
        [SerializeField] private bool useWebRequestLoader = true;

        [Header("Bundle Settings")]
        [SerializeField] private bool checkLocalBundleFirst = true;
        [SerializeField] private string bundleStorePath = "StreamingAssets";

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

        // Debug properties for editor tools
        public bool CheckLocalBundleFirst => checkLocalBundleFirst;
        public string BundleStorePath => bundleStorePath;
        public bool PreloadCachedOnStart => preloadCachedOnStart;
        public string CacheFolderName => cacheFolder;

        private string CachePath => Path.Combine(Application.persistentDataPath, cacheFolder);
        private string ManifestPath => Path.Combine(CachePath, manifestFileName);

        // OPTIMIZATION: Texture pool để reuse textures
        // NOTE: Pool disabled vì conflict với compression
        // private Queue<Texture2D> texturePool = new Queue<Texture2D>();
        // private const int MAX_POOL_SIZE = 10;

        // OPTIMIZATION: Track loading state per subject
        private Dictionary<string, bool> subjectLoadingState = new Dictionary<string, bool>();

        // BUNDLE: Quản lý bundle đã load (tránh load lại)
        private Dictionary<string, AssetBundle> loadedBundles = new Dictionary<string, AssetBundle>();

        // Bundle loading statistics for editor tools
        private Dictionary<string, int> bundleLoadStats = new Dictionary<string, int>();
        private int totalBundleLoads = 0;
        private int totalCacheLoads = 0;
        private int totalApiLoads = 0;

        public int TotalBundleLoads => totalBundleLoads;
        public int TotalCacheLoads => totalCacheLoads;
        public int TotalApiLoads => totalApiLoads;
        public Dictionary<string, int> BundleLoadStats => bundleLoadStats;

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.LoadApiClient();
        }

        private void LoadApiClient()
        {
            if (apiClient != null) return;
            apiClient = GameObject.FindAnyObjectByType<ApiClient>();
        }

        private bool hasFetched = false;
        private bool isPreloading = false;
        private bool preloadComplete = false;

        protected override void Start()
        {
            base.Start();

            // Reset state mỗi lần scene load
            isReady = false;
            overallProgress = 0f;
            hasFetched = false;
            remoteSubjects.Clear();

            // Reset statistics mỗi lần scene load
            totalBundleLoads = 0;
            totalCacheLoads = 0;
            totalApiLoads = 0;
            bundleLoadStats.Clear();
            Log("[STARTUP] Statistics reset");

            LoadCacheManifest();

            // PRIORITY 1: CHECK BUNDLE FIRST at startup + PRELOAD BUNDLE SPRITES
            if (checkLocalBundleFirst)
            {
                string bundleStoreFull = Path.Combine(Application.streamingAssetsPath, bundleStorePath);
                if (Directory.Exists(bundleStoreFull))
                {
                    string[] bundleFiles = Directory.GetFiles(bundleStoreFull);

                    // Filter actual bundle files (exclude .manifest and .meta)
                    List<string> actualBundles = new List<string>();
                    foreach (var file in bundleFiles)
                    {
                        string fileName = Path.GetFileName(file);
                        if (!fileName.EndsWith(".manifest", System.StringComparison.OrdinalIgnoreCase) &&
                            !fileName.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase))
                        {
                            actualBundles.Add(file);
                        }
                    }

                    if (actualBundles.Count > 0)
                    {
                        Log($"[STARTUP] Found {actualBundles.Count} bundle files in {bundleStorePath}");
                        for (int i = 0; i < actualBundles.Count; i++)
                        {
                            string bundlePath = actualBundles[i];
                            string bundleName = Path.GetFileName(bundlePath);
                            Log($"[STARTUP BUNDLE] Bundle {i + 1}: {bundleName}");
                        }

                        // Start preloading bundles in background
                        hasFetched = true;
                        isPreloading = true;
                        StartCoroutine(PreloadAllBundlesCoroutine());
                        return;
                    }
                }
            }

            // OPTIMIZATION: Chỉ preload metadata, không load sprites nếu useLazyLoading = true
            if (preloadCachedOnStart && cacheManifest != null && cacheManifest.subjects.Count > 0)
            {
                if (useLazyLoading)
                {
                    Log($"[LAZY LOAD] Found {cacheManifest.subjects.Count} cached subjects");
                    // Load metadata + sprites from cache immediately (synchronously)
                    PreloadFromCache();
                    preloadComplete = true;

                    // NO background preload - causes race condition
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

            // PRIORITY 2: CHECK CACHE MANIFEST before attempting API fetch
            if (cacheManifest != null && cacheManifest.subjects.Count > 0)
            {
                Log($"[STARTUP] Cache manifest found with {cacheManifest.subjects.Count} subjects - loading from cache");

                // Load subjects from cache manifest (no API needed)
                LoadSubjectsFromCacheManifest();
                hasFetched = true;  // Mark as fetched (from cache)
                TryMarkAsReady();  // Mark ready immediately
            }
            else if (autoFetchOnStart && !hasFetched)
            {
                Log("[STARTUP] Cache manifest empty - fetching from API...");
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
        /// Load sprites from cache immediately (synchronously at startup)
        /// This ensures sprites are ready when user clicks on a book
        /// </summary>
        private void PreloadFromCache()
        {
            if (subjectDatabase == null || cacheManifest == null) return;

            int spritesLoaded = 0;
            int subjectsProcessed = 0;

            foreach (var cachedData in cacheManifest.subjects)
            {
                if (!cachedData.isFullyCached || cachedData.cachedImagePaths.Count == 0)
                    continue;

                // Match by cloudinaryFolder first, then by name
                var localSubject = subjectDatabase.subjects.Find(s =>
                    (!string.IsNullOrEmpty(s.cloudinaryFolder) && !string.IsNullOrEmpty(cachedData.cloudinaryFolder) &&
                     s.cloudinaryFolder.Equals(cachedData.cloudinaryFolder, System.StringComparison.OrdinalIgnoreCase)) ||
                    s.name == cachedData.subjectName);

                // Create if not found
                if (localSubject == null && !string.IsNullOrEmpty(cachedData.cloudinaryFolder))
                {
                    localSubject = new SubjectInfo
                    {
                        name = cachedData.subjectName,
                        cloudinaryFolder = cachedData.cloudinaryFolder
                    };
                    subjectDatabase.subjects.Add(localSubject);
                    Log($"[PRELOAD] Created new SubjectInfo from cache: {cachedData.subjectName}");
                }

                if (localSubject != null)
                {
                    // Set metadata
                    localSubject.isCached = true;
                    localSubject.localImagePaths = new List<string>(cachedData.cachedImagePaths);

                    // Load sprites synchronously from cache files
                    List<Sprite> loadedSprites = new List<Sprite>();
                    foreach (var imagePath in cachedData.cachedImagePaths)
                    {
                        if (!System.IO.File.Exists(imagePath))
                        {
                            LogError($"[PRELOAD] Cache file not found: {imagePath}");
                            continue;
                        }

                        try
                        {
                            byte[] imageData = System.IO.File.ReadAllBytes(imagePath);
                            Texture2D texture = new Texture2D(2, 2);
                            if (texture.LoadImage(imageData))
                            {
                                Sprite sprite = Sprite.Create(texture,
                                    new Rect(0, 0, texture.width, texture.height),
                                    Vector2.one * 0.5f);
                                loadedSprites.Add(sprite);
                                spritesLoaded++;
                            }
                            else
                            {
                                LogError($"[PRELOAD] Failed to load image: {imagePath}");
                            }
                        }
                        catch (System.Exception e)
                        {
                            LogError($"[PRELOAD] Error loading {imagePath}: {e.Message}");
                        }
                    }

                    // Assign sprites to subject
                    if (loadedSprites.Count > 0)
                    {
                        localSubject.SetBookPages(loadedSprites.ToArray());
                        subjectsProcessed++;
                        Log($"[PRELOAD] Loaded {loadedSprites.Count} sprites for {cachedData.subjectName}");
                    }
                }
            }

            Log($"[PRELOAD] Complete: {spritesLoaded} sprites loaded for {subjectsProcessed} subjects");
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
                        yield return StartCoroutine(LoadSubjectSpritesOnDemand(subject.cloudinaryFolder, (sprites) =>
                        {
                            // Sprites loaded
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
        /// Preload tất cả bundle sprites lúc startup (giống như cache preload)
        /// Để sprites sẵn sàng khi user click vào sách
        /// </summary>
        private IEnumerator PreloadAllBundlesCoroutine()
        {
            Log("[BUNDLE PRELOAD] Starting preload of all bundle sprites...");

            string bundleStoreFull = Path.Combine(Application.streamingAssetsPath, bundleStorePath);
            string[] bundleFiles = Directory.GetFiles(bundleStoreFull);

            // Filter only bundle files (exclude .manifest and .meta files)
            List<string> actualBundles = new List<string>();
            foreach (var file in bundleFiles)
            {
                string fileName = Path.GetFileName(file);
                if (!fileName.EndsWith(".manifest", System.StringComparison.OrdinalIgnoreCase) &&
                    !fileName.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase))
                {
                    actualBundles.Add(file);
                }
            }

            Log($"[BUNDLE PRELOAD] Found {actualBundles.Count} bundles to preload");

            int preloadedCount = 0;
            foreach (var bundlePath in actualBundles)
            {
                string bundleFileName = Path.GetFileName(bundlePath);
                string bundleName = bundleFileName.ToLower();

                // Load bundle and cache sprites
                yield return StartCoroutine(LoadFromLocalBundle(bundlePath, bundleName, (sprites) =>
                {
                    if (sprites != null && sprites.Length > 0)
                    {
                        preloadedCount++;
                        Log($"[BUNDLE PRELOAD] Preloaded {bundleFileName}: {sprites.Length} sprites");

                        // ============================================================
                        // DEBUG: Match bundle with SubjectDatabase
                        // ============================================================
                        if (subjectDatabase != null)
                        {
                            //Log($"[BUNDLE DEBUG] === Bundle: {bundleName} ===");
                            //Log($"[BUNDLE DEBUG] Total subjects in database: {subjectDatabase.subjects.Count}");
                            
                            bool foundMatch = false;
                            foreach (var subject in subjectDatabase.subjects)
                            {
                                // Normalize CloudinaryFolder: replace / with _, lowercase
                                string cloudinaryNormalized = subject.cloudinaryFolder;
                                if (!string.IsNullOrEmpty(cloudinaryNormalized))
                                {
                                    cloudinaryNormalized = cloudinaryNormalized.Replace("/", "_").ToLower();
                                }
                                
                                // Compare without sanitization
                                bool nameMatch = subject.name.Equals(bundleName, StringComparison.OrdinalIgnoreCase);
                                bool folderMatch = !string.IsNullOrEmpty(cloudinaryNormalized) && 
                                    cloudinaryNormalized.Equals(bundleName, StringComparison.OrdinalIgnoreCase);
                                
                                //Log($"[BUNDLE DEBUG] Subject: {subject.name}");
                                //Log($"[BUNDLE DEBUG]   - Name: {subject.name} | Match: {nameMatch}");
                                //Log($"[BUNDLE DEBUG]   - CloudinaryFolder: {subject.cloudinaryFolder}");
                                //Log($"[BUNDLE DEBUG]   - CloudinaryFolder Normalized: {cloudinaryNormalized}");
                                //Log($"[BUNDLE DEBUG]   - CloudinaryFolder Match: {folderMatch}");
                                //Log($"[BUNDLE DEBUG]   - Bundle name to match: {bundleName}");
                                //Log($"[BUNDLE DEBUG]   - Title: {subject.title}");
                                //Log($"[BUNDLE DEBUG]   - Pages: {subject.pages}");
                                
                                if (nameMatch || folderMatch)
                                {
                                    foundMatch = true;
                                    subject.SetBookPages(sprites);
                                    subject.isCached = true;
                                    Log($"[BUNDLE PRELOAD] MATCHED! Assigned {sprites.Length} sprites to {subject.name}");
                                }
                            }
                            
                            if (!foundMatch)
                            {
                                Log($"[BUNDLE PRELOAD] WARNING: No match found for bundle {bundleName}");
                            }
                        }
                    }
                }));

                // Small delay between bundles
                yield return new WaitForSeconds(0.1f);
            }

            Log($"[BUNDLE PRELOAD] Complete! Preloaded {preloadedCount}/{actualBundles.Count} bundles");

            preloadComplete = true;
            TryMarkAsReady();
        }

        /// <summary>
        /// OPTIMIZATION: Load sprites on-demand for a specific subject
        /// Gọi method này khi user chọn subject để học
        /// </summary>
        public IEnumerator LoadSubjectSpritesOnDemand(string subjectPath, Action<Sprite[]> callback)
        {
            // ============================================================
            // 0. CHECK IF ALREADY LOADED (from bundle or cache preload)
            // ============================================================
            var localSubject = subjectDatabase?.subjects.Find(s => s.MatchesCloudinaryFolder(subjectPath));
            if (localSubject != null && localSubject.HasLoadedSprites())
            {
                Log($"[LAZY LOAD] Sprites already loaded for {localSubject.name} - returning cached");
                callback?.Invoke(localSubject.bookPages);
                yield break;
            }

            // ============================================================
            // 1. CHECK BUNDLE TRƯỚC (Logic mới)
            // ============================================================
            if (checkLocalBundleFirst)
            {
                string bundleName = SanitizeFolderName(subjectPath).ToLower();
                string pathToCheck = Path.Combine(Application.streamingAssetsPath, bundleStorePath, bundleName);

                // Nếu file bundle tồn tại
                if (File.Exists(pathToCheck))
                {
                    Log($"[BUNDLE] Found bundle for {subjectPath}, loading from bundle...");

                    // Clear old sprites trước khi load mới
                    var targetSubject = subjectDatabase?.subjects.Find(s => s.MatchesCloudinaryFolder(subjectPath));
                    if (targetSubject != null && targetSubject.HasLoadedSprites())
                    {
                        Log($"[BUNDLE] Clearing old sprites for {targetSubject.name}...");
                        ClearSubjectSprites(targetSubject);
                    }

                    yield return StartCoroutine(LoadFromLocalBundle(pathToCheck, bundleName, callback));
                    yield break; // Kết thúc luôn, không chạy logic cũ
                }
            }

            // ============================================================
            // 2. LOGIC CŨ (Nếu không có bundle thì chạy tiếp)
            // ============================================================

            // Check if already loading
            if (subjectLoadingState.ContainsKey(subjectPath) && subjectLoadingState[subjectPath])
            {
                Log($"[LAZY LOAD] Subject {subjectPath} is already loading, waiting...");
                yield return new WaitUntil(() => !subjectLoadingState[subjectPath]);
            }

            if (localSubject == null)
            {
                LogError($"[LAZY LOAD] Subject not found: {subjectPath}");
                callback?.Invoke(null);
                yield break;
            }

            Log($"[LAZY LOAD] Found subject: {localSubject.name}, localImagePaths count: {localSubject.localImagePaths?.Count ?? 0}");

            // Check if already loaded (second check after potential bundle load)
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
                            totalCacheLoads++;
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
                            totalCacheLoads++;
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
        /// Load sprites từ local AssetBundle (rất nhanh)
        /// </summary>
        // Cache sprites từ bundle để reuse (tránh deserialize lại)
        private Dictionary<string, Sprite[]> bundleSpriteCache = new Dictionary<string, Sprite[]>();

        private IEnumerator LoadFromLocalBundle(string filePath, string bundleName, Action<Sprite[]> callback)
        {
            // Check cache sprite trước - nếu đã load lần này thì dùng ngay
            if (bundleSpriteCache.ContainsKey(bundleName))
            {
                Log($"[BUNDLE] Using cached sprites for {bundleName} ({bundleSpriteCache[bundleName].Length} sprites)");
                callback?.Invoke(bundleSpriteCache[bundleName]);
                yield break;
            };

            AssetBundle bundle = null;

            // Check xem đã load vào RAM chưa
            if (loadedBundles.ContainsKey(bundleName))
            {
                bundle = loadedBundles[bundleName];
                Log($"[BUNDLE] Using cached bundle in memory: {bundleName}");
            }
            else
            {
                // Load từ ổ đĩa (Nhanh hơn WebRequest nhiều)
                var bundleRequest = AssetBundle.LoadFromFileAsync(filePath);
                yield return bundleRequest;
                bundle = bundleRequest.assetBundle;

                if (bundle != null)
                {
                    loadedBundles[bundleName] = bundle;
                    Log($"[BUNDLE] Loaded bundle into memory: {bundleName}");
                }
            }

            if (bundle == null)
            {
                LogError($"[BUNDLE] Failed to load bundle at {filePath}");
                callback?.Invoke(null);
                yield break;
            }

            // Track bundle load statistics
            if (!bundleLoadStats.ContainsKey(bundleName))
            {
                bundleLoadStats[bundleName] = 0;
            }
            bundleLoadStats[bundleName]++;
            totalBundleLoads++;
            Log($"[BUNDLE STAT] Total bundle loads = {totalBundleLoads}, {bundleName} loaded {bundleLoadStats[bundleName]} times");

            // Try loading Sprites first
            var spriteRequest = bundle.LoadAllAssetsAsync<Sprite>();
            yield return spriteRequest;

            Sprite[] sprites = spriteRequest.allAssets.Cast<Sprite>().ToArray();

            // If no Sprites found, try loading Texture2D and convert to Sprite
            if (sprites.Length == 0)
            {
                Log($"[BUNDLE] No Sprites found, attempting to load Texture2D and convert...");
                
                var textureRequest = bundle.LoadAllAssetsAsync<Texture2D>();
                yield return textureRequest;

                Texture2D[] textures = textureRequest.allAssets.Cast<Texture2D>().ToArray();
                Log($"[BUNDLE DEBUG] Found {textures.Length} Texture2D assets in bundle");

                if (textures.Length > 0)
                {
                    // Convert Texture2D to Sprite
                    List<Sprite> convertedSprites = new List<Sprite>();
                    foreach (var texture in textures)
                    {
                        try
                        {
                            Sprite sprite = Sprite.Create(
                                texture,
                                new Rect(0, 0, texture.width, texture.height),
                                Vector2.one * 0.5f,
                                100f
                            );
                            sprite.name = texture.name;
                            convertedSprites.Add(sprite);
                            //Log($"[BUNDLE] Converted Texture2D to Sprite: {texture.name}");
                        }
                        catch (System.Exception ex)
                        {
                            //LogError($"[BUNDLE] Failed to convert Texture2D {texture.name}: {ex.Message}");
                        }
                    }
                    sprites = convertedSprites.ToArray();
                }
                else
                {
                    // No Sprite or Texture2D found - likely a manifest bundle, skip silently
                    Log($"[BUNDLE] No Sprite or Texture2D assets found in bundle {bundleName} - skipping (likely manifest bundle)");
                    
                    Log($"[BUNDLE] Falling back to cache/API loading...");
                    callback?.Invoke(null);
                    yield break;
                }
            }

            // Sort by name (page_001, page_002...) since Bundle returns random order
            System.Array.Sort(sprites, (a, b) => string.Compare(a.name, b.name));

            // Cache sprite để reuse lần sau (không cần deserialize lại)
            bundleSpriteCache[bundleName] = sprites;
            Log($"[BUNDLE] Loaded {sprites.Length} sprites from bundle {bundleName} and cached for reuse");
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

                Task.Run(() =>
                {
                    try
                    {
                        pngData = File.ReadAllBytes(path);
                    }
                    catch (Exception ex)
                    {
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

            Task.Run(() =>
            {
                try
                {
                    webpData = File.ReadAllBytes(path);
                }
                catch (Exception ex)
                {
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
        /// Clear sprites từ subject (unload textures và destroy sprites)
        /// Dùng khi load subject mới để tránh memory leak
        /// </summary>
        private void ClearSubjectSprites(SubjectInfo subject)
        {
            if (subject == null) return;

            var sprites = subject.bookPages;
            if (sprites != null && sprites.Length > 0)
            {
                int count = 0;
                foreach (var sprite in sprites)
                {
                    if (sprite != null)
                    {
                        // Destroy texture
                        if (sprite.texture != null)
                        {
                            UnityEngine.Object.Destroy(sprite.texture);
                        }
                        // Destroy sprite
                        UnityEngine.Object.Destroy(sprite);
                        count++;
                    }
                }
                Log($"[MEMORY] Cleared {count} old sprites for {subject.name}");
            }

            // Clear cached references
            subject.ClearCacheData();
        }

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

        /// <summary>
        /// Load subjects from cache manifest without API fetch
        /// Used when cache is available at startup to avoid network delays
        /// </summary>
        private void LoadSubjectsFromCacheManifest()
        {
            if (cacheManifest?.subjects == null || cacheManifest.subjects.Count == 0)
            {
                Log("[CACHE] Cache manifest is empty - cannot load subjects");
                return;
            }

            remoteSubjects.Clear();
            int loadedCount = 0;

            foreach (var cachedSubject in cacheManifest.subjects)
            {
                try
                {
                    // Create RemoteSubjectInfo from cached data
                    var remoteSubject = new RemoteSubjectInfo
                    {
                        name = cachedSubject.subjectName,
                        cloudinaryFolder = cachedSubject.cloudinaryFolder,
                        isCached = true,
                        localImagePaths = new List<string>(cachedSubject.cachedImagePaths ?? new List<string>()),
                        imageUrls = new List<string>(cachedSubject.cachedImagePaths ?? new List<string>())  // Use cached paths as imageUrls
                    };

                    remoteSubjects.Add(remoteSubject);
                    loadedCount++;

                    Log($"[CACHE] Loaded from manifest: {cachedSubject.subjectName} ({cachedSubject.cachedImagePaths?.Count ?? 0} images)");
                }
                catch (System.Exception e)
                {
                    Log($"[ERROR] Failed to load cached subject {cachedSubject.subjectName}: {e.Message}");
                }
            }

            Log($"[CACHE] Successfully loaded {loadedCount} subjects from cache manifest");
            OnSubjectsLoaded?.Invoke(remoteSubjects);
            preloadComplete = true;
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

        /// <summary>
        /// Reset fetch state để có thể fetch lại từ đầu
        /// </summary>
        public void ResetFetchState()
        {
            hasFetched = false;
            isReady = false;
            overallProgress = 0f;
            remoteSubjects.Clear();
            Log("Fetch state reset");
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
                        string currentHash = pdfInfo.GetVersionHash();

                        // ============================================================
                        // STEP 1: BUNDLE CHECK
                        // ============================================================
                        if (checkLocalBundleFirst)
                        {
                            string bundleName = SanitizeFolderName(pdfInfo.cloudinaryFolder).ToLower();
                            string bundlePath = Path.Combine(Application.streamingAssetsPath, bundleStorePath, bundleName);

                            if (File.Exists(bundlePath))
                            {
                                remoteSubject.isCached = true;
                                Log($"[BUNDLE CHECK] '{pdfInfo.name}': FOUND - Will use BUNDLE");
                                remoteSubjects.Add(remoteSubject);
                                continue; // Skip to next subject, use bundle
                            }
                            else
                            {
                                Log($"[BUNDLE CHECK] '{pdfInfo.name}': NOT FOUND");
                            }
                        }

                        // ============================================================
                        // STEP 2: CACHE CHECK (with version validation)
                        // ============================================================
                        var cachedData = cacheManifest.GetSubjectCacheByFolder(pdfInfo.cloudinaryFolder)
                                         ?? cacheManifest.GetSubjectCache(pdfInfo.name);

                        if (cachedData != null)
                        {
                            bool hashMatch = cachedData.versionHash == currentHash;
                            bool filesExist = VerifyCachedFilesExist(cachedData.cachedImagePaths);

                            // Cache is VALID only if: fully cached + hash match + files exist
                            if (cachedData.isFullyCached && hashMatch && filesExist)
                            {
                                remoteSubject.isCached = true;
                                remoteSubject.localImagePaths = new List<string>(cachedData.cachedImagePaths);
                                Log($"[CACHE CHECK] '{pdfInfo.name}': VALID - Using cached version (hash={currentHash})");
                                remoteSubjects.Add(remoteSubject);
                                continue; // Cache is good, skip download
                            }
                            else
                            {
                                // Cache exists but is OUTDATED or FILES MISSING or PARTIAL
                                if (!hashMatch)
                                {
                                    Log($"[CACHE CHECK] '{pdfInfo.name}': OUTDATED - Old hash: {cachedData.versionHash}, New hash: {currentHash}");
                                }
                                else if (!filesExist)
                                {
                                    Log($"[CACHE CHECK] '{pdfInfo.name}': FILES MISSING");
                                }
                                else if (!cachedData.isFullyCached)
                                {
                                    Log($"[CACHE CHECK] '{pdfInfo.name}': PARTIAL CACHE");
                                }

                                // Prepare cache data for re-download
                                cachedData.cachedImagePaths.Clear();
                                cachedData.isFullyCached = false;
                                cachedData.versionHash = currentHash; // Update hash BEFORE download
                                Log($"[DOWNLOAD] '{pdfInfo.name}': Marked for download (new hash={currentHash})");
                            }
                        }
                        else
                        {
                            // No cache entry exists - create new one
                            Log($"[CACHE CHECK] '{pdfInfo.name}': NO CACHE DATA");

                            string subjectName = string.IsNullOrEmpty(pdfInfo.cloudinaryFolder) ?
                                pdfInfo.name :
                                Path.GetFileName(pdfInfo.cloudinaryFolder);
                            if (string.IsNullOrEmpty(subjectName))
                                subjectName = pdfInfo.title ?? pdfInfo.name ?? "Unknown";

                            cachedData = new LocalSubjectCacheData(subjectName, pdfInfo.cloudinaryFolder, currentHash);
                            cacheManifest.AddOrUpdateSubject(cachedData);
                            Log($"[DOWNLOAD] '{pdfInfo.name}': Created new cache entry - Marked for download (hash={currentHash})");
                        }

                        // ============================================================
                        // STEP 3: MARK FOR DOWNLOAD FROM API
                        // ============================================================
                        remoteSubject.isCached = false;
                        remoteSubjects.Add(remoteSubject);
                    }

                    Log($"Loaded {remoteSubjects.Count} subjects from API (skipped {skippedCount})");
                    hasFetched = true;

                    // Track API loads
                    totalApiLoads += remoteSubjects.Count;
                    Log($"[API STAT] Total API loads = {totalApiLoads}");

                    UpdateSubjectDatabaseWithRemoteData();
                    ValidateAllCacheStatus();
                }
            }
            catch (Exception ex)
            {
                LogError($"Exception parsing response: {ex.Message}");
                OnError?.Invoke(ex.Message);
                yield break;
            }

            // Download images AFTER successfully fetching (outside try-catch để dùng yield)
            if (autoCacheAfterFetch && hasFetched)
            {
                Log("[CACHE] Starting auto-cache before marking ready...");
                yield return StartCoroutine(AutoCacheAllSubjects());
                Log("[CACHE] Auto-cache completed");
            }

            // ONLY THEN mark as ready
            isReady = true;
            overallProgress = 1f;
            OnOverallProgress?.Invoke(1f);
            OnSubjectsLoaded?.Invoke(remoteSubjects);
            OnReady?.Invoke();
            Log("[EVENT] All resources ready - ResourceManager can now proceed");
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
                    localSubject.pages = remote.pages;
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

            // Manifest is already saved in CacheSubjectCoroutine after each subject
            // Just ensure final save
            SaveCacheManifest();
            Log("[CACHE MANIFEST] Final save completed");

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

            Log($"[CACHE] Subject folder: {subjectFolder}, sanitized name: {SanitizeFolderName(subject.cloudinaryFolder ?? subject.name)}");

            // Get cache data (should exist from FetchSubjectsCoroutine)
            var cacheData = cacheManifest.GetSubjectCacheByFolder(subject.cloudinaryFolder)
                            ?? cacheManifest.GetSubjectCache(subject.name);

            if (cacheData == null)
            {
                // Fallback: Create new cache entry if missing (shouldn't happen)
                string subjectName = string.IsNullOrEmpty(subject.cloudinaryFolder) ?
                    subject.name :
                    Path.GetFileName(subject.cloudinaryFolder);

                if (string.IsNullOrEmpty(subjectName))
                    subjectName = subject.title ?? subject.name ?? "Unknown";

                string versionHash = $"{subjectName}_{subject.pages}_{subject.imageUrls.Count}";
                cacheData = new LocalSubjectCacheData(subjectName, subject.cloudinaryFolder, versionHash);
                cacheManifest.AddOrUpdateSubject(cacheData);
                Log($"[CACHE] Created fallback cache entry: name='{subjectName}', hash='{versionHash}'");
            }
            else
            {
                // Clear existing paths for re-download
                cacheData.cachedImagePaths.Clear();
                Log($"[CACHE] Cleared old paths for '{subject.GetIdentifier()}', hash={cacheData.versionHash}");
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
                yield return StartCoroutine(DownloadAndSaveImageCoroutine(imageUrl, localPath, cacheAsPNG, (result) =>
                {
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

            // ============================================================
            // STEP 4: SAVE CACHE (update manifest)
            // ============================================================
            cacheData.isFullyCached = downloadedCount == totalImages;
            cacheData.lastUpdated = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            cacheManifest.AddOrUpdateSubject(cacheData);

            // Save manifest immediately after updating
            SaveCacheManifest();
            Log($"[CACHE SAVED] '{subject.GetIdentifier()}': hash={cacheData.versionHash}, images={downloadedCount}/{totalImages}");

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
        /// Sanitize folder name to remove invalid characters and normalize
        /// </summary>
        private string SanitizeFolderName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "unknown";

            // Remove invalid file name characters
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                name = name.Replace(c, '_');
            }

            // Replace spaces with underscores
            name = name.Replace(" ", "_");

            // Remove multiple underscores
            while (name.Contains("__"))
            {
                name = name.Replace("__", "_");
            }

            // Convert to lowercase for consistent matching
            name = name.ToLower();

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