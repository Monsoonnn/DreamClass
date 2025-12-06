using DreamClass.NPCCore;
using UnityEngine;

namespace Characters.TeacherQuang {
    public class TQuangCollider : NewMonobehavior {
        [SerializeField] private BoxCollider boxCollider;
        [SerializeField] private TQuangNPCManager npcManager;
        [SerializeField] private TeacherQuang voiceTypeOnEnter = TeacherQuang.Call;
        
        private bool hasGreeted;

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadBoxCollider();
            this.LoadNPCManager();
        }

        protected virtual void LoadBoxCollider() {
            if (this.boxCollider != null) return;
            this.boxCollider = this.GetComponent<BoxCollider>();
        }

        protected virtual void LoadNPCManager() {
            if (this.npcManager != null) return;
            this.npcManager = this.GetComponentInParent<TQuangNPCManager>();
        }

        private void OnTriggerEnter(Collider other) {
            // Only react to player objects
            if (!other.CompareTag("Player")) return;

            // Speak only the first time player enters
            if (hasGreeted) return;

            // Rotate NPC to face the player
            RotateToPlayer(other.transform.position);

            // Play voiceline
            PlayVoiceline();

            hasGreeted = true;
        }

        private void RotateToPlayer(Vector3 playerPosition) {
            if (npcManager == null || npcManager.Model == null) return;

            Vector3 direction = playerPosition - npcManager.Model.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.01f) {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                npcManager.Model.rotation = targetRotation;
            }
        }

        private void PlayVoiceline() {
            if (npcManager == null || npcManager.characterVoiceline == null) {
                Debug.LogWarning("[TQuangCollider] Missing NPCManager or VoicelineManager reference.");
                return;
            }

            _ = npcManager.characterVoiceline.PlayAnimation(voiceTypeOnEnter);
        }

        private void OnTriggerExit(Collider other) {
            if (!other.CompareTag("Player")) return;

            // Reset rotation when player exits
            if (npcManager != null) {
                npcManager.ResetRotation();
            }
        }

        /// <summary>
        /// Reset greeting state to allow greeting again
        /// </summary>
        public void ResetGreeting() {
            hasGreeted = false;
        }
    }
}
