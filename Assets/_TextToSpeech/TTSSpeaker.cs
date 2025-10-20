using com.cyborgAssets.inspectorButtonPro;
using TextToSpeech.TextToSpeech;
using UnityEngine;

namespace TextToSpeech {
    [RequireComponent(typeof(AudioSource))]
    public class TTSSpeaker : NewMonobehavior {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private FPTVoice defaultVoice = FPTVoice.BanMai;


        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadAudioScoure();
        }

        private void LoadAudioScoure() {
            if (audioSource != null) return;
            audioSource = GetComponent<AudioSource>();
        }

        /// <summary>
        /// Call this from any script or inspector button.
        /// </summary>
        [ProButton]
        public void Speak( string text ) {
            if (string.IsNullOrEmpty(text)) {
                Debug.LogWarning("<color=#FFFF55>[TTSSpeaker]</color> Empty text.");
                return;
            }

            Debug.Log($"<color=#55FFFF>[TTSSpeaker]</color> Requesting TTS for: {text}");
            SpeechManager.Instance.Speak(text, defaultVoice, this);
        }

        public void PlayClip( AudioClip clip ) {
            if (clip == null) {
                Debug.LogWarning("<color=#FFFF55>[TTSSpeaker]</color> Null AudioClip!");
                return;
            }

            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.Play();

            Debug.Log($"<color=#55AAFF>[TTSSpeaker]</color> Playing voice: {clip.name}");
        }

        public void Stop() {
            audioSource.Stop();
        }
    }
}
