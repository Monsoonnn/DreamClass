using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System;
using System.Collections.Generic;
using com.cyborgAssets.inspectorButtonPro;
using DreamClass.Subjects;
using System.Collections;
using DreamClass.LearningLecture;
using DreamClass.Network;
using LoginMgrNS = DreamClass.LoginManager;

namespace DreamClass.Lecture
{
    /// <summary>
    /// UI Manager - CHỈ quản lý data và swap UI panels
    /// Waits for LoginManager before fetching remote subjects
    /// </summary>
    public class LearningModeManager : NewMonobehavior
    {
        #region Data: Mode
        public enum LearningMode { None, KiemTra, OnTap }

        [Header("Current Mode")]
        public LearningMode currentMode;

        public ExamUIManager examUIManager;

        public void SetMode(LearningMode mode)
        {
            currentMode = mode;
        }


        public void SetOnTapMode() => SetMode(LearningMode.OnTap);
        public void SetKiemTraMode()
        {
            SetMode(LearningMode.KiemTra);
            examUIManager.ShowSubjectSelection();
        } 
        public LearningMode GetMode() => currentMode;
        #endregion

        #region Data: Subject
        [Header("Subject Data")]
        public SubjectDatabase subjectDatabase;
        public SubjectInfo currentSubject;
        public int currentSubjectIndex = -1;

        [Header("Remote Subject Service")]
        public PDFSubjectService pdfSubjectService;
        public ApiClient apiClient;

        [Header("WebP Loader")]
        [Tooltip("Optional - for loading WebP from URLs directly")]
        public WebPBookLoader webPBookLoader;

        [Header("Auto Fetch Settings")]
        [SerializeField] private bool autoFetchOnLogin = true;


        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.LoadApiClient();
            this.LoadPDFSubjectService();
            this.LoadWebPBookLoader();
            this.LoadLectureSpawner();
        }

        private void LoadApiClient()
        {
            if(apiClient != null) return;
            apiClient = GameObject.FindAnyObjectByType<ApiClient>();
        }

        private void LoadWebPBookLoader()
        {
            if(webPBookLoader != null) return;
            if(bookCtrl != null && bookCtrl.bookObject != null)
            {
                webPBookLoader = bookCtrl.bookObject.GetComponentInChildren<WebPBookLoader>();
            }
            if(webPBookLoader == null)
            {
                webPBookLoader = GameObject.FindAnyObjectByType<WebPBookLoader>();
            }
        }

        private void LoadPDFSubjectService()
        {
            if(pdfSubjectService != null) return;
            pdfSubjectService = GetComponentInChildren<PDFSubjectService>();
            if(pdfSubjectService == null)
                pdfSubjectService = GameObject.FindAnyObjectByType<PDFSubjectService>();
        }

        private void LoadLectureSpawner()
        {
            if(lectureSpawner != null) return;
            if(lectureSelectionPanel != null)
            {
                lectureSpawner = lectureSelectionPanel.GetComponentInChildren<LectureSpawner>();
            }
            if(lectureSpawner == null)
            {
                lectureSpawner = GameObject.FindAnyObjectByType<LectureSpawner>();
            }
        }

        protected override void Start()
        {
            base.Start();

            // Subscribe to PDFSubjectService.OnReady
            PDFSubjectService.OnReady += OnPDFSubjectsReady;

            // Check if already logged in and subjects not fetched yet
            if (autoFetchOnLogin && IsLoggedIn() && !PDFSubjectService.IsReady)
            {
                Debug.Log("[LearningModeManager] Already logged in, initializing PDF subjects...");
                pdfSubjectService?.InitializeAfterLogin();
            }
            // If subjects already ready
            else if (PDFSubjectService.IsReady)
            {
                OnPDFSubjectsReady();
            }
        }

        private void OnDestroy()
        {
            PDFSubjectService.OnReady -= OnPDFSubjectsReady;
        }

        private void OnPDFSubjectsReady()
        {
            Debug.Log("[LearningModeManager] PDF Subjects ready!");
            // Optionally refresh UI or notify spawners
        }

        private bool IsLoggedIn()
        {
            return LoginMgrNS.LoginManager.Instance != null && 
                   LoginMgrNS.LoginManager.Instance.IsLoggedIn();
        }

