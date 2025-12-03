using System;
using Characters.TeacherKien;
using UnityEngine;

namespace DreamClass.NPCCore {
    [Serializable]
    public class VoiceAnimationConfig {
        public ThayKienVoiceType voiceType;
        public bool showSubtitle;
    }

    public class ThayKienNPC : NPCManager {
        public TeacherKienInteraction interaction;
        public GameObject changeClass;

        [Header("Custom Voice Config")]
        public bool useCustomVoice = false;
        public VoiceAnimationConfig[] customVoices;

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadModel();
        }

        public async void OnPlayerEnter() {
            if (interaction == null) return;

            if (useCustomVoice) {
                // Custom mode: chạy voice từ Inspector
                if (customVoices != null) {
                    foreach (var voice in customVoices) {
                        await interaction.PlayAnimation(voice.voiceType, voice.showSubtitle);
                    }
                }
            } else {
                // Default mode: check login
                var loginManager = DreamClass.LoginManager.LoginManager.Instance;

                if (loginManager != null && loginManager.IsLoggedIn()) {
                    await interaction.PlayAnimation(ThayKienVoiceType.welcome, false);
                    await interaction.PlayAnimation(ThayKienVoiceType.guide, true);
                    changeClass.SetActive(true);
                } else {
                    await interaction.PlayAnimation(ThayKienVoiceType.alertLogin);
                    changeClass.SetActive(false);
                }
            }
        }



    }
}
