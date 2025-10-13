using UnityEngine;
using UnityEngine.UI;

namespace DrawingSystem {
    public class SpriteDrawingCanvas : NewMonobehavior {
        public Image image;
        [SerializeField] private float loadingDuration = 2f;

        private Color targetColor;
        private bool isFading = false;
        private float fadeTimer = 0f;
        private float[] opacitySteps = { 1f, 0.7f, 0.4f };
        private int currentStep = 0;

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadSpriteRender();
        }

        protected virtual void LoadSpriteRender() {
            if (image != null) return;
            this.image = GetComponent<Image>();
            image.color = Color.white;
        }

        private void Update() {
            if (isFading && currentStep < opacitySteps.Length) {
                fadeTimer += Time.deltaTime;

                float stepDuration = loadingDuration / opacitySteps.Length;
                int targetStep = Mathf.FloorToInt(fadeTimer / stepDuration);

                if (targetStep > currentStep && targetStep <= opacitySteps.Length) {
                    currentStep = targetStep;

                    Color c = targetColor;
                    c.a = opacitySteps[currentStep - 1];
                    image.color = c;

                    if (c.a >= 1f)
                        isFading = false;
                }
            }
        }

        private void OnTriggerEnter( Collider other ) {
            ColorIndicator indicator = other.GetComponent<ColorIndicator>();
            if (indicator != null && indicator.colorMaterial != null && image != null) {
                Color newColor = indicator.colorMaterial.color;

                if (targetColor != newColor) {
                    targetColor = newColor;
                    fadeTimer = 0f;
                    currentStep = 0;
                    isFading = true;
                } else if (!isFading) {
                    isFading = true;
                }
            }
        }

        private void OnTriggerExit( Collider other ) {
            ColorIndicator indicator = other.GetComponent<ColorIndicator>();
            if (indicator != null) {
                isFading = false;
            }
        }

        public virtual Color ReturnDrawingColor() {
            if (image != null) return image.color;
            return Color.white;
        }
    }
}
