using UnityEngine;
using com.cyborgAssets.inspectorButtonPro;
using HMStudio.EasyQuiz;  // For QuizDatabase and QuestionManager

namespace DreamClass.LearningLecture
{
    /// <summary>
    /// Manager for Exam Mode - Handles subject/chapter selection and panel swapping
    /// </summary>
    public class ExamUIManager : MonoBehaviour
    {
        [Header("Quiz Data")]
        public QuizDatabase quizDatabase;
        public int currentSubjectIndex = -1;
        public int currentChapterIndex = -1;
        public Subject currentSubject;

        [Header("References")]
        public ExamModeManager examModeManager;
        public QuestionManager questionManager;

        [Header("UI Panels")]
        public GameObject subjectSelectionPanel;
        public GameObject chapterSelectionPanel;

        private GameObject currentActivePanel;

        public void SetCurrentSubject(int index)
        {
            if (quizDatabase == null || index < 0 || index >= quizDatabase.Subjects.Count)
            {
                Debug.LogError($"Invalid subject index: {index}");
                return;
            }

            currentSubjectIndex = index;
            currentSubject = quizDatabase.Subjects[index];
            questionManager.subjectID = index;
            Debug.Log($"Exam subject set to: {currentSubject.Name}");
        }

        public void SetCurrentChapter(int index)
        {
            if (currentSubject == null || index < 0 || index >= currentSubject.Chapters.Count)
            {
                Debug.LogError($"Invalid chapter index: {index}");
                return;
            }

            currentChapterIndex = index;
            questionManager.chapterID = index;
            Debug.Log($"Exam chapter set to: {currentSubject.Chapters[index].Name}");

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
            }
            else
            {
                currentActivePanel = null;
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