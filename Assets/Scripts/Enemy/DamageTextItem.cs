using UnityEngine;
using TMPro;
using System;

public class DamageTextItem : MonoBehaviour
{
    [Header("Refs")]
    public TextMeshProUGUI label;

    [Header("Defaults (editable in Inspector)")]
    public bool useInspectorDefaults = true;
    public DamageTextConfig defaultConfig = DamageTextConfig.Default();

    // ===== runtime =====
    private Transform _follow;
    private Vector3 _spawnWorldPos;
    private float _elapsed;
    private Func<DamageTextItem, bool> _onFinished;
    private Camera _cam;

    private DamageTextConfig _cfg;
    private Vector3 _randomOffset;
    private Vector3 _baseLocalScale;

    // scatter motion
    private Vector3 _velocity;     // m/s
    private Vector3 _drift;        // tích lũy nhiễu (m)
    private float _noiseSeedX;
    private float _noiseSeedZ;

    // offset do pool điều khiển để chống chồng chữ
    private float _externalYOffset = 0f;

    public void SetExternalYOffset(float y) => _externalYOffset = y;
    public float GetExternalYOffset() => _externalYOffset;

    void Awake()
    {
        if (!label) label = GetComponentInChildren<TextMeshProUGUI>(true);
        _cam = Camera.main;
        _baseLocalScale = transform.localScale;
        gameObject.SetActive(false);
    }

    public void Play(Transform follow, Vector3 spawnWorldPos, string text, Color color,
                     float lifetime = 0.8f, float startAlpha = 1f, float riseWorldPerSec = 0.6f,
                     bool billboardToCamera = true, Func<DamageTextItem, bool> onFinished = null, Camera cam = null)
    {
        DamageTextConfig cfg = useInspectorDefaults ? defaultConfig : DamageTextConfig.Default();
        if (!useInspectorDefaults)
        {
            cfg.lifetime = lifetime;
            cfg.startAlpha = startAlpha;
            cfg.riseWorldPerSec = riseWorldPerSec;
            cfg.billboardToCamera = billboardToCamera;
        }
        InternalPlay(follow, spawnWorldPos, text, color, cfg, onFinished, cam);
    }

    public void PlayWithConfig(Transform follow, Vector3 spawnWorldPos, float damage, Color color,
                           DamageTextConfig overrideConfig,
                           Func<DamageTextItem, bool> onFinished = null, Camera cam = null)
    {
        InternalPlay(follow, spawnWorldPos, damage.ToString("0"), color, overrideConfig, onFinished, cam, damage);
    }

    private void InternalPlay(Transform follow, Vector3 spawnWorldPos, string text, Color color,
                          DamageTextConfig cfg, Func<DamageTextItem, bool> onFinished, Camera cam,
                          float damageValue = 0f)
    {
        _follow = follow;
        _spawnWorldPos = spawnWorldPos;
        _cfg = cfg;
        _elapsed = 0f;
        _onFinished = onFinished;
        _cam = cam ? cam : (_cam ? _cam : Camera.main);
        _externalYOffset = 0f; // reset mỗi lần play
        _drift = Vector3.zero;

        // random offset khi spawn (tránh đè)
        _randomOffset = new Vector3(
            (_cfg.randomXZ.x == 0f ? 0f : UnityEngine.Random.Range(-_cfg.randomXZ.x, _cfg.randomXZ.x)),
            (_cfg.randomY == 0f ? 0f : UnityEngine.Random.Range(-_cfg.randomY, _cfg.randomY)),
            (_cfg.randomXZ.y == 0f ? 0f : UnityEngine.Random.Range(-_cfg.randomXZ.y, _cfg.randomXZ.y))
        );

        // noise seeds (để mỗi text rung khác nhau)
        _noiseSeedX = UnityEngine.Random.value * 1000f;
        _noiseSeedZ = UnityEngine.Random.value * 1000f;

        // set text/visual
        if (label)
        {
            label.text = text;

            // Scale theo damage: ví dụ sqrt để không quá to
            float dmgScale = 1f;
            if (damageValue > 0f)
                dmgScale = Mathf.Clamp(Mathf.Sqrt(damageValue) * 0.1f, 1f, 3f);
            // 10 dame → 1.0x, 100 dame → ~3x

            transform.localScale = _baseLocalScale * _cfg.startScale * dmgScale;
            color.a = _cfg.startAlpha;
            label.color = color;

            if (_cfg.overrideFont)
            {
                if (_cfg.font) label.font = _cfg.font;
                if (_cfg.fontSize > 0) label.fontSize = _cfg.fontSize;
                label.fontStyle = _cfg.fontStyle;
            }
            label.raycastTarget = false;
        }

        // vị trí & scale bắt đầu
        Vector3 startPos = (_follow ? _follow.position : _spawnWorldPos)
                           + _randomOffset
                           + Vector3.up * (_cfg.extraYOffset + _externalYOffset);
        transform.position = startPos;
        transform.localScale = _baseLocalScale * Mathf.Max(0.0001f, _cfg.startScale);

        // khởi tạo vận tốc ngẫu nhiên (nếu dùng)
        if (_cfg.useVelocityMotion)
        {
            _velocity = RandomConeDirection(Vector3.up, _cfg.spreadAngleDeg)
                        * UnityEngine.Random.Range(Mathf.Min(_cfg.initialSpeedRange.x, _cfg.initialSpeedRange.y),
                                                    Mathf.Max(_cfg.initialSpeedRange.x, _cfg.initialSpeedRange.y));
        }
        else
        {
            _velocity = Vector3.zero;
        }

        // billboard ban đầu
        if (_cfg.billboardToCamera && _cam)
            transform.rotation = Quaternion.LookRotation(transform.position - _cam.transform.position);

        gameObject.SetActive(true);
    }

