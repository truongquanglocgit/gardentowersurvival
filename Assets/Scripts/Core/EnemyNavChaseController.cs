using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyNavChaseController : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Cache player transform để không Find mỗi frame")]
    public Transform player;

    [Header("Combat")]
    public float attackRange = 2f;       // vào tầm thì dừng agent
    public float attackCooldown = 1.0f;  // ví dụ, chưa triển khai logic đánh
    public bool faceWhileAttacking = false; // true nếu muốn xoay chậm khi đang đánh

    [Header("Movement (Agent)")]
    public float maxSpeed = 3.5f;        // sẽ set về agent.speed
    public float accel = 8f;             // agent.acceleration
    public float angularSpeedDeg = 360f; // tốc độ quay tối đa (độ/giây)
    public float stoppingDistance = 1.6f;// nên < attackRange một chút
    public ObstacleAvoidanceType avoidance = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

    [Header("Steering (Car-like)")]
    [Tooltip("Hệ số mượt hướng (0..1), càng cao càng bám steering target nhanh")]
    public float turnResponsiveness = 0.25f;
    [Tooltip("Tốc độ quay tối thiểu để không bị 'đứng hình' khi đổi hướng nhỏ")]
    public float minTurnDegPerSec = 60f;

    [Header("Destination Update")]
    [Tooltip("Chỉ cập nhật SetDestination mỗi N giây để tiết kiệm CPU")]
    public float repathInterval = 0.15f;
    [Tooltip("Chỉ repath nếu player di chuyển quá ngưỡng này kể từ lần trước")]
    public float playerRepathThreshold = 0.4f;

    // State
    private NavMeshAgent agent;
    private bool isDead;
    private float lastAttackTime;
    private Vector3 lastPlayerPosForRepath;
    private Quaternion lastGoodRotation; // hướng gần nhất khi có vận tốc

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        // Áp dụng tham số agent từ Inspector
        agent.speed = maxSpeed;
        agent.acceleration = accel;
        agent.angularSpeed = 0f;         // tắt xoay mặc định, ta tự xoay
        agent.stoppingDistance = stoppingDistance;
        agent.autoBraking = true;
        agent.updateRotation = false;
        agent.updatePosition = true;
        agent.obstacleAvoidanceType = avoidance;
        agent.autoRepath = true;

        // Nếu có Rigidbody thì dùng kinematic để tránh xung đột với agent
        var rb = GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = true;
    }

    void Start()
    {
        if (!player)
        {
            var p = GameObject.FindWithTag("Player");
            if (p) player = p.transform;
        }

        if (player)
        {
            lastPlayerPosForRepath = player.position;
            agent.SetDestination(player.position);
        }
        lastGoodRotation = transform.rotation;
        StartCoroutine(RepathLoop());
    }

    void Update()
    {
        if (isDead || !player) return;

        float dist = Vector3.Distance(transform.position, player.position);

        // Trong/ngoài tầm đánh:
        bool inAttackRange = dist <= attackRange;

        if (inAttackRange)
        {
            // Dừng agent (brake) khi vào tầm
            if (!agent.isStopped) agent.isStopped = true;

            // KHÔNG xoay mặt trừ khi bạn muốn (flag)
            if (faceWhileAttacking)
            {
                // Xoay rất chậm hướng tới player (tuỳ chọn)
                Vector3 to = (player.position - transform.position);
                to.y = 0f;
                if (to.sqrMagnitude > 0.0001f)
                {
                    Quaternion target = Quaternion.LookRotation(to.normalized, Vector3.up);
                    float turnDeg = Mathf.Max(minTurnDegPerSec, angularSpeedDeg * 0.25f); // quay chậm hơn khi đánh
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnDeg * Time.deltaTime);
                    lastGoodRotation = transform.rotation;
                }
            }
            // TODO: Gọi logic đánh/animation, check cooldown ở đây
            // if (Time.time >= lastAttackTime + attackCooldown) { Attack(); lastAttackTime = Time.time; }
        }
        else
        {
            // Nếu đang ngoài tầm, đảm bảo agent đang chạy
            if (agent.isStopped) agent.isStopped = false;

            // --- Xoay theo hướng DI CHUYỂN THỰC TẾ ---
            // Dùng steeringTarget trước (corner anticipation). Nếu không có thì fallback desiredVelocity.
            Vector3 lookDir = agent.steeringTarget - transform.position;
            if (lookDir.sqrMagnitude < 0.0001f)
                lookDir = agent.desiredVelocity;

            lookDir.y = 0f;

            if (lookDir.sqrMagnitude > 0.0001f)
            {
                // Lọc mượt hướng (giảm rung ở góc cua)
                Vector3 smoothDir = Vector3.Slerp(transform.forward, lookDir.normalized, turnResponsiveness);
                Quaternion targetRot = Quaternion.LookRotation(smoothDir, Vector3.up);

                // Giới hạn tốc độ quay như xe oto
                float deg = Mathf.Max(minTurnDegPerSec, angularSpeedDeg);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, deg * Time.deltaTime);
                lastGoodRotation = transform.rotation;
            }
            else
            {
                // Không có hướng rõ ràng (đang bị kẹt/đứng) → giữ hướng cũ
                transform.rotation = lastGoodRotation;
            }
        }
    }

    IEnumerator RepathLoop()
    {
        // Cập nhật destination theo chu kỳ để tiết kiệm CPU
        var wait = new WaitForSeconds(repathInterval);
        while (true)
        {
            if (!isDead && player && agent.enabled)
            {
                // Chỉ repath khi player dịch chuyển đủ xa
                if ((player.position - lastPlayerPosForRepath).sqrMagnitude >= playerRepathThreshold * playerRepathThreshold)
                {
                    agent.SetDestination(player.position);
                    lastPlayerPosForRepath = player.position;
                }
            }
            yield return wait;
        }
    }

    // ==== Public APIs ====

    public void SetDead(bool dead)
    {
        isDead = dead;
        if (dead)
        {
            if (agent) agent.isStopped = true;
            enabled = false;
        }
    }

    public void ApplySlow(float factor, float duration)
    {
        // Giảm speed tạm thời (không chạm acceleration để cảm giác tự nhiên)
        float original = agent.speed;
        agent.speed = Mathf.Max(0.1f, original * factor);
        StartCoroutine(_RestoreSpeedAfter(duration, original));
    }

    private IEnumerator _RestoreSpeedAfter(float duration, float original)
    {
        yield return new WaitForSeconds(duration);
        if (agent && !isDead) agent.speed = original;
    }
}
