using UnityEngine;
using System;

namespace DreamClass.Account
{
    /// <summary>
    /// ScriptableObject để lưu thông tin profile user
    /// Được fetch từ API /api/auth/profile sau khi login
    /// </summary>
    [CreateAssetMenu(fileName = "UserProfile", menuName = "DreamClass/Account/User Profile")]
    public class UserProfileSO : ScriptableObject
    {
        [Header("Basic Info")]
        public string id;
        public string playerId;
        public string userName;
        public string email;
        public string role;

        [Header("Student Info")]
        public string grade;
        public string className;
        public string gender;
        public string dateOfBirth;
        public string address;
        public string notes;
        public string status;

        [Header("Game Stats")]
        public int gold;
        public int points;

        [Header("Account Status")]
        public bool isVerified;
        public string createdAt;
        public string updatedAt;

        [Header("Avatar")]
        public string avatarUrl;
        [NonSerialized] public Texture2D avatarTexture;
        [NonSerialized] public Sprite avatarSprite;

        /// <summary>
        /// Event khi profile được update
        /// </summary>
        public event Action OnProfileUpdated;

        /// <summary>
        /// Update profile từ API response
        /// </summary>
        public void UpdateFromResponse(ProfileData data)
        {
            if (data == null) return;

            id = data._id;
            playerId = data.playerId;
            userName = data.name;
            email = data.email;
            role = data.role;

            grade = data.grade;
            className = data.className;
            gender = data.gender;
            dateOfBirth = data.dateOfBirth;
            address = data.address;
            notes = data.notes;
            status = data.status;

            gold = data.gold;
            points = data.points;

            isVerified = data.isVerified;
            createdAt = data.createdAt;
            updatedAt = data.updatedAt;

            avatarUrl = data.avatar;

            OnProfileUpdated?.Invoke();

            Debug.Log($"[UserProfileSO] Profile updated: {userName} ({playerId})");
        }

        /// <summary>
        /// Clear all profile data (on logout)
        /// </summary>
        public void Clear()
        {
            id = "";
            playerId = "";
            userName = "";
            email = "";
            role = "";

            grade = "";
            className = "";
            gender = "";
            dateOfBirth = "";
            address = "";
            notes = "";
            status = "";

            gold = 0;
            points = 0;

            isVerified = false;
            createdAt = "";
            updatedAt = "";

            avatarUrl = "";
            avatarTexture = null;
            avatarSprite = null;

            Debug.Log("[UserProfileSO] Profile cleared");
        }

        /// <summary>
        /// Set avatar từ downloaded texture
        /// </summary>
        public void SetAvatar(Texture2D texture)
        {
            avatarTexture = texture;
            if (texture != null)
            {
                avatarSprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f
                );
                avatarSprite.name = "UserAvatar";
            }
            else
            {
                avatarSprite = null;
            }

            OnProfileUpdated?.Invoke();
        }

        /// <summary>
        /// Check if profile has been loaded
        /// </summary>
        public bool HasProfile => !string.IsNullOrEmpty(playerId);

        /// <summary>
        /// Get formatted date of birth
        /// </summary>
        public string FormattedDateOfBirth
        {
            get
            {
                if (string.IsNullOrEmpty(dateOfBirth)) return "";
                if (DateTime.TryParse(dateOfBirth, out DateTime dob))
                {
                    return dob.ToString("dd/MM/yyyy");
                }
                return dateOfBirth;
            }
        }
    }

    #region API Response Models

    [Serializable]
    public class ProfileResponse
    {
        public string message;
        public ProfileData data;
    }

    [Serializable]
    public class ProfileData
    {
        public string _id;
        public string playerId;
        public string name;
        public string email;
        public string role;

        public string grade;
        public string className;
        public string gender;
        public string dateOfBirth;
        public string address;
        public string notes;
        public string status;

        public int gold;
        public int points;

        public bool isVerified;
        public string createdAt;
        public string updatedAt;

        public string avatar;
    }

    #endregion
}
