

using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace CountingCore {


    [RequireComponent(typeof(SphereCollider))]
    public class GrabableObject: NewMonobehavior
    {
        [SerializeField] protected SphereCollider sphereCollider;
        
        public ItemGrabCode itemGrabCode;

        public bool isImmortal = false;

        protected Vector3 initialParentPosition;

        protected override void Start() {
            base.Awake();
            if (transform.parent != null)
                initialParentPosition = transform.parent.position;
        }

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadSphereCollider();
        }

        protected virtual void LoadSphereCollider() {
            if (this.sphereCollider != null) return;
            this.sphereCollider = GetComponent<SphereCollider>();
            /*this.sphereCollider.radius = 0.1f;*/
            /*this.sphereCollider.center = new Vector3(0, 0.05f, 0);*/
            Debug.Log(transform.name + ": LoadSphereCollider: ", gameObject);
        }



        public virtual void Checking() { 
            Debug.Log("Checking: " + this.gameObject.name, gameObject);
            Destroy(this.transform.parent.gameObject);
        }

        public virtual void Respawn() {
            if (isImmortal) this.transform.parent.gameObject.SetActive(false);
            if (transform.parent != null)
                transform.parent.position = initialParentPosition;
            
        }
    }
}