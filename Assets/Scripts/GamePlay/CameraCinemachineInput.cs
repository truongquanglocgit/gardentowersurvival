using UnityEngine;
#if CINEMACHINE
using Cinemachine;
#endif

public class CameraCinemachineInput : MonoBehaviour
{
    [Header("Cinemachine Target")]
    [SerializeField] Transform _cinemachineCameraTarget;  // gán GameObject/Transform target trên Player
    [SerializeField] float _cameraAngleOverride = 0f;

    [Header("Clamp")]
    [SerializeField] float _topClamp = 75f;
    [SerializeField] float _bottomClamp = 10f;

    [Header("Speed")]
    [SerializeField] float SpeedRotate = 12f;   // slerp speed

    [Header("Zoom")]
    [SerializeField] float zoomTouchScale = 0.02f;  // ?? nh?y zoom khi pinch
    [SerializeField] float minDistance = 6f;
    [SerializeField] float maxDistance = 30f;
    [SerializeField] float fallbackFOVMin = 40f;
    [SerializeField] float fallbackFOVMax = 70f;

    private float _cinemachineTargetYaw;
    private float _cinemachineTargetPitch;

    // nh?n t? UIVirtualLook
    private Vector2 LookInput;

#if CINEMACHINE
    CinemachineVirtualCamera vcam;
    CinemachineComponentBase body;
#endif
    Camera mainCam;
    float currDistance; // dùng khi ?i?u ch?nh kho?ng cách theo body Cinemachine
    void Awake()
    {
        mainCam = Camera.main;
#if CINEMACHINE
        vcam = FindObjectOfType<CinemachineVirtualCamera>();
        if (vcam) body = vcam.GetCinemachineComponent(CinemachineCore.Stage.Body);
        // L?y kho?ng cách ban ??u (n?u có FramingTransposer)
        var ft = body as CinemachineFramingTransposer;
        if (ft) currDistance = ft.m_CameraDistance;
#endif
    }

    // === Hook t? UIVirtualLook ===
    public void OnLook(Vector2 delta) => LookInput = delta;

    public void OnPinch(float pinchDelta)
    {
#if CINEMACHINE
        var ft = body as CinemachineFramingTransposer;
        if (ft)
        {
            currDistance = Mathf.Clamp(ft.m_CameraDistance - pinchDelta * zoomTouchScale,
                                       minDistance, maxDistance);
            ft.m_CameraDistance = currDistance;
            return;
        }
#endif
        // Fallback: ch?nh FOV n?u không dùng FramingTransposer
        if (mainCam)
        {
            float fov = mainCam.fieldOfView - pinchDelta * zoomTouchScale;
            mainCam.fieldOfView = Mathf.Clamp(fov, fallbackFOVMin, fallbackFOVMax);
        }
    }

    void LateUpdate()
    {
        CameraRotation();
        // reset LookInput v? 0 m?i frame n?u không còn drag (UIVirtualLook s? phát (0,0) khi nh?)
        LookInput = Vector2.zero;
    }

    void CameraRotation()
    {
        if (!_cinemachineCameraTarget) return;

        // áp d?ng input
        _cinemachineTargetYaw += LookInput.x;
        _cinemachineTargetPitch += LookInput.y;

        // clamp
        _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
        _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, _bottomClamp, _topClamp);

        // xoay m??t
        Quaternion targetRot = Quaternion.Euler(_cinemachineTargetPitch + _cameraAngleOverride,
                                                _cinemachineTargetYaw, 0f);

        _cinemachineCameraTarget.rotation = Quaternion.Slerp(
            _cinemachineCameraTarget.rotation, targetRot, SpeedRotate * Time.deltaTime);
    }

    static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        while (lfAngle < -360f) lfAngle += 360f;
        while (lfAngle > 360f) lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }
}
