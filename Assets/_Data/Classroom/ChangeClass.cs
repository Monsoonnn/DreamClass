using Systems.SceneManagement;
using UnityEngine;

public class ChangeClass : ButtonAbstact {
    [SerializeField] private string groupName;
    [SerializeField] private SceneLoader sceneLoader;

    public string GroupName {
        get => groupName;
        set => groupName = value;
    }
    public SceneLoader SceneLoader => sceneLoader;

    protected override void Start() {
        base.Start();
        sceneLoader = GameObject.FindAnyObjectByType<SceneLoader>();
    }

    protected override void OnClick() {
        if (sceneLoader == null) {
            Debug.LogError("SceneLoader not found in scene!");
            return;
        }

        LoadSceneGroup(sceneLoader, groupName);
    }

    static async void LoadSceneGroup( SceneLoader sceneLoader, string groupName ) {
        await sceneLoader.LoadSceneGroup(groupName);
    }
}
