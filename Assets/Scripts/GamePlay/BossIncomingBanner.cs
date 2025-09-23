using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[DisallowMultipleComponent]
public class BossIncomingBanner : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform rect;   // gắn chính RectTransform của banner
    [SerializeField] private Image image;          // ảnh chữ "BOSS COMING"
    [SerializeField] private CanvasGroup cg;       // optional để fade

    [Header("Motion Curves")]
    [SerializeField] private AnimationCurve inCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve outCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Behavior")]
    [SerializeField] private bool destroyOnComplete = true;

    void Reset()
    {
        rect = GetComponent<RectTransform>();
        if (!image) image = GetComponentInChildren<Image>(true);
        if (!cg) cg = GetComponent<CanvasGroup>();
    }

    public void SetSprite(Sprite s)
    {
        if (image) image.sprite = s;
    }

    public IEnumerator PlayHorizontalSwipe(
        bool fromLeftSide,
        float enterTime = 0.35f,
        float holdTime = 2.00f,
        float exitTime = 0.35f,
        float offscreenMargin = 120f)
    {
        if (!rect) rect = GetComponent<RectTransform>();

        // Lấy canvas RT để tính toạ độ biên
        var canvas = GetComponentInParent<Canvas>();
        var canvasRT = canvas ? canvas.GetComponent<RectTransform>() : null;
        float canvasW = canvasRT ? canvasRT.rect.width : Screen.width;

        float half = (canvasW * 0.5f) + (rect.rect.width * 0.5f) + offscreenMargin;

        // target (điểm đứng giữa) = vị trí hiện tại trong prefab
        Vector2 target = rect.anchoredPosition;

        // start & exit
        Vector2 start = target;
        Vector2 exit = target;
        if (fromLeftSide)
        {
            start.x = -half;   // vào từ trái
            exit.x = half;   // ra bên phải
        }
        else
        {
            start.x = half;   // vào từ phải
            exit.x = -half;   // ra bên trái
        }

        // bắt đầu ở ngoài màn
        rect.anchoredPosition = start;
        if (cg) cg.alpha = 1f;

        // IN
        float t = 0f; enterTime = Mathf.Max(0.01f, enterTime);
        while (t < enterTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / enterTime);
            rect.anchoredPosition = Vector2.LerpUnclamped(start, target, inCurve.Evaluate(k));
            yield return null;
        }
        rect.anchoredPosition = target;

        // HOLD (đứng yên đúng số giây)
        if (holdTime > 0f) yield return new WaitForSecondsRealtime(holdTime);

        // OUT
        t = 0f; exitTime = Mathf.Max(0.01f, exitTime);
        while (t < exitTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / exitTime);
            rect.anchoredPosition = Vector2.LerpUnclamped(target, exit, outCurve.Evaluate(k));
            yield return null;
        }
        rect.anchoredPosition = exit;

        if (destroyOnComplete) Destroy(gameObject);
        else gameObject.SetActive(false);
    }
}
