using com.cyborgAssets.inspectorButtonPro;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

[System.Serializable]
public class VoicelineAnimation{
    public string name = "New voiceline group";
    public BaseVoiceType voiceType;
    private Animator animator;
    public AudioClip[] audioClips;
    public string animationName = "talking";
    public int layerIndex = 1;
    public float crossFadeDuration = 0.2f;
    public float animationLength = 2f;


    [ProButton]
    public virtual void PlayVoiceAnimation() {
        
/*         _ = StartDialogue();*/

        Debug.Log($"Playing animation '{animationName}' on layer {layerIndex}");
        
    }


    public async Task StartDialogue(AudioSource audioSource, Animator animator) {

        AudioClip audioClip = audioClips[Random.Range(0, audioClips.Length)];

        if (animator == null) {
            Debug.LogWarning("Missing Animator !");
            this.animator = animator;
            return;
        }

        // Play animation
        for (int i = 1; i <= layerIndex; i++) {
            animator.CrossFade(animationName, crossFadeDuration, i);
        }
        
        float waitTime = animationLength;

        if (audioSource != null && audioClip != null) {
            audioSource.clip = audioClip;
            audioSource.Play();
            Debug.Log("Playing audio clip: " + audioClip.length);
            waitTime = audioClip.length;
        } else {
            AnimationClip clip = GetAnimationClip(animationName);
            if (clip != null) waitTime = clip.length;
            else Debug.LogWarning("Animation clip not found, using default wait time.");
        }

        // Await time (simulate WaitForSeconds)
        await Task.Delay((int)(waitTime * 1000));

        for (int i = 1; i <= layerIndex; i++) {
            animator.CrossFade("empty", crossFadeDuration, i);
        }

        Debug.Log("Returned to Idle on base layer.");

        // Small delay to simulate yield return new WaitForSeconds(0.5f)
        await Task.Delay(500);
    }

    private AnimationClip GetAnimationClip( string animationName ) {
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips) {
            if (clip.name == animationName) {
                return clip;
            }
        }
        return null;
    }

    public virtual void ResetState() {
        
    }
}