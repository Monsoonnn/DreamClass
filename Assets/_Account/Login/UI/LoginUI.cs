using com.cyborgAssets.inspectorButtonPro;
using DevionGames.LoginSystem;
using TMPro;
using UnityEngine;

namespace DreamClass.LoginManager {
    public class LoginUI : NewMonobehavior {
        [SerializeField] private TMP_InputField _username;
        [SerializeField] private TMP_InputField _password;
        [SerializeField] protected LoginManagerUI loginManagerUI;

        [ProButton]
        public void OnClickLogin() {

            LoginManager.Instance.Login(_username.text, _password.text, OnLoginResult);
        }

        private void OnLoginResult( bool success, string response ) {
            if (success) {
                //_statusText.text = "Đăng nhập thành công!";

                loginManagerUI.ShowNotification("Đăng nhập thành công", this.gameObject);
                loginManagerUI.notificationUI.logOutBtn.gameObject.SetActive(true);
                Debug.Log(response);
                // TODO: Load scene khác hoặc mở menu chính
            } else {
                //_statusText.text = "Đăng nhập thất bại!";
                loginManagerUI.ShowNotification("Đăng nhập thất bại!", 2f, this.gameObject);
                Debug.LogError(response);
            }
        }
    }
}
