using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public FloatingJoystick joystick;
    public Transform cameraTransform; // gán MainCamera

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    void FixedUpdate()
    {
        Vector3 input = new Vector3(joystick.Horizontal, 0f, joystick.Vertical);

        // 👉 Nếu không input, bỏ qua
        if (input.magnitude < 0.1f)
            return;

        // 🔄 Xoay input theo góc Y của camera
        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDir = camForward * input.z + camRight * input.x;

        // 🚶 Move + xoay theo hướng di chuyển
        rb.MovePosition(rb.position + moveDir * moveSpeed * Time.fixedDeltaTime);
        transform.forward = moveDir;
    }
}
