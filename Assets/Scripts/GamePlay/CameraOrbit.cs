using UnityEngine;
using UnityEngine.EventSystems;

public class CameraOrbit : MonoBehaviour
{
    public Transform target;
    public Vector3 targetOffset = new(0, 1.5f, 0);

    [Header("Distance")]
    public float distance = 15f, minDistance = 6f, maxDistance = 30f;
    public float zoomSpeedScroll = 5f, zoomSpeedTouch = 0.02f;

    [Header("Rotation")]
    public float yaw = 0f, pitch = 35f;
    public float yawSpeed = 0.2f, pitchSpeed = 0.15f;
    public float mouseYawSpeed = 3f, mousePitchSpeed = 2f;
    public float minPitch = 10f, maxPitch = 75f;

    // --- NEW: Zoom toggle options ---
    [Header("Zoom Toggle")]
    [Tooltip("Bật phím Z để toggle zoom khi chạy trên PC (debug).")]
    public bool enableKeyboardToggle = false;
    public KeyCode toggleKey = KeyCode.Z;

    float targetDistance;
    bool pinchActive = false;
    int rotateFingerId = -1;                 // ⬅️ khóa ngón xoay
    int pinchFingerId0 = -1, pinchFingerId1 = -1;
    float lastPinchDist = 0f;

    // NEW: trạng thái hiện đang ở min hay max
    bool _isAtMinZoom = false;

    void Start()
    {
        targetDistance = distance = Mathf.Clamp(distance, minDistance, maxDistance);
        _isAtMinZoom = Mathf.Abs(distance - minDistance) <= Mathf.Epsilon;
    }

    void Update()
    {
        if (!target) return;

        HandleMouse();
        HandleTouch();

        // NEW: phím tắt toggle (tùy chọn)
        if (enableKeyboardToggle && Input.GetKeyDown(toggleKey))
            OnClickToggleZoom();
    }

    void LateUpdate()
    {
        if (!target) return;

        // Mượt: nội suy khoảng cách về targetDistance
        distance = Mathf.Lerp(distance, targetDistance, Time.deltaTime * 10f);

        var rot = Quaternion.Euler(pitch, yaw, 0f);
        var targetPos = target.position + targetOffset;
        var dir = rot * Vector3.forward;
        transform.position = targetPos - dir * distance;
        transform.LookAt(targetPos);
    }

    void HandleMouse()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        // Xoay bằng chuột phải
        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * mouseYawSpeed;
            pitch -= Input.GetAxis("Mouse Y") * mousePitchSpeed;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        // --- [CƠ CHẾ CŨ - ĐÃ TẮT THEO YÊU CẦU] Lăn chuột để zoom ---
        /*
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
            targetDistance = Mathf.Clamp(targetDistance - scroll * zoomSpeedScroll * 10f, minDistance, maxDistance);
        */
#endif
    }

    bool IsOverUI(int fingerId) =>
        EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(fingerId);

    void HandleTouch()
    {
        int tc = Input.touchCount;
        if (tc == 0)
        {
            // reset baseline khi không còn touch
            rotateFingerId = -1;
            pinchActive = false;
            pinchFingerId0 = pinchFingerId1 = -1;
            return;
        }

        // --- [CƠ CHẾ CŨ - ĐÃ TẮT THEO YÊU CẦU] Pinch 2 ngón để zoom ---
        /*
        if (tc >= 2)
        {
            // chọn 2 ngón KHÔNG đè UI
            int a = -1, b = -1;
            for (int i = 0; i < tc; i++)
            {
                var t = Input.GetTouch(i);
                if (IsOverUI(t.fingerId)) continue;
                if (a == -1) a = i;
                else if (b == -1) { b = i; break; }
            }
            if (a != -1 && b != -1)
            {
                var t0 = Input.GetTouch(a);
                var t1 = Input.GetTouch(b);

                if (!pinchActive || t0.fingerId != pinchFingerId0 || t1.fingerId != pinchFingerId1)
                {
                    // khởi tạo pinch mới
                    pinchActive = true;
                    pinchFingerId0 = t0.fingerId;
                    pinchFingerId1 = t1.fingerId;
                    lastPinchDist = (t0.position - t1.position).magnitude;
                }
                else
                {
                    float curr = (t0.position - t1.position).magnitude;
                    float diff = curr - lastPinchDist;
                    if (Mathf.Abs(diff) > 0.01f) // deadzone nhỏ
                        targetDistance = Mathf.Clamp(targetDistance - diff * zoomSpeedTouch, minDistance, maxDistance);
                    lastPinchDist = curr;
                }

                // khi đang pinch thì KHÔNG xoay bằng 1 ngón
                rotateFingerId = -1;
                return;
            }
        }
        */

        // --- Xoay bằng 1 ngón bên phải, không đè UI (giữ nguyên) ---
        Touch? rotTouch = null;

        if (rotateFingerId >= 0)
        {
            // nếu còn tồn tại ngón đã khóa
            for (int i = 0; i < tc; i++)
            {
                var t = Input.GetTouch(i);
                if (t.fingerId == rotateFingerId) { rotTouch = t; break; }
            }
            // nếu ngón cũ biến mất → bỏ khóa
            if (rotTouch == null) rotateFingerId = -1;
        }

        if (rotateFingerId < 0)
        {
            // tìm ngón mới hợp lệ (bên phải & không đè UI)
            for (int i = 0; i < tc; i++)
            {
                var t = Input.GetTouch(i);
                if (IsOverUI(t.fingerId)) continue;
                if (t.position.x < Screen.width * 0.5f) continue; // bỏ bên trái
                if (t.phase == TouchPhase.Began || t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
                {
                    rotateFingerId = t.fingerId;
                    rotTouch = t;
                    break;
                }
            }
        }

        if (rotTouch.HasValue)
        {
            var t = rotTouch.Value;
            if (t.phase == TouchPhase.Moved)
            {
                Vector2 d = t.deltaPosition;
                // deadzone để loại spike nhỏ
                if (d.sqrMagnitude > 0.5f)
                {
                    yaw += d.x * yawSpeed;
                    pitch -= d.y * pitchSpeed;
                    pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
                }
            }
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                rotateFingerId = -1;
            }
        }

        // (cũ) rời pinch khi còn <2 ngón hợp lệ
        // if (tc < 2) { pinchActive = false; pinchFingerId0 = pinchFingerId1 = -1; }
    }

    // --- NEW: Toggle zoom bằng nút ---
    public void OnClickToggleZoom()
    {
        // Chuyển đích giữa min <-> max và để LateUpdate nội suy mượt
        _isAtMinZoom = !_isAtMinZoom;
        targetDistance = _isAtMinZoom ? minDistance : maxDistance;
        targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
    }
}
