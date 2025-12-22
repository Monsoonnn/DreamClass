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
            // Optimization: Find ISceneLoadNotifier specifically if possible, 
            // or rely on a known manager instead of scanning all MonoBehaviours.
            // For now, keeping it but using FindFirstObjectByType if available or caching.
            // Actually, usually SceneLoader is a singleton or easy to find.
            
            // Try to find specific component implementing it
            var notifier = FindAnyObjectByType<Systems.SceneManagement.SceneLoader>();
            if (notifier != null)
            {
                sceneLoadNotifier = notifier;
                sceneLoadNotifier.OnSceneLoadComplete += OnSceneLoadComplete;
                Debug.Log($"[AudioManager] Subscribed to scene load notifier on {notifier.gameObject.name}");
            }
            else
            {
                 // Fallback to old scan if specific class not found
                MonoBehaviour[] allMonoBehaviours = FindObjectsOfType<MonoBehaviour>(true);
                foreach (MonoBehaviour mb in allMonoBehaviours)
                {
                    if (mb is ISceneLoadNotifier n)
                    {
                        sceneLoadNotifier = n;
                        sceneLoadNotifier.OnSceneLoadComplete += OnSceneLoadComplete;
                        Debug.Log($"[AudioManager] Subscribed to scene load notifier on {mb.gameObject.name}");
                        break;
                    }
                }
            }
            
            if (sceneLoadNotifier == null)
            {
                Debug.LogWarning("[AudioManager] No ISceneLoadNotifier found. Welcome audio will not be triggered automatically.");
            }
        }

        /// <summary>
        /// Called when scene loading is complete.
        /// </summary>
        private void OnSceneLoadComplete()
        {
            Debug.Log("[AudioManager] Scene load complete. Playing registered audio...");
            PlayNewWelcomeAudio();
        }

        /// <summary>
        /// Registers a player to the manager. 
        /// Should be called by IWelcomeAudioPlayer in Start/OnEnable.
        /// </summary>
        public void RegisterPlayer(IWelcomeAudioPlayer player)
        {
            if (!welcomeAudioPlayers.Contains(player))
            {
                welcomeAudioPlayers.Add(player);
                // Try playing immediately if scene is already loaded? 
                // Or just wait for SceneLoadComplete?
                // Usually we wait for SceneLoadComplete. 
                // But if this is registered LATE (after load), we might want to trigger it?
                // For safety, let's just add it. The OnSceneLoadComplete handles the main flow.
            }
        }

        public void UnregisterPlayer(IWelcomeAudioPlayer player)
        {
            if (welcomeAudioPlayers.Contains(player))
            {
                welcomeAudioPlayers.Remove(player);
            }
        }

        /// <summary>
        /// Plays welcome audio that meets criteria (PlayOnce check).
        /// </summary>
        private void PlayNewWelcomeAudio()
        {
            if (welcomeAudioPlayers.Count == 0) return;

            // Sort or prioritize? For now just iterate.
            // Remove nulls just in case
            welcomeAudioPlayers.RemoveAll(p => p == null);

            foreach (var player in welcomeAudioPlayers)
            {
                // Check PlayOnce condition
                if (player.PlayOnce && playedAudioIds.Contains(player.AudioId))
                {
                    Debug.Log($"[AudioManager] Skipping played audio (PlayOnce): {player.AudioId}");
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
                
                // Mark as played if PlayOnce is true (or always? usually track history anyway)
                if (player.PlayOnce)
                {
                    playedAudioIds.Add(player.AudioId);
                }
                
                // Only play the first valid audio found? 
                // Original logic had "break". 
                // If multiple are present, usually we only want one "Welcome".
                break;
            }
        }

        // ... (rest of methods)

        /// <summary>
        /// Manually trigger welcome audio discovery and playback.
        /// Useful for testing or special scenarios.
        /// </summary>
        public void TriggerWelcomeAudio()
        {
            Debug.Log("[AudioManager] Manually triggering welcome audio...");
            //DiscoverWelcomeAudioPlayers();
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