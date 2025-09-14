using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class MobileOrbitNoPanel : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Vector3 targetOffset = new Vector3(0, 1.5f, 0);

    [Header("Distance")]
    public float distance = 12f;
    public float minDistance = 5f;
    public float maxDistance = 25f;
    public float pinchSensitivity = 0.02f;

    [Header("Rotation")]
    public float yaw = 0f;
    public float pitch = 35f;
    public float minPitch = 10f;
    public float maxPitch = 75f;
    public float yawSpeedTouch = 0.3f;
    public float pitchSpeedTouch = 0.25f;
    public float rotateLerp = 12f;

    [Header("Joystick (optional)")]
    public JoystickPointerTracker joystickTracker; // gán BG của joystick (nếu có)

    // internal state
    enum State { None, Orbit, Zoom }
    State state = State.None;
    int orbitFingerId = -1;
    int pinchA = -1, pinchB = -1;
    Vector2 prevA, prevB;
    float targetDistance;
    Quaternion smoothRot;

    void Awake()
    {
        targetDistance = distance;
        smoothRot = Quaternion.Euler(pitch, yaw, 0f);
        Input.simulateMouseWithTouches = false;
    }

    void Update()
    {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        HandleTouches();
#else
        HandleMouseEditor(); // cho bạn test trong Editor
#endif
    }

    void LateUpdate()
    {
        if (!target) return;

        distance = Mathf.Lerp(distance, targetDistance, Time.deltaTime * 10f);
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        var rot = Quaternion.Euler(pitch, yaw, 0f);
        smoothRot = Quaternion.Slerp(smoothRot, rot, rotateLerp * Time.deltaTime);

        Vector3 look = target.position + targetOffset;
        Vector3 dir = smoothRot * Vector3.forward;

        transform.position = look - dir * distance;
        transform.LookAt(look);
    }

    // ---------- TOUCH ----------
    void HandleTouches()
    {
        if (Input.touchCount == 0) { ResetAll(); return; }

        // Lọc ra những touch "tự do": không ở trên UI & không phải ngón joystick
        List<Touch> free = new();
        int joyId = joystickTracker ? joystickTracker.ActiveFingerId : -1;

        for (int i = 0; i < Input.touchCount; i++)
        {
            var t = Input.GetTouch(i);
            if (t.fingerId == joyId) continue; // bỏ ngón joystick
            if (EventSystem.current && EventSystem.current.IsPointerOverGameObject(t.fingerId)) continue; // bỏ UI
            free.Add(t);
        }

        if (free.Count == 0) { ResetAll(); return; }

        if (free.Count == 1)
        {
            var t = free[0];

            if (state == State.Zoom) return; // sau pinch, yêu cầu tap lại để orbit

            if (state == State.None && t.phase == TouchPhase.Began)
            {
                state = State.Orbit;
                orbitFingerId = t.fingerId;
                return;
            }

            if (state == State.Orbit && t.fingerId == orbitFingerId)
            {
                if (t.phase == TouchPhase.Moved)
                {
                    yaw += t.deltaPosition.x * yawSpeedTouch;
                    pitch -= t.deltaPosition.y * pitchSpeedTouch;
                }
                else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    state = State.None;
                    orbitFingerId = -1;
                }
            }
            return;
        }

        // >= 2 ngón tự do -> pinch zoom
        var a = free[0];
        var b = free[1];

        if (state != State.Zoom || !PinchPairValid(a, b))
        {
            state = State.Zoom;
            pinchA = a.fingerId; pinchB = b.fingerId;
            prevA = a.position; prevB = b.position;
            orbitFingerId = -1;
            return;
        }

        float prevDist = (prevA - prevB).magnitude;
        float currDist = (a.position - b.position).magnitude;
        float diff = currDist - prevDist;

        targetDistance = Mathf.Clamp(targetDistance - diff * pinchSensitivity, minDistance, maxDistance);

        prevA = a.position; prevB = b.position;
    }

    bool PinchPairValid(Touch a, Touch b)
    {
        return (a.fingerId == pinchA && b.fingerId == pinchB) ||
               (a.fingerId == pinchB && b.fingerId == pinchA);
    }

    void ResetAll()
    {
        state = State.None;
        orbitFingerId = -1;
        pinchA = pinchB = -1;
    }

    // ---------- EDITOR MOUSE ----------
    public bool useMouseLeftButtonInEditor = true;
    public float mouseYawSpeed = 3f, mousePitchSpeed = 2f, mouseZoomSpeed = 8f;

    void HandleMouseEditor()
    {
        bool btn = useMouseLeftButtonInEditor ? Input.GetMouseButton(0) : Input.GetMouseButton(1);
        if (btn)
        {
            yaw += Input.GetAxis("Mouse X") * mouseYawSpeed;
            pitch -= Input.GetAxis("Mouse Y") * mousePitchSpeed;
        }

        float wheel = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(wheel) > 0.0001f)
            targetDistance = Mathf.Clamp(targetDistance - wheel * mouseZoomSpeed, minDistance, maxDistance);
    }
}
