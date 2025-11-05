using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using com.cyborgAssets.inspectorButtonPro;
using DreamClass.Subjects;

namespace DreamClass.Lecture
{
    public class LectureSpawner : NewMonobehavior
    {
        [Header("LearningModeManager")]
        public LearningModeManager manager;

        [Header("Prefab Settings")]
        public GameObject lecturePrefab; // Chỉ 1 prefab duy nhất
        // Prefab chứa 2 child: ChapterText, LectureText

        [Header("Spawn Settings")]
        public Transform spawnParent;
        public bool groupByChapter = true;
        public bool spawnOnStart = false;

        private readonly List<GameObject> spawnedLectures = new List<GameObject>();

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
                SpawnLectures();
        }

        [ProButton]
        [ContextMenu("Spawn Lectures")]
        public void SpawnLectures()
        {
            if (manager == null)
            {
                Debug.LogError("LearningModeManager is NULL! Cannot spawn lectures.");
                return;
            }

            if (lecturePrefab == null)
            {
                Debug.LogError("Lecture prefab is NULL! Assign it in inspector.");
                return;
            }

            SubjectInfo currentSubject = manager.GetCurrentSubject();
            if (currentSubject == null)
            {
                Debug.LogWarning("No subject selected in UIManager!");
                return;
            }

            List<CSVLectureInfo> lectures = currentSubject.lectures;
            if (lectures == null || lectures.Count == 0)
            {
                Debug.LogWarning($"No lectures found in subject: {currentSubject.name}");
                return;
            }

            ClearSpawnedLectures();

            if (groupByChapter)
                SpawnGroupedByChapter(lectures);
            else
                SpawnFlat(lectures);

            Debug.Log($"Spawned {spawnedLectures.Count} items for {currentSubject.name}");
        }

        void SpawnGroupedByChapter(List<CSVLectureInfo> lectures)
        {
            int currentChapter = -1;

            for (int i = 0; i < lectures.Count; i++)
            {
                CSVLectureInfo lecture = lectures[i];

                // Chapter header
                if (lecture.chapter != currentChapter)
                {
                    currentChapter = lecture.chapter;
                    SpawnSingleItem(lecture, true, i); // isChapter = true
                }

                // Lecture
                SpawnSingleItem(lecture, false, i);
            }
        }

        void SpawnFlat(List<CSVLectureInfo> lectures)
        {
            for (int i = 0; i < lectures.Count; i++)
            {
                SpawnSingleItem(lectures[i], false, i);
            }
        }


        void SpawnSingleItem(CSVLectureInfo lecture, bool isChapter, int capturedIndex)
        {
            GameObject obj = Instantiate(lecturePrefab, spawnParent);

            // Tìm child
            var chapterText = obj.transform.Find("Chapter")?.GetComponent<TextMeshProUGUI>();
            var lectureText = obj.transform.Find("Lecture")?.GetComponent<TextMeshProUGUI>();

            if (isChapter)
            {
                if (chapterText != null)
                {
                    chapterText.text = $"── Chương {lecture.chapter}: {lecture.groupName} ──";
                }
                var button = obj.GetComponent<Button>();
                button.enabled = false;
                if (lectureText != null)
                    lectureText.gameObject.SetActive(false);
            }
            else
            {
                if (lectureText != null)
                    lectureText.text = lecture.lectureName;
                if (chapterText != null)
                    chapterText.gameObject.SetActive(false);

                // Button listener
                var button = obj.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => OnLectureClicked(capturedIndex));
                }
            }

            obj.name = isChapter ? $"Chapter_{lecture.chapter}" : $"Lecture_{lecture.page}_{lecture.lectureName}";
            obj.SetActive(true);
            spawnedLectures.Add(obj);
        }

        public void OnLectureClicked(int index)
        {
            if (manager == null) return;

            manager.SetCurrentLecture(index);
            CSVLectureInfo lecture = manager.GetCurrentLecture();
            if (lecture != null)
                Debug.Log($"Loading page {lecture.page} for lecture: {lecture.lectureName}");
        }

        [ProButton]
        [ContextMenu("Clear Spawned Lectures")]
        public void ClearSpawnedLectures()
        {
            foreach (var obj in spawnedLectures)
            {
#if UNITY_EDITOR
                DestroyImmediate(obj);
#else
                Destroy(obj);
#endif
            }
            spawnedLectures.Clear();
        }
    }
}