    void Update()
    {
        float dt = _cfg.useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        _elapsed += dt;
        float t = Mathf.Clamp01(_elapsed / Mathf.Max(0.0001f, _cfg.lifetime));

        // --- Upward rise distance (đường cơ bản) ---
        float totalRise = (_cfg.totalRiseOverrideMeters > 0f)
            ? _cfg.totalRiseOverrideMeters
            : (_cfg.riseWorldPerSec * _cfg.lifetime);

        float rise = _cfg.useRiseCurve
            ? totalRise * _cfg.riseCurve.Evaluate(t)
            : totalRise * t;

        // --- Velocity-based scatter (lateral/up) ---
        if (_cfg.useVelocityMotion)
        {
            // gia tốc
            _velocity += _cfg.acceleration * dt;

            // drag tuyến tính
            float drag = Mathf.Clamp01(_cfg.dragPerSec * dt);
            _velocity *= (1f - drag);

            // tích lũy nhiễu (nhẹ) cho X/Z (không allocate)
            if (_cfg.noiseAmplitude > 0f && _cfg.noiseFrequency > 0f)
            {
                float nx = (Mathf.PerlinNoise(_noiseSeedX, _elapsed * _cfg.noiseFrequency) - 0.5f) * 2f;
                float nz = (Mathf.PerlinNoise(_noiseSeedZ, _elapsed * _cfg.noiseFrequency) - 0.5f) * 2f;
                _drift.x = nx * _cfg.noiseAmplitude;
                _drift.z = nz * _cfg.noiseAmplitude;
            }
            else
            {
                _drift = Vector3.zero;
            }
        }

        // --- Tính vị trí ---
        // Pos cơ sở: anchor + random offset + rise + dịch chuyển do velocity
        Vector3 anchor = (_follow ? _follow.position : _spawnWorldPos);
        Vector3 velDisp = _cfg.useVelocityMotion ? (_velocity * _elapsed) : Vector3.zero;

        Vector3 pos = anchor
                    + _randomOffset
                    + new Vector3(velDisp.x, 0f, velDisp.z)   // lateral do velocity
                    + _drift                                   // nhiễu lateral
                    + Vector3.up * (_cfg.extraYOffset + _externalYOffset + rise
                                    + (_cfg.useVelocityMotion ? Mathf.Max(0f, velDisp.y) : 0f)); // y không cho đi xuống quá (nhìn đẹp hơn)

        transform.position = pos;

        // --- Billboard ---
        if (_cfg.billboardToCamera && _cam)
            transform.rotation = Quaternion.LookRotation(transform.position - _cam.transform.position);

        // --- Fade ---
        if (label)
        {
            var c = label.color;
            c.a = 1f; // luôn full alpha
            label.color = c;
        }


        // --- Scale (to dần) ---
        float scl = Mathf.Lerp(_cfg.startScale, _cfg.endScale, t);
        transform.localScale = _baseLocalScale * Mathf.Max(0.0001f, scl);

        // --- Kết thúc ---
        if (_elapsed >= _cfg.lifetime)
        {
            gameObject.SetActive(false);
            _onFinished?.Invoke(this);
        }
    }

    // ===== Helpers =====

    /// <summary>
    /// Tạo vector ngẫu nhiên trong hình nón quanh trục "axis" với góc mở "angleDeg".
    /// </summary>
    private static Vector3 RandomConeDirection(Vector3 axis, float angleDeg)
    {
        axis = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.up;
        float angRad = Mathf.Deg2Rad * Mathf.Clamp(angleDeg, 0f, 90f);

        // chọn góc lệch từ 0..angRad (ưu tiên lệch nhỏ hơn để tập trung nhiều ở giữa)
        float u = UnityEngine.Random.value;           // 0..1
        float theta = Mathf.Acos(Mathf.Lerp(1f, Mathf.Cos(angRad), u)); // phân bố đều trên nón
        float phi = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

        // hướng trong local (nón quanh +Z)
        Vector3 dirLocal = new Vector3(
            Mathf.Sin(theta) * Mathf.Cos(phi),
            Mathf.Sin(theta) * Mathf.Sin(phi),
            Mathf.Cos(theta)
        );

        // xoay local +Z thành "axis"
        Quaternion toAxis = Quaternion.FromToRotation(Vector3.forward, axis);
        return toAxis * dirLocal;
    }
}
