using UnityEngine;

namespace DreamClass.QuestSystem
{
    public abstract class QuestStep : NewMonobehavior
    {
        [Header("Step Info")]
        public string StepId;
        public bool IsComplete;

        public QuestCtrl questCtrl;

        protected override void LoadComponents()
        {
            base.LoadComponents();
            this.LoadQuestCtrl();
        }
        
        protected virtual void LoadQuestCtrl()
        {
            if (this.questCtrl != null) return;
            this.questCtrl = transform.parent.GetComponent<QuestCtrl>();
        }

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
            this.questCtrl.UpdateProgress();

        }
    }
}
