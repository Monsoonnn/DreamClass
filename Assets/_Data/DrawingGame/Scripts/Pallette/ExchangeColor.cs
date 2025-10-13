using UnityEngine;

namespace DrawingSystem {

    [RequireComponent(typeof(BoxCollider))]
    public class ExchangeColor : NewMonobehavior {
        
        public BoxCollider boxCollider;
        [SerializeField] private PalleteColor color;



        private void OnTriggerEnter( Collider other ) {
            ColorIndicator indicator = other.GetComponent<ColorIndicator>();
            if (indicator != null) {
                 indicator.colorMaterial = color.material;   
                 indicator.ApplyMaterial();
            }
        }



        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadBoxColider3D();
            this.LoadPalleteColor();
        }

        protected virtual void LoadPalleteColor() { 
            if(color != null) return;
            this.color = GetComponentInParent<PalleteColor>();
            Debug.Log(transform.name + " : LoadPalleteColor: " + gameObject);
        }

        protected virtual void LoadBoxColider3D() { 
            if(boxCollider != null) return;
            this.boxCollider = GetComponent<BoxCollider>();
            this.boxCollider.isTrigger = true;
            this.boxCollider.center = new Vector3(-0.01f, 0.1f, -0.035f);
            this.boxCollider.size = new Vector3(0.15f, 0.15f, 0.15f);
            Debug.Log(transform.name + " : LoadBoxColider3D: " + gameObject);
        }

       

    }




}