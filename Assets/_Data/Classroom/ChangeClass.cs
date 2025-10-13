using Systems.SceneManagement;
using UnityEngine;

public class ChangeClass : ButtonAbstact {

    public int GroupScene = 0;
    public SceneLoader sceneLoader;

    protected override void Start() {
        base.Start();
        sceneLoader = GameObject.FindAnyObjectByType<SceneLoader>();
    }

    protected override void OnClick() {
        LoadSceneGroup( sceneLoader, GroupScene );
    }

    static async void LoadSceneGroup( SceneLoader sceneLoader, int index ) {
        await sceneLoader.LoadSceneGroup(index);
    }
}
