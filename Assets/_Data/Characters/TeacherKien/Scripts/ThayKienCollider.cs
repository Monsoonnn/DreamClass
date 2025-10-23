using DreamClass.NPCCore;
using UnityEngine;

namespace Characters.TeacherKien {
    public class ThayKienCollider : NewMonobehavior {
        [SerializeField] private BoxCollider boxCollider;
        [SerializeField] private ThayKienNPC npcManager;
      
        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadNPCManager();
        }

        protected virtual void LoadNPCManager() {
            if (this.npcManager != null) return;
            this.npcManager = this.GetComponentInParent<ThayKienNPC>();
        }

        private void OnTriggerEnter( Collider other ) {
            // Check if the collider belongs to the Player
            if (!other.CompareTag("Player")) return;

            // Get the player's position
            Vector3 playerPos = other.transform.position;

            // Calculate the direction from NPC to Player (ignore Y to keep rotation horizontal)
            Vector3 direction = playerPos - npcManager.Model.transform.position;
            direction.y = 0f;

            // If direction is not zero, rotate NPC toward the Player
            if (direction.sqrMagnitude > 0.01f) {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                npcManager.Model.transform.rotation = targetRotation;
            }
            
            npcManager.OnPlayerEnter();

        }

        private void OnTriggerExit( Collider other ) {
            if (!other.CompareTag("Player")) return;

            Debug.Log("Exit Collider Teacher");

            npcManager.AnimationManager.PlayStartGroup();
            npcManager.interaction.CancelAudio();
            npcManager.changeClass.SetActive(false);
        }
    }
}
