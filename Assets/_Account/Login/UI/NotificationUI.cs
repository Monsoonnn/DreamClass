using com.cyborgAssets.inspectorButtonPro;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamClass.LoginManager
{
    public class NotificationUI : NewMonobehavior {
        [SerializeField] private Text message;
        public GameObject logOutBtn;
        [SerializeField] private LoginManagerUI loginManagerUI;
        public void Show(string message) {
            this.message.text = message;
        }

        public void Hide() {
            this.message.text = string.Empty;
        }

        [ProButton]
        public void OnClickLogout() {

            LoginManager.Instance.Logout(OnLogoutResult);
        }

        private void OnLogoutResult( bool success, string response ) {
            if (success) {
                loginManagerUI.BackToLogin(this.gameObject);
                logOutBtn.SetActive(false);
                Debug.Log(response);
            } else {
                loginManagerUI.ShowNotification(response, 2f, this.gameObject);
                Debug.LogError(response);
            }
        }
    }

}