using com.cyborgAssets.inspectorButtonPro;
using TextToSpeech.TextToSpeech;
using UnityEngine;

namespace TextToSpeech {
    public class SpeechManager : SingletonCtrl<SpeechManager> {
        [SerializeField] private FPTTTSHandler handler;
        [SerializeField] private TTSServices ttsService;

        /// <summary>
        /// Main entry to request TTS from anywhere.
        /// </summary>
        public void Speak( string text, FPTVoice voice, TTSSpeaker targetSpeaker ) {
            if (string.IsNullOrEmpty(text)) {
                Debug.LogWarning("<color=#FFFF55>[SpeechManager]</color> Empty text input, skipping speak.");
                return;
            }

            if (targetSpeaker == null) {
                Debug.LogError("<color=#FF5555>[SpeechManager]</color> Missing target TTSSpeaker!");
                return;
            }

            StopAllCoroutines();
            StartCoroutine(handler.RequestTTS(text, voice, ttsService, clip => {
                if (clip == null) {
                    Debug.LogError("<color=#FF5555>[SpeechManager]</color> Audio clip generation failed.");
                    return;
                }

                Debug.Log($"<color=#55FF55>[SpeechManager]</color> Delivering clip to speaker '{targetSpeaker.name}'.");
                targetSpeaker.PlayClip(clip);
            }));
        }
    }
}
