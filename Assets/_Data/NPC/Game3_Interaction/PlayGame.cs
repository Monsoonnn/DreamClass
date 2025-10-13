using GrabQuiz.Animals;
using UnityEngine;

namespace DrawingSystem {

    public class PlayGame : ButtonAbstact {

        public GameplayDrawingGameCtrl gamePlayCtrl;

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadGameplayCtrl();
        }

        protected virtual void LoadGameplayCtrl() {
            if (this.gamePlayCtrl != null) return;
            this.gamePlayCtrl = GameObject.FindAnyObjectByType<GameplayDrawingGameCtrl>();
            Debug.Log(transform.name + ": LoadGameplayCtrl ", gameObject);
        }


        protected override void OnClick() {
            Debug.Log(transform.name + ": OnClick ", gameObject);
            gamePlayCtrl.StartNewGame();
        }
    }



}