        public void SetCurrentSubject(int index)
        {
            var allSubjects = GetAllSubjects();
            if (allSubjects == null || index < 0 || index >= allSubjects.Count)
            {
                Debug.LogError($"Invalid subject index: {index}");
                return;
            }

            currentSubjectIndex = index;
            currentSubject = allSubjects[index];
            Debug.Log($"Current subject set to: {currentSubject.name}");

            // Load sprites to BookSpriteManager
            LoadSpritesToBookManager(currentSubject);
        }

        public void SetCurrentSubject(SubjectInfo subject)
        {
            currentSubject = subject;
            var allSubjects = GetAllSubjects();
            currentSubjectIndex = allSubjects.IndexOf(subject);
            Debug.Log($"Current subject set to: {currentSubject.name}");

            // Load sprites to BookSpriteManager
            LoadSpritesToBookManager(currentSubject);
        }

        /// <summary>
        /// Load sprites từ SubjectInfo.bookPages lên BookSpriteManager
        /// CHỈ load từ cache - không load từ URL
        /// </summary>
        private void LoadSpritesToBookManager(SubjectInfo subject)
        {
            if (bookCtrl == null || bookCtrl.bookObject == null)
            {
                Debug.LogError("[LearningModeManager] BookCtrl or bookObject not assigned!");
                return;
            }

            var spriteManager = bookCtrl.bookObject.GetComponentInChildren<BookSpriteManager>();
            if (spriteManager == null)
            {
                Debug.LogError("[LearningModeManager] BookSpriteManager not found!");
                return;
            }

            // Check if subject has loaded sprites (already in memory)
            if (subject.HasLoadedSprites())
            {
                spriteManager.bookPages = subject.bookPages;
                spriteManager.currentPage = 2; // Reset to first page
                spriteManager.UpdateSprites();
                Debug.Log($"[LearningModeManager] Loaded {subject.bookPages.Length} sprites to BookSpriteManager for: {subject.name}");
            }
            // If not loaded but cached, load from cache
            else if (subject.isCached)
            {
                Debug.Log($"[LearningModeManager] Subject cached but sprites not loaded, loading from cache...");
                StartCoroutine(LoadCachedSpritesForSubject(subject));
            }
            // Has path but NOT cached - cannot load (must download cache first)
            else if (!string.IsNullOrEmpty(subject.path) && !subject.isCached)
            {
                Debug.LogWarning($"[LearningModeManager] Subject NOT cached, cannot load: {subject.name}. Must download cache first.");
                // Do nothing - subject should not be selectable if not cached
            }
            else
            {
                Debug.Log($"[LearningModeManager] Local subject without API data: {subject.name}");
            }
        }

        public SubjectInfo GetCurrentSubject() => currentSubject;

        /// <summary>
        /// Get all subjects from SubjectDatabase
        /// PDFSubjectService đã update remote data trực tiếp vào database rồi
        /// </summary>
        public List<SubjectInfo> GetAllSubjects()
        {
            if (subjectDatabase == null || subjectDatabase.subjects == null)
            {
                Debug.LogWarning("[LearningModeManager] SubjectDatabase is null!");
                return new List<SubjectInfo>();
            }

            return subjectDatabase.subjects;
        }

        /// <summary>
        /// Fetch remote subjects từ API
        /// </summary>
        [ProButton]
        public void FetchRemoteSubjects()
        {
            if (pdfSubjectService == null)
            {
                Debug.LogError("PDFSubjectService not assigned!");
                return;
            }

            pdfSubjectService.OnSubjectsLoaded += OnRemoteSubjectsLoaded;
            pdfSubjectService.OnError += OnRemoteSubjectsError;
            pdfSubjectService.FetchSubjects();
        }

        private void OnRemoteSubjectsLoaded(List<RemoteSubjectInfo> subjects)
        {
            Debug.Log($"Loaded {subjects.Count} remote subjects");
            pdfSubjectService.OnSubjectsLoaded -= OnRemoteSubjectsLoaded;
            pdfSubjectService.OnError -= OnRemoteSubjectsError;

            // Optionally refresh UI
            // ShowSubjectSelection();
        }

