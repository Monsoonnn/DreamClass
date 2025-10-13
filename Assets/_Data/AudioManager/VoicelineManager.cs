using com.cyborgAssets.inspectorButtonPro;
using System.Collections.Generic;
using UnityEngine;

namespace AudioManager {
    public class VoicelineManager : NewMonobehavior {
        [Header("Folder inside Resources/ (e.g. 'Voicelines')")]
        public string folderPath = "Voicelines";

        public AudioClip[] voicelines;

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadVoicelines();
        }

        protected virtual void LoadVoicelines() {
            voicelines = Resources.LoadAll<AudioClip>(folderPath);

            if (voicelines.Length == 0) {
                Debug.LogWarning("No voicelines found at: Resources/" + folderPath);
            } else {
                Debug.Log("Loaded " + voicelines.Length + " voicelines.");
            }
        }

        public AudioClip GetRandomVoiceline() {
            if (voicelines == null || voicelines.Length == 0) return null;
            return voicelines[Random.Range(0, voicelines.Length)];
        }


        [ProButton]
        public virtual void SetVolume(int newVolume) {

            List<MetaXRAudioSource> audioManager = new List<MetaXRAudioSource>(
             GameObject.FindObjectsByType<MetaXRAudioSource>(FindObjectsSortMode.None)
             );

            foreach (MetaXRAudioSource audioSource in audioManager) {
                audioSource.GainBoostDb = newVolume;
            }

        }
    }

}