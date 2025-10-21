using UnityEngine;

namespace DrawingSystem {

    public class CheckingAnswer : ButtonAbstact {

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
            if (gamePlayCtrl.activeDrawingObject == null) return;

            Debug.Log(gamePlayCtrl.CheckingAnswer());
            if (gamePlayCtrl.CheckingAnswer()) {
                _ = VoicelineCtrl.Instance.PlayAnimation(BaseVoiceType.rightawnser);
            } else { 
                _ = VoicelineCtrl.Instance.PlayAnimation(BaseVoiceType.wrongawnser);
            }
            

           /* gamePlayCtrl.CheckingAnswer();*/
        }
    }



}