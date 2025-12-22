using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using com.cyborgAssets.inspectorButtonPro;

namespace Gameplay.Exam
{
    /// <summary>
    /// Spawn các bài kiểm tra (ExamData) vào nhiều container
    /// Tương tự SubjectsSpawner + QuestNPCHolder
    /// </summary>
    public class ExamSpawner : NewMonobehavior
    {
        [Header("Exam Database")]
        [Tooltip("Danh sách tất cả bài kiểm tra có thể spawn")]
        public List<ExamData> examDatabase = new List<ExamData>();

        [Header("Prefab Settings")]
        [Tooltip("Prefab cho item bài kiểm tra (Button + Text)")]
        public GameObject examItemPrefab;

        [Header("Spawn Groups")]
        [Tooltip("Các nhóm spawn với container riêng")]
        public List<ExamSpawnGroup> spawnGroups = new List<ExamSpawnGroup>();

        [Header("Exam Controller")]
        [Tooltip("ExamController để start bài kiểm tra khi click")]
        public ExamController examController;

        public ExamTimeAnnouncer examTimeAnnouncer;

        [Header("Settings")]
        public bool spawnOnStart = true;
        public bool clearBeforeSpawn = true;

        [Header("UI Colors")]
        public Color availableExamColor = Color.white;
        public Color completedExamColor = Color.green;
        public Color lockedExamColor = Color.gray;

        // Tracking
        private readonly Dictionary<string, GameObject> spawnedExams = new Dictionary<string, GameObject>();
        private readonly HashSet<string> completedExamIds = new HashSet<string>();

        // Events
        public event System.Action<ExamData> OnExamSelected;

        #region Lifecycle

