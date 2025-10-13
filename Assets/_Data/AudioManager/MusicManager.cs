using UnityEngine;

public class MusicManager : NewMonobehavior {
    [Header("Folder path inside Resources/")]
    public string folderPath = "Music";

    public AudioClip[] musicClip;

    protected override void LoadComponents() {
        base.LoadComponents();
        this.LoadAllClips();
    }

    void LoadAllClips() {
        musicClip = Resources.LoadAll<AudioClip>(folderPath);

        if (musicClip.Length == 0) {
            Debug.LogWarning("No music clips found at: Resources/" + folderPath);
        } else {
            Debug.Log("Loaded " + musicClip.Length + " music clips.");
            foreach (AudioClip clip in musicClip) {
                Debug.Log($"Loaded: {clip.name}");
            }
        }
    }

    public AudioClip GetClipByName( string clipName ) {
        foreach (AudioClip clip in musicClip) {
            if (clip.name == clipName) {
                return clip;
            }
        }

        Debug.LogWarning($"[MusicManager] Clip not found: {clipName}");
        return null;
    }
}
