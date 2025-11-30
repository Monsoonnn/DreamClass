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
        /// Download avatar image (supports WebP, PNG, JPG formats)
        /// </summary>
        private IEnumerator DownloadAvatarCoroutine(string avatarUrl)
        {
            Log($"Downloading avatar: {avatarUrl}");

            // First, try to download as raw bytes to detect format
            using (UnityWebRequest request = UnityWebRequest.Get(avatarUrl))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    LogError($"Failed to download avatar: {request.error}");
                    yield break;
                }

                byte[] imageData = request.downloadHandler.data;
                
                if (imageData == null || imageData.Length < 4)
                {
                    LogError("Invalid avatar data received");
                    yield break;
                }

                // Detect format by magic bytes
                ImageFormat format = DetectImageFormat(imageData);
                Log($"Detected avatar format: {format}");

                Texture2D texture = null;

                switch (format)
                {
                    case ImageFormat.WebP:
                        WebP.Error error;
                        texture = WebP.Texture2DExt.CreateTexture2DFromWebP(imageData, lMipmaps: false, lLinear: false, out error);
                        if (error != WebP.Error.Success)
                        {
                            LogError($"Failed to decode WebP avatar: {error}");
                            yield break;
                        }
                        break;

                    case ImageFormat.PNG:
                    case ImageFormat.JPG:
                        texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (!texture.LoadImage(imageData))
                        {
                            LogError("Failed to load PNG/JPG avatar");
                            UnityEngine.Object.Destroy(texture);
                            yield break;
                        }
                        break;

                    default:
                        // Try Unity's standard loader as fallback
                        Log("Unknown format, trying standard loader...");
                        texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (!texture.LoadImage(imageData))
                        {
                            LogError("Failed to load avatar with standard loader");
                            UnityEngine.Object.Destroy(texture);
                            yield break;
                        }
                        break;
                }

                if (texture != null)
                {
                    userProfile.SetAvatar(texture);
                    Log($"Avatar ({format}) downloaded successfully");
                    OnAvatarLoaded?.Invoke(userProfile.avatarSprite);
                }
            }
        }

        /// <summary>
        /// Detect image format from magic bytes
        /// </summary>
        private enum ImageFormat { Unknown, PNG, JPG, WebP }
        
        private ImageFormat DetectImageFormat(byte[] data)
        {
            if (data == null || data.Length < 12) return ImageFormat.Unknown;

            // PNG: 89 50 4E 47 (‰PNG)
            if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
                return ImageFormat.PNG;

            // JPG/JPEG: FF D8 FF
            if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
                return ImageFormat.JPG;

            // WebP: RIFF....WEBP (52 49 46 46 ... 57 45 42 50)
            if (data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 &&
                data.Length >= 12 && data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
                return ImageFormat.WebP;

            return ImageFormat.Unknown;
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
