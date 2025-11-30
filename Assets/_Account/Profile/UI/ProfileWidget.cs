using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DreamClass.Account;

namespace DreamClass.Account.UI
{
    /// <summary>
    /// Compact profile widget cho header/navbar
    /// Hiển thị avatar, tên và gold
    /// </summary>
    public class ProfileWidget : MonoBehaviour
    {
        [Header("Profile Data")]
        [SerializeField] private UserProfileSO userProfile;
        [SerializeField] private ProfileService profileService;

        [Header("UI Elements")]
        [SerializeField] private Image avatarImage;
        [SerializeField] private Sprite defaultAvatar;
        [SerializeField] private TextMeshProUGUI userNameText;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private Image goldIcon;

        [Header("Optional")]
        [SerializeField] private Button profileButton;
        [SerializeField] private GameObject profilePanel;

        private void OnEnable()
        {
            if (userProfile != null)
            {
                userProfile.OnProfileUpdated += RefreshUI;
            }

            if (profileService != null)
            {
                profileService.OnAvatarLoaded += OnAvatarLoaded;
            }

            RefreshUI();
        }

        private void OnDisable()
        {
            if (userProfile != null)
            {
                userProfile.OnProfileUpdated -= RefreshUI;
            }

            if (profileService != null)
            {
                profileService.OnAvatarLoaded -= OnAvatarLoaded;
            }
        }

        private void Start()
        {
            // Try to find references
            if (profileService == null)
            {
                profileService = FindAnyObjectByType<ProfileService>();
            }

            if (userProfile == null && profileService != null)
            {
                userProfile = profileService.GetProfile();
            }

            // Setup button
            if (profileButton != null)
            {
                profileButton.onClick.AddListener(ToggleProfilePanel);
            }

            RefreshUI();
        }

        public void RefreshUI()
        {
            if (userProfile == null || !userProfile.HasProfile)
            {
                ShowLoggedOutState();
                return;
            }

            // Avatar
            if (avatarImage != null)
            {
                avatarImage.sprite = userProfile.avatarSprite != null 
                    ? userProfile.avatarSprite 
                    : defaultAvatar;
            }

            // Name (shortened if too long)
            if (userNameText != null)
            {
                string displayName = userProfile.userName;
                if (displayName.Length > 12)
                {
                    displayName = displayName.Substring(0, 10) + "...";
                }
                userNameText.text = displayName;
            }

            // Gold
            if (goldText != null)
            {
                goldText.text = FormatNumber(userProfile.gold);
            }
        }

        private void ShowLoggedOutState()
        {
            if (avatarImage != null)
            {
                avatarImage.sprite = defaultAvatar;
            }

            if (userNameText != null)
            {
                userNameText.text = "Đăng nhập";
            }

            if (goldText != null)
            {
                goldText.text = "0";
            }
        }

        private void OnAvatarLoaded(Sprite avatar)
        {
            if (avatarImage != null && avatar != null)
            {
                avatarImage.sprite = avatar;
            }
        }

        private void ToggleProfilePanel()
        {
            if (profilePanel != null)
            {
                profilePanel.SetActive(!profilePanel.activeSelf);
            }
        }

        private string FormatNumber(int number)
        {
            if (number >= 1000000)
            {
                return (number / 1000000f).ToString("0.#") + "M";
            }
            else if (number >= 1000)
            {
                return (number / 1000f).ToString("0.#") + "K";
            }
            return number.ToString();
        }

        /// <summary>
        /// Update gold display (can be called when gold changes)
        /// </summary>
        public void UpdateGold(int newGold)
        {
            if (goldText != null)
            {
                goldText.text = FormatNumber(newGold);
            }
        }
    }
}
