using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DreamClass.Account;
using com.cyborgAssets.inspectorButtonPro;

namespace DreamClass.Account.UI
{
    /// <summary>
    /// UI Panel để hiển thị thông tin user profile
    /// Tự động update khi profile thay đổi
    /// </summary>
    public class ProfileUIPanel : MonoBehaviour
    {
        [Header("Profile Data")]
        [SerializeField] private UserProfileSO userProfile;
        [SerializeField] private ProfileService profileService;

        [Header("Avatar")]
        [SerializeField] private Image avatarImage;
        [SerializeField] private Sprite defaultAvatar;

        [Header("Basic Info")]
        [SerializeField] private TextMeshProUGUI userNameText;
        [SerializeField] private TextMeshProUGUI userNameInfo;
        [SerializeField] private TextMeshProUGUI emailText;
        [SerializeField] private TextMeshProUGUI roleText;

        [Header("Student Info")]
        [SerializeField] private TextMeshProUGUI gradeText;
        [SerializeField] private TextMeshProUGUI classNameText;
        [SerializeField] private TextMeshProUGUI genderText;
        [SerializeField] private TextMeshProUGUI dateOfBirthText;

        [SerializeField] private TextMeshProUGUI addressText;

        [Header("Stats")]
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI pointsText;

        [Header("Status")]
        [SerializeField] private GameObject verifiedIcon;
        [SerializeField] private GameObject loadingIndicator;

        [Header("Settings")]
        [SerializeField] private bool autoRefresh = true;
        [SerializeField] private bool hideEmptyFields = true;

        private void OnEnable()
        {
            // Subscribe to events
            if (userProfile != null)
            {
                userProfile.OnProfileUpdated += RefreshUI;
            }

            if (profileService != null)
            {
                profileService.OnProfileLoaded += OnProfileLoaded;
                profileService.OnAvatarLoaded += OnAvatarLoaded;
            }

            // Initial refresh
            if (autoRefresh)
            {
                RefreshUI();
            }
        }

        private void OnDisable()
        {
            // Unsubscribe from events
            if (userProfile != null)
            {
                userProfile.OnProfileUpdated -= RefreshUI;
            }

            if (profileService != null)
            {
                profileService.OnProfileLoaded -= OnProfileLoaded;
                profileService.OnAvatarLoaded -= OnAvatarLoaded;
            }
        }

        private void Start()
        {
            // Try to find references if not assigned
            if (profileService == null)
            {
                profileService = ProfileService.Instance;
            }

            if (userProfile == null && profileService != null)
            {
                userProfile = profileService.GetProfile();
            }

            RefreshUI();
        }

        /// <summary>
        /// Refresh all UI elements with current profile data
        /// </summary>
        [ProButton]
        public void RefreshUI()
        {
            if (userProfile == null)
            {
                ShowEmptyState();
                return;
            }

            // Hide loading indicator
            SetLoadingState(false);

            // Avatar
            if (avatarImage != null)
            {
                if (userProfile.avatarSprite != null)
                {
                    avatarImage.sprite = userProfile.avatarSprite;
                }
                else
                {
                    avatarImage.sprite = defaultAvatar;
                }
            }

            // Basic Info
            SetText(userNameText, userProfile.userName);
            SetText(userNameInfo, userProfile.userName);
            SetText(emailText, userProfile.email);
            SetText(roleText, FormatRole(userProfile.role));

            // Student Info
            SetText(gradeText, userProfile.grade);
            SetText(classNameText, userProfile.className);
            SetText(genderText, FormatGender(userProfile.gender));
            SetText(dateOfBirthText, userProfile.FormattedDateOfBirth);
            SetText(addressText, userProfile.address);

            // Stats
            SetText(goldText, userProfile.gold.ToString("N0"));
            SetText(pointsText, userProfile.points.ToString("N0"));

            // Status
            // if (verifiedIcon != null)
            // {
            //     verifiedIcon.SetActive(userProfile.isVerified);
            // }
        }

        /// <summary>
        /// Show empty/logged out state
        /// </summary>
        public void ShowEmptyState()
        {
            if (avatarImage != null)
            {
                avatarImage.sprite = defaultAvatar;
            }

            SetText(userNameText, "Chưa đăng nhập");
            SetText(userNameInfo, "");
            SetText(emailText, "");
            SetText(roleText, "");
            SetText(gradeText, "");
            SetText(classNameText, "");
            SetText(genderText, "");
            SetText(dateOfBirthText, "");
            SetText(goldText, "0");
            SetText(pointsText, "0");

            if (verifiedIcon != null)
            {
                verifiedIcon.SetActive(false);
            }
        }

        /// <summary>
        /// Show loading state
        /// </summary>
        public void SetLoadingState(bool isLoading)
        {
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(isLoading);
            }
        }

        #region Event Handlers

        private void OnProfileLoaded(UserProfileSO profile)
        {
            userProfile = profile;
            RefreshUI();
        }

        private void OnAvatarLoaded(Sprite avatar)
        {
            if (avatarImage != null && avatar != null)
            {
                avatarImage.sprite = avatar;
            }
        }

        #endregion

        #region Helper Methods

        private void SetText(TextMeshProUGUI textComponent, string value, string prefix = "")
        {
            if (textComponent == null) return;

            if (string.IsNullOrEmpty(value))
            {
                if (hideEmptyFields)
                {
                    textComponent.gameObject.SetActive(false);
                }
                else
                {
                    textComponent.text = prefix + "—";
                }
            }
            else
            {
                textComponent.gameObject.SetActive(true);
                textComponent.text = prefix + value;
            }
        }

        private string FormatRole(string role)
        {
            if (string.IsNullOrEmpty(role)) return "";

            return role.ToLower() switch
            {
                "student" => "Học sinh",
                "teacher" => "Giáo viên",
                "admin" => "Quản trị",
                _ => role
            };
        }

        private string FormatGender(string gender)
        {
            if (string.IsNullOrEmpty(gender)) return "";

            return gender.ToLower() switch
            {
                "male" => "Nam",
                "female" => "Nữ",
                "other" => "Khác",
                _ => gender
            };
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Request profile refresh from server
        /// </summary>
        public void RequestRefresh()
        {
            if (profileService != null)
            {
                SetLoadingState(true);
                profileService.FetchProfile();
            }
        }

        /// <summary>
        /// Clear profile display (on logout)
        /// </summary>
        public void ClearProfile()
        {
            ShowEmptyState();
        }

        #endregion
    }
}
