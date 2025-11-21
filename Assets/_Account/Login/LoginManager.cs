using UnityEngine;
using DreamClass.Network;
using System.Collections;
using System.Text;
using System;
using com.cyborgAssets.inspectorButtonPro;
using System.Security.Cryptography;

namespace DreamClass.LoginManager {
    public class LoginManager : SingletonCtrl<LoginManager> {
        [SerializeField] private ConfigSO _configServer;
        [SerializeField] private ApiClient apiClient;

        [Header("Session Info")]
        private string sessionCookie;
        private string playerId;

        [Header("User Info (Remember Me)")]
        [SerializeField] private bool rememberMe = false;
        [SerializeField] private string savedUsername = "";
        [SerializeField] private string savedPassword = "";

        private Action<bool, string> _onLoginResult;
        private Action<bool, string> _onLogoutResult;

        private const string KEY_REMEMBER = "RememberMe";
        private const string KEY_USER = "SavedUser";
        private const string KEY_PASS = "SavedPass";
        private const string SECRET = "dreamclass_key_salt";

        protected override void Awake() {
            base.Awake();
            apiClient.SetBaseUrl(_configServer.hostURL);
            LoadSavedLogin();
        }

        protected override void Start()
        {
            base.Start();
            // Auto-login if credentials were saved
            if (rememberMe && !string.IsNullOrEmpty(savedUsername) && !string.IsNullOrEmpty(savedPassword))
            {
                Debug.Log("[LoginManager] Auto-logging in with saved credentials...");
                Login(savedUsername, savedPassword, remember: true);
            }
        }

        public void Login( string email, string password, Action<bool, string> onResult = null, bool remember = false ) {
            StartCoroutine(DelayedLogin(email, password, onResult, remember));
        }

        [ProButton]
        public void QuickLogin() {
            // Dùng saved credentials nếu có, ngược lại dùng default test account
            string email = string.IsNullOrEmpty(savedUsername) ? "test@example.com" : savedUsername;
            string password = string.IsNullOrEmpty(savedPassword) ? "password123" : savedPassword;
            
            Debug.Log($"[LoginManager] Quick Login with: {email}");
            Login(email, password, onResult: null, remember: false);
        }

        private IEnumerator DelayedLogin( string email, string password, Action<bool, string> onResult, bool remember ) {
            yield return null; // Wait 1 frame before sending first request

            _onLoginResult = onResult;
            rememberMe = remember;

            string json = JsonUtility.ToJson(new LoginRequest(email, password));
            ApiRequest req = new ApiRequest("/api/auth/login", "POST", json);

            StartCoroutine(apiClient.SendRequest(req, OnLoginResponse));

            if (rememberMe) SaveLogin(email, password);
            else ClearSavedLogin();
        }


