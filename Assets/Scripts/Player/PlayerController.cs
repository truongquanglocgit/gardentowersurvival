using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    public GameController gameController;

    [Header("Stats")]
    public float MaxHp = 150f;
    public float Hp;
    public float Speed;
    [Header("Regen (heal over time)")]
    [Tooltip("Bật/tắt hồi máu sau khi không bị đánh")]
    public bool enableRegen = true;
    [Min(0f)] public float regenDelaySeconds = 5f;     // sau 5s không bị đánh
    [Min(0f)] public float regenPerSecond = 8f;        // hồi X máu mỗi giây
    float _lastDamageTime = -999f;                     // thời điểm bị đánh gần nhất
    [Header("UI - Damage Overlay")]
    [Tooltip("CanvasGroup của ảnh viền đỏ (đặt trên Canvas, full screen)")]
    public CanvasGroup damageOverlay;
    [Header("UI - Health Overlay (NEW)")]
    [Tooltip("CanvasGroup overlay hiển thị khi chưa full máu (nhấp nháy)")]
    public CanvasGroup healthOverlay;
    private bool _isRegenerating = false;
    [Range(0f, 1f)] public float healthOverlayAlpha = 0.4f;
    [Min(0.2f)] public float healthOverlayBlinkFrequency = 1.5f; // chu kỳ nháy
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
    [Header("Invulnerability")]
    [Tooltip("Bất tử trong thời gian SetInvulnerableForSeconds()")]
    public bool isInvulnerable = false;
    [Header("Invulnerability VFX")]
    [Tooltip("Kéo vào GameObject/Prefab VFX shield gắn sẵn trên Player")]
    public GameObject shieldVFX;
    [Tooltip("Nếu có ParticleSystem trên shieldVFX, sẽ Play/Stop thay vì chỉ SetActive")]
    public bool controlParticle = true;
    bool _shieldPanelOpen = false; // nhớ trạng thái panel

    void Awake()
    {
        Hp = MaxHp;
        ApplyOverlayAlpha(0f);

        // NEW: đảm bảo tắt shield khi vào scene
        if (shieldVFX)
        {
            var ps = shieldVFX.GetComponent<ParticleSystem>();
            if (controlParticle && ps) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            shieldVFX.SetActive(false);
        }
    }

    

    void Update()
    {
        // === Regen logic ===
        if (enableRegen && Hp > 0f && Hp < MaxHp && Time.timeScale > 0f)
        {
            if (Time.time - _lastDamageTime >= regenDelaySeconds)
            {
                // Bắt đầu regen → bật cờ
                if (!_isRegenerating) _isRegenerating = true;

                float before = Hp;
                Hp = Mathf.Min(MaxHp, Hp + regenPerSecond * Time.deltaTime);
                if (!Mathf.Approximately(before, Hp))
                {
                    UpdateLowHpState();
                }

                // Nếu đầy máu → tắt regen
                if (Hp >= MaxHp)
                {
                    _isRegenerating = false;
                }
            }
            else
            {
                // Chưa tới thời gian delay → chưa regen
                _isRegenerating = false;
            }
        }
        else
        {
            _isRegenerating = false;
        }
        // Tính phần trăm HP
        float hpPct = (MaxHp > 0f) ? (Hp / MaxHp) : 0f;
        bool isLowHp = hpPct <= 0.5f;

        // ✨ Chỉ mở khi vừa chuyển sang low HP
        if (isLowHp && !_shieldPanelOpen)
        {
            gameController.openShieldPanel(); // gọi qua GameController như bạn đang làm
            _shieldPanelOpen = true;
        }
        // ✨ Chỉ đóng khi hồi lại trên ngưỡng
        else if (!isLowHp && _shieldPanelOpen)
        {
            gameController.closeShieldPanel();
            _shieldPanelOpen = false;
        }

        // === NEW: HealthOverlay chỉ nháy khi đang regen ===
        if (healthOverlay != null)
        {
            if (_isRegenerating)
            {
                float t = Time.unscaledTime;
                float blink = (Mathf.Sin((t / healthOverlayBlinkFrequency) * Mathf.PI * 2f) * 0.5f + 0.5f);
                float a = blink * healthOverlayAlpha;
                ApplyHealthOverlayAlpha(a);
            }
            else
            {
                ApplyHealthOverlayAlpha(0f);
            }
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
    // ========= PUBLIC API cho revive/heal =========
    public void ReviveFull()
    {
        Hp = MaxHp;
        StopAllOverlay();
        ApplyOverlayAlpha(0f);
        StopLowHpLoopSfx();
        UpdateLowHpState();
        _lastDamageTime = Time.time;

        // (Không bật shield ở đây; shield bật khi GameController gọi SetInvulnerableForSeconds)
    }
    // Nếu cần heal full mà không reset overlay
    public void HealToFull()
    {
        Hp = MaxHp;
        UpdateLowHpState();
        _lastDamageTime = Time.time;
    }

    // Bất tử tạm (GameController sẽ gọi)
    public void SetInvulnerableForSeconds(float seconds)
    {

        if (seconds <= 0f)
        {
            isInvulnerable = false;
            UpdateShieldVFX(false); // NEW
            return;
        }

        StopCoroutine(nameof(CoInvulnerable));
        StartCoroutine(CoInvulnerable(seconds));
    }
    public void useInvulnerable(float seconds)
    {
#if ON_ADS
        MAXUtil.Instance.ShowRewardAd(FirebaseEventMKTEnum.popup_low_health.ToString(), (placement, adUnit, adInfo, countryCodes) =>
        {
            if (adUnit == null && adInfo == null && countryCodes == null)
            {
                //GameManager.Instance.NoticeNoAds.gameObject.SetActive(true);
            }
            else
            {
                FirebaseUtil.Instance.LogEventEndWaveAds(FirebaseEventMKTEnum.popup_low_health);
                SetInvulnerableForSeconds(seconds);
            }
        });
#else
                PlayerDataManager.Instance.AddAdsCount();
                
#endif
        
    }
    public IEnumerator CoInvulnerable(float sec)
    {
        isInvulnerable = true;
        UpdateShieldVFX(true); // NEW: bật VFX

        float t = 0f;
        while (t < sec)
        {
            t += Time.unscaledDeltaTime; // unscaled để hoạt động khi pause
            yield return null;
        }

        isInvulnerable = false;
        UpdateShieldVFX(false); // NEW: tắt VFX
    }
    void UpdateShieldVFX(bool on)
    {
        if (!shieldVFX) return;

        if (on)
        {
            if (!shieldVFX.activeSelf) shieldVFX.SetActive(true);
            if (controlParticle)
            {
                var ps = shieldVFX.GetComponent<ParticleSystem>();
                if (ps) ps.Play(true);
            }
        }
        else
        {
            if (controlParticle)
            {
                var ps = shieldVFX.GetComponent<ParticleSystem>();
                if (ps) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            shieldVFX.SetActive(false);
        }
    }
    // ========= Existing =========
    public void ResetHp()
    {
        Hp = MaxHp;
        StopAllOverlay();
        ApplyOverlayAlpha(0f);
        StopLowHpLoopSfx();
        _lastDamageTime = Time.time;
    }

    public void Healing()
    {
        // (tuỳ bạn dùng hay không)
    }

    public void TakeDamage(float damage)
    {
        if (Hp <= 0) return;
        if (isInvulnerable) return;

        Hp = Mathf.Max(0f, Hp - damage);
        _lastDamageTime = Time.time;

        // NEW: reset regen khi bị đánh
        _isRegenerating = false;

        if (audioSource && hitSfx) audioSource.PlayOneShot(hitSfx);
        StartFlash();
        UpdateLowHpState();

        if (Hp <= 0f)
        {
            StopAllOverlay();
            StopLowHpLoopSfx();
            gameController.die();
        }
    }
    void OnDeathCleanupShield()
    {
        UpdateShieldVFX(false);
    }

    void ApplyHealthOverlayAlpha(float a)
    {
        if (healthOverlay) healthOverlay.alpha = Mathf.Clamp01(a);
    }
    // ... (các coroutine/overlay/low HP giữ nguyên)
}

