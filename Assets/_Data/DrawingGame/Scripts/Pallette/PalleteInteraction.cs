using UnityEngine;

namespace DrawingSystem {

    public class PalleteInteraction : NewMonobehavior {
        [SerializeField] public PalleteCtrl palleteCtrl;
        [SerializeField] public GameObject player = null;
        [SerializeField] private LayerMask _collisionLayers = 1 << 0;
        [SerializeField] private float _collisionRadius = 0.2f;


        protected virtual void FixedUpdate() {
            if (player != null) {
                DetectHit(player.transform.position);
            }
        }

        protected override void LoadComponents() { 
            base.LoadComponents();
            if(palleteCtrl == null) palleteCtrl = GetComponentInParent<PalleteCtrl>();
        }



        private void DetectHit( Vector3 loc ) {

            Collider[] objs = Physics.OverlapSphere(loc, _collisionRadius, _collisionLayers, QueryTriggerInteraction.Ignore);
            foreach (var obj in objs) {

                BusketInteractionCtrlUI busket = obj.GetComponent<BusketInteractionCtrlUI>();
                if (busket != null) {
                    busket.isInteract = true;
                    busket.getColorBusketBtn.pallete = palleteCtrl;
                }
            }
        }

    }





}
