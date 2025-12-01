using UnityEngine;
using DreamClass.Network;
using DreamClass.Account;
using System.Collections;
using System.Text;
using System;
using com.cyborgAssets.inspectorButtonPro;
using System.Security.Cryptography;
using UnityEngine.UI;

namespace DreamClass.LoginManager {
    public class LoginManager : SingletonCtrl<LoginManager> {
        [SerializeField] private ConfigSO _configServer;
        [SerializeField] private ApiClient apiClient;

        [Header("Profile Service")]
        [SerializeField] private ProfileService profileService;

        [Header("Session Info")]
        private string sessionCookie;
        private string playerId;

        [Header("User Info (Remember Me)")]
        [SerializeField] private bool rememberMe = false;
        [SerializeField] private string savedUsername = "";
        [SerializeField] private string savedPassword = "";

        [Header("Login Retry")]
        private bool isRetryingLogin = false;

        // Events
        public static event Action OnLoginSuccess;
        public static event Action OnLogoutSuccess;

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

        }

        public void Login( string username, string password, Action<bool, string> onResult = null, bool remember = false ) {
            // Reset retry flag when starting new login attempt
            isRetryingLogin = false;
            StartCoroutine(DelayedLogin(username, password, onResult, remember));
        }

        [ProButton]
        public void QuickLogin() {
            // Dùng saved credentials nếu có, ngược lại dùng default test account
            string username = string.IsNullOrEmpty(savedUsername) ? "test@example.com" : savedUsername;
            string password = string.IsNullOrEmpty(savedPassword) ? "password123" : savedPassword;
            
            Debug.Log($"[LoginManager] Quick Login with: {username}");
            Login(username, password, onResult: null, remember: false);
        }

        private IEnumerator DelayedLogin( string username, string password, Action<bool, string> onResult, bool remember ) {
            yield return null; // Wait 1 frame before sending first request

            _onLoginResult = onResult;
            rememberMe = remember;

            string json = JsonUtility.ToJson(new LoginRequest(username, password));
            ApiRequest req = new ApiRequest("/api/auth/login", "POST", json);

            StartCoroutine(apiClient.SendRequest(req, OnLoginResponse));

            if (rememberMe) SaveLogin(username, password);
            else ClearSavedLogin();
        }


