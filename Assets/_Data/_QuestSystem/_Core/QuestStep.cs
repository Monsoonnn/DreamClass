using UnityEngine;

namespace DreamClass.QuestSystem
{
    public abstract class QuestStep : MonoBehaviour
    {
        [Header("Step Info")]
        public string StepId;
        public bool IsComplete;

        public virtual void StartStep()
        {
            IsComplete = false;
            Debug.Log($"[QuestStep] Started step: {StepId}");
        }

        public abstract void OnUpdate(object context);

        public virtual void OnComplete()
        {
            IsComplete = true;
            Debug.Log($"[QuestStep] Completed step: {StepId}");
            // Publish event with instance
            QuestEventBus.Instance.Publish("QuestStepCompleted", this);
        }
    }
}
