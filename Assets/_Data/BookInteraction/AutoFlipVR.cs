using UnityEngine;
using System.Collections;
using com.cyborgAssets.inspectorButtonPro;

[RequireComponent(typeof(BookVR))]
public class AutoFlipVR : MonoBehaviour
{
    [Header("Book Reference")]
    public BookVR ControledBook;

    [Header("Auto Flip Settings")]
    [Tooltip("Bật/tắt chức năng AutoFlip")]
    public bool enableAutoFlip = true;

    [Header("Animation Settings")]
    public float PageFlipTime = 0.01f;
    public int AnimationFramesCount = 5;

    [Header("Fast Jump Settings")]
    [Tooltip("Số bước lật tối đa khi JumpToPage. Nếu = 0 → lật bình thường theo từng trang.")]
    public int maxJumpSteps = 0;


    [Header("VR Button Controls")]
    public bool enableVRButtons = true;
    public OVRInput.Button flipRightButton = OVRInput.Button.One; // A button
    public OVRInput.Button flipLeftButton = OVRInput.Button.Two;  // B button
    public OVRInput.Controller controller = OVRInput.Controller.RTouch;

    [Header("Debug Info")]
    [SerializeField] private bool isFlipping = false;
    [SerializeField] private bool isJumping = false;
    [SerializeField] private int activeCoroutines = 0;

    private Coroutine jumpCoroutine;
    private Coroutine flipCoroutine;

    void Start()
    {
        if (!ControledBook)
            ControledBook = GetComponent<BookVR>();

        ControledBook.OnFlip.AddListener(new UnityEngine.Events.UnityAction(PageFlipped));
    }

    void Update()
    {
        if (!enableAutoFlip) return;

        // VR button controls - lật từng trang
        if (enableVRButtons && !isJumping)
        {
            if (OVRInput.GetDown(flipRightButton, controller))
            {
                FlipRightPage();
            }

            if (OVRInput.GetDown(flipLeftButton, controller))
            {
                FlipLeftPage();
            }
        }
    }

    void PageFlipped()
    {
        isFlipping = false;
    }

    /// <summary>
    /// Dừng tất cả hoạt động lật trang và clear coroutines
    /// </summary>
    [ProButton]
    public void Clear()
    {
        if (jumpCoroutine != null)
        {
            StopCoroutine(jumpCoroutine);
            jumpCoroutine = null;
        }

        if (flipCoroutine != null)
        {
            StopCoroutine(flipCoroutine);
            flipCoroutine = null;
        }

        StopAllCoroutines();

        isFlipping = false;
        isJumping = false;
        activeCoroutines = 0;

        Debug.Log("Cleared all flipping coroutines");
    }

    /// <summary>
    /// Nhảy đến trang được chỉ định
    /// </summary>
    /// <param name="pageNumber">Số trang cần đến (0-based index)</param>
    [ProButton]
    public void JumpToPage(int pageNumber)
    {
        if (!enableAutoFlip)
        {
            Debug.LogWarning("AutoFlip is disabled!");
            return;
        }

        if (isJumping || isFlipping)
        {
            Debug.LogWarning("Đang trong quá trình lật trang, vui lòng đợi!");
            return;
        }

        // Kiểm tra trang hợp lệ
        if (pageNumber < 0 || pageNumber >= ControledBook.TotalPageCount)
        {
            Debug.LogError($"Trang {pageNumber} không hợp lệ! Tổng số trang: {ControledBook.TotalPageCount}");
            return;
        }

        // Đảm bảo trang là số chẵn (vì sách lật 2 trang một lúc)
        int pageTargetNumber = (pageNumber / 2) * 2;

        if (jumpCoroutine != null)
        {
            StopCoroutine(jumpCoroutine);
        }

        jumpCoroutine = StartCoroutine(JumpToPageCoroutine(pageTargetNumber, pageNumber));
    }

    IEnumerator JumpToPageCoroutine(int targetPageNumber, int baseTarget)
    {
        isJumping = true;
        activeCoroutines++;

        int currentPage = ControledBook.CurrentPage;

        Debug.Log($"Jumping from page {currentPage} → {targetPageNumber}");

        while (currentPage != targetPageNumber)
        {
            int pagesRemaining = targetPageNumber - currentPage;
            bool flipRight = pagesRemaining > 0;

            // Tính số trang sẽ flip lần này
            int step = (maxJumpSteps > 0) ? maxJumpSteps * 2 : Mathf.Abs(pagesRemaining);

            // Nếu số trang còn lại nhỏ hơn step → flip đúng số còn lại
            step = Mathf.Min(Mathf.Abs(pagesRemaining), step);

            int nextPage = flipRight ? currentPage + step : currentPage - step;

            // Nếu bước nhảy cuối sẽ vượt qua target, đặt nextPage = targetPageNumber
            if ((flipRight && nextPage > targetPageNumber) || (!flipRight && nextPage < targetPageNumber))
                nextPage = targetPageNumber;

            // Bắt đầu flip animation
            if (flipRight)
                yield return StartCoroutine(FlipRightPageCoroutine());
            else
                yield return StartCoroutine(FlipLeftPageCoroutine());

            // Đặt trang chính xác
            ControledBook.SetPageInstant(nextPage);
            currentPage = nextPage;

            yield return new WaitForSeconds(0.05f);
        }

        if(ControledBook.CurrentPage != baseTarget)
            ControledBook.SetPageInstant(baseTarget);

        Debug.Log($"Arrived at page {ControledBook.CurrentPage}");

        isJumping = false;
        activeCoroutines--;
        jumpCoroutine = null;
    }

