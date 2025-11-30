using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using com.cyborgAssets.inspectorButtonPro;
using DreamClass.Network;

namespace DreamClass.Account
{
    /// <summary>
    /// Service để fetch và quản lý user profile
    /// Tự động fetch sau khi login thành công
    /// </summary>
    public class ProfileService : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ApiClient apiClient;
        [SerializeField] private UserProfileSO userProfile;

        [Header("API Settings")]
        [SerializeField] private string profileEndpoint = "/api/auth/profile";

        [Header("Avatar Settings")]
        [SerializeField] private bool autoDownloadAvatar = true;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLog = true;

        // Events
        public event Action<UserProfileSO> OnProfileLoaded;
        public event Action<Sprite> OnAvatarLoaded;
        public event Action<string> OnError;

        // State
        private bool isLoading = false;
        public bool IsLoading => isLoading;
        public bool HasProfile => userProfile != null && userProfile.HasProfile;

        private void Start()
        {
            if (apiClient == null)
            {
                apiClient = FindAnyObjectByType<ApiClient>();
            }
        }

        /// <summary>
        /// Fetch profile từ API (được gọi sau khi login)
        /// </summary>
        [ProButton]
        public void FetchProfile()
        {
            if (apiClient == null)
            {
                LogError("ApiClient not assigned!");
                OnError?.Invoke("ApiClient not assigned!");
                return;
            }

            if (isLoading)
            {
                Log("Already loading profile...");
                return;
            }

            StartCoroutine(FetchProfileCoroutine());
        }

        private IEnumerator FetchProfileCoroutine()
        {
            isLoading = true;
            Log("Fetching user profile...");

            ApiRequest request = new ApiRequest(profileEndpoint, "GET");
            ApiResponse response = null;

            yield return apiClient.SendRequest(request, (res) => response = res);

            if (response == null || !response.IsSuccess)
            {
                string error = response?.Error ?? "Unknown error";
                LogError($"Failed to fetch profile: {error}");
                OnError?.Invoke(error);
                isLoading = false;
                yield break;
            }

            // Parse response outside of try-catch to allow yield
            ProfileResponse profileResponse = null;
            bool parseSuccess = false;
            string parseError = null;

            try
            {
                profileResponse = JsonUtility.FromJson<ProfileResponse>(response.Text);
                parseSuccess = profileResponse != null && profileResponse.data != null;
            }
            catch (Exception ex)
            {
                parseError = ex.Message;
            }

            if (!parseSuccess)
            {
                string error = parseError ?? "Failed to parse profile response";
                LogError($"Exception parsing profile: {error}");
                OnError?.Invoke(error);
                isLoading = false;
                yield break;
            }

            // Update ScriptableObject
            userProfile.UpdateFromResponse(profileResponse.data);

            Log($"Profile loaded: {userProfile.userName} (Gold: {userProfile.gold})");

            OnProfileLoaded?.Invoke(userProfile);

            // Auto download avatar if enabled (outside try-catch)
            if (autoDownloadAvatar && !string.IsNullOrEmpty(userProfile.avatarUrl))
            {
                yield return DownloadAvatarCoroutine(userProfile.avatarUrl);
            }

            isLoading = false;
        }

        /// <summary>
        /// Download avatar image (supports WebP format)
        /// </summary>
        private IEnumerator DownloadAvatarCoroutine(string avatarUrl)
        {
            Log($"Downloading avatar: {avatarUrl}");

            // Check if URL is WebP format
            bool isWebP = avatarUrl.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) || 
                          avatarUrl.Contains("/upload/") || // Cloudinary URLs
                          avatarUrl.Contains("cloudinary");

            if (isWebP)
            {
                // Download as raw bytes and decode WebP
                using (UnityWebRequest request = UnityWebRequest.Get(avatarUrl))
                {
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        byte[] webpData = request.downloadHandler.data;
                        
                        WebP.Error error;
                        Texture2D texture = WebP.Texture2DExt.CreateTexture2DFromWebP(webpData, lMipmaps: false, lLinear: false, out error);

                        if (error == WebP.Error.Success && texture != null)
                        {
                            userProfile.SetAvatar(texture);
                            Log("Avatar (WebP) downloaded successfully");
                            OnAvatarLoaded?.Invoke(userProfile.avatarSprite);
                        }
                        else
                        {
                            LogError($"Failed to decode WebP avatar: {error}");
                        }
                    }
                    else
                    {
                        LogError($"Failed to download avatar: {request.error}");
                    }
                }
            }
            else
            {
                // Standard image format (PNG, JPG)
                using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(avatarUrl))
                {
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        Texture2D texture = DownloadHandlerTexture.GetContent(request);
                        userProfile.SetAvatar(texture);

                        Log("Avatar downloaded successfully");
                        OnAvatarLoaded?.Invoke(userProfile.avatarSprite);
                    }
                    else
                    {
                        LogError($"Failed to download avatar: {request.error}");
                    }
                }
            }
        }

        /// <summary>
        /// Clear profile data (call on logout)
        /// </summary>
        [ProButton]
        public void ClearProfile()
        {
            if (userProfile != null)
            {
                userProfile.Clear();
                Log("Profile cleared");
            }
        }

        /// <summary>
        /// Get current profile
        /// </summary>
        public UserProfileSO GetProfile()
        {
            return userProfile;
        }

        /// <summary>
        /// Get user's gold amount
        /// </summary>
        public int GetGold()
        {
            return userProfile?.gold ?? 0;
        }

        /// <summary>
        /// Get user's avatar sprite
        /// </summary>
        public Sprite GetAvatarSprite()
        {
            return userProfile?.avatarSprite;
        }

        #region Debug

        [ProButton]
        public void DebugProfileInfo()
        {
            if (userProfile == null)
            {
                Debug.Log("[ProfileService] UserProfile not assigned");
                return;
            }

            Debug.Log("=== User Profile ===");
            Debug.Log($"Name: {userProfile.userName}");
            Debug.Log($"Email: {userProfile.email}");
            Debug.Log($"Player ID: {userProfile.playerId}");
            Debug.Log($"Role: {userProfile.role}");
            Debug.Log($"Class: {userProfile.className}");
            Debug.Log($"Grade: {userProfile.grade}");
            Debug.Log($"Gold: {userProfile.gold}");
            Debug.Log($"Points: {userProfile.points}");
            Debug.Log($"Avatar URL: {userProfile.avatarUrl}");
            Debug.Log($"Has Avatar: {userProfile.avatarSprite != null}");
        }

        private void Log(string message)
        {
            if (enableDebugLog)
                Debug.Log($"[ProfileService] {message}");
        }

        private void LogError(string message)
        {
            Debug.LogError($"[ProfileService] {message}");
        }

        #endregion
    }
}
