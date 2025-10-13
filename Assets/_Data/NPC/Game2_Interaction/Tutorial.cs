using GrabQuiz.Animals;
using UnityEngine;

public class Tutorial : ButtonAbstact {

    public GameplayAnimalCtrl gamePlayCtrl;

    protected override void LoadComponents() {
        base.LoadComponents();
        this.LoadGameplayCtrl();
    }

    protected virtual void LoadGameplayCtrl() {
        if (this.gamePlayCtrl != null) return;
        this.gamePlayCtrl = GameObject.FindAnyObjectByType<GameplayAnimalCtrl>();
        Debug.Log(transform.name + ": LoadGameplayCtrl ", gameObject);
    }


    protected override void OnClick() {
        Debug.Log(transform.name + ": OnClick ", gameObject);
        gamePlayCtrl.StartTutorial();
    }
}
