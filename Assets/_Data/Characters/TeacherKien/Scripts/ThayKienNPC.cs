using Characters.TeacherKien;
using UnityEngine;

namespace DreamClass.NPCCore {
    public class ThayKienNPC : NPCManager {
        public TeacherKienInteraction interaction;
        public GameObject changeClass;

        protected override void LoadComponents() {
            base.LoadComponents();
            this.LoadModel();
        }

        public async void OnPlayerEnter() {
            if (interaction == null) return;

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
