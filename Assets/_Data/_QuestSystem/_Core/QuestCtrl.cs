using System.Collections.Generic;
using UnityEngine;

namespace DreamClass.QuestSystem
{
    public abstract class QuestCtrl : NewMonobehavior
    {
        [Header("Quest Info")]
        public string QuestId;
        public string QuestName;
        [TextArea] public string Description;

        [Header("Quest Steps")]
        public List<QuestStep> steps = new List<QuestStep>();

        [Header("Runtime State")]
        public QuestState State = QuestState.NOT_START;

        private int currentStepIndex = 0;
        public bool IsComplete { get; private set; }

        public void SetState(QuestState newState)
        {
            State = newState;
        }

        public void StartQuest()
        {
            if (State == QuestState.NOT_PREMISE)
            {
                Debug.LogWarning($"[QuestCtrl] Quest '{QuestName}' cannot start yet.");
                return;
            }

            if (steps.Count == 0)
            {
                Debug.LogWarning($"[QuestCtrl] Quest '{QuestName}' has no steps.");
                return;
            }

            IsComplete = false;
            currentStepIndex = 0;
            steps[currentStepIndex].StartStep();
            State = QuestState.IN_PROGRESS;

            Debug.Log($"[QuestCtrl] Started quest: {QuestName}");
        }

        public void UpdateProgress(object context)
        {
            if (State != QuestState.IN_PROGRESS) return;

            QuestStep step = steps[currentStepIndex];

            if (step.IsComplete)
            {
                step.OnComplete();
                currentStepIndex++;

                if (currentStepIndex < steps.Count)
                {
                    steps[currentStepIndex].StartStep();
                }
                else
                {
                    CompleteQuest();
                }
            }
        }

        private void CompleteQuest()
        {
            IsComplete = true;
            State = QuestState.FINISHED;
            Debug.Log($"[QuestCtrl] Quest '{QuestName}' completed!");
        }
    }
}
