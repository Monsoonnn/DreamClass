using System.Collections;
using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;
using UnityEngine.Networking;
using WebP;

/// <summary>
/// Load WebP từ URL và convert thành Sprite để inject vào BookSpriteManager
/// Giữ nguyên hệ thống Sprite hiện tại, chỉ thêm khả năng load WebP
/// </summary>
public class WebPBookLoader : MonoBehaviour
{
    [Header("References")]
    public BookSpriteManager spriteManager;

    [Header("WebP Page URLs")]
    [SerializeField] private string[] pageURLs;

    public Sprite[] loadedWebPSprites;

    private void Start()
    {
        if (spriteManager == null)
        {
            spriteManager = GetComponentInChildren<BookSpriteManager>();
        }
    }

    /// <summary>
    /// Load WebP từ URL và convert thành Sprite
    /// </summary>
    [ProButton]
    public void LoadWebPPage(string url, int pageIndex)
    {
        if (spriteManager == null)
        {
            Debug.LogError("[WebPBookLoader] SpriteManager not assigned");
            return;
        }

        StartCoroutine(LoadWebPCoroutine(url, pageIndex));
    }

    /// <summary>
    /// Load tất cả WebP pages từ URL array
    /// </summary>
    [ProButton]
    public void LoadAllWebPPages()
    {
        if (pageURLs == null || pageURLs.Length == 0)
        {
            Debug.LogError("[WebPBookLoader] pageURLs is empty");
            return;
        }

        if (spriteManager == null)
        {
            Debug.LogError("[WebPBookLoader] SpriteManager not assigned");
            return;
        }

        // Initialize sprite array nếu chưa có
        if (loadedWebPSprites == null || loadedWebPSprites.Length != pageURLs.Length)
        {
            loadedWebPSprites = new Sprite[pageURLs.Length];
        }

        StartCoroutine(LoadAllWebPCoroutine());
    }

    /// <summary>
    /// Thay thế bookPages của SpriteManager bằng loaded WebP sprites
    /// </summary>
    [ProButton]
    public void ReplaceBookPagesWithWebP()
    {
        if (loadedWebPSprites == null || loadedWebPSprites.Length == 0)
        {
            Debug.LogError("[WebPBookLoader] No WebP pages loaded yet");
            return;
        }

        if (spriteManager == null)
        {
            Debug.LogError("[WebPBookLoader] SpriteManager not assigned");
            return;
        }

        spriteManager.bookPages = loadedWebPSprites;
        spriteManager.UpdateSprites();
        Debug.Log($"[WebPBookLoader] Replaced bookPages with {loadedWebPSprites.Length} WebP sprites");
    }

    /// <summary>
    /// Load 1 WebP page và thêm vào danh sách
    /// </summary>
    private IEnumerator LoadWebPCoroutine(string url, int pageIndex)
    {
        Debug.Log($"[WebPBookLoader] Loading WebP from: {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                byte[] webpData = request.downloadHandler.data;

                Error error;
                Texture2D texture = Texture2DExt.CreateTexture2DFromWebP(webpData, false, false, out error);

                if (error == Error.Success && texture != null)
                {
                    // Convert Texture2D to Sprite
                    Sprite sprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f
                    );
                    sprite.name = $"WebPPage_{pageIndex}";

                    // Lưu vào array
                    if (loadedWebPSprites == null)
                    {
                        loadedWebPSprites = new Sprite[pageURLs.Length];
                    }

                    if (pageIndex >= 0 && pageIndex < loadedWebPSprites.Length)
                    {
                        loadedWebPSprites[pageIndex] = sprite;
                        Debug.Log($"[WebPBookLoader] Successfully loaded WebP page {pageIndex}");
                    }
                }
                else
                {
                    Debug.LogError($"[WebPBookLoader] Failed to convert WebP. Error: {error}");
                }
            }
            else
            {
                Debug.LogError($"[WebPBookLoader] Failed to download WebP: {request.error}");
            }
        }
    }

    /// <summary>
    /// Load tất cả WebP pages từ URLs array
    /// </summary>
    private IEnumerator LoadAllWebPCoroutine()
    {
        loadedWebPSprites = new Sprite[pageURLs.Length];

        for (int i = 0; i < pageURLs.Length; i++)
        {
            if (string.IsNullOrEmpty(pageURLs[i]))
            {
                Debug.LogWarning($"[WebPBookLoader] Page URL {i} is empty, skipping...");
                continue;
            }

            yield return StartCoroutine(LoadWebPCoroutine(pageURLs[i], i));
            yield return new WaitForSeconds(0.3f); // Delay giữa các request
        }

        Debug.Log("[WebPBookLoader] All WebP pages loaded!");
    }

    /// <summary>
    /// Get loaded WebP sprites
    /// </summary>
    public Sprite[] GetLoadedWebPSprites()
    {
        return loadedWebPSprites;
    }

    /// <summary>
    /// Clear loaded sprites
    /// </summary>
    [ProButton]
    public void ClearLoadedSprites()
    {
        if (loadedWebPSprites != null)
        {
            foreach (var sprite in loadedWebPSprites)
            {
                if (sprite != null && sprite.texture != null)
                {
                    Destroy(sprite.texture);
                    Destroy(sprite);
                }
            }
        }
        loadedWebPSprites = null;
        Debug.Log("[WebPBookLoader] Cleared all loaded sprites");
    }
}
