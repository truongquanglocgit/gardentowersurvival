using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RotateImage : MonoBehaviour
{
    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 180f; // độ/giây (luôn dương)
    [SerializeField] private bool clockwise = true;

    [Header("Timing")]
    [Tooltip("Bỏ qua Time.timeScale để vẫn xoay khi pause.")]
    [SerializeField] private bool useUnscaledTime = true;

    [Tooltip("Giới hạn dt để tránh nhảy góc lớn khi app resume/minimize.")]
    [SerializeField] private float maxDelta = 0.05f;

    [Header("Lifecycle")]
    [Tooltip("Reset về góc 0 mỗi lần Enable.")]
    [SerializeField] private bool resetOnEnable = false;

    private RectTransform rectTransform;
    private float zAngle; // góc tích lũy Z

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (!rectTransform)
            Debug.LogWarning("[RotateImage] Missing RectTransform.");
    }

    private void OnEnable()
    {
        if (resetOnEnable)
        {
            zAngle = 0f;
            if (rectTransform) rectTransform.localRotation = Quaternion.identity;
        }
    }

    private void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (maxDelta > 0f) dt = Mathf.Min(dt, maxDelta);

        float dir = clockwise ? -1f : 1f; // UI hệ trục ngược, clockwise = -1
        zAngle += rotationSpeed * dir * dt;

        // Giữ góc gọn để tránh tràn số
        if (zAngle > 360f || zAngle < -360f) zAngle %= 360f;

        if (rectTransform)
            rectTransform.localRotation = Quaternion.Euler(0f, 0f, zAngle);
    }
}