        protected override void Start()
        {
            base.Start();

            if (spawnOnStart)
            {
                SpawnAllExams();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Spawn tất cả bài kiểm tra theo các group đã cấu hình
        /// </summary>
        [ProButton]
        public void SpawnAllExams()
        {
            if (examItemPrefab == null)
            {
                Debug.LogError($"[ExamSpawner] Exam item prefab is NULL!");
                return;
            }

            if (clearBeforeSpawn)
            {
                ClearAllSpawnedExams();
            }

            foreach (var group in spawnGroups)
            {
                SpawnGroup(group);
            }

            Debug.Log($"[ExamSpawner] Spawned {spawnedExams.Count} exams total");
        }

        /// <summary>
        /// Spawn một group cụ thể
        /// </summary>
        public void SpawnGroup(ExamSpawnGroup group)
        {
            if (group.spawnParent == null)
            {
                Debug.LogWarning($"[ExamSpawner] Group '{group.groupName}' has no spawnParent!");
                return;
            }

            foreach (string examId in group.examIds)
            {
                // Kiểm tra đã spawn chưa
                if (spawnedExams.ContainsKey(examId))
                {
                    Debug.Log($"[ExamSpawner] Exam '{examId}' already spawned, skipping.");
                    continue;
                }

                // Tìm ExamData từ database
                ExamData examData = GetExamDataById(examId);
                if (examData == null)
                {
                    Debug.LogWarning($"[ExamSpawner] ExamData not found for ID: {examId}");
                    continue;
                }

                SpawnExamItem(examData, group.spawnParent);
            }
        }

        /// <summary>
        /// Spawn một bài kiểm tra cụ thể
        /// </summary>
        public void SpawnExamItem(ExamData examData, Transform parent)
        {
            if (examData == null || parent == null) return;

            GameObject examObj = Instantiate(examItemPrefab, parent);
            examObj.name = $"[Exam] {examData.examName}";

            // Setup text
            var tmp = examObj.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = examData.examName;
                tmp.color = GetExamColor(examData);
            }

            // Setup button
            Button button = examObj.GetComponent<Button>();
            if (button != null)
            {
                ExamData capturedData = examData;
                button.onClick.AddListener(() => OnExamClicked(capturedData, examObj));
            }

            examObj.SetActive(true);
            spawnedExams[examData.examId] = examObj;

            Debug.Log($"[ExamSpawner] Spawned exam: {examData.examName} ({examData.examId})");
        }

        /// <summary>
        /// Xóa tất cả exam đã spawn
        /// </summary>
        [ProButton]
        public void ClearAllSpawnedExams()
        {
            foreach (var kvp in spawnedExams)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value);
                }
            }
            spawnedExams.Clear();
        }

        /// <summary>
        /// Xóa exam trong một group cụ thể
        /// </summary>
        public void ClearGroup(ExamSpawnGroup group)
        {
            if (group.spawnParent == null) return;

            foreach (string examId in group.examIds)
            {
                if (spawnedExams.TryGetValue(examId, out GameObject obj))
                {
                    if (obj != null) Destroy(obj);
                    spawnedExams.Remove(examId);
                }
            }
        }

        /// <summary>
        /// Đánh dấu exam đã hoàn thành
        /// </summary>
        public void MarkExamCompleted(string examId)
        {
            completedExamIds.Add(examId);
            RefreshExamVisual(examId);
        }

        /// <summary>
        /// Kiểm tra exam đã hoàn thành chưa
        /// </summary>
        public bool IsExamCompleted(string examId)
        {
            return completedExamIds.Contains(examId);
        }

        /// <summary>
        /// Lấy ExamData từ database theo ID
        /// </summary>
        public ExamData GetExamDataById(string examId)
        {
            return examDatabase.Find(e => e.examId == examId);
        }

        /// <summary>
        /// Refresh visual của một exam cụ thể
        /// </summary>
        public void RefreshExamVisual(string examId)
        {
            if (!spawnedExams.TryGetValue(examId, out GameObject obj)) return;
            if (obj == null) return;

            ExamData examData = GetExamDataById(examId);
            if (examData == null) return;

            var tmp = obj.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.color = GetExamColor(examData);
            }
        }

        /// <summary>
        /// Refresh tất cả visuals
        /// </summary>
        [ProButton]
        public void RefreshAllVisuals()
        {
            foreach (var examId in spawnedExams.Keys)
            {
                RefreshExamVisual(examId);
            }
        }

        #endregion

        #region Private Methods

        private Color GetExamColor(ExamData examData)
        {
            if (IsExamCompleted(examData.examId))
                return completedExamColor;

            // Có thể thêm logic check locked ở đây
            // if (IsExamLocked(examData)) return lockedExamColor;

            return availableExamColor;
        }

        private void OnExamClicked(ExamData examData, GameObject thisClone)
        {
            Debug.Log($"[ExamSpawner] Exam clicked: {examData.examName}");
        
            // Start exam nếu có ExamController
            if (examController != null)
            {
                if(examController.IsExamRunning)
                {
                    Debug.Log("[ExamSpawner] Exam is already running! Playing warning.");
                    if (examTimeAnnouncer != null)
                    {
                        _ = examTimeAnnouncer.AnnounceWarningDuringTest();
                         thisClone.gameObject.SetActive(true);
                    }
                    return; // Don't start new exam
                }
                thisClone.gameObject.SetActive(false);
                examController.StartExam(examData);
            }
            else
            {
                Debug.LogWarning("[ExamSpawner] ExamController is not assigned!");
            }
            
            OnExamSelected?.Invoke(examData);
        }

        #endregion
    }

    /// <summary>
    /// Cấu hình một nhóm spawn (tương tự QuestSpawnGroup)
    /// </summary>
    [System.Serializable]
    public class ExamSpawnGroup
    {
        [Tooltip("Tên nhóm để dễ quản lý")]
        public string groupName;
        
        [Tooltip("Transform parent để spawn các exam items")]
        public Transform spawnParent;
        
        [Tooltip("Danh sách exam IDs sẽ spawn vào group này")]
        public List<string> examIds = new List<string>();
    }
}
