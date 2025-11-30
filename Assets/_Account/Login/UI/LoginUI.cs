using TMPro;
using UnityEngine;
using UnityEngine.UI;
using com.cyborgAssets.inspectorButtonPro;
using DreamClass.NPCCore;
using Meta.XR.ImmersiveDebugger.UserInterface.Generic;

namespace DreamClass.LoginManager {
    public class LoginUI : NewMonobehavior {
        [SerializeField] private TMP_InputField _username;
        [SerializeField] private TMP_InputField _password;
        [SerializeField] protected LoginManagerUI loginManagerUI;
        [SerializeField] private GameObject loadingObject;
        [SerializeField] protected MaiNPC maiNPC;

        [SerializeField] private UnityEngine.UI.Toggle rememberLoginToggle;

        private int failCount = 0;
        private bool isLoggingIn = false;

        private void OnEnable()
        {
            // Check toggle và load saved credentials vào textfield
            if (rememberLoginToggle != null && rememberLoginToggle.isOn)
            {
                LoadSavedCredentialsToFields();
            }
            loadingObject.gameObject.SetActive(false); 
        }

        /// <summary>
        /// Load saved credentials vào textfield nếu toggle Remember ON
        /// </summary>
        private void LoadSavedCredentialsToFields()
        {
            string savedUsername = LoginManager.Instance.GetSavedUsername();
            string savedPassword = LoginManager.Instance.GetDecryptedPassword();


            // Only load if both username and password are valid
            if (!string.IsNullOrEmpty(savedUsername) && !string.IsNullOrEmpty(savedPassword))
            {
                if (_username != null)
                    _username.text = savedUsername;

                if (_password != null)
                    _password.text = savedPassword;

                Debug.Log("[LoginUI] Loaded saved credentials from Remember Me");
            }
            else
            {
                Debug.Log("[LoginUI] No valid saved credentials to load");
            }
        }

        [ProButton]
        public void OnClickLogin() {
            // Chặn ấn nhiều lần
            if (isLoggingIn)
            {
                Debug.LogWarning("[LoginUI] Login request already in progress. Please wait...");
                return;
            }

            // Validate input
            if (string.IsNullOrEmpty(_username.text) || string.IsNullOrEmpty(_password.text))
            {
                loginManagerUI.ShowNotification("Vui lòng nhập tài khoản và mật khẩu", this.gameObject);
                return;
            }

            // Set flag
            isLoggingIn = true;
            loadingObject.gameObject.SetActive(true); 
            // Pass remember state từ toggle
            bool remember = rememberLoginToggle != null && rememberLoginToggle.isOn;
            LoginManager.Instance.Login(_username.text, _password.text, OnLoginResult, remember: remember);
        }

        private void OnLoginResult( bool success, string response ) {
            // Reset flag
            isLoggingIn = false;
            loadingObject.gameObject.SetActive(false); 
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