        private void OnLoginResponse( ApiResponse res ) {
            if (res.IsSuccess) {
                Debug.Log("Login success!");   
                if (!string.IsNullOrEmpty(res.SetCookie)) {
                    sessionCookie = ParseCookie(res.SetCookie);

                    Debug.Log("Login Cookie: " + sessionCookie);

                    apiClient.SetCookie(sessionCookie);
                }

                // Parse playerId from response
                try
                {
                    LoginResponse loginResponse = JsonUtility.FromJson<LoginResponse>(res.Text);
                    if (loginResponse != null && loginResponse.data != null && !string.IsNullOrEmpty(loginResponse.data.playerId))
                    {
                        playerId = loginResponse.data.playerId;
                        Debug.Log($"[LoginManager] PlayerId: {playerId}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[LoginManager] Failed to parse playerId from response: {e.Message}");
                }

                // Initialize quests after successful login
                var questManager = DreamClass.QuestSystem.QuestManager.Instance;
                if (questManager != null)
                {
                    questManager.InitializeQuests();
                }

                _onLoginResult?.Invoke(true, res.Text);
            } else {
                Debug.LogError($"Login failed: {res.StatusCode}");
                _onLoginResult?.Invoke(false, res.Text);
            }
        }


        public void Logout( Action<bool, string> onResult = null ) {
            _onLogoutResult = onResult;
            ApiRequest req = new ApiRequest("/api/auth/logout", "POST");
            StartCoroutine(apiClient.SendRequest(req, OnLogoutResponse));
        }

        [ProButton]
        public void QuickLogout() {
            Debug.Log("[LoginManager] Quick Logout");
            Logout(onResult: null);
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

        // ============================================================
        // Remember Me Section
        // ============================================================

        private void SaveLogin( string username, string password ) {
            savedUsername = username;
            savedPassword = Encrypt(password);

            PlayerPrefs.SetInt(KEY_REMEMBER, 1);
            PlayerPrefs.SetString(KEY_USER, savedUsername);
            PlayerPrefs.SetString(KEY_PASS, savedPassword);
            PlayerPrefs.Save();

            Debug.Log("[LoginManager] Credentials saved securely.");
        }

        private (string username, string password) LoadSavedLogin() {
            if (PlayerPrefs.GetInt(KEY_REMEMBER, 0) == 1) {
                rememberMe = true;
                savedUsername = PlayerPrefs.GetString(KEY_USER, "");
                savedPassword = Decrypt(PlayerPrefs.GetString(KEY_PASS, ""));
                Debug.Log("[LoginManager] Loaded saved credentials.");
                return (savedUsername, savedPassword);
            } else {
                rememberMe = false;
                return ("", "");
            }
        }


        private void ClearSavedLogin() {
            PlayerPrefs.DeleteKey(KEY_REMEMBER);
            PlayerPrefs.DeleteKey(KEY_USER);
            PlayerPrefs.DeleteKey(KEY_PASS);
            PlayerPrefs.Save();
            Debug.Log("[LoginManager] Cleared saved credentials.");
        }

        // ============================================================
        // Simple AES Encryption
        // ============================================================

        private string Encrypt( string plainText ) {
            if (string.IsNullOrEmpty(plainText)) return "";

            byte[] key = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(SECRET));
            byte[] iv = new byte[16];
            Array.Copy(key, iv, 16);

            using (Aes aes = Aes.Create()) {
                aes.Key = key;
                aes.IV = iv;

                using (ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV)) {
                    byte[] bytes = Encoding.UTF8.GetBytes(plainText);
                    byte[] encrypted = encryptor.TransformFinalBlock(bytes, 0, bytes.Length);
                    return Convert.ToBase64String(encrypted);
                }
            }
        }

        private string Decrypt( string cipherText ) {
            if (string.IsNullOrEmpty(cipherText)) return "";

            byte[] key = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(SECRET));
            byte[] iv = new byte[16];
            Array.Copy(key, iv, 16);

            using (Aes aes = Aes.Create()) {
                aes.Key = key;
                aes.IV = iv;

                using (ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV)) {
                    byte[] bytes = Convert.FromBase64String(cipherText);
                    byte[] decrypted = decryptor.TransformFinalBlock(bytes, 0, bytes.Length);
                    return Encoding.UTF8.GetString(decrypted);
                }
            }
        }

        // ============================================================

        [System.Serializable]
        private struct LoginRequest {
            public string email;
            public string password;
            public LoginRequest( string email, string password ) {
                this.email = email;
                this.password = password;
            }
        }

        [System.Serializable]
        private class LoginResponse
        {
            public string message;
            public LoginData data;

            [System.Serializable]
            public class LoginData
            {
                public string id;
                public string name;
                public string email;
                public string role;
                public string playerId;
            }
        }

        // Public getters
        public string GetSavedUsername() => savedUsername;
        public string GetSavedPassword() => savedPassword;
        public bool IsRemembered() => rememberMe;
        public string GetPlayerId() => playerId;
        public bool IsLoggedIn() {
            //Debug.Log("Login Cookie: " + sessionCookie);
            return !string.IsNullOrEmpty(apiClient.DefaultCookie);
        }

        private void OnApplicationQuit()
        {
            // Logout when game closes if user was logged in but "Remember Me" is OFF
            if (IsLoggedIn() && !rememberMe)
            {
                Debug.Log("[LoginManager] Logging out on application quit (Remember Me OFF)");
                Logout();
            }
        }
    }
}
