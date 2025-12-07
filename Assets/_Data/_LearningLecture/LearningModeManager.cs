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

namespace DreamClass.Lecture
{
    /// <summary>
    /// UI Manager - CHỈ quản lý data và swap UI panels
    /// PDFSubjectService auto-fetches on game start
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
        [Tooltip("Optional - for loading WebP from URLs directly (legacy)")]
        public WebPBookLoader webPBookLoader;

        [Header("Book Page Loader")]
        [Tooltip("Recommended - for loading sprites from cache")]
        public BookPageLoader bookPageLoader;


        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.LoadApiClient();
            this.LoadPDFSubjectService();
            this.LoadWebPBookLoader();
            this.LoadBookPageLoader();
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

        private void LoadBookPageLoader()
        {
            if(bookPageLoader != null) return;
            if(bookCtrl != null && bookCtrl.bookObject != null)
            {
                bookPageLoader = bookCtrl.bookObject.GetComponentInChildren<BookPageLoader>();
            }
            if(bookPageLoader == null)
            {
                bookPageLoader = GameObject.FindAnyObjectByType<BookPageLoader>();
            }
        }

        private void LoadPDFSubjectService()
        {
            if(pdfSubjectService != null) return;
            // Use Singleton instance
            pdfSubjectService = PDFSubjectService.Instance;
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

            // Check if PDFSubjectService is already ready
            if (PDFSubjectService.IsReady)
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
        /// Ưu tiên lazy loading - load sprites async khi click vào sách
        /// </summary>
        private void LoadSpritesToBookManager(SubjectInfo subject)
        {
            // Use lazy loading via BookPageLoader (load sprites async when clicked)
            if (bookPageLoader != null)
            {
                bookPageLoader.LoadSubjectWithLazyLoading(subject, (success) =>
                {
                    if (success)
                    {
                        Debug.Log($"[LearningModeManager] Lazy loaded sprites for: {subject.name}");
                    }
                    else
                    {
                        Debug.LogError($"[LearningModeManager] Failed to lazy load subject: {subject.name}");
                    }
                });
                return;
            }

            // Fallback: Direct load to BookSpriteManager
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
            else if (!string.IsNullOrEmpty(subject.cloudinaryFolder) && !subject.isCached)
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
            if (pdfSubjectService == null || string.IsNullOrEmpty(subject.cloudinaryFolder))
            {
                Debug.LogWarning("Cannot cache subject without path or service not available");
                return;
            }

            // Find by path first, then by name
            var remoteSubject = pdfSubjectService.RemoteSubjects.Find(s => 
                (!string.IsNullOrEmpty(subject.cloudinaryFolder) && s.cloudinaryFolder == subject.cloudinaryFolder) || s.name == subject.name);
            
            if (remoteSubject == null)
            {
                Debug.LogError($"Remote subject not found: {subject.name} (path: {subject.cloudinaryFolder})");
                return;
            }

            StartCoroutine(CacheAndLoadCoroutine(remoteSubject));
        }

        private IEnumerator CacheAndLoadCoroutine(RemoteSubjectInfo remoteSubject)
        {
            // Load sprites on demand (PDFSubjectService sẽ tự cache nếu cần)
            Sprite[] sprites = null;
            yield return pdfSubjectService.LoadSubjectSpritesOnDemand(remoteSubject.cloudinaryFolder, (result) => sprites = result);

            // After load complete, update currentSubject with latest data
            if (currentSubject != null && currentSubject.MatchesCloudinaryFolder(remoteSubject.cloudinaryFolder))
            {
                // Find updated subject from database
                var updatedSubject = subjectDatabase.subjects.Find(s => s.MatchesCloudinaryFolder(remoteSubject.cloudinaryFolder));
                if (updatedSubject != null && updatedSubject.HasLoadedSprites())
                {
                    LoadSpritesToBookManager(updatedSubject);
                }
            }
        }

        private IEnumerator LoadCachedSpritesForSubject(SubjectInfo subject)
        {
            if (pdfSubjectService == null)
            {
                Debug.LogError("PDFSubjectService not assigned!");
                yield break;
            }

            // Find by cloudinaryFolder first, then by name
            var remoteSubject = pdfSubjectService.RemoteSubjects.Find(s => 
                (!string.IsNullOrEmpty(subject.cloudinaryFolder) && s.cloudinaryFolder == subject.cloudinaryFolder) || s.name == subject.name);
            
            if (remoteSubject == null)
            {
                Debug.LogError($"Remote subject not found: {subject.name} (cloudinaryFolder: {subject.cloudinaryFolder})");
                yield break;
            }

            Sprite[] sprites = null;
            yield return pdfSubjectService.LoadSubjectSpritesOnDemand(remoteSubject.cloudinaryFolder, (result) => sprites = result);

            if (sprites != null && sprites.Length > 0)
            {
                // Update subject with loaded sprites
                subject.SetBookPages(sprites);

                // Use BookPageLoader if available
                if (bookPageLoader != null)
                {
                    bookPageLoader.LoadFromSubject(subject);
                }
                else if (bookCtrl != null && bookCtrl.bookObject != null)
                {
                    // Fallback: Update BookSpriteManager directly
                    var spriteManager = bookCtrl.bookObject.GetComponentInChildren<BookSpriteManager>();
                    if (spriteManager != null)
                    {
                        spriteManager.bookPages = sprites;
                        spriteManager.currentPage = 2;
                        spriteManager.UpdateSprites();
                        Debug.Log($"[LearningModeManager] Loaded {sprites.Length} sprites for subject: {subject.name}");
                    }
                }

                // Also update in SubjectDatabase
                var dbSubject = subjectDatabase.subjects.Find(s => s.MatchesCloudinaryFolder(subject.cloudinaryFolder));
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
            // Poll để chờ sprites ready thay vì delay cố định
            // (lazy loading có thể mất 15-20s, không thể delay 20s)
            var autoFlip = bookCtrl.bookObject.GetComponentInChildren<AutoFlipVR>();
            if (autoFlip == null)
            {
                Debug.LogError("[LearningModeManager] AutoFlipVR not found!");
                yield break;
            }

            var bookSpriteManager = bookCtrl.bookObject.GetComponentInChildren<BookSpriteManager>();
            if (bookSpriteManager == null)
            {
                Debug.LogError("[LearningModeManager] BookSpriteManager not found!");
                yield break;
            }

            // Chờ cho tới khi book có sprites (TotalPageCount > 0)
            float maxWaitTime = 60f; // Max 60s chờ
            float waitedTime = 0f;
            
            while (bookSpriteManager.TotalPageCount == 0 && waitedTime < maxWaitTime)
            {
                yield return new WaitForSeconds(0.5f);
                waitedTime += 0.5f;
            }

            if (bookSpriteManager.TotalPageCount == 0)
            {
                Debug.LogError($"[LearningModeManager] Timeout waiting for sprites (waited {waitedTime}s)");
                yield break;
            }

            Debug.Log($"[LearningModeManager] Sprites ready after {waitedTime}s, jumping to page {page}");
            autoFlip.JumpToPage(page);
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