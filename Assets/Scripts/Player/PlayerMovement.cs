using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Input")]
    public FloatingJoystick joystick;            // bỏ trống -> vẫn chạy WASD

    [Header("Camera-Relative")]
    public Transform cameraTransform;            // auto lấy MainCamera nếu trống

    [Header("Smoothing")]
    [Tooltip("Làm mượt joystick/WASD để tránh nhảy 8 hướng")]
    public float inputSmoothTime = 0.08f;
    Vector2 _rawInput;
    Vector2 _smoothInput;
    Vector2 _smoothInputVel;

    float _smoothYawVel;
    [Tooltip("Thời gian hãm (respose) khi quay mặt")]
    public float faceSmoothTime = 0.06f;

    [Tooltip("Giới hạn tốc độ quay tối đa (deg/s) khi dùng SmoothDampAngle")]
    public float maxTurnSpeedDeg = 720f;

    [Header("Movement")]
    public float maxSpeed = 20f;
    public float acceleration = 50f;
    public float deceleration = 25f;

    [Header("Facing")]
    public float turnSpeedDeg = 540f;
    public bool alwaysFaceInput = true;
    public bool faceByVelocity = true;
    public float faceMinSpeed = 0.15f;
    public bool keepUpright = true;

    [Header("Animation")]
    public Animator animator;                    // auto-find nếu trống
    public string moveBoolName = "IsMoving";
    public string speedFloatName = "Speed";
    public float moveThreshold = 0.15f;
    public float animDamp = 0.12f;

    [Header("Debug")]
    public bool debugLogs = false;
    public float debugEvery = 0.25f;

    Rigidbody rb;

    // state
    Vector3 inputPlanar;
    Vector3 desiredFaceDir;
    float targetYaw;
    float _nextDebugTime;
    Quaternion _lastFixedSetRot;
    bool _wroteRotationThisFixed;

    // animator cache
    int _hMove, _hSpeed;
    bool _hasMove, _hasSpeed;

    [Header("Visual")]
    public Transform visual; // gán trong Inspector (child chứa Animator/Mesh)

    // =========================
    // FOOTSTEP SFX (NEW)
    // =========================
    [Header("Footstep SFX")]
    public bool enableFootsteps = true;

    [Tooltip("Danh sách clip bước chân. Sẽ chọn ngẫu nhiên 1 clip mỗi lần phát. Bỏ trống -> không phát.")]
    public AudioClip[] footstepClips;

    [Range(0f, 1f)] public float footstepVolume = 0.85f;

    [Tooltip("Khoảng cách (m) cần di chuyển trên mặt phẳng XZ cho mỗi lần phát bước.")]
    public float stepDistance = 2.0f;

    [Tooltip("Độ trễ trước khi cho phép phát bước đầu tiên sau khi bắt đầu di chuyển (s).")]
    public float firstStepDelay = 0.12f;

    [Tooltip("Vận tốc phẳng tối thiểu (m/s) để xét là đang di chuyển (chặn rung nhẹ).")]
    public float minStepSpeed = 0.2f;

    [Tooltip("Điểm đặt âm (mặc định là transform của Player). Có thể gán về chân mesh.")]
    public Transform footstepAnchor;

    [Tooltip("Randomize pitch mỗi bước cho tự nhiên hơn.")]
    public bool randomizePitch = true;
    public Vector2 pitchRange = new Vector2(0.95f, 1.05f);

    [Tooltip("Dùng AudioSource cục bộ 3D thay vì PlayClipAtPoint để tránh tạo GO tạm.")]
    public bool useLocalAudioSource = true;

    [Tooltip("Nếu để trống, sẽ tự tạo AudioSource con tên \"FootstepSource\".")]
    public AudioSource footstepSourcePrefab;

    AudioSource _footSrc;
    Vector3 _prevPos;
    float _accumPlanarDist;
    bool _movingForFoot;
    float _nextEarliestStepTime;

    // =========================

    void Awake()
    {
        if (!visual)
        {
            var anim = GetComponentInChildren<Animator>(true);
            if (anim) visual = anim.transform;
            else if (transform.childCount > 0) visual = transform.GetChild(0);
        }
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
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

        if (debugLogs)
        {
            Debug.Log($"[PlayerMovement] RB kinematic={rb.isKinematic}, interp={rb.interpolation}, cam={(cameraTransform ? cameraTransform.name : "<null>")}");
        }

        // --- Footstep init ---
        _prevPos = transform.position;
        _accumPlanarDist = 0f;
        _movingForFoot = false;
        _nextEarliestStepTime = 0f;

        if (enableFootsteps && useLocalAudioSource)
        {
            if (footstepSourcePrefab != null)
            {
                _footSrc = Instantiate(footstepSourcePrefab, transform);
            }
            else
            {
                var go = new GameObject("FootstepSource");
                go.transform.SetParent(transform, false);
                _footSrc = go.AddComponent<AudioSource>();
                _footSrc.playOnAwake = false;
                _footSrc.loop = false;
                _footSrc.spatialBlend = 0f;   // 2D, volume luôn ổn định
                _footSrc.volume = 1f;
                _footSrc.dopplerLevel = 0f;

            }
        }

        if (!footstepAnchor) footstepAnchor = transform;
    }

    void Update()
    {
        // ===== 1) Đọc input (thô) =====
        _rawInput = Vector2.zero;
        if (joystick) _rawInput = new Vector2(joystick.Horizontal, joystick.Vertical);

#if UNITY_EDITOR || UNITY_STANDALONE
        float ax = Input.GetAxisRaw("Horizontal");
        float ay = Input.GetAxisRaw("Vertical");
        if (Mathf.Abs(ax) > 0.01f || Mathf.Abs(ay) > 0.01f)
            _rawInput = new Vector2(ax, ay);
        else
        {
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) _rawInput.x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) _rawInput.x += 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) _rawInput.y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) _rawInput.y -= 1f;
        }
