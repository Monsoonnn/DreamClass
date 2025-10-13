using UnityEngine;
using UnityEngine.UI;

namespace DrawingSystem {
    public class TargetDrawing : NewMonobehavior {
        [SerializeField] private Image image;
        [SerializeField] private float[] opacitySteps = { 1f, 0.7f, 0.4f };
        [Range(0, 2)] public int opacityIndex = 0;
        public Material targetMaterial;

        protected override void OnValidate() {
            base.Start();
            if (image != null && image.material != null) {
                Color newColor = targetMaterial.color;
                newColor.a = opacitySteps[Mathf.Clamp(opacityIndex, 0, opacitySteps.Length - 1)];
                image.color = newColor;
            }
        }

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadImageRender();
        }

        protected virtual void LoadImageRender() {
            if (image != null) return;
            this.image = GetComponent<Image>();
        }

        public virtual Color ReturnTargetColor() {
            if (image != null) return image.color;
            return Color.white;
        }
    }
}
