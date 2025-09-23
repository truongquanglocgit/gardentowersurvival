using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    public GameController gameController;

    [Header("Stats")]
    public float MaxHp = 150f;
    public float Hp;
    public float Speed;

    [Header("UI - Damage Overlay")]
    [Tooltip("CanvasGroup của ảnh viền đỏ (đặt trên Canvas, full screen)")]
    public CanvasGroup damageOverlay;
    [Range(0f, 1f)] public float hitFlashAlpha = 0.85f;
    [Tooltip("Thời gian mờ dần sau khi bị hit")]
    [Min(0.05f)] public float hitFadeDuration = 0.5f;

    [Header("Low HP Blink")]
    [Range(0.01f, 0.5f)] public float lowHpPercent = 0.10f;     // 10%
    [Range(0f, 1f)] public float lowHpBlinkAlpha = 0.6f;
    [Min(0.5f)] public float lowHpBlinkFrequency = 2f;          // chu kỳ nháy (s)

    [Header("SFX")]
    [Tooltip("AudioSource để phát SFX (nên đặt trên Player)")]
    public AudioSource audioSource;
    [Tooltip("Âm đánh khi bị trúng đòn")]
    public AudioClip hitSfx;
    [Tooltip("Âm cảnh báo low HP (tuỳ chọn) – sẽ loop khi HP < ngưỡng")]
    public AudioClip lowHpLoopSfx;

    // --- Runtime ---
    Coroutine _flashCo, _blinkCo;
    float _flashAlpha;   // alpha do flash mỗi lần bị hit
    float _blinkAlpha;   // alpha do blink khi low HP
    bool _lowHpLoopRunning;

    void Awake()
    {
        Hp = MaxHp; // reset HP đúng khi load scene
        ApplyOverlayAlpha(0f);
    }

    public void ResetHp()
    {
        Hp = MaxHp;
        StopAllOverlay();
        ApplyOverlayAlpha(0f);
        StopLowHpLoopSfx();
    }
    public void Healing()
    {
        
    }
    public void TakeDamage(float damage)
    {
        if (Hp <= 0) return;

        Hp = Mathf.Max(0f, Hp - damage);

        // SFX hit 1 lần
        if (audioSource && hitSfx) audioSource.PlayOneShot(hitSfx);

        // Flash viền đỏ rồi mờ dần
        StartFlash();

        // Bật/tắt trạng thái blink + loop SFX theo HP
        UpdateLowHpState();

        if (Hp <= 0f)
        {
            StopAllOverlay();
            StopLowHpLoopSfx();
            gameController.die();
        }
    }

    // ===================== Overlay Logic =====================

    void StartFlash()
    {
        _flashAlpha = hitFlashAlpha;
        if (_flashCo != null) StopCoroutine(_flashCo);
        _flashCo = StartCoroutine(CoFlashFadeOut());
    }

    IEnumerator CoFlashFadeOut()
    {
        float t = 0f;
        while (t < hitFadeDuration)
        {
            t += Time.unscaledDeltaTime;
            // giảm từ hitFlashAlpha về 0
            _flashAlpha = Mathf.Lerp(hitFlashAlpha, 0f, t / hitFadeDuration);
            ApplyOverlayAlpha(Mathf.Max(_flashAlpha, _blinkAlpha));
            yield return null;
        }
        _flashAlpha = 0f;
        ApplyOverlayAlpha(Mathf.Max(_flashAlpha, _blinkAlpha));
        _flashCo = null;
    }

    void StartBlink()
    {
        if (_blinkCo != null) return;
        _blinkCo = StartCoroutine(CoBlink());
    }

    IEnumerator CoBlink()
    {
        // Nháy alpha dạng sin: 0 -> lowHpBlinkAlpha -> 0
        while (true)
        {
            float t = 0f;
            while (t < lowHpBlinkFrequency)
            {
                t += Time.unscaledDeltaTime;
                float s = Mathf.Sin((t / lowHpBlinkFrequency) * Mathf.PI); // 0..1..0
                _blinkAlpha = s * lowHpBlinkAlpha;
                ApplyOverlayAlpha(Mathf.Max(_flashAlpha, _blinkAlpha));
                yield return null;
            }
        }
    }

    void StopBlink()
    {
        if (_blinkCo != null) StopCoroutine(_blinkCo);
        _blinkCo = null;
        _blinkAlpha = 0f;
        ApplyOverlayAlpha(Mathf.Max(_flashAlpha, _blinkAlpha));
    }

    void StopAllOverlay()
    {
        if (_flashCo != null) StopCoroutine(_flashCo);
        if (_blinkCo != null) StopCoroutine(_blinkCo);
        _flashCo = null; _blinkCo = null;
        _flashAlpha = 0f; _blinkAlpha = 0f;
    }

    void ApplyOverlayAlpha(float a)
    {
        if (damageOverlay) damageOverlay.alpha = Mathf.Clamp01(a);
    }

    // ===================== Low HP State =====================

    void UpdateLowHpState()
    {
        bool isLow = Hp <= MaxHp * lowHpPercent;

        if (isLow)
        {
            StartBlink();
            StartLowHpLoopSfx();
        }
        else
        {
            StopBlink();
            StopLowHpLoopSfx();
        }
    }

    void StartLowHpLoopSfx()
    {
        if (!audioSource || !lowHpLoopSfx) return;
        if (_lowHpLoopRunning) return;

        audioSource.loop = true;
        audioSource.clip = lowHpLoopSfx;
        audioSource.Play();
        _lowHpLoopRunning = true;
    }

    void StopLowHpLoopSfx()
    {
        if (!audioSource) return;
        if (!_lowHpLoopRunning) return;

        // Chỉ dừng nếu đúng đang phát clip cảnh báo
        if (audioSource.clip == lowHpLoopSfx)
        {
            audioSource.Stop();
            audioSource.clip = null;
            audioSource.loop = false;
        }
        _lowHpLoopRunning = false;
    }
}
