using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

namespace DreamClass.Ranking
{
    /// <summary>
    /// Prefab holder để hiển thị thông tin ranking của một student
    /// </summary>
    public class RankingHolder : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI rankText;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI classText;
        [SerializeField] private TextMeshProUGUI gradeText;
        [SerializeField] private TextMeshProUGUI pointsText;
        [SerializeField] private Image avatarImage;

        [Header("Avatar Settings")]
        [SerializeField] private Sprite defaultAvatar;
        [SerializeField] private bool loadAvatarFromURL = true;

        /// <summary>
        /// Cập nhật UI với dữ liệu ranking (không load avatar)
        /// </summary>
        public void SetData(RankingStudentData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[RankingHolder] Data is null!");
                return;
            }

            if (rankText != null) rankText.text = data.rank.ToString();
            if (nameText != null) nameText.text = data.name ?? "N/A";
            if (classText != null) classText.text = data.className ?? "N/A";
            if (gradeText != null) gradeText.text = data.grade ?? "N/A";
            if (pointsText != null) pointsText.text = data.points.ToString();

            // Set default avatar
            if (avatarImage != null && defaultAvatar != null)
            {
                avatarImage.sprite = defaultAvatar;
            }
        }

        /// <summary>
        /// Set avatar sprite (được gọi từ Manager sau khi fetch)
        /// </summary>
        public void SetAvatar(Sprite sprite)
        {
            if (avatarImage != null && sprite != null)
            {
                avatarImage.sprite = sprite;
            }
        }

        /// <summary>
        /// Load avatar từ URL (internal method, không dùng nữa)
        /// </summary>
        [System.Obsolete("Use SetAvatar instead. Avatar should be loaded by RankingManager")]
        public void LoadAvatar(string avatarUrl)
        {
            if (!loadAvatarFromURL || string.IsNullOrEmpty(avatarUrl))
                return;

            if (avatarImage != null)
            {
                StartCoroutine(LoadAvatarFromURL(avatarUrl));
            }
        }

        /// <summary>
        /// Clear tất cả text fields
        /// </summary>
        public void Clear()
        {
            if (rankText != null) rankText.text = "";
            if (nameText != null) nameText.text = "";
            if (classText != null) classText.text = "";
            if (gradeText != null) gradeText.text = "";
            if (pointsText != null) pointsText.text = "";
            if (avatarImage != null && defaultAvatar != null)
            {
                avatarImage.sprite = defaultAvatar;
            }
        }

        /// <summary>
        /// Coroutine load avatar từ URL (hỗ trợ PNG, JPG, WebP)
        /// </summary>
        private IEnumerator LoadAvatarFromURL(string url)
        {
            // Set default avatar first
            if (defaultAvatar != null && avatarImage != null)
            {
                avatarImage.sprite = defaultAvatar;
            }

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    byte[] imageData = request.downloadHandler.data;

                    // Check if it's WebP format
                    if (imageData.Length >= 12 && 
                        imageData[0] == 'R' && imageData[1] == 'I' && 
                        imageData[2] == 'F' && imageData[3] == 'F' &&
                        imageData[8] == 'W' && imageData[9] == 'E' && 
                        imageData[10] == 'B' && imageData[11] == 'P')
                    {
                        // Decode WebP
                        WebP.Error error;
                        Texture2D texture = WebP.Texture2DExt.CreateTexture2DFromWebP(imageData, false, false, out error);
                        
                        if (error == WebP.Error.Success && texture != null)
                        {
                            Sprite sprite = Sprite.Create(
                                texture,
                                new Rect(0, 0, texture.width, texture.height),
                                new Vector2(0.5f, 0.5f)
                            );
                            
                            if (avatarImage != null)
                            {
                                avatarImage.sprite = sprite;
                            }
                        }
                    }
                    else
                    {
                        // Standard PNG/JPG
                        Texture2D texture = new Texture2D(2, 2);
                        if (texture.LoadImage(imageData))
                        {
                            Sprite sprite = Sprite.Create(
                                texture,
                                new Rect(0, 0, texture.width, texture.height),
                                new Vector2(0.5f, 0.5f)
                            );
                            
                            if (avatarImage != null)
                            {
                                avatarImage.sprite = sprite;
                            }
                        }
                    }
                }
            }
        }
    }
}
