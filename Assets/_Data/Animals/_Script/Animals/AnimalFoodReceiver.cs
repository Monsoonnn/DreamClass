using CountingCore;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;



namespace GrabQuiz.Animals {
    [RequireComponent(typeof(SphereCollider))]
    public class AnimalFoodReceiver : NewMonobehavior {
        public SphereCollider sphereCollider;
        public SuggetionCounter suggetionCounter;
        public AnimalCtrl animalCtrl;
        public Image imageEmoji;
        public bool isTutorial = false;
        private void OnTriggerEnter( Collider other ) {


            Debug.Log("OnTriggerEnter: " + other.gameObject.name, gameObject);

            AnimalFoodSender animalFood = other.gameObject.GetComponentInChildren<AnimalFoodSender>();

            if (animalFood == null) return;
          
            if (animalFood.animalsCodeName != this.animalCtrl.animalsCodeName) {
                this.SetEmoji(2);
                StartCoroutine(ResetEmojiAfterDelay(1.5f));
                if (!suggetionCounter.UpdateSuggetion()) {
                    _ = VoicelineCtrl.Instance.PlayAnimation(VoiceType.wrongawnser);
                }
                animalFood.Respawn();
                return;
            }
            this.SetEmoji(1);
            if (isTutorial) _ = VoicelineCtrl.Instance.PlayAnimation(VoiceType.tutorialAnswer);
            else _ = VoicelineCtrl.Instance.PlayAnimation(VoiceType.rightawnser);

            animalCtrl.SetMoving(false);
            animalFood.Checking();

        }
        private IEnumerator ResetEmojiAfterDelay( float delay) {
            yield return new WaitForSeconds(delay);
            this.SetEmoji(0);
        }

        public void SetEmoji( int status ) {
            switch (status) {
                case 0:
                    imageEmoji.sprite = this.animalCtrl.animalSprites[0];
                    break;
                case 1:
                    imageEmoji.sprite = this.animalCtrl.animalSprites[1];
                    break;
                case 2:
                    imageEmoji.sprite = this.animalCtrl.animalSprites[2];
                    break;
            }
        }
        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadSphereCollider();
            this.LoadAnimalCtrl();
            this.LoadImageEmoji();
            this.LoadSuggetionCounter();
        }

        protected virtual void LoadSuggetionCounter() { 
            if(this.suggetionCounter != null) return;
            this.suggetionCounter = this.transform.parent.parent.parent.GetComponentInChildren<SuggetionCounter>();
            Debug.Log(transform.name + " LoadSuggetionCounter: " + this.suggetionCounter);
        }

        protected virtual void LoadImageEmoji() { 
            if(this.imageEmoji != null) return;
            this.imageEmoji = this.animalCtrl.GetComponentInChildren<Image>();
            Debug.Log(transform.name + " LoadImageEmoji: " + this.imageEmoji);
        }

        protected virtual void LoadSphereCollider() {
            if (this.sphereCollider != null) return;
            this.sphereCollider = this.GetComponent<SphereCollider>();
            Debug.Log(transform.name + " LoadSphereCollider: " + this.sphereCollider);
        }

        protected virtual void LoadAnimalCtrl() {
            if (this.animalCtrl != null) return;
            this.animalCtrl = this.GetComponentInParent<AnimalCtrl>();
            Debug.Log(transform.name + " LoadAnimalCtrl: " + this.animalCtrl);
        }
        

    }

}
