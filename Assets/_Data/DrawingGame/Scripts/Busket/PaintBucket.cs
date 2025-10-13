using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;



namespace DrawingSystem {


    public class PaintBucket : NewMonobehavior {

        [SerializeField] protected Material material;     

        [SerializeField] protected string targetMaterialName = "mat8";

        [SerializeField] private Renderer currentRenderer;


        protected override void OnValidate() {
            base.OnValidate();
            if (currentRenderer != null) ReplaceMaterial();
        }


        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadRenderer();
        }


        protected virtual void LoadRenderer() { 
            if(currentRenderer != null) return;
            this.currentRenderer = GetComponent<Renderer>();
            Debug.Log("Renderer loaded!");
        }


        [ProButton]
        public virtual void ReplaceMaterial() {

            if(currentRenderer == null) LoadRenderer();

            Renderer targetRenderer = this.currentRenderer;

            if (targetRenderer == null || material == null) return;

            Material[] materials = Application.isPlaying
                ? targetRenderer.materials
                : targetRenderer.sharedMaterials;

            for (int i = 0; i < materials.Length; i++) {
                string matName = materials[i].name;
                if (matName.StartsWith(targetMaterialName)) {
                    Material newMat = new Material(material);
                    newMat.name = matName;
                    materials[i] = newMat;

                    if (Application.isPlaying)
                        targetRenderer.materials = materials;
                    else
                        targetRenderer.sharedMaterials = materials;


                    return;
                }
            }


        }


    }



}

