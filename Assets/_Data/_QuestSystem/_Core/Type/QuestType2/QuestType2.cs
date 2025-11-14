using DreamClass.QuestSystem;
using System.Threading.Tasks;
using UnityEngine;
namespace DreamClass.QuestSystem
{
    public class QuestType2 : QuestCtrl
    {
        [Header("Experiment Control")]
        [SerializeField] private string experimentID;
        [SerializeField] private GameController experimentController;

        [Header("Guide Integration")]
        [SerializeField] private string guideID;
        [SerializeField] private bool autoLoadGuide = true;

        protected override void LoadComponents()
        {
            base.LoadComponents();
            LoadExperimentController();
        }

        protected virtual void LoadExperimentController()
        {
            if (experimentController != null) return;

            GameController[] controllers = GuideStepManager.Instance.GameControllerList.ToArray();
            foreach (var ctrl in controllers)
            {
                if (ctrl.GetExperimentName() == experimentID)
                {
                    experimentController = ctrl;
                    Debug.Log($"[QuestType2] Found experiment controller: {experimentID}");
                    break;
                }
            }

            if (experimentController == null)
            {
                Debug.LogWarning($"[QuestType2] Experiment controller '{experimentID}' not found!");
            }
        }

        public override void StartQuest()
        {
           
            // Đăng ký quest này với GuideStepManager
            GuideStepManager.Instance?.SetCurrentQuest(this);

            // Load Guide
            if (autoLoadGuide && !string.IsNullOrEmpty(guideID))
            {
                GuideStepManager.Instance?.SetCurrentGuide(guideID);
                Debug.Log($"[QuestType2] Loaded guide: {guideID}");
            }

            // Setup experiment
            if (experimentController != null)
            {
                experimentController.SetupExperiment();
                Debug.Log($"[QuestType2] Setup experiment: {experimentID}");
            }

            base.StartQuest();
            
        }

        /// <summary>
        /// Callback khi quest được restart từ GuideStepManager
        /// </summary>
        private void OnQuestRestarted()
        {
            Debug.Log($"[QuestType2] Quest restarted callback");

            ResetQuestVariables();
        }

        /// <summary>
        /// Override để reset các biến specific của quest
        /// </summary>
        protected virtual void ResetQuestVariables()
        {
            this.SetComplete(false);
            State = QuestState.NOT_START;

            // Reset step progress
            for (int i = 0; i < steps.Count; i++)
            {
                steps[i].IsComplete = false;
            }

            currentStepIndex = 0;
            steps[currentStepIndex].StartStep();

        }

        protected override async Task OnBeforeReward()
        {
            if (experimentController != null && experimentController.IsExperimentRunning())
            {
                experimentController.StopExperiment();
                Debug.Log($"[QuestType2] Stopped experiment before reward");
            }

            await Task.CompletedTask;
        }

        protected override async Task OnAfterReward()
        {
            await base.OnAfterReward();
        }

        // Utility methods
        public void CompleteGuideStep(string stepID)
        {
            GuideStepManager.Instance?.CompleteStep(stepID);
        }

        public void ActivateGuideStep(string stepID)
        {
            GuideStepManager.Instance?.ActivateStep(stepID);
        }

        public GameController GetExperimentController()
        {
            return experimentController;
        }

        public string GetExperimentID()
        {
            return experimentID;
        }

        public string GetGuideID()
        {
            return guideID;
        }

        // Cleanup
        private void OnDestroy()
        {
            if (experimentController != null)
            {
                experimentController.StopAllTracking();
            }
        }
    }
}