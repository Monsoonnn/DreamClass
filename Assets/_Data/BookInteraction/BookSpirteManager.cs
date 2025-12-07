using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;

public class BookSpriteManager : MonoBehaviour
{
    [Header("Sprite Settings")]
    public Sprite background;
    [SerializeField] private Sprite[] _bookPages;

    /// <summary>
    /// Event khi bookPages thay đổi
    /// </summary>
    public event Action<Sprite[]> OnBookPagesChanged;

    /// <summary>
    /// Get/Set book pages - auto update sprites khi set
    /// </summary>
    public Sprite[] bookPages
    {
        get => _bookPages;
        set
        {
            _bookPages = value;
            currentPage = 2; // Reset về trang đầu
            UpdateSprites();
            OnBookPagesChanged?.Invoke(_bookPages);
            Debug.Log($"[BookSpriteManager] Book pages updated: {_bookPages?.Length ?? 0} pages");
        }
    }

    [Header("UI References")]
    public Image ClippingPlane;
    public Image NextPageClip;
    public Image Shadow;
    public Image ShadowLTR;
    public Image Left;
    public Image LeftNext;
    public Image Right;
    public Image RightNext;

    [Header("Settings")]
    public bool enableShadowEffect = true;

    public int currentPage = 2;
    private FlipMode mode;

    // Lưu trữ vị trí và rotation ban đầu
    private Vector3 leftNextInitialPosition;
    private Quaternion leftNextInitialRotation;
    private Vector3 rightNextInitialPosition;
    private Quaternion rightNextInitialRotation;
    private Transform leftNextInitialParent;
    private Transform rightNextInitialParent;

    public int CurrentPage
    {
        get { return currentPage; }
        set { currentPage = value; }
    }

    public int TotalPageCount
    {
        get { return bookPages.Length; }
    }

    void Start()
    {
        Left.gameObject.SetActive(false);
        Right.gameObject.SetActive(false);

        // Lưu trữ vị trí và rotation ban đầu
        SaveInitialTransforms();

        UpdateSprites();
    }

    void SaveInitialTransforms()
    {
        if (LeftNext != null)
        {
            leftNextInitialPosition = LeftNext.transform.localPosition;
            leftNextInitialRotation = LeftNext.transform.localRotation;
            leftNextInitialParent = LeftNext.transform.parent;
            //Debug.Log($"✓ Saved LeftNext initial: pos={leftNextInitialPosition}, rot={leftNextInitialRotation.eulerAngles}");
        }

        if (RightNext != null)
        {
            rightNextInitialPosition = RightNext.transform.localPosition;
            rightNextInitialRotation = RightNext.transform.localRotation;
            rightNextInitialParent = RightNext.transform.parent;
            //Debug.Log($"✓ Saved RightNext initial: pos={rightNextInitialPosition}, rot={rightNextInitialRotation.eulerAngles}");
        }
    }

    public void ResetNextPagesToInitial()
    {
        if (LeftNext != null)
        {
            LeftNext.transform.SetParent(leftNextInitialParent);
            LeftNext.transform.localPosition = leftNextInitialPosition;
            LeftNext.transform.localRotation = leftNextInitialRotation;
            LeftNext.transform.localScale = Vector3.one;
        }

        if (RightNext != null)
        {
            RightNext.transform.SetParent(rightNextInitialParent);
            RightNext.transform.localPosition = rightNextInitialPosition;
            RightNext.transform.localRotation = rightNextInitialRotation;
            RightNext.transform.localScale = Vector3.one;
        }

        //Debug.Log("✓ Reset LeftNext & RightNext to initial transforms");
    }

    public void UpdateSprites()
    {
        // Safety check
        if (bookPages == null || bookPages.Length == 0)
        {
            Debug.LogWarning("[BookSpriteManager] UpdateSprites: bookPages is null or empty!");
            LeftNext.sprite = background;
            RightNext.sprite = background;
            return;
        }

        LeftNext.sprite = (currentPage > 0 && currentPage <= bookPages.Length) ? bookPages[currentPage - 1] : background;
        RightNext.sprite = (currentPage >= 0 && currentPage < bookPages.Length) ? bookPages[currentPage] : background;
    }

