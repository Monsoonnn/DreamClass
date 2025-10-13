using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class Bootstrapper : SingletonCtrl<Bootstrapper> {
    static readonly int sceneIndex = 0;
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init() {
        Debug.Log("Bootstrapper...");
#if UNITY_EDITOR

        /*EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(EditorBuildSettings.scenes[sceneIndex].path);*/
        EditorSceneManager.playModeStartScene = null;
#endif
    }
}
