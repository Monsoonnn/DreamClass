using Systems.SceneManagement;
using UnityEngine;
using static UnityEngine.GUI;

public class AcceptWarning : ButtonAbstact {
    public int GroupScene;
    public bool isAccept;
    public GameObject wall;
    public SceneLoader sceneLoader;
    public InteractionUICtrl interactionCanvas;
    public GameObject canvas;
    

    protected override void Start() {
        base.Start();
       
        sceneLoader = GameObject.FindAnyObjectByType<SceneLoader>();
    }

    protected override void OnClick() {
        Debug.Log("AcceptWarning");
        if(!isAccept) Application.Quit();
        else this.EnableChangeWall();
    }

    static async void LoadSceneGroup( SceneLoader sceneLoader, int index ) {
        await sceneLoader.LoadSceneGroup(index);
    }

    protected void EnableChangeWall() { 

       
        interactionCanvas.gameObject.SetActive(false);
        canvas.gameObject.SetActive(false);
        _ = VoicelineCtrl.Instance.PlayAnimation(VoiceType.tutorialChangeRoom);
        wall.SetActive(false);
    }

}
