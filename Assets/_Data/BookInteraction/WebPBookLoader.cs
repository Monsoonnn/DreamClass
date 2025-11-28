using System;
using System.Collections;
using System.Collections.Generic;
using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;
using UnityEngine.Networking;
using WebP;

/// <summary>
/// Load WebP từ URL và convert thành Sprite để inject vào BookSpriteManager
/// Được tích hợp với LearningModeManager để load sprites từ API
/// </summary>
public class WebPBookLoader : MonoBehaviour
{
    [Header("References")]
    public BookSpriteManager spriteManager;

    [Header("Loaded Sprites")]
    public Sprite[] loadedWebPSprites;

    // Events for async loading
    public event Action<Sprite[]> OnSpritesLoaded;
    public event Action<int, int> OnLoadProgress; // current, total
    public event Action<string> OnLoadError;

    // Loading state
    public bool IsLoading { get; private set; }

    private void Start()
    {
        if (spriteManager == null)
        {
            spriteManager = GetComponentInChildren<BookSpriteManager>();
        }
    }

    #region Public API for LearningModeManager Integration

    /// <summary>
    /// Load WebP pages từ danh sách URLs (API public cho LearningModeManager)
    /// </summary>
    /// <param name="urls">Danh sách URLs của WebP images</param>
    /// <param name="autoApplyToSpriteManager">Tự động apply vào spriteManager khi load xong</param>
    public void LoadFromURLList(List<string> urls, bool autoApplyToSpriteManager = true)
    {
        if (urls == null || urls.Count == 0)
        {
            Debug.LogError("[WebPBookLoader] URL list is empty!");
            OnLoadError?.Invoke("URL list is empty");
            return;
        }

        StartCoroutine(LoadFromURLListCoroutine(urls, autoApplyToSpriteManager));
    }

    /// <summary>
    /// Load WebP pages từ danh sách URLs (Coroutine version với callback)
    /// </summary>
    public IEnumerator LoadFromURLListCoroutine(List<string> urls, bool autoApplyToSpriteManager, Action<Sprite[]> callback = null)
    {
        if (urls == null || urls.Count == 0)
        {
            Debug.LogError("[WebPBookLoader] URL list is empty!");
            OnLoadError?.Invoke("URL list is empty");
            callback?.Invoke(null);
            yield break;
        }

        IsLoading = true;
        loadedWebPSprites = new Sprite[urls.Count];
        int loadedCount = 0;
        int failedCount = 0;

        Debug.Log($"[WebPBookLoader] Starting to load {urls.Count} WebP pages...");

        for (int i = 0; i < urls.Count; i++)
        {
            string url = urls[i];
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogWarning($"[WebPBookLoader] URL at index {i} is empty, skipping...");
                failedCount++;
                OnLoadProgress?.Invoke(i + 1, urls.Count);
                continue;
            }

            yield return StartCoroutine(LoadWebPFromURLCoroutine(url, i, (sprite, index) =>
            {
                if (sprite != null && index < loadedWebPSprites.Length)
                {
                    loadedWebPSprites[index] = sprite;
                    loadedCount++;
                }
                else
                {
                    failedCount++;
                }
            }));

            OnLoadProgress?.Invoke(i + 1, urls.Count);
            yield return new WaitForSeconds(0.1f); // Small delay between requests
        }

        IsLoading = false;
        Debug.Log($"[WebPBookLoader] Loaded {loadedCount}/{urls.Count} WebP pages (failed: {failedCount})");

        if (loadedCount > 0)
        {
            // Auto apply to SpriteManager if enabled
            if (autoApplyToSpriteManager && spriteManager != null)
            {
                spriteManager.bookPages = loadedWebPSprites;
                Debug.Log($"[WebPBookLoader] Applied {loadedWebPSprites.Length} sprites to SpriteManager");
            }

            OnSpritesLoaded?.Invoke(loadedWebPSprites);
            callback?.Invoke(loadedWebPSprites);
        }
        else
        {
            OnLoadError?.Invoke($"Failed to load any WebP pages (0/{urls.Count})");
            callback?.Invoke(null);
        }
    }

    /// <summary>
    /// Load single WebP từ URL với callback
    /// </summary>
    private IEnumerator LoadWebPFromURLCoroutine(string url, int index, Action<Sprite, int> callback)
    {
        Debug.Log($"[WebPBookLoader] Loading WebP [{index}] from: {url}");

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
                    Sprite sprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f
                    );
                    sprite.name = $"WebPPage_{index}";
                    callback?.Invoke(sprite, index);
                }
                else
                {
                    Debug.LogError($"[WebPBookLoader] Failed to convert WebP. Error: {error}");
                    callback?.Invoke(null, index);
                }
            }
            else
            {
                Debug.LogError($"[WebPBookLoader] Failed to download WebP: {request.error}");
                callback?.Invoke(null, index);
            }
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Set SpriteManager reference programmatically
    /// </summary>
    public void SetSpriteManager(BookSpriteManager manager)
    {
        spriteManager = manager;
    }

    /// <summary>
    /// Get loaded WebP sprites
    /// </summary>
    public Sprite[] GetLoadedWebPSprites()
    {
        return loadedWebPSprites;
    }

    /// <summary>
    /// Apply loaded sprites to SpriteManager
    /// </summary>
    [ProButton]
    public void ApplyToSpriteManager()
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
        Debug.Log($"[WebPBookLoader] Applied {loadedWebPSprites.Length} WebP sprites to SpriteManager");
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

    #endregion
}
