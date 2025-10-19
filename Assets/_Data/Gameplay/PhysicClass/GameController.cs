using UnityEngine;

public abstract class GameController : NewMonobehavior {

    public GuideStepManager guideStepManager;

    public void OnActiveGame() {
        guideStepManager.SetCurrentGuide(this.GetExperimentName());
        guideStepManager.gameController = this;
    }

    public void OnDisableGame() {
       
        guideStepManager.gameController = null;
        guideStepManager.RestartGuide();
    }


    public virtual void StartExperiment() {
        //Override
    }

    public virtual void StopExperiment() {
        //Override
    }

    public abstract string GetExperimentName();

    public void ActiveGamePlay(string id) { 
        if(id == GetExperimentName()) transform.parent.gameObject.SetActive(true);
        return;
    }
}
