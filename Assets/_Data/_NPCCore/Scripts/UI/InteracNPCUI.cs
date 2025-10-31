using UnityEngine;

namespace DreamClass.NPCCore {
    public class InteractionUICtrl : NewMonobehavior, IInteractionUI {
        public Canvas canvas;
        public bool isInteract = false;

        [Header("Position Settings")]
        public float distance = 2f;
        public float verticalOffset = -0.8f;
        public Transform positionSource;

        private bool wasInteract = false; // Track previous state

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadCanvas();
        }

        protected override void Start() {
            if (positionSource == null)
                positionSource = Camera.main.transform;
        }

        protected virtual void LoadCanvas() {
            if (canvas != null) return;
            Transform ui = this.transform.parent.Find("UICanvas");
            if (ui != null)
                this.canvas = ui.GetComponent<Canvas>();
        }

        protected virtual void Update() {
            if (canvas == null) return;

            // Spawn once when isInteract switches from false -> true
            if (isInteract && !wasInteract) {
                RepositionCanvas();
                canvas.gameObject.SetActive(true);
            }

            // Hide canvas when isInteract is false
            if (!isInteract) {
                canvas.gameObject.SetActive(false);
            }

            wasInteract = isInteract; 

            isInteract = false;
        }

        public void ToggleCanvas() {
            isInteract = true;
        }

        public void ShowUI() {
            isInteract = true;
        }

        private void RepositionCanvas() {
            if (positionSource == null || canvas == null) return;

            Vector3 direction = positionSource.forward;
            direction.y = 0;
            direction.Normalize();

            Vector3 targetPosition = positionSource.position
                                     + direction * distance
                                     + Vector3.up * verticalOffset;

            canvas.transform.position = targetPosition;
            canvas.transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}
