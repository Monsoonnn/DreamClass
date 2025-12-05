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
    /// Supports both Excel and API modes
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
            if (manager == null)
            {
                Debug.LogError("Manager is NULL!");
                return;
            }

            // Check based on mode
            if (manager.IsAPIMode)
            {
                if (manager.currentAPISubject == null)
                {
                    Debug.LogError("Current API subject is NULL!");
                    return;
                }
            }
            else
            {
                if (manager.currentSubject == null)
                {
                    Debug.LogError("Current Excel subject is NULL!");
                    return;
                }
            }

            int chapterCount = manager.GetChapterCount();
            if (chapterCount == 0)
            {
                Debug.LogWarning("No chapters in selected subject!");
                return;
            }

            ClearSpawnedChapters();

            for (int i = 0; i < chapterCount; i++)
            {
                SpawnSingleChapter(i);
            }

            string subjectName = manager.IsAPIMode ? manager.currentAPISubject.Name : manager.currentSubject.Name;
            Debug.Log($"Spawned {spawnedChapters.Count} chapters for subject {subjectName} (Mode: {manager.quizDatabase.DataMode})");
        }

        private void SpawnSingleChapter(int index)
        {
            GameObject chapterObj = Instantiate(chapterPrefab, spawnParent);

            var tmp = chapterObj.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = manager.GetChapterName(index);
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