        private void OnRemoteSubjectsError(string error)
        {
            Debug.LogError($"Failed to load remote subjects: {error}");
            pdfSubjectService.OnSubjectsLoaded -= OnRemoteSubjectsLoaded;
            pdfSubjectService.OnError -= OnRemoteSubjectsError;
        }

        /// <summary>
        /// Cache subject images và load vào BookSpriteManager
        /// </summary>
        public void CacheAndLoadSubject(SubjectInfo subject)
        {
            if (pdfSubjectService == null || string.IsNullOrEmpty(subject.path))
            {
                Debug.LogWarning("Cannot cache subject without path or service not available");
                return;
            }

            // Find by path first, then by name
            var remoteSubject = pdfSubjectService.RemoteSubjects.Find(s => 
                (!string.IsNullOrEmpty(subject.path) && s.path == subject.path) || s.name == subject.name);
            
            if (remoteSubject == null)
            {
                Debug.LogError($"Remote subject not found: {subject.name} (path: {subject.path})");
                return;
            }

            StartCoroutine(CacheAndLoadCoroutine(remoteSubject));
        }

        private IEnumerator CacheAndLoadCoroutine(RemoteSubjectInfo remoteSubject)
        {
            // Cache nếu chưa cached
            if (!remoteSubject.isCached)
            {
                bool cacheComplete = false;
                pdfSubjectService.OnSubjectCacheComplete += (s) => 
                {
                    if (s.name == remoteSubject.name)
                        cacheComplete = true;
                };

                pdfSubjectService.CacheSubject(remoteSubject);

                // Wait for cache complete
                while (!cacheComplete)
                {
                    yield return null;
                }
            }

            // After cache complete, sprites should be loaded in SubjectDatabase
            // Update currentSubject with latest data
            if (currentSubject != null && currentSubject.MatchesPath(remoteSubject.path))
            {
                // Find updated subject from database
                var updatedSubject = subjectDatabase.subjects.Find(s => s.MatchesPath(remoteSubject.path));
                if (updatedSubject != null && updatedSubject.HasLoadedSprites())
                {
                    LoadSpritesToBookManager(updatedSubject);
                }
            }
        }

        private IEnumerator LoadCachedSpritesForSubject(SubjectInfo subject)
        {
            if (pdfSubjectService == null || bookCtrl == null)
            {
                Debug.LogError("PDFSubjectService or BookCtrl not assigned!");
                yield break;
            }

            // Find by path first, then by name
            var remoteSubject = pdfSubjectService.RemoteSubjects.Find(s => 
                (!string.IsNullOrEmpty(subject.path) && s.path == subject.path) || s.name == subject.name);
            
            if (remoteSubject == null)
            {
                Debug.LogError($"Remote subject not found: {subject.name} (path: {subject.path})");
                yield break;
            }

            Sprite[] sprites = null;
            yield return pdfSubjectService.LoadCachedSpritesCoroutine(remoteSubject, (result) => sprites = result);

            if (sprites != null && sprites.Length > 0)
            {
                // Update subject with loaded sprites
                subject.SetBookPages(sprites);

                // Update BookSpriteManager
                var spriteManager = bookCtrl.bookObject.GetComponentInChildren<BookSpriteManager>();
                if (spriteManager != null)
                {
                    spriteManager.bookPages = sprites;
                    spriteManager.currentPage = 2;
                    spriteManager.UpdateSprites();
                    Debug.Log($"[LearningModeManager] Loaded {sprites.Length} sprites for subject: {subject.name}");
                }

                // Also update in SubjectDatabase
                var dbSubject = subjectDatabase.subjects.Find(s => s.MatchesPath(subject.path));
                if (dbSubject != null)
                {
                    dbSubject.SetBookPages(sprites);
                    dbSubject.isCached = true;
                }
            }
        }

        // URL loading methods removed - all sprites must be loaded from cache
        // Use PDFSubjectService.CacheSubject() to download and cache first
        #endregion

        #region Data: Lecture
        [Header("Lecture Data")]
        public CSVLectureInfo currentLecture;
        public int currentLectureIndex = -1;

