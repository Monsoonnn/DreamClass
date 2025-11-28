using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using com.cyborgAssets.inspectorButtonPro;
using DreamClass.Subjects;
using System.Collections;

namespace DreamClass.Lecture
{
    /// <summary>
    /// Spawn subjects - Lấy DATA từ UIManager, giữ lại prefab reference riêng
    /// CHỈ spawn subjects đã được cached - không hiển thị subjects chưa cache
    /// </summary>
    public class SubjectsSpawner : NewMonobehavior
    {
        [Header("LearningModeManager")]
        public LearningModeManager manager;

        [Header("Prefab Settings")]
        public GameObject subjectPrefab; // Prefab chứa Button + TextMeshProUGUI

        [Header("Spawn Settings")]
        public Transform spawnParent;
        public bool spawnOnStart = false;
        public bool autoFetchRemote = true;

        [Header("Subject UI Colors")]
        [Tooltip("Color for cached remote subjects (ready to use)")]
        public Color cachedSubjectColor = Color.green;
        [Tooltip("Color for local subjects without remote data")]
        public Color localSubjectColor = Color.white;

        private readonly List<GameObject> spawnedSubjects = new List<GameObject>();

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.LoadLearningModeManager();
        }

        protected virtual void LoadLearningModeManager()
        {
            if (manager != null) return;
            manager = transform.parent.parent.GetComponent<LearningModeManager>();
            Debug.Log(manager != null 
                ? $"LearningModeManager loaded: {manager.name}" 
                : "LearningModeManager NOT FOUND!");
        }

        protected override void Start()
        {
            base.Start();

            // Subscribe to PDFSubjectService.OnReady (login-based pattern)
            PDFSubjectService.OnReady += OnPDFSubjectsReady;

            // Subscribe to individual events for updates
            if (manager != null && manager.pdfSubjectService != null)
            {
                manager.pdfSubjectService.OnSubjectsLoaded += OnRemoteSubjectsLoaded;
                manager.pdfSubjectService.OnSubjectCacheComplete += OnSubjectCacheComplete;
            }

            // If already ready, spawn immediately
            if (PDFSubjectService.IsReady)
            {
                OnPDFSubjectsReady();
            }
            else if (spawnOnStart)
            {
                // Spawn local subjects first while waiting for remote
                SpawnSubjects();
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe events
            PDFSubjectService.OnReady -= OnPDFSubjectsReady;

            if (manager != null && manager.pdfSubjectService != null)
            {
                manager.pdfSubjectService.OnSubjectsLoaded -= OnRemoteSubjectsLoaded;
                manager.pdfSubjectService.OnSubjectCacheComplete -= OnSubjectCacheComplete;
            }
        }

        private void OnPDFSubjectsReady()
        {
            Debug.Log("[SubjectsSpawner] PDF Subjects ready after login, refreshing...");
            SpawnSubjects();
        }

        private void OnRemoteSubjectsLoaded(List<RemoteSubjectInfo> subjects)
        {
            Debug.Log($"[SubjectsSpawner] Remote subjects loaded: {subjects.Count}");
            // Refresh UI
            SpawnSubjects();
        }

        private void OnSubjectCacheComplete(RemoteSubjectInfo subject)
        {
            Debug.Log($"[SubjectsSpawner] Subject cached: {subject.name}");
            // Refresh color of the subject button
            RefreshSubjectVisual(subject.name);
        }

        [ProButton]
        [ContextMenu("Spawn Subjects")]
        public void SpawnSubjects()
        {
            // Validate manager
            if (manager == null)
            {
                Debug.LogError("LearningModeManager is NULL! Cannot spawn subjects.");
                return;
            }

            // Validate prefab (local reference)
            if (subjectPrefab == null)
            {
                Debug.LogError("Subject prefab is NULL! Assign it in inspector.");
                return;
            }

            // Lấy DATA từ UIManager (combined local + remote)
            List<SubjectInfo> subjects = manager.GetAllSubjects();
            if (subjects == null || subjects.Count == 0)
            {
                Debug.LogWarning("No subjects found in UIManager database!");
                return;
            }

            ClearSpawnedSubjects();

            int spawnedCount = 0;
            int skippedCount = 0;

            // Spawn từng subject - CHỈ spawn nếu đã cached hoặc không có path (local only)
            for (int i = 0; i < subjects.Count; i++)
            {
                var subject = subjects[i];
                
                // Skip subjects with path that are NOT cached
                if (!string.IsNullOrEmpty(subject.path) && !subject.isCached)
                {
                    Debug.Log($"[SubjectsSpawner] Skipping uncached subject: {subject.name}");
                    skippedCount++;
                    continue;
                }

                SpawnSingleSubject(subject, i);
                spawnedCount++;
            }

            Debug.Log($"[SubjectsSpawner] Spawned {spawnedCount} subjects, skipped {skippedCount} uncached");
        }

        void SpawnSingleSubject(SubjectInfo subject, int index)
        {
            GameObject subjectObj = Instantiate(subjectPrefab, spawnParent);

            // Gán text
            var tmp = subjectObj.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                string displayText = subject.GetDisplayName();
                tmp.text = displayText;

                // Set color based on cache status
                tmp.color = GetSubjectColor(subject);
            }

            // Gán button listener
            Button button = subjectObj.GetComponent<Button>();
            if (button != null)
            {
                int capturedIndex = index;
                button.onClick.AddListener(() => OnSubjectClicked(capturedIndex));
            }
            else
            {
                Debug.LogWarning($"Subject prefab missing Button component!");
            }

            // Store reference for later updates
            subjectObj.name = $"Subject_{subject.name}";
            subjectObj.SetActive(true);
            spawnedSubjects.Add(subjectObj);
        }

