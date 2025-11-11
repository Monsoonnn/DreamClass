using com.cyborgAssets.inspectorButtonPro;
using DreamClass.NPCCore;
using UnityEngine;
using System;
using System.Threading.Tasks;

namespace DreamClass.QuestSystem.Q001 {
    public class QuestOptionDynamic : NewMonobehavior {
        public QuestStepOption questStep;
        private bool isStarting = false;
        public bool isCompleted = false;

        [Header("Voice Config (Dynamic)")]
        public VoiceEnumSource voiceEnumSource;

        [VoiceValueDropdown]
        public string voiceValue;

        protected override void LoadComponents() {
            base.LoadComponents();
            LoadQuestStep();
            SyncVoiceSource();
        }

        protected virtual void LoadQuestStep() {
            if (questStep != null) return;
            questStep = GetComponentInParent<QuestStepOption>();
        }


        protected virtual void SyncVoiceSource() {
            if (voiceEnumSource == null && questStep != null) {
                voiceEnumSource = questStep.voiceEnumSource;
            }
        }

        [ProButton]
        public virtual async void SelectOption(String voiceValue) {
            if (isStarting) return;
            isStarting = true;

            this.voiceValue = voiceValue;

            if (questStep.questCtrl is QuestType1 type1Ctrl) {
                await PlayDynamicVoice(type1Ctrl.npcCtrl);

                if (isCompleted) questStep.OnComplete();
                else questStep.questCtrl.transform.parent.gameObject.SetActive(true);

                type1Ctrl.npcCtrl.ResetRotation();

            }

            questStep.DestroyOptionUI(gameObject);
            isStarting = false;
        }
        public void OnComplete() {
            isCompleted = true;
        }

        protected virtual async Task PlayDynamicVoice( NPCManager npcCtrl ) {
            if (voiceEnumSource == null || string.IsNullOrEmpty(voiceValue)) {
                Debug.LogWarning($"[{name}] Missing voiceEnumSource or voiceValue");
                return;
            }

            try {
                string fullEnumName = $"{voiceEnumSource.namespaceName}.{voiceEnumSource.enumName}";
                Type enumType = Type.GetType(fullEnumName);

                if (enumType == null) {
                    Debug.LogError($"[{name}] Cannot find enum type: {fullEnumName}");
                    return;
                }

                Debug.Log($"[{name}] Playing {fullEnumName}.{voiceValue}");
                await npcCtrl.CharacterVoiceline.PlayAnimation(voiceValue, true);
            }
            catch (Exception ex) {
                Debug.LogError($"[{name}] PlayDynamicVoice failed: {ex.Message}");
            }
        }
    }
}
