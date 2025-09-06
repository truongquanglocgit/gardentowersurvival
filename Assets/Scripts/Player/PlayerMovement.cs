using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public FloatingJoystick joystick;
    public Transform cameraTransform; // gán MainCamera

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        if (!cameraTransform && Camera.main) cameraTransform = Camera.main.transform;
    }

    void FixedUpdate()
    {
        // 📱 Joystick luôn chạy (mobile)
        Vector3 input = new Vector3(
            joystick ? joystick.Horizontal : 0f,
            0f,
            joystick ? joystick.Vertical : 0f
        );

        // ⌨️ WASD chỉ dùng khi chạy trong Editor/Standalone
#if UNITY_EDITOR || UNITY_STANDALONE
        // Ưu tiên Input Manager (old)
        float ax = Input.GetAxisRaw("Horizontal");
        float az = Input.GetAxisRaw("Vertical");
        if (Mathf.Abs(ax) > 0.01f || Mathf.Abs(az) > 0.01f)
        {
            input = new Vector3(ax, 0f, az);
        }
        else
        {
            // Fallback đọc phím trực tiếp (kể cả New Input System)
            float x = 0f, z = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) z += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) z -= 1f;
            if (x != 0f || z != 0f) input = new Vector3(x, 0f, z);
        }
#endif

        if (input.sqrMagnitude < 0.0001f) return;

        // 🔄 Xoay theo camera
        Vector3 fwd = cameraTransform ? cameraTransform.forward : Vector3.forward;
        Vector3 right = cameraTransform ? cameraTransform.right : Vector3.right;
        fwd.y = 0f; right.y = 0f; fwd.Normalize(); right.Normalize();

        Vector3 moveDir = (fwd * input.z + right * input.x);
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        rb.MovePosition(rb.position + moveDir * moveSpeed * Time.fixedDeltaTime);
        if (moveDir.sqrMagnitude > 0.0001f) transform.forward = moveDir;
    }
}
