using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Input")]
    public FloatingJoystick joystick;          // có/không đều chạy

    [Header("Camera-Relative")]
    public Transform cameraTransform;          // auto lấy MainCamera nếu trống

    [Header("Movement (Instant)")]
    public float maxSpeed = 20f;               // tốc độ tức thì

    [Header("Facing")]
    public bool faceToInput = true;            // quay theo input khi đang có input
    public bool keepUpright = true;

    [Header("Animation (optional)")]
    public Animator animator;                  // auto-find nếu trống
    public string moveBoolName = "IsMoving";
    public string speedFloatName = "Speed";
    public float moveThreshold = 0.1f;

    Rigidbody rb;

    // runtime
    Vector2 _rawInput;
    Vector3 _inputPlanar;
    Vector3 _desiredFaceDir;
    int _hMove, _hSpeed;
    bool _hasMove, _hasSpeed;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // Khoá cả 3 trục để physics không được phép xoay nhân vật
        rb.constraints = RigidbodyConstraints.FreezeRotationX
                       | RigidbodyConstraints.FreezeRotationY
                       | RigidbodyConstraints.FreezeRotationZ;

        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (!cameraTransform && Camera.main) cameraTransform = Camera.main.transform;
        if (!animator) animator = GetComponentInChildren<Animator>(true);
        if (animator) animator.applyRootMotion = false;

        _hMove = Animator.StringToHash(moveBoolName);
        _hSpeed = Animator.StringToHash(speedFloatName);
        if (animator)
        {
            foreach (var p in animator.parameters)
            {
                if (p.nameHash == _hMove && p.type == AnimatorControllerParameterType.Bool) _hasMove = true;
                if (p.nameHash == _hSpeed && p.type == AnimatorControllerParameterType.Float) _hasSpeed = true;
            }
        }
    }

    void Update()
    {
        // 1) Đọc input thô (không làm mượt)
        _rawInput = Vector2.zero;
        if (joystick) _rawInput = new Vector2(joystick.Horizontal, joystick.Vertical);

#if UNITY_EDITOR || UNITY_STANDALONE
        // WASD / phím mũi tên (Raw)
        _rawInput.x += Input.GetAxisRaw("Horizontal");
        _rawInput.y += Input.GetAxisRaw("Vertical");
#endif
        _rawInput = Vector2.ClampMagnitude(_rawInput, 1f);

        // 2) Chuyển sang hướng theo camera (XZ)
        Vector3 fwd = cameraTransform ? cameraTransform.forward : Vector3.forward;
        Vector3 right = cameraTransform ? cameraTransform.right : Vector3.right;
        fwd.y = 0f; right.y = 0f; fwd.Normalize(); right.Normalize();

        _inputPlanar = (right * _rawInput.x + fwd * _rawInput.y);
        if (_inputPlanar.sqrMagnitude > 1f) _inputPlanar.Normalize();

        // 3) Hướng quay: snap theo input nếu đang có input
        _desiredFaceDir = (_inputPlanar.sqrMagnitude > 0.0001f && faceToInput) ? _inputPlanar : Vector3.zero;

        // 4) Animator (tuỳ chọn)
        if (animator)
        {
            Vector3 planarVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            bool moving = planarVel.magnitude > moveThreshold || _inputPlanar.sqrMagnitude > 0.0001f;
            if (_hasMove) animator.SetBool(_hMove, moving);
            if (_hasSpeed) animator.SetFloat(_hSpeed, planarVel.magnitude);
        }
    }

    void FixedUpdate()
    {
        // set velocity như cũ
        Vector3 curVel = rb.velocity;
        Vector3 newPlanar = _inputPlanar * maxSpeed;
        rb.velocity = new Vector3(newPlanar.x, curVel.y, newPlanar.z);

        // quay theo input
        if (_desiredFaceDir != Vector3.zero)
        {
            Quaternion look = Quaternion.LookRotation(_desiredFaceDir, Vector3.up);
            // đã khoá rotation X/Z nên không cần ép upright nữa, nhưng giữ cũng không sao
            if (keepUpright) look = Quaternion.Euler(0f, look.eulerAngles.y, 0f);
            // khoá rotation rồi thì set transform cũng được
            transform.rotation = look; // hoặc rb.MoveRotation(look);
        }

        // an toàn thêm: triệt tiêu góc quay còn sót (nếu bạn chưa khoá Y)
        // rb.angularVelocity = Vector3.zero;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (maxSpeed < 0f) maxSpeed = 0f;
    }
#endif
}
