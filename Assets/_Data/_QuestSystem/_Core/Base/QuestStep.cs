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
            
            // Show UICanvas when animation/step completes
            if (questCtrl != null)
            {
                questCtrl.ShowNPCUICanvas();
            }
            
            // Check if this step should skip server update
            bool shouldUpdateServer = !HasSkipServerUpdateFlag();
            
            // Update step on server if using Server API (unless skipped)
            if (shouldUpdateServer && questCtrl != null)
            {
                QuestManager.Instance?.UpdateStepOnServer(questCtrl.QuestId, StepId);
            }
            
            this.questCtrl.UpdateProgress();
        }
        
        public virtual bool HasSkipServerUpdateFlag()
        {
            // Allow derived classes to override this behavior
            // For ExperimentQuestStep, check its SkipServerUpdate property
            var experimentStep = this as ExperimentQuestStep;
            if (experimentStep != null)
            {
                return experimentStep.SkipServerUpdate;
            }
            return false;
        }
    }
}
