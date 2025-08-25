using UnityEngine;
using UnityEngine.EventSystems;
public class CameraOrbit : MonoBehaviour
{
    public Transform target; // Gán player tại runtime
    public Vector3 targetOffset = new Vector3(0, 1.5f, 0);

    public float distance = 15f;
    public float minDistance = 6f;
    public float maxDistance = 30f;
    public float zoomSpeedScroll = 5f;
    public float zoomSpeedTouch = 0.02f;

    public float yaw = 0f;
    public float pitch = 35f;
    public float yawSpeed = 0.2f;
    public float pitchSpeed = 0.15f;
    public float mouseYawSpeed = 3f;
    public float mousePitchSpeed = 2f;
    public float minPitch = 10f;
    public float maxPitch = 75f;

    float targetDistance;
    Vector2 lastTouch0, lastTouch1;
    bool hadTwoLastFrame = false;

    void Start()
    {
        targetDistance = distance;
    }

    void Update()
    {
        if (target == null) return;

        HandleMouse();
        HandleTouch();
    }

    void LateUpdate()
    {
        if (target == null) return;

        distance = Mathf.Lerp(distance, targetDistance, Time.deltaTime * 10f);

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 targetPos = target.position + targetOffset;
        Vector3 dir = rot * Vector3.forward;
        transform.position = targetPos - dir * distance;
        transform.LookAt(targetPos);
    }

    void HandleMouse()
    {
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
    }

    void HandleTouch()
    {
        if (Input.touchCount == 0) { hadTwoLastFrame = false; return; }

        for (int i = 0; i < Input.touchCount; i++)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(Input.GetTouch(i).fingerId))
                return;
        }

        if (Input.touchCount == 1)
        {
            Touch t = Input.GetTouch(0);
            if (t.position.x < Screen.width * 0.5f) return;

            if (t.phase == TouchPhase.Moved)
            {
                Vector2 delta = t.deltaPosition;
                yaw += delta.x * yawSpeed;
                pitch -= delta.y * pitchSpeed;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }
            hadTwoLastFrame = false;
        }
        else if (Input.touchCount >= 2)
        {
            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            if (!hadTwoLastFrame)
            {
                lastTouch0 = t0.position;
                lastTouch1 = t1.position;
                hadTwoLastFrame = true;
                return;
            }

            float prevDist = (lastTouch0 - lastTouch1).magnitude;
            float currDist = (t0.position - t1.position).magnitude;
            float diff = currDist - prevDist;

            targetDistance = Mathf.Clamp(targetDistance - diff * zoomSpeedTouch, minDistance, maxDistance);

            lastTouch0 = t0.position;
            lastTouch1 = t1.position;
        }
    }
}
