using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using com.cyborgAssets.inspectorButtonPro;
using HMStudio.EasyQuiz; 

namespace DreamClass.LearningLecture
{
    /// <summary>
    /// Spawner for Exam Subjects from QuizDatabase
    /// </summary>
    public class SubjectExamSpawner : MonoBehaviour
    {
        [Header("ExamQuizManager")]
        public ExamUIManager manager;

        [Header("Prefab Settings")]
        public GameObject subjectPrefab;  // Prefab with Button + TMP

        [Header("Spawn Settings")]
        public Transform spawnParent;
        public bool spawnOnStart = false;

        private List<GameObject> spawnedSubjects = new List<GameObject>();

        private void Start()
        {
            if (spawnOnStart)
                SpawnSubjects();
        }

        [ProButton]
        public void SpawnSubjects()
        {
            if (manager == null)
            {
                Debug.LogError("ExamQuizManager is NULL!");
                return;
            }

            if (subjectPrefab == null)
            {
                Debug.LogError("Subject prefab is NULL!");
                return;
            }

            if (manager.quizDatabase == null)
            {
                Debug.LogError("QuizDatabase not assigned in manager!");
                return;
            }

            var subjects = manager.quizDatabase.Subjects;
            if (subjects == null || subjects.Count == 0)
            {
                Debug.LogWarning("No subjects in QuizDatabase!");
                return;
            }

            ClearSpawnedSubjects();

            for (int i = 0; i < subjects.Count; i++)
            {
                SpawnSingleSubject(subjects[i], i);
            }

            Debug.Log($"Spawned {spawnedSubjects.Count} exam subjects");
        }

        private void SpawnSingleSubject(Subject subject, int index)
        {
            GameObject subjectObj = Instantiate(subjectPrefab, spawnParent);

            var tmp = subjectObj.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = subject.Name;
            }
            subjectObj.SetActive(true);
            Button button = subjectObj.GetComponent<Button>();
            if (button != null)
            {
                int capturedIndex = index;
                button.onClick.AddListener(() => OnSubjectClicked(capturedIndex));
            }

            spawnedSubjects.Add(subjectObj);
        }

        public void OnSubjectClicked(int index)
        {
            manager.SetCurrentSubject(index);
            manager.ShowChapterSelection();
        }

        [ProButton]
        public void ClearSpawnedSubjects()
        {
            foreach (var obj in spawnedSubjects)
            {
                if (obj != null)
                {
                    Destroy(obj);  // Or DestroyImmediate in Editor
                }
            }
            spawnedSubjects.Clear();
        }
    }
}