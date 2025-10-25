using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AudioManager {
    [RequireComponent(typeof(AudioSource))]
    public class WelcomeAudio : NewMonobehavior {
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private List<AudioClip> audioClips;

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadAudioSource();
        }

        protected void LoadAudioSource() {
            if (_audioSource != null) return;
            _audioSource = GetComponent<AudioSource>();
        }

        protected override void Start() {
            base.Start();
            if (audioClips != null && audioClips.Count > 0)
                StartCoroutine(PlayAllClips());
        }

        private IEnumerator PlayAllClips() {
            foreach (var clip in audioClips) {
                if (clip == null) continue;
                _audioSource.clip = clip;
                _audioSource.Play();

                // Wait for the clip to finish
                yield return new WaitForSeconds(clip.length);
            }
        }
    }
}
