using UnityEngine;

public class EnemyAttack : MonoBehaviour
{
    [Header("Facing")]
    [Tooltip("Tốc độ xoay mặt về phía player khi sắp tấn công (độ/giây)")]
    public float turnSpeedDeg = 720f;

    private Transform player;
    private PlayerController playerCtrl;

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p)
        {
            player = p.transform;
            playerCtrl = p.GetComponent<PlayerController>();
        }
    }

    /// <summary>Quay mặt dần về phía player (gọi ngay trước khi trigger Attack).</summary>
    public void FacePlayer()
    {
        if (!player) return;
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            target,
            turnSpeedDeg * Time.deltaTime
        );
    }

    /// <summary>Gây damage – gọi bằng Animation Event ở frame impact, hoặc từ EnemyController.OnAttackImpact().</summary>
    public void DoDamage(float damage)
    {
        if (!playerCtrl) return;
        playerCtrl.TakeDamage(Mathf.RoundToInt(damage));
#if UNITY_EDITOR
        // Debug.Log($"[EnemyAttack] Hit player: {damage}");
#endif
    }
}
