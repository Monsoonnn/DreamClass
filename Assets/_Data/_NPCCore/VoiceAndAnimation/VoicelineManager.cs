using System.Collections.Generic;
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

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadAudioSource();
            this.LoadNPCManager();
        }

        protected virtual void LoadAudioSource() {
            if (audioSource != null) return;
            audioSource = GetComponent<AudioSource>();
        }

        protected virtual void LoadNPCManager() {
            if (NPCManager != null) return;
            NPCManager = transform.parent.GetComponent<NPCManager>();
        }

        [ProButton] public virtual async Task PlayAnimation( TVoiceType voiceType, bool disableLoop = false ) { 
            var voiceline = GetVoiceline(voiceType); 
            if (voiceline != null) 
                await voiceline.PlayAsync(audioSource, NPCManager.AnimationManager, disableLoop); 
            else Debug.LogWarning($"Voiceline {voiceType} not found!"); 
        }


        protected virtual VoicelineAnimation<TVoiceType> GetVoiceline( TVoiceType type ) {
            foreach (var v in voicelines)
                if (EqualityComparer<TVoiceType>.Default.Equals(v.voiceType, type))
                    return v;
            return null;
        }

        public virtual void CancelAudio() => audioSource.Stop();

        // Interface method — convert from string -> enum
        public async Task PlayAnimation( string voiceKey , bool disableLoop = false ) {
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
