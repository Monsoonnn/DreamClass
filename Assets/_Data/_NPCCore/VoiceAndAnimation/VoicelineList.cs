using System.Threading;
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

        public async Task PlayAsync(
            AudioSource audioSource, 
            AnimationManager animationManager, 
            bool disableLoop = false,
            CancellationToken cancellationToken = default 
        ) {
            // Kiểm tra hủy ngay từ đầu
            if (cancellationToken.IsCancellationRequested) {
                Debug.Log($"[Voiceline] {voiceType} cancelled before start");
                return;
            }

            if (animationManager == null) {
                Debug.LogWarning("Missing AnimationManager in Voiceline!");
                return;
            }

            // Pick random audio clip
            AudioClip clip = audioClips != null && audioClips.Length > 0
                ? audioClips[Random.Range(0, audioClips.Length)]
                : null;

            // Step 1 - Play animation (no auto return)
            animationManager.PlayGroupByName(animationGroupName, disableLoop, clip?.length ?? 0);

            float waitTime = 0f;

            // Step 2 - Play voice clip
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

            // Wait với cancellation token
            try {
                await Task.Delay((int)(waitTime * 1000), cancellationToken);
            } catch (TaskCanceledException) {
                Debug.Log($"[Voiceline] {voiceType} cancelled during playback");
                
                if (audioSource != null) {
                    audioSource.Stop();
                }
                return;
            }

            // Kiểm tra null trước khi dừng
            if (audioSource != null) {
                audioSource.Stop();
                Debug.Log($"[Voiceline] Finished {voiceType}, returned to Idle.");
            } else {
                Debug.LogWarning($"[Voiceline] AudioSource destroyed while playing {voiceType}");
            }
        }
    }
}