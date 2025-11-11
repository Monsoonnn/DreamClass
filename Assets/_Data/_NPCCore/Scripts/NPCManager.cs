using NPCCore.Animation;
using NPCCore.Voiceline;
using UnityEngine;

namespace DreamClass.NPCCore {
    public class NPCManager : NewMonobehavior {
        public Transform Model;
        public AnimationManager AnimationManager;
        public ICharacterVoiceline CharacterVoiceline;

        private float rotationSpeed = 5f; // Speed for smooth rotation

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadModel();
        }

        protected void LoadModel() {
            if (Model != null) return;
            Model = transform.Find("Model");
        }

        /// <summary>
        /// Rotate NPC model smoothly toward a given direction.
        /// </summary>
        public virtual void RotateTo( Vector3 direction ) {
            if (Model == null || direction == Vector3.zero) return;

            direction.y = 0f; // Keep upright
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            Model.rotation = Quaternion.Slerp(Model.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }

        /// <summary>
        /// Instantly look at target Transform.
        /// </summary>
        public virtual void LookAtTarget( Transform target ) {
            if (Model == null || target == null) return;

            Vector3 direction = target.position - Model.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
                Model.rotation = Quaternion.LookRotation(direction);
        }

        /// <summary>
        /// Instantly look at the player (main camera).
        /// </summary>
        public virtual void LookAtPlayer() {
            Camera cam = Camera.main;
            if (cam == null) return;
            LookAtTarget(cam.transform);
        }

        public virtual void ResetRotation()
        {
            if (Model == null) return;
            Model.localRotation = Quaternion.identity;
        }
        
        public virtual void Rotation(Quaternion rotation)
        {
            if(Model == null) return;
            Model.localRotation = rotation;
        }
    }
}
