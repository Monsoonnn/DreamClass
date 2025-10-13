using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;

namespace GrabQuiz.Animals {
    public class GameplayAnimalCtrl : NewMonobehavior {
        [SerializeField] protected AnimalsHouse animalsHouse;
        [SerializeField] protected TutorialCtrl tutorialCtrl;

        protected override void Start() {
            this.DeactivateChildren();
        }

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadAnimalsHouse();
        }

        protected virtual void LoadAnimalsHouse() {
            if (this.animalsHouse != null) return;
            this.animalsHouse = this.GetComponentInChildren<AnimalsHouse>();
            Debug.Log(transform.name + " LoadAnimalsHouse: " + this.animalsHouse);
        }

        protected virtual void DeactivateChildren() {
            foreach (Transform child in transform) {
                child.gameObject.SetActive(false);
            }
        }

        protected virtual void ActivateChildren() {
            foreach (Transform child in transform) {
                child.gameObject.SetActive(true);
            }
        }
        [ProButton]
        public void StartGameplay() {
            this.ActivateChildren();
            this.animalsHouse.StartRespawnCycle();
        }
        [ProButton]
        public void StopGameplay() {
            this.animalsHouse.StopRespawnCycle();
        }
        [ProButton]
        public void StartTutorial() {
            _ = this.tutorialCtrl.StartTutorial();
        }
    }
}
