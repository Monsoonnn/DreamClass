using UnityEngine;
using TMPro;
using System.Collections.Generic;
using com.cyborgAssets.inspectorButtonPro;
using DreamClass.Subjects;
using System.Collections;
using DreamClass.LearningLecture;

namespace DreamClass.Lecture
{
    /// <summary>
    /// UI Manager - CHỈ quản lý data và swap UI panels
    /// </summary>
    public class LearningModeManager : MonoBehaviour
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

        public void SetCurrentSubject(int index)
        {
            if (subjectDatabase == null || index < 0 || index >= subjectDatabase.subjects.Count)
            {
                Debug.LogError($"Invalid subject index: {index}");
                return;
            }

            currentSubjectIndex = index;
            currentSubject = subjectDatabase.subjects[index];
            Debug.Log($"Current subject set to: {currentSubject.name}");
        }

        public void SetCurrentSubject(SubjectInfo subject)
        {
            currentSubject = subject;
            currentSubjectIndex = subjectDatabase.subjects.IndexOf(subject);
            Debug.Log($"Current subject set to: {currentSubject.name}");
        }

        public SubjectInfo GetCurrentSubject() => currentSubject;

        public List<SubjectInfo> GetAllSubjects()
        {
            return subjectDatabase?.subjects;
        }
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
            // Wait for 0.5 seconds
            yield return new WaitForSeconds(1.5f);

            // Jump to page after delay
            var autoFlip = bookCtrl.bookObject.GetComponentInChildren<AutoFlipVR>();
            if (autoFlip != null)
            {
                autoFlip.JumpToPage(page);
            }

            Debug.Log($"LearningModeManager: Jumped to page: {page}");

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
        public void ShowLectureSelection() => SwapToPanel(lectureSelectionPanel);

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