using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using DG.Tweening;

public class UseShieldPanel : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Slider slider;                 // Kéo Slider vào đây
    [SerializeField] private bool autoCloseOnFinish = false;// Hết về 0 thì auto đóng panel

    [Header("Slide Settings")]
    [Min(0.05f)] public float timerDuration = 10.0f;         // Thời gian trượt về 0
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Coroutine slideCo;
    private bool _pauseTaken;

    // ===== Lifecycle =====
    private void OnEnable()
    {
        if (!slider) slider = GetComponentInChildren<Slider>(true);

        

        // Dừng thời gian
        if (!_pauseTaken)
        {
            PauseManager.PushPause();
            _pauseTaken = true;
        }
        if (slider)
        {
            slider.value = 1f; // sẽ lerp về 0
            if (slideCo != null) StopCoroutine(slideCo);
            slideCo = StartCoroutine(TimerRoutine());
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
#if ON_ADS


        
        this.gameObject.SetActive(false);
        MAXUtil.Instance.ShowInterAd(FirebaseEventMKTEnum.popup_back_mainmenu.ToString());
#else
        gameObject.SetActive(false);
#endif
        
    }
    
    /// <summary>
    /// Đặt slider tới giá trị s rồi tự động trượt ngược về 0.
    /// </summary>
    public void SetAndSlideBack(float s)
    {
        if (!slider) return;

        if (slideCo != null) StopCoroutine(slideCo);

        slider.value = Mathf.Clamp(s, slider.minValue, slider.maxValue);
        slideCo = StartCoroutine(TimerRoutine());
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
    private IEnumerator TimerRoutine()
    {
        float t = 0f;
        while (t < timerDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / timerDuration);
            float eased = ease != null ? ease.Evaluate(k) : k;
            // slider về 0
            if (slider) slider.value = 1f - eased;
            yield return null;
        }
        if (autoCloseOnFinish) ClosePanel();
        slideCo = null;
    }
}
