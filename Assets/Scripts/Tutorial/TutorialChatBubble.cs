using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TutorialChatBubble : MonoBehaviour
{
    [Header("Refs")]
    public RectTransform root;     // chính nó
    public Image avatar;
    public TMP_Text text;

    [Header("Style")]
    public Vector2 defaultOffset = new Vector2(0, 120);

    private RectTransform _follow;
    private Canvas _canvas;
    private Camera _uiCam;

    // ==== TYPEWRITER ====
    Coroutine _typingCR;
    public bool IsTyping { get; private set; }

    /// <summary>
    /// Khởi tạo như cũ (không tự chạy typewriter để backward-compatible).
    /// Gọi StartTypewriter(...) ngay sau Setup nếu muốn gõ chữ dần.
    /// </summary>
    public void Setup(Canvas canvas, RectTransform follow, string content, Sprite avatarSprite, Vector2 offset)
    {
        _canvas = canvas;
        _uiCam = canvas ? canvas.worldCamera : null;  // với ScreenSpace-Overlay sẽ là null
        _follow = follow;
        avatar.sprite = avatarSprite;
        defaultOffset = offset;

        // set text ngay (để layout sẵn); typewriter sẽ điều khiển hiển thị bằng maxVisibleCharacters
        if (text)
        {
            text.SetText(content ?? "");
            text.ForceMeshUpdate();
            text.maxVisibleCharacters = int.MaxValue; // mặc định hiện hết
        }

        gameObject.SetActive(true);
    }

    /// <summary>
    /// Bắt đầu gõ từng ký tự. Sử dụng maxVisibleCharacters của TMP nên an toàn với rich text/emoji.
    /// </summary>
    public void StartTypewriter(float charsPerSecond = 28f, bool useUnscaledTime = true, bool allowSkip = true)
    {
        if (!text) return;

        // Nếu đang gõ dở → dừng
        if (_typingCR != null) StopCoroutine(_typingCR);
        _typingCR = StartCoroutine(CoTypewriter(charsPerSecond, useUnscaledTime, allowSkip));
    }

    /// <summary>Hiện toàn bộ nội dung ngay lập tức.</summary>
    public void ShowFullNow()
    {
        if (!text) return;
        if (_typingCR != null) { StopCoroutine(_typingCR); _typingCR = null; }
        IsTyping = false;
        text.maxVisibleCharacters = int.MaxValue;
    }

    IEnumerator CoTypewriter(float cps, bool useUnscaled, bool allowSkip)
    {
        IsTyping = true;

        // Chuẩn bị đếm ký tự
        text.ForceMeshUpdate();
        int total = text.textInfo.characterCount; // TMP sẽ tính đúng với rich text
        if (total <= 0)
        {
            text.maxVisibleCharacters = int.MaxValue;
            IsTyping = false;
            yield break;
        }

        text.maxVisibleCharacters = 0;
        float acc = 0f;
        if (cps <= 0f) cps = 9999f;

        while (text.maxVisibleCharacters < total)
        {
            float dt = useUnscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            acc += cps * dt;

            int add = Mathf.FloorToInt(acc);
            if (add > 0)
            {
                text.maxVisibleCharacters = Mathf.Min(total, text.maxVisibleCharacters + add);
                acc -= add;
            }

            // Skip → hiện hết
            if (allowSkip && (Input.GetMouseButtonDown(0) || Input.touchCount > 0))
            {
                text.maxVisibleCharacters = int.MaxValue;
                break;
            }

            yield return null;
        }

        IsTyping = false;
        _typingCR = null;
    }

    private void LateUpdate()
    {
        if (_follow == null || _canvas == null) return;

        Vector3 worldPos = _follow.TransformPoint(_follow.rect.center);
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(_uiCam, worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.transform as RectTransform,
            screenPos,
            _uiCam,
            out Vector2 local);

        root.anchoredPosition = local + defaultOffset;
    }

    private void OnDisable()
    {
        // đảm bảo không “kẹt” đang gõ
        if (_typingCR != null) { StopCoroutine(_typingCR); _typingCR = null; }
        IsTyping = false;
        if (text) text.maxVisibleCharacters = int.MaxValue;
    }
}
