using System;
using System.Collections;
using UnityEngine;
using com.cyborgAssets.inspectorButtonPro;
using DreamClass.Subjects;
using DreamClass.LearningLecture;

/// <summary>
/// Utility để load sprites từ SubjectInfo vào BookSpriteManager
/// Tất cả sprites đã được load từ cache bởi PDFSubjectService
/// Thay thế cho WebPBookLoader khi dùng cache system
/// </summary>
public class BookPageLoader : MonoBehaviour
{
    [Header("References")]
    public BookSpriteManager spriteManager;

    [Header("Current State")]
    [SerializeField] private SubjectInfo currentSubject;
    [SerializeField] private int loadedPageCount;

    // Events
    public event Action<Sprite[]> OnSpritesLoaded;
    public event Action<string> OnLoadError;

    // Loading state
    public bool IsLoading { get; private set; }
    public bool HasLoadedSprites => loadedPageCount > 0;

    private void Start()
    {
        if (spriteManager == null)
        {
            spriteManager = GetComponentInChildren<BookSpriteManager>();
        }
    }

    #region Public API

    /// <summary>
    /// Load sprites từ SubjectInfo vào BookSpriteManager
    /// Sprites phải đã được load từ cache (subject.HasLoadedSprites() = true)
    /// </summary>
    /// <param name="subject">SubjectInfo với sprites đã load</param>
    /// <param name="resetToFirstPage">Reset về trang đầu sau khi load</param>
    /// <returns>True nếu load thành công</returns>
    public bool LoadFromSubject(SubjectInfo subject, bool resetToFirstPage = true)
    {
        if (subject == null)
        {
            Debug.LogError("[BookPageLoader] Subject is null!");
            OnLoadError?.Invoke("Subject is null");
            return false;
        }

        if (!subject.HasLoadedSprites())
        {
            Debug.LogError($"[BookPageLoader] Subject '{subject.name}' has no loaded sprites! Must load from cache first.");
            OnLoadError?.Invoke($"No sprites loaded for: {subject.name}");
            return false;
        }

        if (spriteManager == null)
        {
            Debug.LogError("[BookPageLoader] BookSpriteManager not assigned!");
            OnLoadError?.Invoke("BookSpriteManager not assigned");
            return false;
        }

        // Apply sprites to BookSpriteManager
        currentSubject = subject;
        spriteManager.bookPages = subject.bookPages;
        loadedPageCount = subject.bookPages.Length;

        if (resetToFirstPage)
        {
            spriteManager.currentPage = 2; // First readable page
            spriteManager.UpdateSprites();
        }

        Debug.Log($"[BookPageLoader] Loaded {loadedPageCount} pages for: {subject.name}");
        OnSpritesLoaded?.Invoke(subject.bookPages);
        return true;
    }

    /// <summary>
    /// Load sprites từ SubjectInfo với callback khi hoàn thành
    /// </summary>
    public void LoadFromSubjectAsync(SubjectInfo subject, Action<bool> callback, bool resetToFirstPage = true)
    {
        bool success = LoadFromSubject(subject, resetToFirstPage);
        callback?.Invoke(success);
    }

    /// <summary>
    /// Load sprites trực tiếp từ Sprite array
    /// </summary>
    public bool LoadFromSprites(Sprite[] sprites, bool resetToFirstPage = true)
    {
        if (sprites == null || sprites.Length == 0)
        {
            Debug.LogError("[BookPageLoader] Sprites array is null or empty!");
            OnLoadError?.Invoke("Sprites array is null or empty");
            return false;
        }

        if (spriteManager == null)
        {
            Debug.LogError("[BookPageLoader] BookSpriteManager not assigned!");
            OnLoadError?.Invoke("BookSpriteManager not assigned");
            return false;
        }

        spriteManager.bookPages = sprites;
        loadedPageCount = sprites.Length;
        currentSubject = null;

        if (resetToFirstPage)
        {
            spriteManager.currentPage = 2;
            spriteManager.UpdateSprites();
        }

        Debug.Log($"[BookPageLoader] Loaded {loadedPageCount} pages from sprite array");
        OnSpritesLoaded?.Invoke(sprites);
        return true;
    }

    /// <summary>
    /// Set SpriteManager reference
    /// </summary>
    public void SetSpriteManager(BookSpriteManager manager)
    {
        spriteManager = manager;
    }

    /// <summary>
    /// Get current loaded sprites
    /// </summary>
    public Sprite[] GetLoadedSprites()
    {
        return spriteManager?.bookPages;
    }

    /// <summary>
    /// Get current subject info
    /// </summary>
    public SubjectInfo GetCurrentSubject()
    {
        return currentSubject;
    }

    /// <summary>
    /// Clear loaded sprites without resetting page state
    /// Used before lazy loading new subject
    /// </summary>
    internal void ClearSpritesOnly()
    {
        if (spriteManager != null)
        {
            // Only clear sprites, don't change currentPage or call UpdateSprites
            spriteManager.bookPages = new Sprite[0];
        }
        loadedPageCount = 0;
        Debug.Log("[BookPageLoader] Cleared sprites only (kept page state)");
    }

