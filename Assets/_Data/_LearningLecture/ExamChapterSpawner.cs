using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using com.cyborgAssets.inspectorButtonPro;
using HMStudio.EasyQuiz;  // For QuizDatabase

namespace DreamClass.LearningLecture
{
    /// <summary>
    /// Spawner for Exam Chapters from selected subject
    /// </summary>
    public class ExamChapterSpawner : MonoBehaviour
    {
        [Header("ExamQuizManager")]
        public ExamUIManager manager;

        [Header("Prefab Settings")]
        public GameObject chapterPrefab; 

        [Header("Spawn Settings")]
        public Transform spawnParent;
        public bool spawnOnEnable = true;  

        private List<GameObject> spawnedChapters = new List<GameObject>();

        private void OnEnable()
        {
            if (spawnOnEnable)
                SpawnChapters();
        }

        [ProButton]
        public void SpawnChapters()
        {
            if (manager == null || manager.currentSubject == null)
            {
                Debug.LogError("Manager or current subject is NULL!");
                return;
            }

            var chapters = manager.currentSubject.Chapters;
            if (chapters == null || chapters.Count == 0)
            {
                Debug.LogWarning("No chapters in selected subject!");
                return;
            }

            ClearSpawnedChapters();

            for (int i = 0; i < chapters.Count; i++)
            {
                SpawnSingleChapter(chapters[i], i);
            }

            Debug.Log($"Spawned {spawnedChapters.Count} chapters for subject {manager.currentSubject.Name}");
        }

        private void SpawnSingleChapter(Chapter chapter, int index)
        {
            GameObject chapterObj = Instantiate(chapterPrefab, spawnParent);

            var tmp = chapterObj.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = chapter.Name;
            }
            chapterObj.SetActive(true);
            Button button = chapterObj.GetComponent<Button>();
            if (button != null)
            {
                int capturedIndex = index;
                button.onClick.AddListener(() => OnChapterClicked(capturedIndex));
            }

            spawnedChapters.Add(chapterObj);
        }

        public void OnChapterClicked(int index)
        {
            manager.SetCurrentChapter(index);
            manager.HideAllPanels();  // Hide panels before starting exam
        }

        [ProButton]
        public void ClearSpawnedChapters()
        {
            foreach (var obj in spawnedChapters)
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
            spawnedChapters.Clear();
        }
    }
}