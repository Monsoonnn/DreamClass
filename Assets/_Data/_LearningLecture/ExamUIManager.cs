using UnityEngine;
using com.cyborgAssets.inspectorButtonPro;
using HMStudio.EasyQuiz;  // For QuizDatabase and QuestionManager
using System.Collections.Generic;

namespace DreamClass.LearningLecture
{
    /// <summary>
    /// Manager for Exam Mode - Handles subject/chapter selection and panel swapping
    /// Supports both Excel and API modes
    /// </summary>
    public class ExamUIManager : MonoBehaviour
    {
        [Header("Quiz Data")]
        public QuizDatabase quizDatabase;
        public int currentSubjectIndex = -1;
        public int currentChapterIndex = -1;
        
        // For Excel mode
        public Subject currentSubject;
        // For API mode
        public APISubjectData currentAPISubject;

        [Header("References")]
        public ExamModeManager examModeManager;
        public QuestionManager questionManager;

        [Header("UI Panels")]
        public GameObject subjectSelectionPanel;
        public GameObject chapterSelectionPanel;

        [Header("Canvas Interaction")]
        public GameObject rayCanvasInteraction;
        public GameObject pokeCanvasInteraction;

        /// <summary>
        /// Check if using API mode
        /// </summary>
        public bool IsAPIMode => quizDatabase != null && quizDatabase.DataMode == QuizDataMode.API;

        /// <summary>
        /// Get subject count based on current mode
        /// </summary>
        public int GetSubjectCount()
        {
            if (quizDatabase == null) return 0;
            
            if (IsAPIMode)
                return quizDatabase.APISubjects?.Count ?? 0;
            else
                return quizDatabase.ExcelSubjects?.Count ?? 0;
        }

        /// <summary>
        /// Get subject name by index based on current mode
        /// </summary>
        public string GetSubjectName(int index)
        {
            if (quizDatabase == null) return "";
            
            if (IsAPIMode)
            {
                if (index >= 0 && index < quizDatabase.APISubjects.Count)
                    return $"{quizDatabase.APISubjects[index].Name} (Lớp {quizDatabase.APISubjects[index].Grade})";
            }
            else
            {
                if (index >= 0 && index < quizDatabase.ExcelSubjects.Count)
                    return quizDatabase.ExcelSubjects[index].Name;
            }
            return "";
        }

        /// <summary>
        /// Get chapter count for current subject based on mode
        /// </summary>
        public int GetChapterCount()
        {
            if (IsAPIMode)
                return currentAPISubject?.Chapters?.Count ?? 0;
            else
                return currentSubject?.Chapters?.Count ?? 0;
        }

        /// <summary>
        /// Get chapter name by index based on current mode
        /// </summary>
        public string GetChapterName(int index)
        {
            if (IsAPIMode)
            {
                if (currentAPISubject != null && index >= 0 && index < currentAPISubject.Chapters.Count)
                    return $"{currentAPISubject.Chapters[index].Name} ({currentAPISubject.Chapters[index].QuestionCount} câu)";
            }
            else
            {
                if (currentSubject != null && index >= 0 && index < currentSubject.Chapters.Count)
                    return currentSubject.Chapters[index].Name;
            }
            return "";
        }

        private void SetCanvasInteractionActive(bool active)
        {
            if (rayCanvasInteraction != null)
                rayCanvasInteraction.SetActive(active);

            if (pokeCanvasInteraction != null)
                pokeCanvasInteraction.SetActive(active);
        }


        private GameObject currentActivePanel;

        public void SetCurrentSubject(int index)
        {
            if (quizDatabase == null)
            {
                Debug.LogError("QuizDatabase not assigned!");
                return;
            }

            if (IsAPIMode)
            {
                // API Mode
                if (index < 0 || index >= quizDatabase.APISubjects.Count)
                {
                    Debug.LogError($"Invalid API subject index: {index}");
                    return;
                }

                currentSubjectIndex = index;
                currentAPISubject = quizDatabase.APISubjects[index];
                currentSubject = null; // Clear Excel subject
                questionManager.subjectID = index;
                Debug.Log($"[API Mode] Exam subject set to: {currentAPISubject.Name}");
            }
            else
            {
                // Excel Mode
                if (index < 0 || index >= quizDatabase.Subjects.Count)
                {
                    Debug.LogError($"Invalid Excel subject index: {index}");
                    return;
                }

                currentSubjectIndex = index;
                currentSubject = quizDatabase.Subjects[index];
                currentAPISubject = null; // Clear API subject
                questionManager.subjectID = index;
                Debug.Log($"[Excel Mode] Exam subject set to: {currentSubject.Name}");
            }
        }

        public void SetCurrentChapter(int index)
        {
            if (IsAPIMode)
            {
                // API Mode
                if (currentAPISubject == null || index < 0 || index >= currentAPISubject.Chapters.Count)
                {
                    Debug.LogError($"Invalid API chapter index: {index}");
                    return;
                }

                currentChapterIndex = index;
                questionManager.chapterID = index;
                Debug.Log($"[API Mode] Exam chapter set to: {currentAPISubject.Chapters[index].Name}");
            }
            else
            {
                // Excel Mode
                if (currentSubject == null || index < 0 || index >= currentSubject.Chapters.Count)
                {
                    Debug.LogError($"Invalid Excel chapter index: {index}");
                    return;
                }

                currentChapterIndex = index;
                questionManager.chapterID = index;
                Debug.Log($"[Excel Mode] Exam chapter set to: {currentSubject.Chapters[index].Name}");
            }

            // After chapter selected, start exam
            examModeManager.StartExam();
        }

        #region UI Panel Management
        [ProButton]
        public void ShowSubjectSelection() => SwapToPanel(subjectSelectionPanel);

        [ProButton]
        public void ShowChapterSelection() => SwapToPanel(chapterSelectionPanel);

        [ProButton]
        public void HideAllPanels() => SwapToPanel(null);

        private void SwapToPanel(GameObject targetPanel)
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
        #endregion

        [ProButton]
        public void DebugCurrentState()
        {
            Debug.Log("=== Exam Quiz Manager State ===");
            Debug.Log($"Subject Index: {currentSubjectIndex}");
            Debug.Log($"Chapter Index: {currentChapterIndex}");
            Debug.Log($"Active Panel: {(currentActivePanel != null ? currentActivePanel.name : "None")}");
        }
    }
}