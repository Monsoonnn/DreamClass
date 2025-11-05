using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using com.cyborgAssets.inspectorButtonPro;
using DreamClass.Subjects;

namespace DreamClass.Lecture
{
    /// <summary>
    /// Spawn subjects - Lấy DATA từ UIManager, giữ lại prefab reference riêng
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
            if (spawnOnStart)
                SpawnSubjects();
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

            // Lấy DATA từ UIManager
            List<SubjectInfo> subjects = manager.GetAllSubjects();
            if (subjects == null || subjects.Count == 0)
            {
                Debug.LogWarning("No subjects found in UIManager database!");
                return;
            }

            ClearSpawnedSubjects();

            // Spawn từng subject
            for (int i = 0; i < subjects.Count; i++)
            {
                SpawnSingleSubject(subjects[i], i);
            }

            Debug.Log($"Spawned {spawnedSubjects.Count} subjects from UIManager data");
        }

        void SpawnSingleSubject(SubjectInfo subject, int index)
        {
            GameObject subjectObj = Instantiate(subjectPrefab, spawnParent);

            // Gán text
            var tmp = subjectObj.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                string displayText = string.IsNullOrEmpty(subject.description) 
                    ? subject.name 
                    : subject.description;
                tmp.text = displayText;
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

            subjectObj.name = $"Subject_{subject.name}";
            subjectObj.SetActive(true);
            spawnedSubjects.Add(subjectObj);
        }

        public void OnSubjectClicked(int index)
        {
            if (manager == null)
            {
                Debug.LogError("Manager is null!");
                return;
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
                Debug.LogError("❌ Manager is NULL!");
            else
                Debug.Log($"✅ Manager found: {manager.name}");
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
                    Debug.Log($"- {subject.name}: {subject.description}");
                }
            }
        }
        #endregion
    }
}