using System.Threading.Tasks;
using UnityEngine;
using NPCCore.Animation;

namespace NPCCore.Voiceline {
    [System.Serializable]
    public class VoicelineAnimation<TVoiceType> {
        public string name = "New Voiceline";
        public TVoiceType voiceType;

        [Header("Audio Settings")]
        public AudioClip[] audioClips;

        [Header("Animation Settings")]
        public string animationGroupName = "Talk";
        public bool disableLoop = true;
        public float crossFade = 0.2f;

        public async Task PlayAsync( AudioSource audioSource, AnimationManager animationManager, bool disableLoop = false ) {
            if (animationManager == null) {
                Debug.LogWarning("Missing AnimationManager in Voiceline!");
                return;
            }


            // Pick random audio clip
            AudioClip clip = audioClips != null && audioClips.Length > 0
                ? audioClips[Random.Range(0, audioClips.Length)]
                : null;

            // Step 1️ - Play animation (no auto return)
            animationManager.PlayGroupByName(animationGroupName, disableLoop, clip.length);

            float waitTime = 0f;

            // Step 2️ - Play voice clip
            if (clip != null && audioSource != null) {
                audioSource.clip = clip;
                audioSource.Play();
                waitTime = clip.length;
                Debug.Log($"[Voiceline] Playing {voiceType} | Clip: {clip.name} ({waitTime:F2}s)");
            } else {
                var group = animationManager.GetAnimationSet()?.GetGroup(animationGroupName);
                if (group != null && group.layerAnimations.Count > 0) {
                    var firstClip = animationManager.FindClipByName(group.layerAnimations[0].animationName);
                    if (firstClip != null)
                        waitTime = firstClip.length;
                }
            }

            if (waitTime <= 0f) waitTime = 1f;

            // Wait until voice finishes
            await Task.Delay((int)(waitTime * 1000));

            audioSource.Stop(); // Clear audio source

            Debug.Log($"[Voiceline] Finished {voiceType}, returned to Idle.");
        }
    }
}
