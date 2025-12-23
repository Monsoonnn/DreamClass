using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
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

        [Header("Cloudinary Optimization")]
        [SerializeField] private bool useCloudinaryOptimization = true;
        [SerializeField] private CloudinaryQuality cloudinaryQuality = CloudinaryQuality.Auto;
        [SerializeField] private CloudinaryFormat cloudinaryFormat = CloudinaryFormat.Auto;

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
        [Tooltip("Number of parallel downloads allowed (Increase for faster download speed)")]
        [SerializeField] private int maxConcurrentDownloads = 8;

        [Header("Bundle Settings")]
        [SerializeField] private bool checkLocalBundleFirst = true;
        [SerializeField] private string bundleStorePath = "StreamingAssets";
        [SerializeField] private string androidAddressableLabel = "PDFAssets_Android";

        [Header("Debug")]
        [SerializeField] private bool enableDebugLog = true;

        // Events
        public event Action<List<RemoteSubjectInfo>> OnSubjectsLoaded;
        public event Action<RemoteSubjectInfo, float> OnSubjectCacheProgress;
        public event Action<RemoteSubjectInfo> OnSubjectCacheComplete;
        public event Action<string> OnError;
        public event Action<string> OnLocalSubjectReady;
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

        // RUNTIME STATE: Separate from ScriptableObject
        public List<SubjectInfo> RuntimeSubjects { get; private set; } = new List<SubjectInfo>();

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

            // Init Runtime State from Database
            InitializeRuntimeSubjects();

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

            // PRIORITY 1: CHECK BUNDLE/ADDRESSABLES FIRST
            if (checkLocalBundleFirst)
            {
                // Start preloading bundles/addressables in background
                hasFetched = true;
                isPreloading = true;
                StartCoroutine(PreloadAllBundlesCoroutine());
                return;
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

        private void InitializeRuntimeSubjects()
        {
            RuntimeSubjects.Clear();
            if (subjectDatabase != null)
            {
                foreach (var subject in subjectDatabase.subjects)
                {
                    RuntimeSubjects.Add(subject.Clone());
                }
                Log($"[RUNTIME] Initialized {RuntimeSubjects.Count} runtime subjects from database.");
            }
        }

        // ... (Skipped Metadata Preload and PreloadFromCache methods)

        // ... (Skipped BackgroundPreloadAllSpritesCoroutine)

        /// <summary>
        /// Preload logic depending on Platform:
        /// - Editor: Load from Assets/PersistentData/Editor/PDFAssetBundles
        /// - Android: Load from StreamingAssets/Android/PDFAssetBundles
        /// </summary>
        private IEnumerator PreloadAllBundlesCoroutine()
        {
            Log("[BUNDLE PRELOAD] Starting preload...");

            if (Application.isEditor)
            {
                yield return StartCoroutine(PreloadFromEditorBundles());
            }
            else if (Application.platform == RuntimePlatform.Android)
            {
                yield return StartCoroutine(PreloadFromAndroidBundles());
            }
            else
            {
                Log($"[BUNDLE PRELOAD] Platform {Application.platform} not supported for specific preload. Skipping.");
            }

            preloadComplete = true;
            yield return StartCoroutine(CheckAndFetchMissingSubjects());
        }

        private IEnumerator PreloadFromEditorBundles()
        {
            // Use Application.dataPath to point to Assets/PersistentData/Editor/PDFAssetBundles
            string editorBundlePath = Path.Combine(Application.dataPath, "PersistentData/Editor/PDFAssetBundles");
            Log($"[BUNDLE PRELOAD] Mode: EDITOR - Path: {editorBundlePath}");

            if (!Directory.Exists(editorBundlePath))
            {
                Log($"[BUNDLE PRELOAD] Editor bundle path not found: {editorBundlePath}");
                yield break;
            }

            string[] bundleFiles = Directory.GetFiles(editorBundlePath);
            List<string> actualBundles = new List<string>();
            foreach (var file in bundleFiles)
            {
                if (!file.EndsWith(".manifest") && !file.EndsWith(".meta"))
                {
                    actualBundles.Add(file);
                }
            }

            Log($"[BUNDLE PRELOAD] Found {actualBundles.Count} bundles in Editor path");

            foreach (var bundlePath in actualBundles)
            {
                string bundleName = Path.GetFileName(bundlePath).ToLower();
                yield return StartCoroutine(LoadFromLocalBundle(bundlePath, bundleName, (sprites) =>
                {
                    if (sprites != null && sprites.Length > 0)
                    {
                        MatchSpritesToSubject(bundleName, sprites, bundlePath);
                    }
                }));
            }
        }

        private IEnumerator PreloadFromAndroidBundles()
        {
            string androidBundlePath = Path.Combine(Application.streamingAssetsPath, "Android/PDFAssetBundles");
            Log($"[BUNDLE PRELOAD] Mode: ANDROID - Path: {androidBundlePath}");

            // On Android, StreamingAssets are inside the APK, we can't use Directory.Exists directly
            // However, AssetBundle.LoadFromFileAsync works with the path
            // For listing files, it's tricky. Usually we'd need a manifest or known names.
            // But if we use AssetBundle.LoadFromFileAsync(androidBundlePath), it might work if it's a folder? No.
            
            // Assuming we have a manifest file to know what bundles to load, or we load "Android" manifest
            string manifestPath = Path.Combine(androidBundlePath, "PDFAssetBundles"); // The main bundle often named after the folder
            
            Log($"[BUNDLE PRELOAD] Loading main manifest from: {manifestPath}");
            
            var manifestRequest = AssetBundle.LoadFromFileAsync(manifestPath);
            yield return manifestRequest;
            AssetBundle manifestBundle = manifestRequest.assetBundle;

            if (manifestBundle == null)
            {
                LogError($"[BUNDLE PRELOAD] Failed to load main manifest bundle at {manifestPath}");
                yield break;
            }

            AssetBundleManifest manifest = manifestBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
            if (manifest == null)
            {
                LogError("[BUNDLE PRELOAD] Failed to load AssetBundleManifest object");
                manifestBundle.Unload(true);
                yield break;
            }

            string[] bundleNames = manifest.GetAllAssetBundles();
            Log($"[BUNDLE PRELOAD] Found {bundleNames.Length} bundles in Android manifest");

            foreach (var bundleName in bundleNames)
            {
                string bundlePath = Path.Combine(androidBundlePath, bundleName);
                yield return StartCoroutine(LoadFromLocalBundle(bundlePath, bundleName, (sprites) =>
                {
                    if (sprites != null && sprites.Length > 0)
                    {
                        MatchSpritesToSubject(bundleName, sprites, bundlePath);
                    }
                }));
            }

            manifestBundle.Unload(false);
        }
        [ProButton]
        private void MatchSpritesToSubject(string identifier, Sprite[] sprites = null, string fullPath = "")
        {
            // Use RuntimeSubjects instead of subjectDatabase.subjects
            if (RuntimeSubjects == null || RuntimeSubjects.Count == 0) return;

            foreach (var subject in RuntimeSubjects)
            {
                // Normalize CloudinaryFolder
                string cloudinaryNormalized = subject.cloudinaryFolder;
                if (!string.IsNullOrEmpty(cloudinaryNormalized))
                {
                    cloudinaryNormalized = cloudinaryNormalized.Replace("/", "_").ToLower();
                }

                // Checks
                bool nameMatch = subject.name.Equals(identifier, StringComparison.OrdinalIgnoreCase);
                
                // Check folder match
                bool pathMatch = false;
                if (!string.IsNullOrEmpty(fullPath) && !string.IsNullOrEmpty(subject.cloudinaryFolder))
                {
                     // Logic cũ: chỉ check EndsWith cơ bản
                     // pathMatch = fullPath.Replace("\\", "/").ToLower().EndsWith(subject.cloudinaryFolder.ToLower());

                     // Logic mới: Chuẩn hóa cả 2 về dạng phẳng (thay / và khoảng trắng bằng _) để so sánh
                     string normFullPath = fullPath.Replace("\\", "/").Replace("/", "_").Replace(" ", "_").ToLower();
                     string normFolder = subject.cloudinaryFolder.Replace("\\", "/").Replace("/", "_").Replace(" ", "_").ToLower();
                     
                     // Check xem đường dẫn file (đã chuẩn hóa) có kết thúc bằng folder (đã chuẩn hóa) không
                     pathMatch = normFullPath.EndsWith(normFolder);
                }
                
                bool cloudinaryMatch = !string.IsNullOrEmpty(cloudinaryNormalized) &&
                                       cloudinaryNormalized.Equals(identifier, StringComparison.OrdinalIgnoreCase);

                if (nameMatch || cloudinaryMatch || pathMatch)
                {
                    subject.SetBookPages(sprites);
                    subject.isCached = true;
                    Log($"[BUNDLE PRELOAD] MATCHED! Assigned {sprites.Length} sprites to {subject.name} (Runtime)");
                    Log($"[BUNDLE PRELOAD] MATCHED! found for {identifier} (Path: {fullPath})");
                    OnLocalSubjectReady?.Invoke(subject.name);
                    return; // Assigned to one subject, stop (assume 1-1)
                }
            }
            
            Log($"[BUNDLE PRELOAD] WARNING: No match found for {identifier} (Path: {fullPath})");
        }

        /// <summary>
        /// Check SubjectDatabase và fetch các subject chưa có bundle (isCached = false)
        /// CHECK: cloudinaryFolder match + validate cache manifest + verify localImagePaths
        /// </summary>
        private IEnumerator CheckAndFetchMissingSubjects()
        {
            if (RuntimeSubjects == null)
            {
                Log("[MISSING CHECK] RuntimeSubjects is null, skipping");
                TryMarkAsReady();
                yield break;
            }

            // Đếm số subject chưa cached
            int missingCount = 0;
            foreach (var subject in RuntimeSubjects)
            {
                if (!subject.isCached)
                {
                    missingCount++;
                }
            }

            if (missingCount == 0)
            {
                Log("[MISSING CHECK] All subjects are cached from bundles - no fetch needed");
                TryMarkAsReady();
                yield break;
            }

            Log($"[MISSING CHECK] Found {missingCount} subjects without bundle - fetching from API...");

            // Fetch từ API
            if (apiClient == null)
            {
                LogError("[MISSING CHECK] ApiClient not assigned!");
                TryMarkAsReady();
                yield break;
            }

            ApiRequest request = new ApiRequest(listEndpoint, "GET");
            ApiResponse response = null;

            yield return apiClient.SendRequest(request, (res) => response = res);

            if (response == null || !response.IsSuccess)
            {
                string error = response?.Error ?? "Unknown error";
                LogError($"[MISSING CHECK] Failed to fetch subjects: {error}");
                TryMarkAsReady();
                yield break;
            }

            // Parse JSON
            PDFListResponse listResponse = null;
            bool parseSuccess = false;

            try
            {
                listResponse = JsonUtility.FromJson<PDFListResponse>(response.Text);
                parseSuccess = true;
            }
            catch (Exception ex)
            {
                LogError($"[MISSING CHECK] Exception parsing response: {ex.Message}");
            }

            if (!parseSuccess || listResponse == null || listResponse.data == null)
            {
                TryMarkAsReady();
                yield break;
            }

            Log($"[MISSING CHECK] API returned {listResponse.data.Count} subjects");

            // Xử lý từng subject
            List<RemoteSubjectInfo> subjectsToCache = new List<RemoteSubjectInfo>();

            foreach (var pdfInfo in listResponse.data)
            {
                // STEP 1: CHỈ CHECK cloudinaryFolder match
                var localSubject = RuntimeSubjects.Find(s =>
                    !string.IsNullOrEmpty(s.cloudinaryFolder) &&
                    !string.IsNullOrEmpty(pdfInfo.cloudinaryFolder) &&
                    s.cloudinaryFolder.Equals(pdfInfo.cloudinaryFolder, StringComparison.OrdinalIgnoreCase));

                // Skip nếu không match hoặc đã cached
                if (localSubject == null || localSubject.isCached)
                {
                    continue;
                }

                Log($"[MISSING CHECK] Processing: {pdfInfo.name} (folder: {pdfInfo.cloudinaryFolder})");

                // STEP 2: CHECK CACHE MANIFEST
                var cachedData = cacheManifest.GetSubjectCacheByFolder(pdfInfo.cloudinaryFolder);

                RemoteSubjectInfo remoteSubject = new RemoteSubjectInfo(pdfInfo);
                string currentHash = pdfInfo.GetVersionHash();

                bool needsDownload = false;
                string downloadReason = "";

                if (cachedData == null)
                {
                    // Không có cache manifest
                    needsDownload = true;
                    downloadReason = "No cache manifest found";
                    Log($"[MISSING CHECK] '{pdfInfo.name}': {downloadReason}");
                }
                else
                {
                    // Có cache manifest - validate kỹ
                    Log($"[MISSING CHECK] '{pdfInfo.name}': Found cache manifest with {cachedData.cachedImagePaths.Count} paths");

                    // STEP 3: CHECK SỐ LƯỢNG TRANG
                    int expectedPages = pdfInfo.pages;
                    int cachedPages = cachedData.cachedImagePaths.Count;
                    int localPathCount = localSubject.localImagePaths?.Count ?? 0;

                    Log($"[MISSING CHECK] '{pdfInfo.name}': Pages - Expected:{expectedPages}, Cached:{cachedPages}, LocalPaths:{localPathCount}");

                    // Check số lượng không khớp với manifest
                    if (cachedPages != expectedPages)
                    {
                        needsDownload = true;
                        downloadReason = $"Page count mismatch with manifest (expected:{expectedPages}, cached:{cachedPages})";
                        Log($"[MISSING CHECK] '{pdfInfo.name}': {downloadReason}");
                    }
                    // Check hash version
                    else if (cachedData.versionHash != currentHash)
                    {
                        needsDownload = true;
                        downloadReason = $"Version hash mismatch (cached:{cachedData.versionHash}, current:{currentHash})";
                        Log($"[MISSING CHECK] '{pdfInfo.name}': {downloadReason}");
                    }
                    // STEP 4: CHECK localImagePaths của SubjectDatabase
                    else if (localPathCount == 0 || localSubject.localImagePaths == null || localSubject.localImagePaths.Count == 0)
                    {
                        // Không có localImagePaths trong SubjectDatabase - validate cache files từ manifest
                        Log($"[MISSING CHECK] '{pdfInfo.name}': No localImagePaths in SubjectDatabase - validating manifest cache files...");

                        bool allFilesExist = VerifyCachedFilesExist(cachedData.cachedImagePaths);

                        if (allFilesExist)
                        {
                            // Cache files hợp lệ - sử dụng cache và gán vào SubjectDatabase
                            Log($"[MISSING CHECK] '{pdfInfo.name}': All {cachedPages} manifest cache files valid - using cache");

                            remoteSubject.isCached = true;
                            remoteSubject.localImagePaths = new List<string>(cachedData.cachedImagePaths);
                            localSubject.isCached = true;
                            localSubject.localImagePaths = new List<string>(cachedData.cachedImagePaths);

                            // Load sprites nếu cần
                            if (!useLazyLoading)
                            {
                                yield return StartCoroutine(LoadSubjectSpritesOnDemand(pdfInfo.cloudinaryFolder, null));
                            }

                            continue; // Skip download
                        }
                        else
                        {
                            needsDownload = true;
                            downloadReason = "No localImagePaths and manifest cache files missing/invalid";
                            Log($"[MISSING CHECK] '{pdfInfo.name}': {downloadReason}");
                        }
                    }
                    // STEP 5: Validate localImagePaths SubjectDatabase
                    else
                    {
                        Log($"[CACHE CHECK] '{pdfInfo.name}': Validating localImagePaths (local:{localPathCount}, expected:{expectedPages})");

                        // Count mismatch → WARNING only (không fail cứng)
                        if (localPathCount != expectedPages)
                        {
                            Log($"[CACHE CHECK][WARN] '{pdfInfo.name}': Page count mismatch " +
                                $"(expected:{expectedPages}, local:{localPathCount})");
                        }

                        // Validate file existence
                        bool hasAnyValidFile = false;
                        bool allPathsValid = true;

                        foreach (var path in localSubject.localImagePaths)
                        {
                            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                            {
                                allPathsValid = false;
                                continue;
                            }

                            hasAnyValidFile = true;
                        }

                        // CACHE
                        if (hasAnyValidFile)
                        {
                            Log($"[CACHE CHECK] '{pdfInfo.name}': Usable local cache detected");

                            remoteSubject.isCached = true;
                            remoteSubject.localImagePaths = new List<string>(localSubject.localImagePaths);
                            localSubject.isCached = true;
                            localSubject.localImagePaths = new List<string>(localSubject.localImagePaths);

                            if (!useLazyLoading)
                            {
                                yield return StartCoroutine(
                                    LoadSubjectSpritesOnDemand(pdfInfo.cloudinaryFolder, null)
                                );
                            }

                            continue; // Skip download
                        }

                        // fallback manifest
                        Log($"[CACHE CHECK] '{pdfInfo.name}': No usable local files - checking manifest cache");

                        bool manifestValid = VerifyCachedFilesExist(cachedData.cachedImagePaths);

                        if (manifestValid)
                        {
                            Log($"[CACHE CHECK] '{pdfInfo.name}': Manifest cache valid - restoring cache");

                            remoteSubject.isCached = true;
                            remoteSubject.localImagePaths = new List<string>(cachedData.cachedImagePaths);
                            localSubject.isCached = true;
                            localSubject.localImagePaths = new List<string>(cachedData.cachedImagePaths);

                            if (!useLazyLoading)
                            {
                                yield return StartCoroutine(
                                    LoadSubjectSpritesOnDemand(pdfInfo.cloudinaryFolder, null)
                                );
                            }

                            continue; // Skip download
                        }

                        // DOWNLOAD
                        needsDownload = true;
                        downloadReason = "No usable local files and manifest cache missing";
                        Log($"[CACHE CHECK] '{pdfInfo.name}': {downloadReason}");
                    }

                }

                // STEP 6: PREPARE FOR DOWNLOAD
                if (needsDownload)
                {
                    Log($"[MISSING CHECK] '{pdfInfo.name}': NEEDS DOWNLOAD - Reason: {downloadReason}");

                    // Prepare/update cache data
                    if (cachedData == null)
                    {
                        string subjectName = Path.GetFileName(pdfInfo.cloudinaryFolder);
                        if (string.IsNullOrEmpty(subjectName))
                            subjectName = pdfInfo.title ?? pdfInfo.name ?? "Unknown";

                        cachedData = new LocalSubjectCacheData(subjectName, pdfInfo.cloudinaryFolder, currentHash);
                        cacheManifest.AddOrUpdateSubject(cachedData);
                    }
                    else
                    {
                        // Clear old cache data for redownload
                        cachedData.versionHash = currentHash;
                        cachedData.cachedImagePaths.Clear();
                        cachedData.isFullyCached = false;
                    }

                    remoteSubject.isCached = false;
                    subjectsToCache.Add(remoteSubject);
                    remoteSubjects.Add(remoteSubject);
                }
            }

            // STEP 7: DOWNLOAD
            if (subjectsToCache.Count > 0)
            {
                Log($"[MISSING CHECK] Downloading {subjectsToCache.Count} missing subjects...");

                int cachedCount = 0;
                for (int i = 0; i < subjectsToCache.Count; i++)
                {
                    var remote = subjectsToCache[i];
                    Log($"[MISSING CACHE] Caching '{remote.GetIdentifier()}' ({i + 1}/{subjectsToCache.Count})");

                    yield return StartCoroutine(CacheSubjectCoroutine(remote));
                    cachedCount++;

                    overallProgress = (float)cachedCount / subjectsToCache.Count;
                    OnOverallProgress?.Invoke(overallProgress);
                }

                Log($"[MISSING CHECK] Downloaded {cachedCount} subjects");
                UpdateRuntimeSubjectsWithRemoteData();
                UpdateSubjectDatabaseWithRemoteData();
            }
            else
            {
                Log("[MISSING CHECK] No subjects need downloading - all valid from cache");
            }

            hasFetched = true;
            totalApiLoads += subjectsToCache.Count;

            TryMarkAsReady();
            OnSubjectsLoaded?.Invoke(remoteSubjects);
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
            var localSubject = RuntimeSubjects?.Find(s => s.MatchesCloudinaryFolder(subjectPath));
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
                    var targetSubject = RuntimeSubjects?.Find(s => s.MatchesCloudinaryFolder(subjectPath));
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

        // ... (Skipped LoadFromLocalBundle)

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
            }
            ;

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
            var localSubject = RuntimeSubjects?.Find(s => s.MatchesCloudinaryFolder(subjectPath));
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

        #region Cache Utils

        [ProButton]
        public void OpenCacheFolder()
        {
            EnsureCacheDirectoryExists();
            Application.OpenURL("file://" + CachePath);
            Log($"Opened cache folder: {CachePath}");
        }

        [ProButton]
        public void ClearCache()
        {
            if (Directory.Exists(CachePath))
            {
                try
                {
                    Directory.Delete(CachePath, true);
                    Log($"Deleted cache folder: {CachePath}");
                }
                catch (Exception e)
                {
                    LogError($"Failed to delete cache folder: {e.Message}");
                }
            }

            // Re-create directory and manifest
            EnsureCacheDirectoryExists();
            cacheManifest = new SubjectCacheManifest();
            SaveCacheManifest();

            // Clear local runtime cache flags
            if (RuntimeSubjects != null)
            {
                foreach (var subject in RuntimeSubjects)
                {
                    subject.isCached = false;
                    if (subject.localImagePaths != null)
                        subject.localImagePaths.Clear();
                }
                // No need to save AssetDatabase since we are modifying RuntimeSubjects
            }

            Log("Cache cleared successfully");
        }

        [ProButton]
        public void LogCacheFolder()
        {
            Log($"Cache Folder Path: {CachePath}");
        }

        #endregion

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

            if (Application.isPlaying)
            {
                StartCoroutine(FetchSubjectsCoroutine(null, null));
            }
#if UNITY_EDITOR
            else
            {
                RunEditorCoroutine(FetchSubjectsCoroutine(null, null));
            }
#endif
        }

        private IEnumerator FetchSubjectsCoroutine(string gradeFilter = null, CloudinarySettings settings = null)
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
                        // Filter by Grade if specified
                        if (!string.IsNullOrEmpty(gradeFilter) && !string.Equals(pdfInfo.grade, gradeFilter, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (onlyLoadMatchingSubjects && RuntimeSubjects != null)
                        {
                            bool hasMatch = RuntimeSubjects.Exists(s => s.MatchesCloudinaryFolder(pdfInfo.cloudinaryFolder));
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

                    UpdateRuntimeSubjectsWithRemoteData();
                    UpdateSubjectDatabaseWithRemoteData();

                }
            }
            catch (Exception ex)
            {
                LogError($"Exception parsing response: {ex.Message}");
                OnError?.Invoke(ex.Message);
                yield break;
            }

            // Download images AFTER successfully fetching (outside try-catch để dùng yield)
            // If gradeFilter is set, we assume we WANT to download/cache them now.
            if ((autoCacheAfterFetch || !string.IsNullOrEmpty(gradeFilter)) && hasFetched)
            {
                Log("[CACHE] Starting auto-cache before marking ready...");
                yield return StartCoroutine(AutoCacheAllSubjects(settings));
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

        private void UpdateRuntimeSubjectsWithRemoteData()
        {
            if (RuntimeSubjects == null) return;

            foreach (var remote in remoteSubjects)
            {
                var localSubject = RuntimeSubjects.Find(s => s.MatchesCloudinaryFolder(remote.cloudinaryFolder));

                if (localSubject != null)
                {
                    localSubject.title = remote.title;
                    localSubject.grade = remote.grade;
                    localSubject.category = remote.category;
                    localSubject.pages = remote.pages;
                    localSubject.note = remote.note;
                }
            }
            // No Editor saving needed
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
        private IEnumerator AutoCacheAllSubjects(CloudinarySettings settings = null)
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
                yield return StartCoroutine(CacheSubjectCoroutine(remote, settings));

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
        private IEnumerator CacheSubjectCoroutine(RemoteSubjectInfo subject, CloudinarySettings settings = null)
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

            // Sort paths by index to keep order in list
            string[] finalPaths = new string[totalImages];
            List<DownloadJob> activeJobs = new List<DownloadJob>();

            // Loop through all images
            for (int i = 0; i < totalImages; i++)
            {
                string imageUrl = subject.imageUrls[i];
                
                // OPTIMIZATION: Cloudinary URL Transformation
                if (useCloudinaryOptimization)
                {
                    imageUrl = GenerateCloudinaryUrl(imageUrl, settings);
                }
                
                // Fix double-encoded URLs
                try
                {
                    if (imageUrl.Contains("%C3%83") || imageUrl.Contains("%C2%") || imageUrl.Contains("%25"))
                    {
                        imageUrl = Uri.UnescapeDataString(imageUrl);
                    }
                }
                catch { }

                string extension = cacheAsPNG ? ".png" : GetExtensionFromUrl(imageUrl);
                string fileName = $"page_{i:D3}{extension}";
                string localPath = Path.Combine(subjectFolder, fileName);

                // Start Job
                UnityWebRequest request = UnityWebRequest.Get(imageUrl);
                request.SendWebRequest(); // Start immediately

                DownloadJob job = new DownloadJob
                {
                    index = i,
                    request = request,
                    localPath = localPath,
                    convertToPNG = cacheAsPNG,
                    url = imageUrl
                };
                
                activeJobs.Add(job);
                Log($"[CACHE] Started download {i + 1}/{totalImages}: {imageUrl}");

                // Wait if max concurrent reached
                while (activeJobs.Count >= maxConcurrentDownloads)
                {
                    // Check for finished jobs
                    yield return ProcessFinishedJobs(activeJobs, finalPaths, (success) => {
                        if (success) downloadedCount++;
                        float progress = (float)downloadedCount / totalImages;
                        OnSubjectCacheProgress?.Invoke(subject, progress);
                    });
                }
            }

            // Wait for remaining jobs
            while (activeJobs.Count > 0)
            {
                 yield return ProcessFinishedJobs(activeJobs, finalPaths, (success) => {
                    if (success) downloadedCount++;
                    float progress = (float)downloadedCount / totalImages;
                    OnSubjectCacheProgress?.Invoke(subject, progress);
                });
            }

            // Collect valid paths
            cacheData.cachedImagePaths.Clear();
            foreach (var p in finalPaths)
            {
                if (!string.IsNullOrEmpty(p)) cacheData.cachedImagePaths.Add(p);
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

            // Update local runtime database
            var localSubject = RuntimeSubjects?.Find(s => s.MatchesCloudinaryFolder(subject.cloudinaryFolder));
            if (localSubject != null)
            {
                localSubject.isCached = subject.isCached;
                localSubject.localImagePaths = new List<string>(subject.localImagePaths);
            }

            Log($"[CACHE] Completed '{subject.GetIdentifier()}': {downloadedCount}/{totalImages} images");
            OnSubjectCacheComplete?.Invoke(subject);
        }

        private IEnumerator ProcessFinishedJobs(List<DownloadJob> activeJobs, string[] finalPaths, Action<bool> onComplete)
        {
            // Wait a frame
            yield return null;

            for (int i = activeJobs.Count - 1; i >= 0; i--)
            {
                var job = activeJobs[i];
                if (job.request.isDone)
                {
                    bool success = false;
                    
                    if (job.request.result == UnityWebRequest.Result.Success)
                    {
                        try
                        {
                            byte[] data = job.request.downloadHandler.data;
                            if (job.convertToPNG)
                            {
                                // Decode WebP/Image -> Texture -> PNG
                                WebP.Error error;
                                Texture2D texture = WebP.Texture2DExt.CreateTexture2DFromWebP(data, lMipmaps: false, lLinear: false, out error);
                                if (error == WebP.Error.Success && texture != null)
                                {
                                     // Encode and Save
                                     byte[] pngData = texture.EncodeToPNG();
                                     File.WriteAllBytes(job.localPath, pngData);
                                     UnityEngine.Object.Destroy(texture);
                                     success = true;
                                }
                                else
                                {
                                     LogError($"[CACHE] Failed to decode image {job.index}: {error}");
                                }
                            }
                            else
                            {
                                File.WriteAllBytes(job.localPath, data);
                                success = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            LogError($"[CACHE] Failed to save {job.index}: {ex.Message}");
                        }
                    }
                    else
                    {
                        LogError($"[CACHE] Failed to download {job.index}: {job.request.error}");
                    }

                    if (success)
                    {
                        finalPaths[job.index] = job.localPath;
                        Log($"[CACHE] Finished {job.index + 1}");
                    }

                    job.request.Dispose();
                    activeJobs.RemoveAt(i);
                    onComplete?.Invoke(success);
                }
            }
        }

        /// <summary>
        /// Generates an optimized Cloudinary URL based on current settings
        /// </summary>
        private string GenerateCloudinaryUrl(string originalUrl, CloudinarySettings settings = null)
        {
            if (string.IsNullOrEmpty(originalUrl)) return originalUrl;
            if (!originalUrl.Contains("res.cloudinary.com")) return originalUrl; // Only optimize Cloudinary URLs

            // 1. Check if already has transformations (simple check for now)
            // Cloudinary URLs: /image/upload/v1234... or /image/upload/w_100.../v1234...
            // We want to insert transformations after /upload/

            string uploadToken = "/upload/";
            int uploadIndex = originalUrl.IndexOf(uploadToken, StringComparison.OrdinalIgnoreCase);

            if (uploadIndex == -1) return originalUrl;

            // Build transformation string
            List<string> paramsList = new List<string>();

            // Get settings or defaults
            int targetSize = settings != null ? settings.textureSize : maxTextureSize;
            CloudinaryQuality targetQuality = settings != null ? settings.quality : cloudinaryQuality;
            CloudinaryFormat targetFormat = settings != null ? settings.format : cloudinaryFormat;

            // SIZE (Resize/Crop)
            // Use maxTextureSize as the target width/height
            // c_fill is usually good for fitting into a box, but for PDF pages we likely want to limit width/height (c_limit or c_fit)
            // User requested "custom fill" -> likely c_fill or c_fit with w/h
            if (targetSize > 0)
            {
                paramsList.Add($"w_{targetSize}");
                paramsList.Add($"h_{targetSize}");
                paramsList.Add("c_limit"); // Use limit to maintain aspect ratio and not upscale
            }

            // QUALITY
            string q = GetQualityString(targetQuality);
            if (!string.IsNullOrEmpty(q)) paramsList.Add(q);

            // FORMAT
            string f = GetFormatString(targetFormat);
            if (!string.IsNullOrEmpty(f)) paramsList.Add(f);

            if (paramsList.Count == 0) return originalUrl;

            string transformString = string.Join(",", paramsList);

            // Insert transformation
            string prefix = originalUrl.Substring(0, uploadIndex + uploadToken.Length);
            string suffix = originalUrl.Substring(uploadIndex + uploadToken.Length);

            return $"{prefix}{transformString}/{suffix}";
        }

        private string GetQualityString(CloudinaryQuality quality)
        {
            switch (quality)
            {
                case CloudinaryQuality.Auto: return "q_auto";
                case CloudinaryQuality.Best: return "q_auto:best";
                case CloudinaryQuality.Good: return "q_auto:good";
                case CloudinaryQuality.Eco: return "q_auto:eco";
                case CloudinaryQuality.Low: return "q_auto:low";
                case CloudinaryQuality.Fixed_80: return "q_80";
                case CloudinaryQuality.Fixed_60: return "q_60";
                default: return "q_auto";
            }
        }

        private string GetFormatString(CloudinaryFormat format)
        {
            switch (format)
            {
                case CloudinaryFormat.Auto: return "f_auto";
                case CloudinaryFormat.Jpg: return "f_jpg";
                case CloudinaryFormat.Png: return "f_png";
                case CloudinaryFormat.WebP: return "f_webp";
                default: return "f_auto";
            }
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

        public void DownloadSubject(string subjectIdentifier, CloudinarySettings settings = null)
        {
            if (Application.isPlaying)
            {
                StartCoroutine(DownloadSubjectCoroutine(subjectIdentifier, settings));
            }
#if UNITY_EDITOR
            else
            {
                RunEditorCoroutine(DownloadSubjectCoroutine(subjectIdentifier, settings));
            }
#endif
        }

        [ProButton]
        public void DownloadSubjectManual(string identifier, int size = 1024, CloudinaryQuality quality = CloudinaryQuality.Auto, CloudinaryFormat format = CloudinaryFormat.Auto)
        {
             CloudinarySettings settings = new CloudinarySettings(size, quality, format);
             DownloadSubject(identifier, settings);
        }

        private IEnumerator DownloadSubjectCoroutine(string subjectIdentifier, CloudinarySettings settings)
        {
            if (string.IsNullOrEmpty(subjectIdentifier))
            {
                LogError("[DownloadSubject] Identifier cannot be empty");
                yield break;
            }

            if (apiClient == null)
            {
                LoadApiClient();
                if (apiClient == null)
                {
                    LogError("[DownloadSubject] ApiClient not found");
                    yield break;
                }
            }

            // Ensure remote subjects are fetched
            if (remoteSubjects == null || remoteSubjects.Count == 0)
            {
                Log("[DownloadSubject] Fetching list from API...");
                ApiRequest request = new ApiRequest(listEndpoint, "GET");
                ApiResponse response = null;
                yield return apiClient.SendRequest(request, (res) => response = res);

                if (response != null && response.IsSuccess)
                {
                    try
                    {
                        PDFListResponse listResponse = JsonUtility.FromJson<PDFListResponse>(response.Text);
                        if (listResponse != null && listResponse.data != null)
                        {
                            remoteSubjects.Clear();
                            foreach (var pdfInfo in listResponse.data)
                            {
                                remoteSubjects.Add(new RemoteSubjectInfo(pdfInfo));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"[DownloadSubject] Parse error: {ex.Message}");
                    }
                }
            }

            // Find remote subject
            var remoteSubject = remoteSubjects.Find(r => r.MatchesCloudinaryFolder(subjectIdentifier) || r.name == subjectIdentifier);
            if (remoteSubject == null)
            {
                LogError($"[DownloadSubject] Remote subject not found for: {subjectIdentifier}");
                yield break;
            }

            Log($"[DownloadSubject] Downloading {remoteSubject.name} with settings: Size={(settings != null ? settings.textureSize : maxTextureSize)}, Quality={(settings != null ? settings.quality : cloudinaryQuality)}");

            yield return StartCoroutine(CacheSubjectCoroutine(remoteSubject, settings));

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UpdateSubjectDatabaseWithRemoteData();
            }
#endif
        }

#if UNITY_EDITOR
        [ProButton]
        public void DownloadGrade(string grade)
        {
             if (string.IsNullOrEmpty(grade))
             {
                 LogError("[DownloadGrade] Grade cannot be empty");
                 return;
             }

             // Get settings from class fields
             CloudinarySettings settings = new CloudinarySettings(maxTextureSize, cloudinaryQuality, cloudinaryFormat);
             
             if (Application.isPlaying)
             {
                 StartCoroutine(FetchSubjectsCoroutine(grade, settings));
             }
             else
             {
                 // Ensure RuntimeSubjects is valid for matching
                 if (RuntimeSubjects == null || RuntimeSubjects.Count == 0) InitializeRuntimeSubjects();
                 
                 StartCoroutine(FetchSubjectsCoroutine(grade, settings));
             }
        }

        private static void RunEditorCoroutine(IEnumerator routine)
        {
            UnityEditor.EditorApplication.CallbackFunction update = null;
            Stack<IEnumerator> stack = new Stack<IEnumerator>();
            stack.Push(routine);

            update = () =>
            {
                if (stack.Count == 0)
                {
                    UnityEditor.EditorApplication.update -= update;
                    return;
                }

                IEnumerator current = stack.Peek();
                object currentYield = current.Current;

                if (currentYield is UnityEngine.AsyncOperation asyncOp && !asyncOp.isDone)
                {
                    return; // Wait for AsyncOperation
                }
                if (currentYield is IEnumerator nested)
                {
                    stack.Push(nested);
                    return;
                }

                if (!current.MoveNext())
                {
                    stack.Pop();
                }
            };
            UnityEditor.EditorApplication.update += update;
        }
#endif

        private void Log(string message)
        {
            if (enableDebugLog)
                Debug.Log($"[PDFSubjectService] {message}");
        }

        private void LogError(string message)
        {
            Debug.LogError($"[PDFSubjectService] {message}");
        }

    }
}