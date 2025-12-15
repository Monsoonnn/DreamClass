using UnityEngine;
using System.Collections.Generic;
using Systems.SceneManagement;

namespace DreamClass.Audio
{
    /// <summary>
    /// Singleton manager for handling global audio playback.
    /// Discovers and manages IWelcomeAudioPlayer components after scene loads.
    /// Persists across all scenes.
    /// </summary>
    public class AudioManager : SingletonCtrl<AudioManager>
    {
        private ISceneLoadNotifier sceneLoadNotifier;

        // Track which audio IDs have been played this session
        private static HashSet<string> playedAudioIds = new HashSet<string>();
        
        // Store discovered audio players
        private List<IWelcomeAudioPlayer> welcomeAudioPlayers = new List<IWelcomeAudioPlayer>();

        protected override void Awake()
        {
            base.Awake();
            // Subscribe early in Awake to catch the first scene load event
            SubscribeToSceneLoader();
        }

        private void SubscribeToSceneLoader()
        {
            MonoBehaviour[] allMonoBehaviours = FindObjectsOfType<MonoBehaviour>(true);
            foreach (MonoBehaviour mb in allMonoBehaviours)
            {
                if (mb is ISceneLoadNotifier notifier)
                {
                    sceneLoadNotifier = notifier;
                    sceneLoadNotifier.OnSceneLoadComplete += OnSceneLoadComplete;
                    Debug.Log($"[AudioManager] Subscribed to scene load notifier on {mb.gameObject.name}");
                    break;
                }
            }
            
            if (sceneLoadNotifier == null)
            {
                Debug.LogWarning("[AudioManager] No ISceneLoadNotifier found. Welcome audio will not be triggered automatically.");
            }
        }

        /// <summary>
        /// Called when scene loading is complete.
        /// Discovers and plays welcome audio that hasn't been played yet.
        /// </summary>
        private void OnSceneLoadComplete()
        {
            Debug.Log("[AudioManager] Scene load complete. Discovering welcome audio players...");
            
            // Discover all welcome audio players in the scene
            DiscoverWelcomeAudioPlayers();
            
            // Play audio that hasn't been played yet
            PlayNewWelcomeAudio();
        }

        /// <summary>
        /// Finds all MonoBehaviours that implement IWelcomeAudioPlayer.
        /// </summary>
        private void DiscoverWelcomeAudioPlayers()
        {
            welcomeAudioPlayers.Clear();
            
            MonoBehaviour[] allMonoBehaviours = FindObjectsOfType<MonoBehaviour>(true);
            foreach (MonoBehaviour mb in allMonoBehaviours)
            {
                if (mb is IWelcomeAudioPlayer player)
                {
                    welcomeAudioPlayers.Add(player);
                    Debug.Log($"[AudioManager] Discovered welcome audio player: {player.AudioId} on {mb.gameObject.name}");
                }
            }
            
            Debug.Log($"[AudioManager] Total welcome audio players found: {welcomeAudioPlayers.Count}");
        }

        /// <summary>
        /// Plays welcome audio that hasn't been played this session.
        /// </summary>
        private void PlayNewWelcomeAudio()
        {
            if (welcomeAudioPlayers.Count == 0)
            {
                Debug.Log("[AudioManager] No welcome audio players found in scene.");
                return;
            }

            foreach (var player in welcomeAudioPlayers)
            {
                // Check if this audio has already been played
                if (playedAudioIds.Contains(player.AudioId))
                {
                    Debug.Log($"[AudioManager] Skipping already played audio: {player.AudioId}");
                    continue;
                }

                // Check if player is ready
                if (!player.IsReady)
                {
                    Debug.LogWarning($"[AudioManager] Player not ready: {player.AudioId}");
                    continue;
                }

                // Play the audio
                Debug.Log($"[AudioManager] Playing welcome audio: {player.AudioId}");
                player.Play();
                
                // Mark as played
                playedAudioIds.Add(player.AudioId);
                
                // Only play the first unplayed audio
                break;
            }
        }

        /// <summary>
        /// Manually trigger welcome audio discovery and playback.
        /// Useful for testing or special scenarios.
        /// </summary>
        public void TriggerWelcomeAudio()
        {
            Debug.Log("[AudioManager] Manually triggering welcome audio...");
            DiscoverWelcomeAudioPlayers();
            PlayNewWelcomeAudio();
        }

        /// <summary>
        /// Check if a specific audio ID has been played.
        /// </summary>
        public bool HasPlayedAudio(string audioId)
        {
            return playedAudioIds.Contains(audioId);
        }

        /// <summary>
        /// Reset the played audio history (useful for testing).
        /// </summary>
        public void ResetPlayedAudioHistory()
        {
            playedAudioIds.Clear();
            Debug.Log("[AudioManager] Played audio history reset.");
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (Instance == this && sceneLoadNotifier != null)
            {
                sceneLoadNotifier.OnSceneLoadComplete -= OnSceneLoadComplete;
            }
        }
    }
}