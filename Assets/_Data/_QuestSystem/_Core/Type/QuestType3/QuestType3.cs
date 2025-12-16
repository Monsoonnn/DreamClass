using System.Linq;
using DreamClass.Subjects;
using UnityEngine;

namespace DreamClass.QuestSystem
{
    public class QuestType3 : QuestCtrl
    {
        [Header("Book Quest Settings")]
        public BookVR book;

        protected override void LoadComponents()
        {
            base.LoadComponents();
            if (book == null)
            {
                // Try to find the book if not assigned
                book = GameObject.FindAnyObjectByType<BookVR>();
            }

            // Observer: Subscribe to BookVR page change
            if (book != null)
            {
                book.OnPageChanged += OnBookPageChanged;
            }

            // Observer: Subscribe to LearningModeManager events
            var learningMode = GameObject.FindAnyObjectByType<DreamClass.Lecture.LearningModeManager>();
            if (learningMode != null)
            {
                learningMode.OnSubjectChanged += OnSubjectChanged;
                learningMode.OnLectureChanged += OnLectureChanged;
            }
        }

        // Observer event handlers
        private SubjectInfo trackedSubject;
        private CSVLectureInfo trackedLecture;
        private int trackedPage;

        private void OnSubjectChanged(SubjectInfo subject)
        {
            trackedSubject = subject;
            Debug.Log($"[QuestType3] Subject changed: {subject?.name}");
        }

        private void OnLectureChanged(CSVLectureInfo lecture)
        {
            trackedLecture = lecture;
            Debug.Log($"[QuestType3] Lecture changed: {lecture?.lectureName}");
        }

        private void OnBookPageChanged(int page)
        {
            trackedPage = page;
            Debug.Log($"[QuestType3] Page changed: {page}");
        }
        void Update()
        {
            if (State == QuestState.IN_PROGRESS && currentStepIndex < steps.Count)
            {
                if (steps[currentStepIndex] is ReadingQuestStep readingStep)
                {
                    // Pass the book reference to the step's OnUpdate method
                    readingStep.OnUpdate(book);
                }
                else
                {
                    // Handle cases where a step is not a ReadingQuestStep if needed
                }
            }
        }

        public override void StartQuest()
        {
            base.StartQuest();
            if (book == null)
            {
                book = Object.FindObjectsByType<BookVR>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                ).FirstOrDefault();
            }

            if (steps.Count > 0 && currentStepIndex == 0)
            {
                steps[0].StartStep();
            }

        }

        // Example of an event-driven approach
        // void CheckCurrentPage()
        // {
        //     if (State == QuestState.IN_PROGRESS && currentStepIndex < steps.Count)
        //     {
        //         if (steps[currentStepIndex] is ReadingQuestStep readingStep)
        //         {
        //             readingStep.OnUpdate(book);
        //         }
        //     }
        // }
    }
}
