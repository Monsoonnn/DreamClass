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
        
        [Header("Authentication")]
        [SerializeField] private AuthType authType = AuthType.Cookie;
        [SerializeField] private string defaultCookie;
        [SerializeField] private string jwtToken;
        
        public AuthType CurrentAuthType => authType;
        public string DefaultCookie => defaultCookie;
        public string JwtToken => jwtToken;
        
        public void SetBaseUrl( string url ) => baseUrl = url;
        
        // Cookie methods
        public void SetCookie( string cookie ) {
            defaultCookie = cookie;
            if (authType == AuthType.Cookie) {
                Debug.Log($"[ApiClient] Cookie set: {cookie}");
            }
        }
        
        public void ClearCookie() {
            defaultCookie = null;
            Debug.Log("[ApiClient] Cookie cleared");
        }
        
        // JWT methods
        public void SetJwtToken( string token ) {
            jwtToken = token;
            if (authType == AuthType.JWT) {
                Debug.Log($"[ApiClient] JWT token set");
            }
        }
        
        public void ClearJwtToken() {
            jwtToken = null;
            Debug.Log("[ApiClient] JWT token cleared");
        }
        
        // Clear all auth
        public void ClearAuth() {
            ClearCookie();
            ClearJwtToken();
        }
        
        // Check if authenticated
        public bool IsAuthenticated() {
            return authType switch {
                AuthType.Cookie => !string.IsNullOrEmpty(defaultCookie),
                AuthType.JWT => !string.IsNullOrEmpty(jwtToken),
                _ => false
            };
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
            switch (authType) {
                case AuthType.Cookie:
                    if (!string.IsNullOrEmpty(defaultCookie)) {
                        webRequest.SetRequestHeader("Cookie", defaultCookie);
                        Debug.Log($"[ApiClient] Header - Cookie: {defaultCookie}");
                    }
                    break;
                    
                case AuthType.JWT:
                    if (!string.IsNullOrEmpty(jwtToken)) {
                        webRequest.SetRequestHeader("Authorization", $"Bearer {jwtToken}");
                        Debug.Log($"[ApiClient] Header - Authorization: Bearer {jwtToken.Substring(0, System.Math.Min(20, jwtToken.Length))}...");
                    }
                    break;
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
