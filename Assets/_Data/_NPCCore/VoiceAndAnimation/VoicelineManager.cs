using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using NPCCore.Animation;
using DreamClass.NPCCore;
using com.cyborgAssets.inspectorButtonPro;

namespace NPCCore.Voiceline {
    public abstract class VoicelineManager<TVoiceType> : NewMonobehavior, ICharacterVoiceline {
        [Header("Voicelines")]
        public VoicelineAnimation<TVoiceType>[] voicelines;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private NPCManager NPCManager;

        private CancellationTokenSource cancellationTokenSource;

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadAudioSource();
            this.LoadNPCManager();
        }

        protected virtual void LoadAudioSource() {
            if (audioSource != null)
            {
                if (audioSource.clip != null) audioSource.Stop();
                return;
            }
            audioSource = GetComponent<AudioSource>();
        }

        protected virtual void LoadNPCManager() {
            if (NPCManager != null) return;
            NPCManager = transform.parent.GetComponent<NPCManager>();
        }

        // Tạo token mới mỗi khi enable
        protected virtual void OnEnable() {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = new CancellationTokenSource();
        }

        // Hủy tất cả task khi disable/destroy
        protected virtual void OnDisable() {
            CancelAllTasks();
        }

        protected virtual void OnDestroy() {
            CancelAllTasks();
        }

        private void CancelAllTasks() {
            if (cancellationTokenSource != null) {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
                Debug.Log($"[VoicelineManager] All tasks cancelled for {gameObject.name}");
            }
        }

        [ProButton] 
        public virtual async Task PlayAnimation(TVoiceType voiceType, bool disableLoop = false) { 
            // Kiểm tra token hợp lệ
            if (cancellationTokenSource == null || cancellationTokenSource.Token.IsCancellationRequested) {
                Debug.LogWarning($"[VoicelineManager] Cannot play {voiceType} - token cancelled or null");
                return;
            }

            var voiceline = GetVoiceline(voiceType); 
            if (voiceline != null) {
                try {
                    // Truyền token vào
                    await voiceline.PlayAsync(
                        audioSource, 
                        NPCManager.AnimationManager, 
                        disableLoop,
                        cancellationTokenSource.Token // ← QUAN TRỌNG
                    );
                } catch (System.Exception ex) {
                    if (ex is TaskCanceledException) {
                        Debug.Log($"[VoicelineManager] {voiceType} playback cancelled");
                    } else {
                        Debug.LogError($"[VoicelineManager] Error playing {voiceType}: {ex.Message}");
                    }
                }
            } else {
                Debug.LogWarning($"Voiceline {voiceType} not found!"); 
            }
        }

        protected virtual VoicelineAnimation<TVoiceType> GetVoiceline(TVoiceType type) {
            foreach (var v in voicelines)
                if (EqualityComparer<TVoiceType>.Default.Equals(v.voiceType, type))
                    return v;
            return null;
        }

        
        public virtual void CancelAudio() {
            if (audioSource != null && audioSource.isPlaying) {
                audioSource.Stop();
            }
            
            // Hủy task đang chạy
            CancelAllTasks();
            
            // Tạo token mới cho lần chạy tiếp theo
            cancellationTokenSource = new CancellationTokenSource();
        }

        // Interface method — convert from string -> enum
        public async Task PlayAnimation(string voiceKey, bool disableLoop = false) {
            try {
                if (System.Enum.TryParse(typeof(TVoiceType), voiceKey, out var parsed))
                    await PlayAnimation((TVoiceType)parsed, disableLoop);
                else
                    Debug.LogWarning($"[VoicelineManager] Invalid voice key '{voiceKey}' for {typeof(TVoiceType).Name}");
            }
            catch (System.Exception ex) {
                Debug.LogError($"[VoicelineManager] Failed to play '{voiceKey}': {ex.Message}");
            }
        }
    }
}