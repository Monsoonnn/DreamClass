using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using System;
using UnityEngine.Networking;
using System.Globalization;

namespace DreamClass.ItemsAchivement
{
    public class ItemPrefabHolder : MonoBehaviour {
        public Image iconImage; // Changed to UI.Image
        public TextMeshProUGUI itemName;
        public TextMeshProUGUI timestamp;
        public TextMeshProUGUI description;

        public void SetData(InventoryItem item)
        {
            if (itemName && item.itemDetails != null) itemName.text = item.itemDetails.name;
            if (description && item.itemDetails != null) description.text = item.itemDetails.description;
            
            if (timestamp)
            {
                 if (DateTime.TryParse(item.obtainedDate, null, DateTimeStyles.RoundtripKind, out DateTime date))
                {
                    timestamp.text = date.ToString("dd/MM/yyyy HH:mm");
                }
                else
                {
                    timestamp.text = item.obtainedDate;
                }
            }

            if (iconImage && item.itemDetails != null && !string.IsNullOrEmpty(item.itemDetails.image))
            {
                StartCoroutine(LoadImage(item.itemDetails.image));
            }
        }

        private IEnumerator LoadImage(string url)
        {
            using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(url))
            {
                yield return uwr.SendWebRequest();

                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    Texture2D texture = DownloadHandlerTexture.GetContent(uwr);
                    if (texture != null && iconImage != null)
                    {
                        iconImage.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    }
                }
                else
                {
                    Debug.LogWarning($"[ItemPrefabHolder] Failed to load image: {url}");
                }
            }
        }

    }
    
}