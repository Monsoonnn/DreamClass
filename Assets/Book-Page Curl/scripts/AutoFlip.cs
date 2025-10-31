using UnityEngine;
using System.Collections;

[RequireComponent(typeof(BookVR))]
public class AutoFlipVR : MonoBehaviour {
    public FlipMode Mode;
    public float PageFlipTime = 1;
    public float TimeBetweenPages = 1;
    public float DelayBeforeStarting = 0;
    public bool AutoStartFlip = true;
    public BookVR ControledBook;
    public int AnimationFramesCount = 40;

    [Header("VR Controls")]
    public OVRInput.Button flipRightButton = OVRInput.Button.One; // A button
    public OVRInput.Button flipLeftButton = OVRInput.Button.Two;  // B button
    public OVRInput.Controller controller = OVRInput.Controller.RTouch;

    bool isFlipping = false;

    void Start() {
        if (!ControledBook)
            ControledBook = GetComponent<BookVR>();

        if (AutoStartFlip)
            StartFlipping();

        ControledBook.OnFlip.AddListener(new UnityEngine.Events.UnityAction(PageFlipped));
    }

    void Update() {
        // VR button controls
        if (OVRInput.GetDown(flipRightButton, controller)) {
            FlipRightPage();
        }

        if (OVRInput.GetDown(flipLeftButton, controller)) {
            FlipLeftPage();
        }
    }

    void PageFlipped() {
        isFlipping = false;
    }

    public void StartFlipping() {
        StartCoroutine(FlipToEnd());
    }

    public void FlipRightPage() {
        if (isFlipping) return;
        if (ControledBook.currentPage >= ControledBook.TotalPageCount) return;
        isFlipping = true;

        float frameTime = PageFlipTime / AnimationFramesCount;
        float xc = (ControledBook.EndBottomRight.x + ControledBook.EndBottomLeft.x) / 2;
        float xl = ((ControledBook.EndBottomRight.x - ControledBook.EndBottomLeft.x) / 2) * 0.9f;
        float h = Mathf.Abs(ControledBook.EndBottomRight.y) * 0.9f;
        float dx = (xl) * 2 / AnimationFramesCount;

        StartCoroutine(FlipRTL(xc, xl, h, frameTime, dx));

        // Optional: Haptic feedback
        OVRInput.SetControllerVibration(0.2f, 0.1f, controller);
    }

    public void FlipLeftPage() {
        if (isFlipping) return;
        if (ControledBook.currentPage <= 0) return;
        isFlipping = true;

        float frameTime = PageFlipTime / AnimationFramesCount;
        float xc = (ControledBook.EndBottomRight.x + ControledBook.EndBottomLeft.x) / 2;
        float xl = ((ControledBook.EndBottomRight.x - ControledBook.EndBottomLeft.x) / 2) * 0.9f;
        float h = Mathf.Abs(ControledBook.EndBottomRight.y) * 0.9f;
        float dx = (xl) * 2 / AnimationFramesCount;

        StartCoroutine(FlipLTR(xc, xl, h, frameTime, dx));

        // Optional: Haptic feedback
        OVRInput.SetControllerVibration(0.2f, 0.1f, controller);
    }

    IEnumerator FlipToEnd() {
        yield return new WaitForSeconds(DelayBeforeStarting);

        float frameTime = PageFlipTime / AnimationFramesCount;
        float xc = (ControledBook.EndBottomRight.x + ControledBook.EndBottomLeft.x) / 2;
        float xl = ((ControledBook.EndBottomRight.x - ControledBook.EndBottomLeft.x) / 2) * 0.9f;
        float h = Mathf.Abs(ControledBook.EndBottomRight.y) * 0.9f;
        float dx = (xl) * 2 / AnimationFramesCount;

        switch (Mode) {
            case FlipMode.RightToLeft:
                while (ControledBook.currentPage < ControledBook.TotalPageCount) {
                    StartCoroutine(FlipRTL(xc, xl, h, frameTime, dx));
                    yield return new WaitForSeconds(TimeBetweenPages);
                }
                break;

            case FlipMode.LeftToRight:
                while (ControledBook.currentPage > 0) {
                    StartCoroutine(FlipLTR(xc, xl, h, frameTime, dx));
                    yield return new WaitForSeconds(TimeBetweenPages);
                }
                break;
        }
    }

    IEnumerator FlipRTL( float xc, float xl, float h, float frameTime, float dx ) {
        float x = xc + xl;
        float y = (-h / (xl * xl)) * (x - xc) * (x - xc);

        ControledBook.DragRightPageToPoint(new Vector3(x, y, 0));

        for (int i = 0; i < AnimationFramesCount; i++) {
            y = (-h / (xl * xl)) * (x - xc) * (x - xc);
            ControledBook.UpdateBookRTLToPoint(new Vector3(x, y, 0));
            yield return new WaitForSeconds(frameTime);
            x -= dx;
        }

        ControledBook.ReleasePage();
    }

    IEnumerator FlipLTR( float xc, float xl, float h, float frameTime, float dx ) {
        float x = xc - xl;
        float y = (-h / (xl * xl)) * (x - xc) * (x - xc);

        ControledBook.DragLeftPageToPoint(new Vector3(x, y, 0));

        for (int i = 0; i < AnimationFramesCount; i++) {
            y = (-h / (xl * xl)) * (x - xc) * (x - xc);
            ControledBook.UpdateBookLTRToPoint(new Vector3(x, y, 0));
            yield return new WaitForSeconds(frameTime);
            x += dx;
        }

        ControledBook.ReleasePage();
    }
}