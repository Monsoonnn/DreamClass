using com.cyborgAssets.inspectorButtonPro;
using DreamClass.NPCCore;
using UnityEngine;

namespace DreamClass.QuestSystem.Q001 {
    public class S001Option : NewMonobehavior {
        public S001 questStep;
        private QuestNPCHolder questNPCHolder;
        private bool isStarting = false; // Prevent multiple option triggers

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadQuestStep();
            this.LoadQuestNPCHolder();
        }

        protected virtual void LoadQuestNPCHolder() {
            if (this.questNPCHolder != null) return;
            this.questNPCHolder = this.GetComponentInParent<QuestNPCHolder>();
        }

        protected virtual void LoadQuestStep() {
            if (this.questStep != null) return;
            this.questStep = this.GetComponentInParent<S001>();
        }

        [ProButton]
        public virtual async void Option1() {
            if (isStarting) return;
            isStarting = true;

            if (questStep.questCtrl is Q001Ctrl q001Ctrl) {
                await q001Ctrl.npcCtrl.loginInteraction.PlayAnimation(Characters.Mai.MaiVoiceType.Q001_Option1);
            }

            questStep.OnComplete();
            questStep.DestroyOptionUI(this.gameObject);
            isStarting = false;

        }

        [ProButton]
        public virtual async void Option2() {
            if (isStarting) return;
            isStarting = true;

            questStep.questCtrl.State = QuestState.NOT_START;

            if (questStep.questCtrl is Q001Ctrl q001Ctrl) {
                await q001Ctrl.npcCtrl.loginInteraction.PlayAnimation(Characters.Mai.MaiVoiceType.Q001_Option2);
            }

            questStep.questCtrl.transform.parent.gameObject.SetActive(true);
            questStep.DestroyOptionUI(this.gameObject);

            isStarting = false;
        }

        [ProButton]
        public virtual async void Option3() {
            if (isStarting) return;
            isStarting = true;

            questStep.questCtrl.State = QuestState.NOT_START;

            if (questStep.questCtrl is Q001Ctrl q001Ctrl) {
                await q001Ctrl.npcCtrl.loginInteraction.PlayAnimation(Characters.Mai.MaiVoiceType.Q001_Option3);
            }

            questStep.questCtrl.transform.parent.gameObject.SetActive(true);
            questStep.DestroyOptionUI(this.gameObject);

            isStarting = false;
        }
    }
}
