using UnityEngine;
using UnityEngine.EventSystems;

public class CameraOrbit : MonoBehaviour
{
    public Transform target;
    public Vector3 targetOffset = new(0, 1.5f, 0);
    public float distance = 15f, minDistance = 6f, maxDistance = 30f;
    public float zoomSpeedScroll = 5f, zoomSpeedTouch = 0.02f;
    public float yaw = 0f, pitch = 35f;
    public float yawSpeed = 0.2f, pitchSpeed = 0.15f;
    public float mouseYawSpeed = 3f, mousePitchSpeed = 2f;
    public float minPitch = 10f, maxPitch = 75f;

    float targetDistance;
    bool pinchActive = false;
    int rotateFingerId = -1;                 // ⬅️ khóa ngón xoay
    int pinchFingerId0 = -1, pinchFingerId1 = -1;
    float lastPinchDist = 0f;

    void Start() => targetDistance = distance;

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
        var rot = Quaternion.Euler(pitch, yaw, 0f);
        var targetPos = target.position + targetOffset;
        var dir = rot * Vector3.forward;
        transform.position = targetPos - dir * distance;
        transform.LookAt(targetPos);
    }

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
            targetDistance = Mathf.Clamp(targetDistance - scroll * zoomSpeedScroll * 10f, minDistance, maxDistance);
#endif
    }

    bool IsOverUI(int fingerId) =>
        EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(fingerId);

    void HandleTouch()
    {
        int tc = Input.touchCount;
        if (tc == 0)
        {
            // reset tất cả baseline khi không còn touch
            rotateFingerId = -1;
            pinchActive = false;
            pinchFingerId0 = pinchFingerId1 = -1;
            return;
        }

        // --- Ưu tiên pinch khi có >=2 ngón không đè UI ---
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

        // --- Xoay bằng 1 ngón bên phải, không đè UI ---
        // chọn đúng ngón điều khiển, khóa theo fingerId
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

        // rời pinch khi còn <2 ngón hợp lệ
        if (tc < 2) { pinchActive = false; pinchFingerId0 = pinchFingerId1 = -1; }
    }
}
