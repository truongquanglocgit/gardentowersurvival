using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Tắt/bật Renderer của cây theo khoảng cách tới Camera.
/// Không đụng colliders/logic khác. Rất rẻ, phù hợp số lượng lớn.
/// </summary>
[DisallowMultipleComponent]
public class TreeAutoDisableByDistance : MonoBehaviour
{
    [Header("Camera")]
    public Camera mainCamera; // để trống -> Camera.main

    [Header("Distances (m)")]
    [Tooltip("Trong khoảng cách này sẽ DISABLE renderer của cây.")]
    public float disableDistance = 5f;
    [Tooltip("Hysteresis để tránh chớp tắt liên tục (bật lại khi > disableDistance + this).")]
    public float reenableHysteresis = 0.75f;

    [Header("Targets")]
    [Tooltip("Để trống => tự quét tất cả Renderer con.")]
    public Renderer[] renderersToToggle;

    // Runtime
    bool _isDisabled;
    readonly List<Renderer> _tmp = new();

    void Awake()
    {
        if (!mainCamera) mainCamera = Camera.main;
        if (renderersToToggle == null || renderersToToggle.Length == 0)
            renderersToToggle = GetComponentsInChildren<Renderer>(true);
    }

    void LateUpdate()
    {
        if (!mainCamera || renderersToToggle == null || renderersToToggle.Length == 0)
            return;

        // Tính khoảng cách gần nhất từ camera tới bounds của các renderer con
        float minDist = float.PositiveInfinity;
        Vector3 camPos = mainCamera.transform.position;

        for (int i = 0; i < renderersToToggle.Length; i++)
        {
            var r = renderersToToggle[i];
            if (!r) continue;
            var b = r.bounds;
            float d = Vector3.Distance(b.ClosestPoint(camPos), camPos);
            if (d < minDist) minDist = d;
        }

        // Quyết định bật/tắt theo ngưỡng + hysteresis
        if (!_isDisabled && minDist <= disableDistance)
            SetEnabled(false);
        else if (_isDisabled && minDist >= disableDistance + reenableHysteresis)
            SetEnabled(true);
    }

    void SetEnabled(bool enabled)
    {
        _isDisabled = !enabled;
        for (int i = 0; i < renderersToToggle.Length; i++)
        {
            var r = renderersToToggle[i];
            if (r) r.enabled = enabled;
        }
    }
}