    public void SetupRightPageDrag()
    {
        // Safety check
        if (bookPages == null || bookPages.Length == 0)
        {
            Debug.LogWarning("[BookSpriteManager] SetupRightPageDrag: bookPages is null or empty!");
            return;
        }

        if (currentPage >= bookPages.Length) return;

        //Debug.Log("SetupRightPageDrag");

        Right.gameObject.SetActive(true);
        Right.transform.position = RightNext.transform.position;
        Right.transform.eulerAngles = Vector3.zero;
        Right.sprite = (currentPage < bookPages.Length - 1) ? bookPages[currentPage + 1] : background;
        RightNext.sprite = (currentPage < bookPages.Length - 2) ? bookPages[currentPage + 2] : background;

        LeftNext.transform.SetAsFirstSibling();

        Left.gameObject.SetActive(true);
        Left.rectTransform.pivot = new Vector2(0, 0);
        Left.transform.position = RightNext.transform.position;
        Left.transform.eulerAngles = Vector3.zero;
        Left.sprite = (currentPage < bookPages.Length) ? bookPages[currentPage] : background;
        Left.transform.SetAsFirstSibling();


        if (enableShadowEffect) Shadow.gameObject.SetActive(true);
    }

    public void SetupLeftPageDrag()
    {
        // Safety check
        if (bookPages == null || bookPages.Length == 0)
        {
            Debug.LogWarning("[BookSpriteManager] SetupLeftPageDrag: bookPages is null or empty!");
            return;
        }

        if (currentPage <= 0) return;

        //Debug.Log("SetupLeftPageDrag");

        Left.gameObject.SetActive(true);
        Left.rectTransform.pivot = new Vector2(1, 0);
        Left.transform.position = LeftNext.transform.position;
        Left.transform.eulerAngles = Vector3.zero;
        Left.sprite = (currentPage >= 2) ? bookPages[currentPage - 2] : background;
        LeftNext.sprite = (currentPage >= 3) ? bookPages[currentPage - 3] : background;

        Right.gameObject.SetActive(true);
        Right.transform.position = LeftNext.transform.position;
        Right.sprite = bookPages[currentPage - 1];
        Right.transform.eulerAngles = Vector3.zero;
        Right.transform.SetAsFirstSibling();

        RightNext.transform.SetAsFirstSibling();
        if (enableShadowEffect) ShadowLTR.gameObject.SetActive(true);
    }

    public void FlipForward(FlipMode flipMode)
    {

        //Debug.Log("Flip Forward");
        mode = flipMode;

        if (mode == FlipMode.RightToLeft)
            currentPage += 2;
        else
            currentPage -= 2;

        LeftNext.transform.SetParent(transform, true);
        Left.transform.SetParent(transform, true);
        LeftNext.transform.SetParent(transform, true);
        Left.gameObject.SetActive(false);
        Right.gameObject.SetActive(false);
        Right.transform.SetParent(transform, true);
        RightNext.transform.SetParent(transform, true);

        // Reset về vị trí ban đầu sau khi flip
        ResetNextPagesToInitial();

        UpdateSprites();
        Shadow.gameObject.SetActive(false);
        ShadowLTR.gameObject.SetActive(false);
    }

    public void ResetPagesAfterTweenBack(FlipMode flipMode, Transform bookPanel)
    {
        mode = flipMode;

        UpdateSprites();

        if (mode == FlipMode.RightToLeft)
        {
            RightNext.transform.SetParent(bookPanel);
            Right.transform.SetParent(bookPanel);
        }
        else
        {
            LeftNext.transform.SetParent(bookPanel);
            Left.transform.SetParent(bookPanel);
        }

        Left.gameObject.SetActive(false);
        Right.gameObject.SetActive(false);

        // Reset về vị trí ban đầu sau khi tween back
        ResetNextPagesToInitial();
    }

    public bool CanFlipRight()
    {
        return currentPage < bookPages.Length - 1;
    }

    public bool CanFlipLeft()
    {
        return currentPage > 2;
    }
    // This updates the page display (left-right sprites)
    public void ShowPage(int pageIndex)
    {
        CurrentPage = pageIndex;
        UpdateSprites();
    }

    // Ensure the visual elements update correctly (shadows, clipping, etc.)
    public void RefreshBookState()
    {
        UpdateSprites();
        //UpdateShadows();
    }

}