using UnityEngine;
using UnityEngine.EventSystems;
using System.Linq;
using System.Collections.Generic;

public class CameraOrbitFiltered : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Vector3 targetOffset = new Vector3(0, 1.5f, 0);

    [Header("Distance")]
    public float distance = 15f;
    public float minDistance = 6f;
    public float maxDistance = 30f;
    public float zoomSpeedScroll = 5f;   // mouse
    public float zoomSpeedTouch = 0.02f; // pinch

    [Header("Rotation")]
    public float yaw = 0f;
    public float pitch = 35f;
    public float yawSpeed = 0.2f;   // touch
    public float pitchSpeed = 0.15f;  // touch
    public float mouseYawSpeed = 3f;
    public float mousePitchSpeed = 2f;
    public float minPitch = 10f;
    public float maxPitch = 75f;

    [Header("Zones / UI")]
    [Tooltip("RectTransform của joystick BG/area. Dùng để loại touch joystick.")]
    public JoystickPointerTracker joystickTracker;
    public RectTransform joystickRect;          // fallback nếu không gắn tracker
    public Camera uiCamera;                     // để RectTransformUtility (để trống nếu Canvas = Screen Space Overlay)
    [Tooltip("Chỉ cho xoay trong nửa phải màn hình (để nửa trái cho joystick).")]
    public bool useRightHalfForOrbit = true;
    public RectTransform cameraZoneRect;        // nếu gán, chỉ nhận xoay/zoom trong rect này (ưu tiên hơn nửa phải)

    float targetDistance;
    enum TouchState { None, Orbiting, Zooming }
    TouchState state = TouchState.None;

    int orbitFingerId = -1;
    int pinchA = -1, pinchB = -1;
    Vector2 prevA, prevB;

    void Start()
    {
        targetDistance = distance;
        Input.simulateMouseWithTouches = false; // tránh chuột giả làm rối
    }

    void Update()
    {
        if (!target) return;
        HandleMouse();
        HandleTouch();
    }

    void LateUpdate()
    {
        if (!target) return;

        distance = Mathf.Lerp(distance, targetDistance, Time.deltaTime * 10f);

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 lookAt = target.position + targetOffset;
        Vector3 dir = rot * Vector3.forward;

        transform.position = lookAt - dir * distance;
        transform.LookAt(lookAt);
    }

    // -------- PC mouse (Editor/Standalone) ----------
    void HandleMouse()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * mouseYawSpeed;
            pitch -= Input.GetAxis("Mouse Y") * mousePitchSpeed;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            targetDistance = Mathf.Clamp(targetDistance - scroll * zoomSpeedScroll * 10f, minDistance, maxDistance);
        }
#endif
    }

    // ------------- Mobile Touch ---------------------
    void HandleTouch()
    {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        Touch[] touches = Input.touches;
        if (touches.Length == 0)
        {
            ResetAll();
            return;
        }

        // Phân loại touches
        List<Touch> joystickTouches = new();
        List<Touch> freeTouches = new();

        foreach (var t in touches)
        {
            // 1) bỏ UI
            if (IsOverUI(t.fingerId)) continue;

            // 2) bỏ ngón joystick (ưu tiên tracker)
            if (joystickTracker && t.fingerId == joystickTracker.ActiveFingerId)
                { joystickTouches.Add(t); continue; }

            // fallback: theo hình chữ nhật joystick
            if (joystickRect && RectTransformUtility.RectangleContainsScreenPoint(joystickRect, t.position, uiCamera))
                { joystickTouches.Add(t); continue; }

            // 3) nếu có Camera Zone → chỉ nhận trong zone
            if (cameraZoneRect && !RectTransformUtility.RectangleContainsScreenPoint(cameraZoneRect, t.position, uiCamera))
                continue;

            // 4) nếu không có Camera Zone mà bật nửa phải
            if (!cameraZoneRect && useRightHalfForOrbit && t.position.x < Screen.width * 0.5f)
                continue;

            freeTouches.Add(t);
        }

        // Ưu tiên logic:
        // - Nếu có >= 2 free touches → ZOOM
        // - Nếu có 1 free touch → ORBIT
        // - Ngón trên joystick bị bỏ qua hoàn toàn

        if (freeTouches.Count >= 2)
        {
            EnterZoomIfNeeded(freeTouches);

            // giữ cặp pinch ổn định theo fingerId
            Touch? ta = null, tb = null;
            foreach (var t in freeTouches)
            {
                if (t.fingerId == pinchA) ta = t;
                if (t.fingerId == pinchB) tb = t;
            }
            // nếu 1 trong 2 mất → chọn lại, reset mốc
            if (!ta.HasValue || !tb.HasValue)
            {
                pinchA = freeTouches[0].fingerId;
                pinchB = freeTouches[1].fingerId;
                prevA  = freeTouches[0].position;
                prevB  = freeTouches[1].position;
                return;
            }

            float prevDist = (prevA - prevB).magnitude;
            float currDist = (ta.Value.position - tb.Value.position).magnitude;
            float diff = currDist - prevDist;

            targetDistance = Mathf.Clamp(targetDistance - diff * zoomSpeedTouch, minDistance, maxDistance);

            prevA = ta.Value.position;
            prevB = tb.Value.position;

            // trong khi zoom, KHÔNG xoay
            orbitFingerId = -1;
            return;
        }

        if (freeTouches.Count == 1)
        {
            var t = freeTouches[0];

            // Nếu đang ở Zooming mà chỉ còn 1 ngón → thoát zoom, KHÔNG tái dùng finger cũ để tránh giật
            if (state == TouchState.Zooming)
            {
                state = TouchState.None;
                pinchA = pinchB = -1;
            }

            // Vào Orbit nếu chưa
            if (state == TouchState.None && t.phase == TouchPhase.Began)
            {
                state = TouchState.Orbiting;
                orbitFingerId = t.fingerId;
                return;
            }

            // Chỉ xoay đúng finger đã đăng ký
            if (state == TouchState.Orbiting && t.fingerId == orbitFingerId)
            {
                if (t.phase == TouchPhase.Moved)
                {
                    yaw   += t.deltaPosition.x * yawSpeed;
                    pitch -= t.deltaPosition.y * pitchSpeed;
                    pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
                }
                else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    state = TouchState.None;
                    orbitFingerId = -1;
                }
            }
            return;
        }

        // 0 free touch → không làm gì
        state = TouchState.None;
        orbitFingerId = -1;
        // pinchA/pinchB giữ nguyên đến khi có >=2 free touch lần sau
#endif
    }

    // --------- Helpers ----------
    bool IsOverUI(int fingerId)
    {
        return EventSystem.current && EventSystem.current.IsPointerOverGameObject(fingerId);
    }

    void EnterZoomIfNeeded(List<Touch> freeTouches)
    {
        if (state != TouchState.Zooming)
        {
            state = TouchState.Zooming;
            pinchA = freeTouches[0].fingerId;
            pinchB = freeTouches[1].fingerId;
            prevA = freeTouches[0].position;
            prevB = freeTouches[1].position;
        }
    }

    void ResetAll()
    {
        state = TouchState.None;
        orbitFingerId = -1;
        pinchA = pinchB = -1;
    }
}
