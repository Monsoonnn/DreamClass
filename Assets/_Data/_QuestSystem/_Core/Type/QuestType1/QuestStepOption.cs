using com.cyborgAssets.inspectorButtonPro;
using DreamClass.NPCCore;
using System;
using System.Threading.Tasks;

using UnityEngine;

namespace DreamClass.QuestSystem {
    public class QuestStepOption : QuestStep {
        [Header("Option UI")]
        public GameObject optionUI;

        [Header("Voice Config (Dynamic)")]
        public VoiceEnumSource voiceEnumSource;

        [VoiceValueDropdown]
        public string voiceValue;
        

        private bool isStarting = false;
        private bool hasSpawned = false;

        protected override void LoadComponents() {
            base.LoadComponents();
            LoadOptionUI();
        }

        protected virtual void LoadOptionUI() {
            if (optionUI != null) return;
            optionUI = transform.Find("OptionUI")?.gameObject;
        }

        public override void StartStep() {
            base.StartStep();
            if (isStarting) return;
            isStarting = true;
            _ = HandleStartAsync();
        }

        protected async Task HandleStartAsync() {
            Debug.Log($"[{name}] HandleStartAsync");
            await OnStartAsync();
            SpawnOptionUI();
            isStarting = false;
        }

        protected virtual async Task OnStartAsync() {
            if (questCtrl is QuestType1 type1 && type1.npcCtrl != null) {
                type1.npcCtrl.Model.gameObject.SetActive(true);
                type1.npcCtrl.LookAtPlayer();

                await PlayDynamicVoice(type1.npcCtrl);
            }
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

                object enumValue = Enum.Parse(enumType, voiceValue);

                Debug.Log($"[{name}] Playing: {fullEnumName}.{voiceValue}");
                await npcCtrl.CharacterVoiceline.PlayAnimation(enumValue.ToString(), true);
            }
            catch (Exception ex) {
                Debug.LogError($"[{name}] PlayDynamicVoice failed: {ex.Message}");
            }
        }

        [ProButton]
        protected virtual void SpawnOptionUI() {
            if (optionUI == null || hasSpawned) return;
            hasSpawned = true;

            Debug.Log($"[{name}] SpawnOptionUI");
            if (questCtrl is QuestType1 type1) {
                GameObject clone = Instantiate(optionUI, type1.holdUI);
                questCtrl.State = QuestState.IN_PROGRESS;
                clone.SetActive(true);

                questCtrl.transform.parent.gameObject.SetActive(false);
            }
        }

        [ProButton]
        public override void OnComplete() {
            base.OnComplete();
            questCtrl.transform.parent.gameObject.SetActive(true);
        }

        public virtual void DestroyOptionUI( GameObject obj ) {
            Destroy(obj);
            hasSpawned = false;
        }

        public override void OnUpdate( object context ) { }
    }
}
