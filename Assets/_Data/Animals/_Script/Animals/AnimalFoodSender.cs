using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GrabQuiz.Animals {
    [RequireComponent(typeof(SphereCollider))]
    public class AnimalFoodSender : NewMonobehavior {
        [SerializeField] protected SphereCollider sphereCollider;

        public AnimalsCodeName animalsCodeName;

        protected Vector3 initialPosition;

        public bool isImortant = false;
        protected override void Awake() {
            base.Awake();
            initialPosition = transform.parent.position;
        }

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadSphereCollider();
        }

        protected virtual void LoadSphereCollider() {
            if (this.sphereCollider != null) return;
            this.sphereCollider = GetComponent<SphereCollider>();
            Debug.Log(transform.name + ": LoadSphereCollider", gameObject);
        }

        public virtual void Checking() {
            Debug.Log("Checking: " + this.gameObject.name, gameObject);
            if(isImortant)  this.transform.parent.gameObject.SetActive(false);
            else Destroy(this.transform.parent.gameObject);
        }

        public virtual void Respawn() {
            Transform parent = transform.parent;

            if (isImortant) {
                this.transform.parent.gameObject.SetActive(false);
                return;
            }

            parent.gameObject.SetActive(false);
            parent.gameObject.SetActive(true);
            parent.position = initialPosition;

            Debug.Log(transform.name + ": Deactivated and moved parent to initial position", gameObject);

            
        }
    }
}