    /// <summary>
    /// Clear loaded sprites and reset state
    /// </summary>
    [ProButton]
    public void ClearSprites()
    {
        if (spriteManager != null)
        {
            // Don't destroy sprites - they're managed by SubjectInfo/PDFSubjectService
            spriteManager.bookPages = new Sprite[0];
            spriteManager.currentPage = 2;
            spriteManager.UpdateSprites();
        }

        currentSubject = null;
        loadedPageCount = 0;
        Debug.Log("[BookPageLoader] Cleared all loaded sprites");
    }

    /// <summary>
    /// Jump to specific page
    /// </summary>
    public void JumpToPage(int pageIndex)
    {
        if (spriteManager == null) return;

        spriteManager.currentPage = Mathf.Clamp(pageIndex, 0, loadedPageCount - 1);
        spriteManager.UpdateSprites();
        Debug.Log($"[BookPageLoader] Jumped to page: {pageIndex}");
    }

    /// <summary>
    /// Load subject with async lazy loading từ PDFSubjectService
    /// Thích hợp khi muốn load sách ngay sau khi click
    /// </summary>
    /// <param name="subject">SubjectInfo với cloudinaryFolder</param>
    /// <param name="callback">Callback khi load xong (success)</param>
    /// <param name="resetToFirstPage">Reset về trang đầu</param>
    public void LoadSubjectWithLazyLoading(SubjectInfo subject, Action<bool> callback = null, bool resetToFirstPage = true)
    {
        if (subject == null)
        {
            Debug.LogError("[BookPageLoader] Subject is null!");
            callback?.Invoke(false);
            return;
        }

        if (spriteManager == null)
        {
            Debug.LogError("[BookPageLoader] BookSpriteManager not assigned!");
            callback?.Invoke(false);
            return;
        }

        // PRIORITY 1: Check if sprites already loaded (from PRELOAD bundle or cache)
        if (subject.HasLoadedSprites())
        {
            Debug.Log($"[BookPageLoader] ✓ Sprites already loaded for {subject.name} - loading directly (NO LAZY LOAD)");
            Debug.Log($"[BookPageLoader] Sprite count: {subject.bookPages?.Length ?? 0}");
            bool success = LoadFromSubject(subject, resetToFirstPage);
            callback?.Invoke(success);
            return;  // RETURN EARLY - don't start lazy loading!
        }

        // PRIORITY 2: Check if PDFSubjectService exists
        if (PDFSubjectService.Instance == null)
        {
            Debug.LogError("[BookPageLoader] PDFSubjectService not found!");
            callback?.Invoke(false);
            return;
        }

        // PRIORITY 3: Start lazy loading (sprites not loaded yet)
        Debug.Log($"[BookPageLoader] Sprites NOT loaded for {subject.name} - starting LAZY LOAD...");
        Debug.Log($"[BookPageLoader] DEBUG: localImagePaths={subject.localImagePaths?.Count ?? 0}, isCached={subject.isCached}");
        Debug.Log($"[BookPageLoader] DEBUG: bookPages={subject.bookPages?.Length ?? 0}");
        
        // Ensure SpriteManager is active before coroutine
        if (!spriteManager.gameObject.activeSelf)
        {
            spriteManager.gameObject.SetActive(true);
            Debug.Log($"[BookPageLoader] Activated SpriteManager for {subject.name}");
        }

        ClearSpritesOnly();
        
        // Delegate to LearningBookCtrl (which is always active to hold coroutine)
        var bookCtrl = GetComponentInParent<LearningBookCtrl>();
        if (bookCtrl != null)
        {
            bookCtrl.StartLazyLoadCoroutine(subject, this, callback, resetToFirstPage);
        }
        else
        {
            Debug.LogError("[BookPageLoader] LearningBookCtrl not found in parent!");
            callback?.Invoke(false);
        }
    }

    /// <summary>
    /// Internal callback - called by LearningBookCtrl when lazy load completes
    /// </summary>
    internal bool ProcessLoadedSprites(SubjectInfo subject, Sprite[] loadedSprites, bool resetToFirstPage)
    {
        if (loadedSprites == null || loadedSprites.Length == 0)
        {
            Debug.LogError($"[BookPageLoader] No sprites loaded for {subject.name}");
            return false;
        }

        Debug.Log($"[BookPageLoader] Received {loadedSprites.Length} sprites from LearningBookCtrl");
        
        // Assign sprites to subject
        subject.SetBookPages(loadedSprites);
        
        // Load into BookSpriteManager
        bool success = LoadFromSubject(subject, resetToFirstPage);
        Debug.Log($"[BookPageLoader] Lazy load completed for {subject.name}: {(success ? "SUCCESS" : "FAILED")}");
        
        if (success)
        {
            loadedPageCount = loadedSprites.Length;
            OnSpritesLoaded?.Invoke(loadedSprites);
        }
        
        return success;
    }

    #endregion

    #region Debug

    [ProButton]
    public void DebugState()
    {
        Debug.Log("=== BookPageLoader State ===");
        Debug.Log($"SpriteManager: {(spriteManager != null ? "Assigned" : "NULL")}");
        Debug.Log($"Current Subject: {(currentSubject != null ? currentSubject.name : "None")}");
        Debug.Log($"Loaded Pages: {loadedPageCount}");
        Debug.Log($"Current Page: {(spriteManager != null ? spriteManager.currentPage.ToString() : "N/A")}");
    }

    #endregion
}
