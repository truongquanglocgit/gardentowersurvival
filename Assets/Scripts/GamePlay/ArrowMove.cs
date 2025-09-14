using UnityEngine;

public class ArrowMove : MonoBehaviour
{
    [Tooltip("Tốc độ di chuyển arrow (m/s)")]
    public float speed = 3f;

    [Tooltip("Transform cần xoay để hướng về player (nếu null sẽ dùng chính object)")]
    public Transform rotateTransform;

    private Transform _target;

    public void Init(Transform target, float spd)
    {
        _target = target;
        speed = spd;
    }

    void Update()
    {
        if (!_target) return;

        // Di chuyển arrow về phía player
        transform.position = Vector3.MoveTowards(
            transform.position,
            _target.position,
            speed * Time.deltaTime
        );

        // Tính hướng và xoay arrow
        Vector3 dir = _target.position - transform.position;
        dir.y = 0f; // giữ ngang (nếu muốn 2D/TopDown)
        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
            if (rotateTransform)
                rotateTransform.rotation = rot;
            else
                transform.rotation = rot;
        }
    }
}
