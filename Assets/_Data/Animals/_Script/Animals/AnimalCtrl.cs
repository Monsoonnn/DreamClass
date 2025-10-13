using com.cyborgAssets.inspectorButtonPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GrabQuiz.Animals {
    public class AnimalCtrl : NewMonobehavior {


        [SerializeField] private AnimalMovingCtrl animalMovingCtrl;
        [SerializeField] private AnimalFoodReceiver foodReceiver;
        [SerializeField] private Transform ThoughtBubble;

        public AnimalsCodeName animalsCodeName;
        public List<Sprite> animalSprites;

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadAnimalMovingCtrl();
            this.LoadFoodReceiver();
            this.LoadThoughtBubbles();
        }

        protected override void Start() {
            this.gameObject.SetActive(false);
        }

        protected virtual void LoadThoughtBubbles() { 
            if(this.ThoughtBubble != null) return;
            this.ThoughtBubble = this.transform.Find("ThoughtBubbles");
            Debug.Log(transform.name + " LoadThoughtBubbles: " + this.ThoughtBubble);
        }


        protected virtual void LoadFoodReceiver() { 
            if(this.foodReceiver != null) return;
            this.foodReceiver = this.GetComponentInChildren<AnimalFoodReceiver>();
            
            Debug.Log(transform.name + " LoadFoodReceiver: " + this.foodReceiver);
        } 

        protected virtual void LoadAnimalMovingCtrl() { 
            if(this.animalMovingCtrl != null) return;
            this.animalMovingCtrl = this.GetComponentInChildren<AnimalMovingCtrl>();
            Debug.Log(transform.name + " LoadAnimalMovingCtrl: " + this.animalMovingCtrl);
        }


        [ProButton]
        public virtual void SetMoving( bool toAppear ) {
            this.animalMovingCtrl.StartMoving(toAppear);
            
        }

        [ProButton]
        public virtual void RespawnAnimals() {
            this.gameObject.SetActive(true);
            this.animalMovingCtrl.StartMoving(true);
            this.foodReceiver.SetEmoji(0);
        }

    }
}
