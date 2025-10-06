using UnityEngine;

/// <summary>
/// Attach script này vào GO A.
/// Gán GO B vào "target".
/// Tick chọn các trục muốn follow.
/// </summary>
public class FollowTransformAxis : MonoBehaviour
{
    [Header("Target to Follow")]
    [SerializeField] private Transform target;   // GO B

    [Header("Axis Settings")]
    [SerializeField] private bool followX = true;
    [SerializeField] private bool followY = true;
    [SerializeField] private bool followZ = true;

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 currentPos = transform.position;
        Vector3 targetPos = target.position;

        // Chỉ thay đổi trục được chọn
        if (followX) currentPos.x = targetPos.x;
        if (followY) currentPos.y = targetPos.y;
        if (followZ) currentPos.z = targetPos.z;

        transform.position = currentPos;
    }
}
