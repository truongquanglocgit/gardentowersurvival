// Assets/Scripts/Tutorial/ArrowBillboard.cs
using UnityEngine;

public class ArrowBillboard : MonoBehaviour
{
    public Camera faceCamera; // để trống sẽ dùng Camera.main
    public float bobAmp = 0.15f;
    public float bobSpeed = 2.5f;

    void LateUpdate()
    {
        var cam = faceCamera ? faceCamera : Camera.main;
        if (cam) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        // nhấp nhô nhẹ (unscaled để vẫn chạy khi pause)
        var p = transform.localPosition;
        p.y += Mathf.Sin(Time.unscaledTime * bobSpeed) * bobAmp;
        transform.localPosition = p;
    }
}
