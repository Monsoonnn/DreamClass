using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;

namespace DreamClass.Network {
    public enum AuthType {
        Cookie,
        JWT
    }

    public class ApiClient : MonoBehaviour {
        [Header("Server Configuration")]
        [SerializeField] private string baseUrl;
        
        [Header("Authentication Data")]
        [SerializeField] private AuthData authData;
        
        public AuthType CurrentAuthType => authData != null ? authData.AuthType : AuthType.Cookie;
        public string DefaultCookie => authData != null ? authData.Cookie : "";
        public string JwtToken => authData != null ? authData.JwtToken : "";
        public AuthData AuthDataAsset => authData;
        
        public void SetBaseUrl( string url ) => baseUrl = url;
        
        // Cookie methods
        public void SetCookie( string cookie ) {
            if (authData == null) {
                Debug.LogError("[ApiClient] AuthData is not assigned!");
                return;
            }
            authData.Cookie = cookie;
            if (authData.AuthType == AuthType.Cookie) {
                Debug.Log($"[ApiClient] Cookie set: {cookie}");
            }
        }
        
        /// <summary>
        /// Set cookie với expiration time
        /// </summary>
        public void SetCookieWithExpiration(string cookie, string expirationTime) {
            if (authData == null) {
                Debug.LogError("[ApiClient] AuthData is not assigned!");
                return;
            }
            authData.Cookie = cookie;
            if (!string.IsNullOrEmpty(expirationTime)) {
                authData.SetExpirationTimeFromString(expirationTime);
            }
            if (authData.AuthType == AuthType.Cookie) {
                Debug.Log($"[ApiClient] Cookie set with expiration: {expirationTime}");
            }
        }
        
        /// <summary>
        /// Set cookie với expiration time (DateTime)
        /// </summary>
        public void SetCookieWithExpiration(string cookie, System.DateTime expirationTime) {
            if (authData == null) {
                Debug.LogError("[ApiClient] AuthData is not assigned!");
                return;
            }
            authData.Cookie = cookie;
            authData.SetExpirationTime(expirationTime);
            if (authData.AuthType == AuthType.Cookie) {
                Debug.Log($"[ApiClient] Cookie set with expiration: {expirationTime}");
            }
        }
        
        public void ClearCookie() {
            if (authData != null) {
                authData.Cookie = "";
                Debug.Log("[ApiClient] Cookie cleared");
            }
        }
        
        // JWT methods
        public void SetJwtToken( string token ) {
            if (authData == null) {
                Debug.LogError("[ApiClient] AuthData is not assigned!");
                return;
            }
            authData.JwtToken = token;
            if (authData.AuthType == AuthType.JWT) {
                Debug.Log($"[ApiClient] JWT token set");
            }
        }
        
        /// <summary>
        /// Set JWT token với expiration time
        /// </summary>
        public void SetJwtTokenWithExpiration(string token, string expirationTime) {
            if (authData == null) {
                Debug.LogError("[ApiClient] AuthData is not assigned!");
                return;
            }
            authData.JwtToken = token;
            if (!string.IsNullOrEmpty(expirationTime)) {
                authData.SetExpirationTimeFromString(expirationTime);
            }
            if (authData.AuthType == AuthType.JWT) {
                Debug.Log($"[ApiClient] JWT token set with expiration");
            }
        }
        
        /// <summary>
        /// Kiểm tra auth đã hết hạn chưa
        /// </summary>
        public bool IsAuthExpired() {
            return authData != null && authData.IsExpired();
        }
        
        /// <summary>
        /// Lấy thời gian còn lại trước khi hết hạn
        /// </summary>
        public System.TimeSpan GetTimeUntilExpiration() {
            return authData != null ? authData.GetTimeUntilExpiration() : System.TimeSpan.MaxValue;
        }
        
        public void ClearJwtToken() {
            if (authData != null) {
                authData.JwtToken = "";
                Debug.Log("[ApiClient] JWT token cleared");
            }
        }
        
        // Clear all auth
        public void ClearAuth() {
            if (authData != null) {
                authData.ClearAuth();
                Debug.Log("[ApiClient] Auth cleared");
            }
        }
        
        // Check if authenticated
        public bool IsAuthenticated() {
            return authData != null && authData.IsAuthenticated();
        }

        public IEnumerator SendRequest( ApiRequest request, System.Action<ApiResponse> callback ) {
            string url = $"{baseUrl}{request.Endpoint}";
            UnityWebRequest webRequest = new UnityWebRequest(url, request.Method);

            // Set body
            if (!string.IsNullOrEmpty(request.Body)) {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(request.Body);
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            }

            webRequest.downloadHandler = new DownloadHandlerBuffer();

            // Default headers
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Accept", "application/json");

            // Set authentication based on type
            if (authData != null) {
                switch (authData.AuthType) {
                    case AuthType.Cookie:
                        if (!string.IsNullOrEmpty(authData.Cookie)) {
                            webRequest.SetRequestHeader("Cookie", authData.Cookie);
                            Debug.Log($"[ApiClient] Header - Cookie: {authData.Cookie}");
                        }
                        break;
                        
                    case AuthType.JWT:
                        if (!string.IsNullOrEmpty(authData.JwtToken)) {
                            webRequest.SetRequestHeader("Authorization", $"Bearer {authData.JwtToken}");
                            Debug.Log($"[ApiClient] Header - Authorization: Bearer {authData.JwtToken.Substring(0, System.Math.Min(20, authData.JwtToken.Length))}...");
                        }
                        break;
                }
            }

            // Custom headers (if any)
            if (request.Headers != null) {
                foreach (var kv in request.Headers) {
                    webRequest.SetRequestHeader(kv.Key, kv.Value);
                    Debug.Log($"[ApiClient] Header - {kv.Key}: {kv.Value}");
                }
            }

            Debug.Log($"[ApiClient] Sending {request.Method} request to: {url}");
            yield return webRequest.SendWebRequest();

            ApiResponse response = new ApiResponse(webRequest);
            callback?.Invoke(response);
        }
    }
}
