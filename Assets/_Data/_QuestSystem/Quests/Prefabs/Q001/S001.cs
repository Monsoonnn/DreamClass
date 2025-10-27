using com.cyborgAssets.inspectorButtonPro;
using DreamClass.NPCCore;
using System.Threading.Tasks;
using UnityEngine;

namespace DreamClass.QuestSystem.Q001 {
    public class S001 : QuestStep {
        public GameObject optionUI;
        private bool isStarting = false; // <--- Prevent multiple calls
        private bool hasSpawned = false; // <--- Prevent multiple spawns

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadOptionUI();
        }

        protected virtual void LoadOptionUI() {
            if (optionUI != null) return;
            optionUI = transform.Find("OptionUI").gameObject;
        }

        public override void StartStep() {
            base.StartStep();

            // Avoid running OnStart() multiple times
            if (isStarting) return;
            isStarting = true;

            _ = OnStart();
        }

        public async Task OnStart() {
            Debug.Log("[S001] OnStart called");

            if (questCtrl is Q001Ctrl q001Ctrl) {
                q001Ctrl.npcCtrl.Model.gameObject.SetActive(true);

                // Rotate the NPC to face the player
                Transform npcTransform = q001Ctrl.npcCtrl.Model.transform;
                Transform playerCamera = Camera.main.transform;
                Vector3 direction = playerCamera.position - npcTransform.position;
                direction.y = 0; // Keep rotation only on Y axis
                if (direction != Vector3.zero) {
                    npcTransform.rotation = Quaternion.LookRotation(direction);
                }

                await q001Ctrl.npcCtrl.loginInteraction.PlayAnimation(Characters.Mai.MaiVoiceType.Q001_Active);
            }

            SpawnOptionUI();
            isStarting = false; // Reset if you want to allow rerun later
        }


        [ProButton]
        protected virtual void SpawnOptionUI() {
            if (optionUI == null || hasSpawned) return;
            hasSpawned = true;

            Debug.Log("[S001] SpawnOptionUI");
            GameObject clone;
            if (questCtrl is QuestType1 type1) {
                clone = Instantiate(optionUI, type1.holdUI);
            } else return;

            questCtrl.State = QuestState.IN_PROGRESS;
            clone.gameObject.SetActive(true);

            questCtrl.transform.parent.gameObject.SetActive(false);
        }

        [ProButton]
        public override void OnComplete() {
            base.OnComplete();
            questCtrl.transform.parent.gameObject.SetActive(true);
        }

        public virtual void DestroyOptionUI( GameObject obj ) {
            Destroy(obj);
            hasSpawned = false; // Reset if destroyed
        }

        public override void OnUpdate( object context ) {

        }
    }
}
