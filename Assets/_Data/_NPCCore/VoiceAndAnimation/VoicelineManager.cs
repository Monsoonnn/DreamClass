using com.cyborgAssets.inspectorButtonPro;
using System.Threading.Tasks;
using UnityEngine;
using NPCCore.Animation;
using DreamClass.NPCCore;
using System.Collections.Generic;

namespace NPCCore.Voiceline {
    public abstract class VoicelineCtrl<TVoiceType> : NewMonobehavior {
        [Header("Voicelines")]
        public VoicelineAnimation<TVoiceType>[] voicelines;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private NPCManager NPCManager;

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadAudioScoure();
            this.LoadNPCManager();
        }

        protected virtual void LoadAudioScoure() {
            if (audioSource != null) return;
            audioSource = GetComponent<AudioSource>();
        }

        protected virtual void LoadNPCManager() {
            if (NPCManager != null) return;
            NPCManager = transform.parent.GetComponent<NPCManager>();
        }

        [ProButton]
        public virtual async Task PlayAnimation( TVoiceType voiceType , bool BackStartGroup = true ) {
            var voiceline = GetVoiceline(voiceType);
            if (voiceline != null)
                await voiceline.PlayAsync(audioSource, NPCManager.AnimationManager , BackStartGroup);
            else
                Debug.LogWarning($"Voiceline {voiceType} not found!");
        }

        protected virtual VoicelineAnimation<TVoiceType> GetVoiceline( TVoiceType type ) {
            foreach (var v in voicelines)
                if (EqualityComparer<TVoiceType>.Default.Equals(v.voiceType, type))
                    return v;
            return null;
        }
    }
}