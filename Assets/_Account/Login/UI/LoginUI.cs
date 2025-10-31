using TMPro;
using UnityEngine;
using com.cyborgAssets.inspectorButtonPro;
using DreamClass.NPCCore;

namespace DreamClass.LoginManager {
    public class LoginUI : NewMonobehavior {
        [SerializeField] private TMP_InputField _username;
        [SerializeField] private TMP_InputField _password;
        [SerializeField] protected LoginManagerUI loginManagerUI;
        [SerializeField] protected MaiNPC maiNPC;

        private int failCount = 0;

        [ProButton]
        public void OnClickLogin() {
            LoginManager.Instance.Login(_username.text, _password.text, OnLoginResult);
        }

        private void OnLoginResult( bool success, string response ) {
            if (success) {
                failCount = 0;

                loginManagerUI.ShowNotification("Đăng nhập thành công", this.gameObject);
                loginManagerUI.notificationUI.logOutBtn.gameObject.SetActive(true);

                if (maiNPC != null)
                    _ = maiNPC.characterVoiceline.PlayAnimation(Characters.Mai.MaiVoiceType.success);

                Debug.Log(response);
            } else {
                failCount++;

                loginManagerUI.ShowNotification($"Đăng nhập thất bại! (Lần {failCount})", 2f, this.gameObject);

                if (maiNPC != null) {
                    if (failCount >= 3)
                        _ = maiNPC.characterVoiceline.PlayAnimation(Characters.Mai.MaiVoiceType.mutiFail);
                    else
                        _ = maiNPC.characterVoiceline.PlayAnimation(Characters.Mai.MaiVoiceType.fail);
                }

                Debug.Log($"Login failed {failCount} times. Response: {response}");
            }
        }
    }
}