        private void OnLoginResponse( ApiResponse res ) {
            if (res.IsSuccess) {
                Debug.Log("Login success!");   
                
                // Handle authentication based on ApiClient's type
                if (apiClient.CurrentAuthType == DreamClass.Network.AuthType.Cookie)
                {
                    Debug.Log("Response: " + res.SetCookie);
                    
                    // Try to get cookie from response
                    if (!string.IsNullOrEmpty(res.SetCookie)) {
                        sessionCookie = ParseCookie(res.SetCookie);
                        Debug.Log("Login Cookie from response: " + sessionCookie);
                        apiClient.SetCookie(sessionCookie);
                    }
                    else
                    {
                        Logout();
                        // Nếu không có cookie và chưa retry, thử logout rồi login lại với saved credentials
                        if (!isRetryingLogin && !string.IsNullOrEmpty(savedUsername) && !string.IsNullOrEmpty(savedPassword))
                        {
                            Debug.Log("[LoginManager] No cookie received, trying logout and retry with saved credentials");
                            isRetryingLogin = true;
                            
                            // Logout trước, sau đó login lại
                            StartCoroutine(LogoutAndRetryLogin());
                            return; // Exit early, don't continue with success flow
                        }
                        else
                        {
                            // Đã retry hoặc không có saved credentials, báo fail
                            Debug.LogError("[LoginManager] Login failed - no cookie and retry unsuccessful");
                            isRetryingLogin = false;
                            _onLoginResult?.Invoke(false, "Login failed: No session cookie received");
                            return;
                        }
                    }
                }
                else if (apiClient.CurrentAuthType == DreamClass.Network.AuthType.JWT)
                {
                    // Parse JWT token from response body
                    try
                    {
                        LoginResponse loginResponse = JsonUtility.FromJson<LoginResponse>(res.Text);
                        if (loginResponse != null && loginResponse.data != null)
                        {
                            // Check if response has token field
                            string token = loginResponse.token ?? loginResponse.accessToken;
                            
                            if (!string.IsNullOrEmpty(token))
                            {
                                apiClient.SetJwtToken(token);
                                Debug.Log("[LoginManager] JWT token set");
                            }
                            else
                            {
                                Debug.LogError("[LoginManager] No JWT token in response");
                                _onLoginResult?.Invoke(false, "Login failed: No JWT token received");
                                return;
                            }
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[LoginManager] Failed to parse JWT token: {e.Message}");
                    }
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

                // Initialize quests after successful login - delay to ensure auth is set
                StartCoroutine(InitializeQuestsAfterLogin());

                // Trigger login success event
                OnLoginSuccess?.Invoke();

                _onLoginResult?.Invoke(true, res.Text);
            } else {
                Debug.LogError($"Login failed: {res.StatusCode}");
                
                // Parse error message from server
                string errorMessage = "Login failed";
                try
                {
                    var errorResponse = JsonUtility.FromJson<ErrorResponse>(res.Text);
                    if (errorResponse != null)
                    {
                        errorMessage = !string.IsNullOrEmpty(errorResponse.error) 
                            ? errorResponse.error 
                            : errorResponse.message;
                    }
                }
                catch
                {
                    errorMessage = res.Text;
                }
                
                Debug.LogError($"[LoginManager] Error details: {errorMessage}");
                _onLoginResult?.Invoke(false, errorMessage);
            }
        }

        [System.Serializable]
        private class ErrorResponse
        {
            public string message;
            public string error;
        }

        private IEnumerator LogoutAndRetryLogin()
        {
            Debug.Log("[LoginManager] Logging out before retry...");
            // Wait a moment
            yield return new WaitForSeconds(0.5f);
            
            // Retry login with saved credentials
            Debug.Log($"[LoginManager] Retrying login with saved credentials: {savedUsername}");
            
            string json = JsonUtility.ToJson(new LoginRequest(savedUsername, savedPassword));
            ApiRequest req = new ApiRequest("/api/auth/login", "POST", json);
            
            StartCoroutine(apiClient.SendRequest(req, OnLoginResponse));
        }

        private IEnumerator InitializeQuestsAfterLogin()
        {
            // Wait multiple frames to ensure cookie is fully set
            int retries = 0;
            int maxRetries = 5;
            
            while (retries < maxRetries && string.IsNullOrEmpty(apiClient.DefaultCookie))
            {
                yield return new WaitForSeconds(0.2f);
                retries++;
                Debug.Log($"[LoginManager] Waiting for cookie... attempt {retries}/{maxRetries}");
            }

            // Double-check that we're logged in before initializing
            if (!IsLoggedIn())
            {
                Debug.LogWarning("[LoginManager] Failed to initialize - not logged in after waiting");
                yield break;
            }

            // Initialize QuestManager
            var questManager = DreamClass.QuestSystem.QuestManager.Instance;
            if (questManager != null)
            {
                questManager.InitializeQuests();
                Debug.Log("[LoginManager] Quests initialized after login");
            }

            // Fetch user profile after login
            if (profileService != null)
            {
                profileService.FetchProfile();
                Debug.Log("[LoginManager] Fetching user profile...");
            }
            else
            {
                // Try to find ProfileService if not assigned
                profileService = FindAnyObjectByType<ProfileService>();
                if (profileService != null)
                {
                    profileService.FetchProfile();
                    Debug.Log("[LoginManager] ProfileService found, fetching profile...");
                }
            }

            // PDFSubjectService now auto-fetches on Start, no need to call here
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
            // Clear authentication based on type
            if (apiClient.CurrentAuthType == DreamClass.Network.AuthType.Cookie)
            {
                apiClient.ClearCookie();
            }
            else if (apiClient.CurrentAuthType == DreamClass.Network.AuthType.JWT)
            {
                apiClient.ClearJwtToken();
            }
            
            // Clear user profile on logout
            if (profileService != null)
            {
                profileService.ClearProfile();
            }

            if (res.IsSuccess) {
                // Trigger logout success event
                OnLogoutSuccess?.Invoke();

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
            savedPassword = password; // Lưu plain text

            PlayerPrefs.SetInt(KEY_REMEMBER, 1);
            PlayerPrefs.SetString(KEY_USER, savedUsername);
            PlayerPrefs.SetString(KEY_PASS, savedPassword);
            PlayerPrefs.Save();

            Debug.Log("[LoginManager] Credentials saved.");
        }

        private (string username, string password) LoadSavedLogin() {
            if (PlayerPrefs.GetInt(KEY_REMEMBER, 0) == 1) {
                rememberMe = true;
                savedUsername = PlayerPrefs.GetString(KEY_USER, "");
                savedPassword = PlayerPrefs.GetString(KEY_PASS, ""); // Load plain text
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

        [System.Serializable]
        private struct LoginRequest {
            public string username;
            public string password;
            public LoginRequest( string username, string password ) {
                this.username = username;
                this.password = password;
            }
        }

        [System.Serializable]
        private class LoginResponse
        {
            public string message;
            public LoginData data;
            
            // JWT fields (optional)
            public string token;
            public string accessToken;

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
        public string GetDecryptedPassword() => savedPassword; // Plain text, không cần decrypt
        public bool IsRemembered() => rememberMe;
        public string GetPlayerId() => playerId;
        
        public bool IsLoggedIn() {
            // Check authentication based on ApiClient's auth type
            return apiClient != null && apiClient.IsAuthenticated();
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
