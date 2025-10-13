using com.cyborgAssets.inspectorButtonPro;
using GrabQuiz.Animals;
using UnityEngine;

namespace CountingCore {
    public class Tutorial : ButtonAbstact {

        public TutorialCtrl tutorialCtrl;

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadGameplayCtrl();
        }

        protected virtual void LoadGameplayCtrl() {
            if (this.tutorialCtrl != null) return;
            this.tutorialCtrl = GameObject.FindAnyObjectByType<TutorialCtrl>();
            Debug.Log(transform.name + ": LoadGameplayCtrl ", gameObject);
        }


        protected override void OnClick() {
            Debug.Log(transform.name + ": OnClick ", gameObject);
            _ = tutorialCtrl.StartTutorial();
        }
    }
}
