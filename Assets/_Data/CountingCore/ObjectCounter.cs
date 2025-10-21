using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CountingCore {
    
    [RequireComponent(typeof(BoxCollider))]
    public class ObjectCounter : NewMonobehavior {

        [SerializeField] protected BoxCollider boxColider;
        [SerializeField] protected CountingManager countingManager;
        [SerializeField] protected SuggetionCounter suggetionCounter;
        public int TargetCount = 3;
        public bool isTutorial = false;
        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadBoxCollider();
            this.LoadCountingManager();
            this.LoadSuggetionCounter();
        }

        protected virtual void LoadSuggetionCounter() { 
            if (this.suggetionCounter != null) return;
            this.suggetionCounter = transform.parent.parent.GetComponentInChildren<SuggetionCounter>();
            Debug.Log(transform.name + ": LoadSuggetionCounter: ", gameObject);
        }
        protected virtual void LoadCountingManager() {
            if (this.countingManager != null) return;
            this.countingManager = transform.GetComponentInParent<CountingManager>();
            Debug.Log(transform.name + ": LoadCountingManager: ", gameObject);
        }


        protected virtual void LoadBoxCollider() {
            if (this.boxColider != null) return;
            this.boxColider = GetComponent<BoxCollider>();
            this.boxColider.isTrigger = true;
            this.boxColider.center= new Vector3(0f,0.7f,0f);
            this.boxColider.size = new Vector3(0.8f,0.1f,0.8f);
            Debug.Log(transform.name + ": LoadBoxCollider: ", gameObject);
        }

        private void OnTriggerEnter( Collider other ) {


            Debug.Log("OnTriggerEnter: " + other.gameObject.name, gameObject);

            GrabableObject grabableObject = other.gameObject.GetComponentInChildren<GrabableObject>();

            if (grabableObject == null) return;

            if (grabableObject.isImmortal) {
                grabableObject.Respawn();
                return;
            }

            if (grabableObject.itemGrabCode != this.countingManager.ItemGrabCode) { 
                Debug.Log("itemGrabCode: " + grabableObject.itemGrabCode + " != " + this.countingManager.ItemGrabCode, gameObject);
                if(!suggetionCounter.UpdateSuggetion()){
                    _ = VoicelineCtrl.Instance.PlayAnimation(BaseVoiceType.wrongawnser);
                }
                grabableObject.Respawn();
                return; 
            }

            grabableObject.Checking();
            if(isTutorial) _ = VoicelineCtrl.Instance.PlayAnimation(BaseVoiceType.tutorialAnswer);
            else _ = VoicelineCtrl.Instance.PlayAnimation(BaseVoiceType.rightawnser);
            this.TargetCount--;

        }

    }

}