        public void SetCurrentLecture(int index)
        {
            if (currentSubject == null || index < 0 || index >= currentSubject.lectures.Count)
            {
                Debug.LogError($"Invalid lecture index: {index}");
                return;
            }

            currentLectureIndex = index;
            currentLecture = currentSubject.lectures[index];

            StartCoroutine(DelayedJump(currentLecture.page));

            Debug.Log($"Current lecture set to: {currentLecture.lectureName} (Page: {currentLecture.page})");
        }

        public void SetCurrentLecture(CSVLectureInfo lecture)
        {
            currentLecture = lecture;
            if (currentSubject != null)
            {
                currentLectureIndex = currentSubject.lectures.IndexOf(lecture);
            }

            // Gọi coroutine delay
            StartCoroutine(DelayedJump(lecture.page));

            Debug.Log($"Current lecture set to: {currentLecture.lectureName}");
        }

        private IEnumerator DelayedJump(int page)
        {
            // Wait for 0.5 seconds
            yield return new WaitForSeconds(1.5f);

            // Jump to page after delay
            var autoFlip = bookCtrl.bookObject.GetComponentInChildren<AutoFlipVR>();
            if (autoFlip != null)
            {
                autoFlip.JumpToPage(page);
            }

            Debug.Log($"LearningModeManager: Jumped to page: {page}");

        }


        public CSVLectureInfo GetCurrentLecture() => currentLecture;

        public List<CSVLectureInfo> GetCurrentLectures()
        {
            return currentSubject?.lectures;
        }
        #endregion

        #region UI Panel Management
        [Header("UI Panels")]
        public GameObject modeSelectionPanel;
        public GameObject subjectSelectionPanel;
        public GameObject lectureSelectionPanel;

        [Header("Spawners")]
        public LectureSpawner lectureSpawner;

        public LearningBookCtrl bookCtrl;

        private GameObject currentActivePanel;

        /// <summary>
        /// Swap giữa các UI panels
        /// </summary>
        public void SwapToPanel(GameObject targetPanel)
        {
            if (currentActivePanel != null)
                currentActivePanel.SetActive(false);

            if (targetPanel != null)
            {
                targetPanel.SetActive(true);
                currentActivePanel = targetPanel;
                SetCanvasInteractionActive(true);
            }
            else
            {   
                currentActivePanel = null;
                SetCanvasInteractionActive(false);
            }
        }

        [ProButton]
        public void ShowModeSelection() => SwapToPanel(modeSelectionPanel);

        [ProButton]
        public void ShowSubjectSelection() => SwapToPanel(subjectSelectionPanel);

        [ProButton]
        public void ShowLectureSelection()
        {
            SwapToPanel(lectureSelectionPanel);
            
            // Auto-spawn lectures for current subject
            if (lectureSpawner != null && currentSubject != null)
            {
                lectureSpawner.SpawnLectures();
            }
            else if (lectureSpawner == null)
            {
                Debug.LogWarning("[LearningModeManager] LectureSpawner not assigned!");
            }
        }

        [ProButton]
        public void ShowBookPanel()
        {
            SwapToPanel(null);
            bookCtrl.SwitchToBookMode();
        }

        [ProButton]
        public void HideAllPanels() => SwapToPanel(null);
        #endregion

        [Header("Canvas Interaction")]
        public GameObject rayCanvasInteraction;
        public GameObject pokeCanvasInteraction;

        private void SetCanvasInteractionActive(bool active)
        {
            if (rayCanvasInteraction != null)
                rayCanvasInteraction.SetActive(active);

            if (pokeCanvasInteraction != null)
                pokeCanvasInteraction.SetActive(active);
        }

        #region Debug

        [ProButton]
        public void DebugCurrentState()
        {
            Debug.Log("=== Learning Mode Manager State ===");
            Debug.Log($"Mode: {currentMode}");
            Debug.Log($"Subject: {(currentSubject != null ? currentSubject.name : "None")}");
            Debug.Log($"Lecture: {(currentLecture != null ? currentLecture.lectureName : "None")}");
            Debug.Log($"Active Panel: {(currentActivePanel != null ? currentActivePanel.name : "None")}");
            Debug.Log($"Total Subjects: {(subjectDatabase != null ? subjectDatabase.subjects.Count : 0)}");
        }
        #endregion
    }
}