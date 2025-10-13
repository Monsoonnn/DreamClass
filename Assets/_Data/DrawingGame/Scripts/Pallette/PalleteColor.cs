using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;

namespace DrawingSystem {
    public class PalleteColor : NewMonobehavior {

        public ColorEnumClass ColorEnumClass;

        public Material material;

        public GameObject model;

        [SerializeField] protected string targetMaterialName = "lambert7SG";

        [SerializeField] private Renderer currentRenderer;

        [SerializeField] protected ExchangeColor exchangeColor;

        public int CountPercent = 1;

        protected override void OnValidate() {
            base.OnValidate();
            if (currentRenderer != null) ReplaceMaterial();
        }


        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadRenderer();
            this.LoadModel();
            this.LoadMaterial();
            this.LoadExchangeColor();
        }

        protected virtual void LoadExchangeColor() { 
            if(exchangeColor != null) return;
            this.exchangeColor = GetComponentInChildren<ExchangeColor>();
            Debug.Log("ExchangeColor loaded!");
        }

        protected virtual void LoadModel() { 
            if(model != null) return;
            this.model = this.transform.Find("color").gameObject;
            this.model.transform.localScale = Vector3.one;
            Debug.Log("Model loaded!");
        }

        protected virtual void LoadMaterial() { 
            if(material != null) return;
            this.material = this.currentRenderer.material;
            Debug.Log("Material loaded!");
        }

        protected virtual void LoadRenderer() {
            if (currentRenderer != null) return;
            this.currentRenderer = GetComponentInChildren<Renderer>();
            Debug.Log("Renderer loaded!");
        }

        [ProButton]
        public virtual void ResetSize() {

            this.model.transform.localScale = Vector3.one;
            this.exchangeColor.boxCollider.isTrigger = true;
            this.CountPercent = 1;
        }



        [ProButton]
        public virtual void DecreaseSize() { 

            if(CountPercent <= 0) return;

            float newY = this.model.transform.localScale.y - 1f;

            this.model.transform.localScale = new Vector3(1, newY, 1);

            CountPercent--;

            if (CountPercent <= 0) this.exchangeColor.boxCollider.isTrigger = false;

        }




        [ProButton]
        public virtual void ReplaceMaterial() {

            if (currentRenderer == null) LoadRenderer();

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