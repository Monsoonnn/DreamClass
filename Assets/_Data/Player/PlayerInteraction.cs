using UnityEngine;

namespace playerCtrl {
    public class PlayerInteraction : NewMonobehavior {

        [SerializeField] public GameObject player = null;
        [SerializeField] private LayerMask _collisionLayers = 1 << 0;
        [SerializeField] private float _collisionRadius = 0.2f;

        protected virtual void FixedUpdate() {
            if (player != null) {
                DetectHit(player.transform.position);
            }
        }

        private void DetectHit( Vector3 loc ) {
            
            Collider[] objs = Physics.OverlapSphere(loc, _collisionRadius, _collisionLayers, QueryTriggerInteraction.Ignore);
            foreach (var obj in objs) {
                
                InteractionUICtrl npc = obj.GetComponent<InteractionUICtrl>();
                if (npc != null) {
                    npc.isInteract = true;
                }
            }
        }
    }
}
