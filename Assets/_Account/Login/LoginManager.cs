using UnityEngine;
using DreamClass.Network;
using System.Collections;
using System.Text;
using System;
using com.cyborgAssets.inspectorButtonPro;

namespace DreamClass.LoginManager {
    public class LoginManager : SingletonCtrl<LoginManager> {
        [SerializeField] private ConfigSO _configServer;
        [SerializeField] private ApiClient apiClient;

        [Header("Session Info")]
        [SerializeField] private string sessionCookie;

        private Action<bool, string> _onLoginResult;
        private Action<bool, string> _onLogoutResult;

        protected override void Awake() {
            base.Awake();
            apiClient.SetBaseUrl(_configServer.hostURL);
        }

        public void Login( string email, string password, Action<bool, string> onResult = null ) {
            _onLoginResult = onResult;

            string json = JsonUtility.ToJson(new LoginRequest(email, password));
            ApiRequest req = new ApiRequest(
                endpoint: "/api/auth/login",
                method: "POST",
                body: json
            );

            StartCoroutine(apiClient.SendRequest(req, OnLoginResponse));
        }

        private void OnLoginResponse( ApiResponse res ) {
            if (res.IsSuccess) {
                Debug.Log("Login success!");
                if (!string.IsNullOrEmpty(res.SetCookie)) {
                    sessionCookie = ParseCookie(res.SetCookie);
                    apiClient.SetCookie(sessionCookie);
                }

                _onLoginResult?.Invoke(true, res.Text); 
            } else {
                Debug.LogError($"Login failed: {res.StatusCode}");
                _onLoginResult?.Invoke(false, res.Text); 
            }
        }

        [ProButton]
        public void Logout(Action<bool, string> onResult = null) {
            _onLogoutResult = onResult;
            ApiRequest req = new ApiRequest("/api/auth/logout", "POST");
            StartCoroutine(apiClient.SendRequest(req, OnLogoutResponse));
        }

        private void OnLogoutResponse( ApiResponse res ) {
            if (res.IsSuccess) {
                _onLogoutResult?.Invoke(true, res.Text);
                Debug.Log("Logout successful!");
            } else {
                _onLogoutResult?.Invoke(false, res.Text);
                Debug.LogError($"Logout failed: {res.StatusCode} - {res.Error}");

            }
                
        }

        private string ParseCookie( string setCookie ) {
            int semi = setCookie.IndexOf(';');
            return semi > 0 ? setCookie.Substring(0, semi) : setCookie;
        }

        [System.Serializable]
        private struct LoginRequest {
            public string email;
            public string password;
            public LoginRequest( string email, string password ) {
                this.email = email;
                this.password = password;
            }
        }
    }
}
