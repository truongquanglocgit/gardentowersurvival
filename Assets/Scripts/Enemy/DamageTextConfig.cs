using UnityEngine;
using TMPro;
using System;

[Serializable]
public struct DamageTextConfig
{
    [Header("Timing & Motion")]
    public float lifetime;                 // thời gian tồn tại (s)
    public bool useUnscaledTime;           // dùng Time.unscaledDeltaTime?
    public float riseWorldPerSec;          // tốc độ bay lên (m/s) - path cơ bản thẳng đứng
    public bool billboardToCamera;         // luôn quay mặt về camera
    public float extraYOffset;             // bù độ cao lúc spawn
    public Vector2 randomXZ;               // random lệch X/Z (m) - lúc spawn
    public float randomY;                  // random lệch Y (m) - lúc spawn

    [Header("Fade & Scale")]
    public float startAlpha;               // alpha bắt đầu
    public bool useAlphaCurve;             // dùng curve alpha?
    public AnimationCurve alphaCurve;      // 0..1 -> alpha (0..1)
    public float startScale;               // scale lúc bắt đầu (nhân với localScale gốc)
    public float endScale;                 // scale lúc kết thúc
    public bool useRiseCurve;              // dùng curve độ cao?
    public AnimationCurve riseCurve;       // 0..1 -> tỉ lệ quãng đường bay lên

    [Header("Text Style (optional)")]
    public bool overrideFont;              // có override font/size/style không
    public TMP_FontAsset font;
    public int fontSize;
    public FontStyles fontStyle;

    [Header("Absolute Height (optional)")]
    [Tooltip("Nếu > 0, dùng chiều cao bay tổng cố định (m) và bỏ qua riseWorldPerSec*lifetime.")]
    public float totalRiseOverrideMeters;

    // ==== NEW: Velocity-based scatter motion ====
    [Header("Velocity Motion (scatter)")]
    [Tooltip("Bật để text bay tứ tung (vận tốc ngẫu nhiên trong hình nón hướng lên).")]
    public bool useVelocityMotion;
    [Tooltip("Tốc độ khởi đầu (m/s): min..max")]
    public Vector2 initialSpeedRange;
    [Tooltip("Độ mở hình nón (độ) quanh hướng Vector3.up")]
    [Range(0f, 90f)] public float spreadAngleDeg;
    [Tooltip("Gia tốc (m/s^2), ví dụ (0, -1, 0) để rơi nhẹ hoặc (0, +1.5, 0) để hút lên")]
    public Vector3 acceleration;
    [Tooltip("Hệ số cản (drag) mỗi giây. 0 = không cản")]
    [Range(0f, 10f)] public float dragPerSec;
    [Tooltip("Biên độ nhiễu lateral (m)")]
    public float noiseAmplitude;
    [Tooltip("Tần số nhiễu (Hz)")]
    public float noiseFrequency;

    public static DamageTextConfig Default()
    {
        return new DamageTextConfig
        {
            // base motion
            totalRiseOverrideMeters = 0f,
            lifetime = 0.9f,
            useUnscaledTime = true,
            riseWorldPerSec = 0.7f,
            billboardToCamera = true,
            extraYOffset = 0.12f,
            randomXZ = new Vector2(0.06f, 0.06f),
            randomY = 0.03f,

            // fade & scale (to dần + mờ dần)
            startAlpha = 1f,
            useAlphaCurve = true,
            alphaCurve = new AnimationCurve(
                new Keyframe(0f, 1f, 0f, -2.5f),
                new Keyframe(0.7f, 0.6f),
                new Keyframe(1f, 0f)
            ),
            startScale = 0.85f,
            endScale = 1.3f,
            useRiseCurve = true,
            riseCurve = new AnimationCurve(
                new Keyframe(0f, 0f, 2.2f, 0.8f),
                new Keyframe(1f, 1f)
            ),

            // font
            overrideFont = false,
            font = null,
            fontSize = 28,
            fontStyle = FontStyles.Bold,

            // velocity motion defaults -> tạo scatter nhẹ và tự nhiên
            useVelocityMotion = true,
            initialSpeedRange = new Vector2(0.6f, 1.2f),
            spreadAngleDeg = 35f,
            acceleration = new Vector3(0f, 0.4f, 0f),   // hơi hút lên
            dragPerSec = 1.2f,                          // giảm dần vận tốc
            noiseAmplitude = 0.08f,
            noiseFrequency = 3.5f
        };
    }
}