#endif

        // ===== 1b) Làm mượt input =====
        _smoothInput = Vector2.SmoothDamp(
            _smoothInput,
            Vector2.ClampMagnitude(_rawInput, 1f),
            ref _smoothInputVel,
            Mathf.Max(0.001f, inputSmoothTime)
        );

        // ===== 2) Chuyển hướng theo camera/world =====
        Vector3 fwd = cameraTransform ? cameraTransform.forward : Vector3.forward;
        Vector3 right = cameraTransform ? cameraTransform.right : Vector3.right;
        fwd.y = 0f; right.y = 0f; fwd.Normalize(); right.Normalize();

        Vector3 wishDir = (right * _smoothInput.x + fwd * _smoothInput.y);
        if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();
        inputPlanar = wishDir;

        // (đoạn code trùng lặp gốc giữ nguyên logic của bạn)
        fwd.y = 0f; right.y = 0f; fwd.Normalize(); right.Normalize();
        if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();
        inputPlanar = wishDir;

        // ===== 3) Chọn hướng quay =====
        Vector3 velPlanar = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        float velMag = velPlanar.magnitude;
        Vector3 dirInput = inputPlanar.sqrMagnitude > 0.0001f ? inputPlanar : Vector3.zero;

        string branch = "NONE";
        desiredFaceDir = Vector3.zero;

        if (alwaysFaceInput && dirInput != Vector3.zero)
        {
            desiredFaceDir = dirInput;
            branch = "INPUT";
        }
        else if (faceByVelocity && velMag > faceMinSpeed)
        {
            desiredFaceDir = velPlanar.normalized;
            branch = "VELOCITY";
        }

        if (desiredFaceDir != Vector3.zero)
        {
            var targetRot = Quaternion.LookRotation(desiredFaceDir, Vector3.up);
            targetYaw = targetRot.eulerAngles.y;
        }

        if (animator)
        {
            bool moving = velMag > moveThreshold || dirInput != Vector3.zero;
            if (_hasMove) animator.SetBool(_hMove, moving);
            if (_hasSpeed) animator.SetFloat(_hSpeed, velMag, animDamp, Time.deltaTime);
        }

        if (debugLogs && Time.time >= _nextDebugTime)
        {
            _nextDebugTime = Time.time + Mathf.Max(0.05f, debugEvery);
        }

        _wroteRotationThisFixed = false;

        if ((rb.constraints & RigidbodyConstraints.FreezeRotationY) != 0)
        {
            rb.constraints &= ~RigidbodyConstraints.FreezeRotationY;
            if (debugLogs) Debug.LogWarning("[PlayerMovement] Unfreeze Y: phát hiện FreezeRotationY và đã mở khoá.");
        }

        if (desiredFaceDir != Vector3.zero)
        {
            float curYaw = rb.rotation.eulerAngles.y;
            float newYaw = Mathf.MoveTowardsAngle(curYaw, targetYaw, turnSpeedDeg * Time.fixedDeltaTime);
            Quaternion newRot = keepUpright
                ? Quaternion.Euler(0f, newYaw, 0f)
                : Quaternion.Euler(rb.rotation.eulerAngles.x, newYaw, rb.rotation.eulerAngles.z);

            Quaternion before = transform.rotation;
            if (!rb.isKinematic) rb.MoveRotation(newRot);
            else transform.rotation = newRot;

            _lastFixedSetRot = newRot;
            _wroteRotationThisFixed = true;

            float changed = Quaternion.Angle(before, transform.rotation);
            if (changed < 0.1f && debugLogs)
                Debug.LogWarning("[PlayerMovement] MoveRotation có vẻ không tác dụng, đã thử set transform.rotation (test).");
        }

        // =========================
        //  FOOTSTEP LOGIC (NEW)
        // =========================
        if (enableFootsteps)
        {
            // Tính quãng đường phẳng tăng thêm trong frame này
            Vector3 curPos = transform.position;
            Vector3 d = curPos - _prevPos;
            float planarDelta = new Vector2(d.x, d.z).magnitude;
            _prevPos = curPos;

            float planarSpeed = new Vector2(rb.velocity.x, rb.velocity.z).magnitude;

            bool canMove = planarSpeed >= minStepSpeed && footstepClips != null && footstepClips.Length > 0;

            if (!canMove)
            {
                // reset trạng thái khi dừng hoặc không có clip
                _movingForFoot = false;
                _accumPlanarDist = 0f;
            }
            else
            {
                if (!_movingForFoot)
                {
                    // Bắt đầu di chuyển -> set delay bước đầu
                    _movingForFoot = true;
                    _accumPlanarDist = 0f;
                    _nextEarliestStepTime = Time.time + Mathf.Max(0f, firstStepDelay);
                }

                _accumPlanarDist += planarDelta;

                if (_accumPlanarDist >= Mathf.Max(0.01f, stepDistance) &&
                    Time.time >= _nextEarliestStepTime)
                {
                    PlayFootstepOnce();
                    _accumPlanarDist = 0f;
                    _nextEarliestStepTime = Time.time; // các bước sau chỉ phụ thuộc distance
                }
            }
        }
        // =========================
    }

    void FixedUpdate()
    {
        // 4) Điều khiển vận tốc
        Vector3 desiredVel = inputPlanar * maxSpeed;
        Vector3 curVel = rb.velocity;
        Vector3 curPlanar = new Vector3(curVel.x, 0f, curVel.z);

        float accel = (desiredVel.sqrMagnitude > 0.0001f) ? acceleration : deceleration;
        Vector3 newPlanar = Vector3.MoveTowards(curPlanar, desiredVel, accel * Time.fixedDeltaTime);
        rb.velocity = new Vector3(newPlanar.x, curVel.y, newPlanar.z);

        _wroteRotationThisFixed = false;

        // 7) Quay mượt bằng SmoothDampAngle
        if (desiredFaceDir != Vector3.zero)
        {
            float curYaw = rb.rotation.eulerAngles.y;
            float newYaw = Mathf.SmoothDampAngle(
                curYaw,
                targetYaw,
                ref _smoothYawVel,
                Mathf.Max(0.001f, faceSmoothTime),
                Mathf.Max(0.1f, maxTurnSpeedDeg),
                Time.fixedDeltaTime
            );

            Quaternion newRot = keepUpright
                ? Quaternion.Euler(0f, newYaw, 0f)
                : Quaternion.Euler(rb.rotation.eulerAngles.x, newYaw, rb.rotation.eulerAngles.z);

            if (!rb.isKinematic) rb.MoveRotation(newRot);
            else transform.rotation = newRot;
        }
    }

    void LateUpdate()
    {
        if (visual && desiredFaceDir != Vector3.zero)
        {
            var look = Quaternion.LookRotation(desiredFaceDir, Vector3.up);
            var e = look.eulerAngles;
            visual.rotation = Quaternion.Euler(0f, e.y, 0f);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (desiredFaceDir != Vector3.zero)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + desiredFaceDir.normalized * 1.5f);
        }
    }
#endif

    // =========================
    // FOOTSTEP: helper (NEW)
    // =========================
    void PlayFootstepOnce()
    {
        if (footstepClips == null || footstepClips.Length == 0) return;

        // Chọn clip ngẫu nhiên
        int idx = Random.Range(0, footstepClips.Length);
        var clip = footstepClips[idx];
        if (!clip) return;

        Vector3 pos = (footstepAnchor ? footstepAnchor.position : transform.position);

        if (useLocalAudioSource && _footSrc != null)
        {
            _footSrc.transform.position = pos;
            _footSrc.pitch = randomizePitch ? Random.Range(pitchRange.x, pitchRange.y) : 1f;
            _footSrc.PlayOneShot(clip, Mathf.Clamp01(footstepVolume));
        }
        else
        {
            // Fallback: không đổi được pitch với PlayClipAtPoint
            AudioSource.PlayClipAtPoint(clip, pos, Mathf.Clamp01(footstepVolume));
        }
    }
}