    /// <summary>
    /// Lật trang phải (công khai để gọi từ UI/Script khác)
    /// </summary>
    public void FlipRightPage()
    {
        if (!enableAutoFlip) return;

        if (isFlipping || isJumping)
        {
            Debug.LogWarning("Đang lật trang, vui lòng đợi!");
            return;
        }

        if (!ControledBook.spriteManager.CanFlipRight())
        {
            Debug.LogWarning("Không thể lật sang phải - đã đến trang cuối!");
            return;
        }

        if (flipCoroutine != null)
        {
            StopCoroutine(flipCoroutine);
        }

        flipCoroutine = StartCoroutine(FlipRightPageCoroutine());
    }

    /// <summary>
    /// Lật trang trái (công khai để gọi từ UI/Script khác)
    /// </summary>
    public void FlipLeftPage()
    {
        if (!enableAutoFlip) return;

        if (isFlipping || isJumping)
        {
            Debug.LogWarning("Đang lật trang, vui lòng đợi!");
            return;
        }

        if (!ControledBook.spriteManager.CanFlipLeft())
        {
            Debug.LogWarning("Không thể lật sang trái - đã đến trang đầu!");
            return;
        }

        if (flipCoroutine != null)
        {
            StopCoroutine(flipCoroutine);
        }

        flipCoroutine = StartCoroutine(FlipLeftPageCoroutine());
    }

    IEnumerator FlipRightPageCoroutine()
    {
        isFlipping = true;
        activeCoroutines++;

        float frameTime = PageFlipTime / AnimationFramesCount;
        float xc = (ControledBook.EndBottomRight.x + ControledBook.EndBottomLeft.x) / 2;
        float xl = ((ControledBook.EndBottomRight.x - ControledBook.EndBottomLeft.x) / 2) * 0.9f;
        float h = Mathf.Abs(ControledBook.EndBottomRight.y) * 0.9f;
        float dx = (xl) * 2 / AnimationFramesCount;

        yield return StartCoroutine(FlipRTL(xc, xl, h, frameTime, dx));

        // Optional: Haptic feedback
        OVRInput.SetControllerVibration(0.2f, 0.1f, controller);

        activeCoroutines--;
        flipCoroutine = null;
    }

    IEnumerator FlipLeftPageCoroutine()
    {
        isFlipping = true;
        activeCoroutines++;

        float frameTime = PageFlipTime / AnimationFramesCount;
        float xc = (ControledBook.EndBottomRight.x + ControledBook.EndBottomLeft.x) / 2;
        float xl = ((ControledBook.EndBottomRight.x - ControledBook.EndBottomLeft.x) / 2) * 0.9f;
        float h = Mathf.Abs(ControledBook.EndBottomRight.y) * 0.9f;
        float dx = (xl) * 2 / AnimationFramesCount;

        yield return StartCoroutine(FlipLTR(xc, xl, h, frameTime, dx));

        // Optional: Haptic feedback
        OVRInput.SetControllerVibration(0.2f, 0.1f, controller);

        activeCoroutines--;
        flipCoroutine = null;
    }

    IEnumerator FlipRTL(float xc, float xl, float h, float frameTime, float dx)
    {
        float x = xc + xl;
        float y = (-h / (xl * xl)) * (x - xc) * (x - xc);

        ControledBook.DragRightPageToPoint(new Vector3(x, y, 0));

        for (int i = 0; i < AnimationFramesCount; i++)
        {
            y = (-h / (xl * xl)) * (x - xc) * (x - xc);
            ControledBook.UpdateBookRTLToPoint(new Vector3(x, y, 0));
            yield return new WaitForSeconds(frameTime);
            x -= dx;
        }

        ControledBook.ReleasePage();
        yield return new WaitForSeconds(0.2f);
    }

    IEnumerator FlipLTR(float xc, float xl, float h, float frameTime, float dx)
    {
        float x = xc - xl;
        float y = (-h / (xl * xl)) * (x - xc) * (x - xc);

        ControledBook.DragLeftPageToPoint(new Vector3(x, y, 0));

        for (int i = 0; i < AnimationFramesCount; i++)
        {
            y = (-h / (xl * xl)) * (x - xc) * (x - xc);
            ControledBook.UpdateBookLTRToPoint(new Vector3(x, y, 0));
            yield return new WaitForSeconds(frameTime);
            x += dx;
        }

        ControledBook.ReleasePage();
        yield return new WaitForSeconds(0.2f);
    }

    // === PUBLIC API ===

    /// <summary>
    /// Kiểm tra xem có đang lật trang không
    /// </summary>
    public bool IsFlipping => isFlipping || isJumping;

    /// <summary>
    /// Nhảy đến trang đầu tiên
    /// </summary>
    [ProButton]
    public void JumpToFirstPage()
    {
        JumpToPage(0);
    }

    /// <summary>
    /// Nhảy đến trang cuối
    /// </summary>
    [ProButton]
    public void JumpToLastPage()
    {
        JumpToPage(ControledBook.TotalPageCount - 1);
    }

    /// <summary>
    /// Bật/tắt AutoFlip
    /// </summary>
    public void SetAutoFlipEnabled(bool enabled)
    {
        enableAutoFlip = enabled;
        if (!enabled)
        {
            Clear();
        }
        Debug.Log($"AutoFlip: {(enabled ? "Enabled" : "Disabled")}");
    }

    void OnDestroy()
    {
        Clear();
    }
}