using System.Collections;
using System.Collections.Generic;
using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;

namespace DreamClass.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class WelcomeAudio : MonoBehaviour, IWelcomeAudioPlayer
    {
        [Header("Audio Configuration")]
        [Tooltip("Unique ID for this welcome audio. Used to track playback history.")]
        [SerializeField] private string audioId = "welcome_main";
        
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private List<AudioClip> audioClips;

        public string AudioId => audioId;
        public bool IsReady => _audioSource != null && audioClips != null && audioClips.Count > 0;

        private void Awake()
        {
            LoadAudioSource();
        }

        private void LoadAudioSource()
        {
            if (_audioSource != null) return;
            _audioSource = GetComponent<AudioSource>();
        }

        /// <summary>
        /// Called by AudioManager to play the welcome audio sequence.
        /// </summary>
        [ProButton]
        public void Play()
        {
            if (!IsReady)
            {
                Debug.LogWarning($"[WelcomeAudio] Cannot play - not ready. AudioId: {audioId}");
                return;
            }

            Debug.Log($"[WelcomeAudio] Playing audio sequence. AudioId: {audioId}");
            StartCoroutine(PlayAllClips());
        }

        private IEnumerator PlayAllClips()
        {
            foreach (var clip in audioClips)
            {
                if (clip == null) continue;
                
                _audioSource.clip = clip;
                _audioSource.Play();
                
                Debug.Log($"[WelcomeAudio] Playing clip: {clip.name}");
                yield return new WaitForSeconds(clip.length);
            }
            
            Debug.Log($"[WelcomeAudio] Finished playing all clips. AudioId: {audioId}");
        }
    }
}