        private Color GetSubjectColor(SubjectInfo subject)
        {
            // Cached subjects get green, others get white
            return subject.isCached ? cachedSubjectColor : localSubjectColor;
        }

        private void RefreshSubjectVisual(string subjectName)
        {
            // When a subject is cached, refresh the entire list to show it
            // This is simpler than trying to add a single button
            SpawnSubjects();
        }

        public void OnSubjectClicked(int index)
        {
            if (manager == null)
            {
                Debug.LogError("Manager is null!");
                return;
            }

            var subjects = manager.GetAllSubjects();
            if (index < 0 || index >= subjects.Count)
            {
                Debug.LogError($"Invalid subject index: {index}");
                return;
            }

            var selectedSubject = subjects[index];

            // Subjects with path MUST be cached to be clickable
            if (!string.IsNullOrEmpty(selectedSubject.path) && !selectedSubject.isCached)
            {
                Debug.LogWarning($"[SubjectsSpawner] Cannot select uncached subject: {selectedSubject.name}");
                return; // Do nothing - this shouldn't happen as uncached subjects aren't spawned
            }

            // Thông báo cho UIManager
            manager.SetCurrentSubject(index);
            
            // Chuyển sang lecture selection
            manager.ShowLectureSelection();
        }

        [ProButton]
        [ContextMenu("Clear Spawned Subjects")]
        public void ClearSpawnedSubjects()
        {
            foreach (var obj in spawnedSubjects)
            {
                if (obj != null)
                {
                    #if UNITY_EDITOR
                    DestroyImmediate(obj);
                    #else
                    Destroy(obj);
                    #endif
                }
            }
            spawnedSubjects.Clear();
            Debug.Log("Cleared all spawned subjects");
        }

        #region Debug
        [ProButton]
        void DebugManagerConnection()
        {
            if (manager == null)
                Debug.LogError("Manager is NULL!");
            else
                Debug.Log($"Manager found: {manager.name}");
        }

        [ProButton]
        void DebugSubjectData()
        {
            if (manager == null)
            {
                Debug.LogError("Manager is NULL!");
                return;
            }

            var subjects = manager.GetAllSubjects();
            if (subjects == null)
                Debug.LogWarning("Subjects list is NULL in manager!");
            else
            {
                Debug.Log($"Total subjects in manager: {subjects.Count}");
                foreach (var subject in subjects)
                {
                    string hasPath = !string.IsNullOrEmpty(subject.path) ? "[API]" : "[LOCAL]";
                    string cached = subject.isCached ? "[CACHED]" : "";
                    Debug.Log($"- {hasPath}{cached} {subject.name}: {subject.GetDisplayName()}");
                }
            }
        }

        [ProButton]
        void RefreshRemoteSubjects()
        {
            if (manager != null)
            {
                manager.FetchRemoteSubjects();
            }
        }
        #endregion
    }
}