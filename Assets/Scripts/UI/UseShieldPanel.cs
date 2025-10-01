using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UseShieldPanel : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Slider slider;                 // Kéo Slider vào đây
    [SerializeField] private bool autoCloseOnFinish = false;// Hết về 0 thì auto đóng panel

    [Header("Slide Settings")]
    [Min(0.05f)] public float slideDuration = 1.0f;         // Thời gian trượt về 0
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Coroutine slideCo;
    private bool _pauseTaken;

    // ===== Lifecycle =====
    private void OnEnable()
    {
        if (!slider) slider = GetComponentInChildren<Slider>(true);

        // Reset slider về 0 khi mở panel
        if (slider) slider.value = 0f;

        // Dừng thời gian
        if (!_pauseTaken)
        {
            PauseManager.PushPause();
            _pauseTaken = true;
        }
    }

    private void OnDisable()
    {
        // Dọn coroutine
        if (slideCo != null)
        {
            StopCoroutine(slideCo);
            slideCo = null;
        }

        // Resume thời gian nếu panel đã lấy pause
        if (_pauseTaken)
        {
            PauseManager.PopPause();
            _pauseTaken = false;
        }
    }

    // ===== Public API =====

    /// <summary>Bật panel (pause game).</summary>
    public void OpenPanel()
    {
        this.gameObject.SetActive(true);
    }

    /// <summary>Tắt panel (resume game).</summary>
    public void ClosePanel()
    {
        Debug.Log("Close Panel");
        this.gameObject.SetActive(false);
    }

    /// <summary>
    /// Đặt slider tới giá trị s rồi tự động trượt ngược về 0.
    /// </summary>
    public void SetAndSlideBack(float s)
    {
        if (!slider) return;

        if (slideCo != null) StopCoroutine(slideCo);

        slider.value = Mathf.Clamp(s, slider.minValue, slider.maxValue);
        slideCo = StartCoroutine(SlideBackRoutine());
    }

    /// <summary>
    /// Đặt ngay giá trị slider (không trượt).
    /// </summary>
    public void SetImmediate(float s)
    {
        if (!slider) return;
        if (slideCo != null) { StopCoroutine(slideCo); slideCo = null; }
        slider.value = Mathf.Clamp(s, slider.minValue, slider.maxValue);
    }

    // ===== Internal =====
    private IEnumerator SlideBackRoutine()
    {
        float startValue = slider.value;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.unscaledDeltaTime; // dùng unscaled để chạy khi pause
            float t = Mathf.Clamp01(elapsed / slideDuration);
            float eased = easeCurve != null ? easeCurve.Evaluate(t) : t;
            slider.value = Mathf.Lerp(startValue, 0f, eased);
            yield return null;
        }

        slider.value = 0f;
        slideCo = null;

        if (autoCloseOnFinish) ClosePanel();
    }
}
