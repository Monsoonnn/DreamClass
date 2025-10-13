using com.cyborgAssets.inspectorButtonPro;
using Unity.VisualScripting;
using UnityEngine;

public class MusicLoader : NewMonobehavior {

    public AudioSource audioSource;

    public string audioClipName;

    protected void OnEnable() { 
        audioSource.Play();
    }

    protected void OnDisable() {
        audioSource.Stop();
        audioSource.time = 0f;
    }

    protected override void LoadComponents() {
        base.LoadComponents();
        this.LoadAudioScoure();
    }

    protected void LoadAudioScoure() {
        if (audioSource != null) return;
        audioSource = GetComponent<AudioSource>();
        LoadAudioClip(audioClipName);
        Debug.Log("Loaded audio clip: " + audioClipName);
        
    }

    [ProButton]
    void LoadAudioClip( string name ) {
        AudioClip clip = GameObject.FindAnyObjectByType<MusicManager>().GetClipByName(name);
        
        if(clip == null) return;

        audioSource.clip = clip;

        audioClipName = name;
            
    }
}
