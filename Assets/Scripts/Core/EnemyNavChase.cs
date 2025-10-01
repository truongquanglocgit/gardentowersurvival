using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyNavChase : MonoBehaviour
{
    public Transform player;
    public float attackRange = 2f;

    [Header("Repath")]
    public float repathInterval = 0.2f;
    public float repathThreshold = 0.5f;

    [Header("Car-like Turning")]
    public float turnSmoothTime = 0.2f;        // 0.15–0.35
    public float maxYawDegPerSec = 180f;       // 120–300
    public float dirLerp = 0.15f;              // 0.1–0.3

    NavMeshAgent agent;
    Vector3 lastPlayerPos;
    float yawVel;
    Vector3 lastMoveDir = Vector3.forward;

    // --- Death/loop state ---
    bool isDead = false;
    Coroutine repathCo;

    const float kSampleMaxDistance = 3f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;
        agent.angularSpeed = 0f;
        agent.autoBraking = true;
    }

    void OnEnable()
    {
        // Reset khi spawn lại (pool)
        isDead = false;
        lastMoveDir = transform.forward;
    }

    void Start()
    {
        if (!player)
        {
            var p = GameObject.FindWithTag("Player");
            if (p) player = p.transform;
        }

        TryRepositionToNavMesh();

        if (player && agent.enabled && agent.isOnNavMesh)
        {
            lastPlayerPos = player.position;
            agent.SetDestination(lastPlayerPos);
            repathCo = StartCoroutine(RepathLoop());
        }
    }

    void Update()
    {
        if (isDead || !player) return;

        // 1) Early-out khi freeze time để tránh toán học sinh NaN
        if (Time.timeScale <= 0f)
        {
            if (agent && agent.enabled && agent.isOnNavMesh)
                agent.isStopped = true;    // đảm bảo đứng yên
            return;
        }

        // Guard: agent hợp lệ trên NavMesh
        if (!agent.enabled || !agent.isOnNavMesh) { TryRepositionToNavMesh(); return; }

        float dist = Vector3.Distance(transform.position, player.position);
        bool inRange = dist <= attackRange;

        // Chỉ gọi isStopped khi agent hợp lệ
        agent.isStopped = inRange;

        if (inRange) return;

        // 2) Tính hướng mong muốn
        Vector3 vel = agent.velocity; vel.y = 0f;

        if (vel.sqrMagnitude > 0.0001f)
        {
            Vector3 wantDir = vel.normalized;
            lastMoveDir = Vector3.Slerp(lastMoveDir, wantDir, dirLerp);
        }
        else
        {
            Vector3 steer = agent.steeringTarget - transform.position; steer.y = 0f;
            if (steer.sqrMagnitude > 0.0001f)
                lastMoveDir = Vector3.Slerp(lastMoveDir, steer.normalized, dirLerp * 0.5f);
        }

        // 3) Chỉ xoay khi có hướng hợp lệ
        if (lastMoveDir.sqrMagnitude > 0.0001f)
        {
            // Lấy dt an toàn (deltaTime > 0 do đã early-out ở trên)
            float dt = Time.deltaTime;

            float curYaw = transform.eulerAngles.y;

            // targetYaw: tránh gọi LookRotation nếu lastMoveDir ~ zero
            float targetYaw = Quaternion.LookRotation(lastMoveDir, Vector3.up).eulerAngles.y;

            // SmoothDampAngle: có thể sinh NaN nếu input hỏng; ta kẹp bảo vệ
            float smoothYaw = Mathf.SmoothDampAngle(curYaw, targetYaw, ref yawVel, turnSmoothTime);

            if (float.IsNaN(smoothYaw) || float.IsInfinity(smoothYaw))
            {
                // Reset vận tốc góc nếu đã bị nhiễm NaN
                yawVel = 0f;
                smoothYaw = targetYaw;
            }

            // Kẹp tốc độ quay theo frame
            float maxStep = maxYawDegPerSec * dt;
            float delta = Mathf.DeltaAngle(curYaw, smoothYaw);
            delta = Mathf.Clamp(delta, -maxStep, maxStep);

            float nextYaw = curYaw + delta;

            if (!float.IsNaN(nextYaw) && !float.IsInfinity(nextYaw))
            {
                transform.rotation = Quaternion.Euler(0f, nextYaw, 0f);
            }
        }
    }


    IEnumerator RepathLoop()
    {
        var wait = new WaitForSeconds(repathInterval);
        while (!isDead)
        {
            if (player && agent.enabled && agent.isOnNavMesh)
            {
                if ((player.position - lastPlayerPos).sqrMagnitude >= repathThreshold * repathThreshold)
                {
                    agent.SetDestination(player.position);
                    lastPlayerPos = player.position;
                }
            }
            else
            {
                TryRepositionToNavMesh();
            }
            yield return wait;
        }
    }

    // === Gọi hàm này khi enemy chết ===
    public void Die()
    {
        if (isDead) return;
        isDead = true;

        // Dừng repath ngay
        if (repathCo != null) { StopCoroutine(repathCo); repathCo = null; }

        // Nếu agent còn hợp lệ, dừng nó trước khi tắt
        if (agent && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        // Vô hiệu hóa script để không còn Update() chạm vào agent
        enabled = false;

        // (Tùy flow của bạn)
        // agent.enabled = false; // nếu bạn chuyển qua ragdoll/physics
        // gameObject.SetActive(false); // nếu dùng pool thì tắt ở nơi quản lý pool
    }

    void OnDisable()
    {
        // Safety: dừng repath nếu object bị disable theo pool
        if (repathCo != null) { StopCoroutine(repathCo); repathCo = null; }
    }

    bool TryRepositionToNavMesh()
    {
        if (!agent || !agent.enabled) return false;
        if (agent.isOnNavMesh) return true;

        if (NavMesh.SamplePosition(transform.position, out var hit, kSampleMaxDistance, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            return true;
        }
        return false;
    }
}
