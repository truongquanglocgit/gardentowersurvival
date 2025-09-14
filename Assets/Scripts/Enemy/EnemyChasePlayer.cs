using UnityEngine;

public class EnemyChasePlayer : MonoBehaviour
{
    public GameObject player;
    public float speed = 2f;
    public float turnSpeed = 10f; // tốc độ xoay quanh trục Y

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation; // không cho Rigidbody tự xoay
    }

    private void FixedUpdate()
    {
        player = GameObject.FindWithTag("Player");
        if (player == null) return;

        // Hướng tới player, nhưng chỉ lấy XZ (không dùng Y)
        Vector3 dir = player.transform.position - transform.position;
        dir.y = 0f; // bỏ chênh lệch Y
        if (dir.sqrMagnitude < 0.001f) return;
        dir.Normalize();

        // Di chuyển theo hướng này
        Vector3 velocity = dir * speed;
        velocity.y = rb.velocity.y; // giữ lại vận tốc Y (gravity)
        rb.velocity = velocity;

        // Xoay mượt chỉ quanh trục Y
        Quaternion targetRotation = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            turnSpeed * Time.fixedDeltaTime
        );
    }
}
