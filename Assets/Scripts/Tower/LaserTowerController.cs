using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class LaserTowerController : MonoBehaviour
{
    [Header("Combat")]
    [Tooltip("Bán kính tìm địch")]
    public float range = 6f;
    [Tooltip("Số lần bắt đầu bắn/giây (chu kỳ: 1/fireRate). Sau khi kết thúc beam sẽ chờ period rồi mới bắn tiếp.")]
    public float fireRate = 0.5f; // ví dụ 0.5 => 1 phát mỗi 2s
    [Header("Whole Tower Rotation")]
    public bool rotateWholeTower = true;        // ✅ Bật để xoay cả root về phía địch
    public bool lockUpright = true;             // true = chỉ yaw (không nghiêng cây)
    public float wholeTurnDegPerSec = 360f;     // tốc độ quay root
    public bool requireAimBeforeFiring = true;  // chỉ bắn khi đã ngắm trong ngưỡng
    [Range(0f, 30f)] public float aimToleranceDeg = 4f;  // ngưỡng “đã ngắm”

    [Header("Laser")]
    public Transform firePoint;
    public LineRenderer line;                     // LineRenderer có 2 points
    [Tooltip("Thời gian giữ tia (1–2s)")]
    public float beamDuration = 1.5f;
    [Tooltip("Bao lâu thì tick damage 1 lần")]
    public float tickInterval = 0.2f;
    [Tooltip("Có đổi mục tiêu trong lúc tia đang bắn không (nếu mục tiêu chết/rời tầm)")]
    public bool retargetDuringBeam = true;
    [Header("Aiming/Rotation")]
    public Transform yawPivot;                 // pivot quay ngang (nếu trống: dùng transform)
    public float yawDegPerSec = 540f;

    [Tooltip("Pivot nòng súng để ngẩng/cúi (quay quanh trục X cục bộ). Đặt neutral hướng thẳng.")]
    public Transform pitchPivot;               // optional: để trống nếu không dùng pitch
    public float pitchDegPerSec = 360f;
    public bool usePitch = true;
    public Vector2 pitchClamp = new Vector2(-10f, 45f); // min/max độ ngẩng/cúi

    [Tooltip("Tên child để ngắm (nếu enemy có child 'Target'). Để rỗng dùng +y offset.")]
    public string targetChildName = "Target";
    public float aimYOffset = 0.6f;
    [Header("Damage")]
    [Tooltip("Nếu có tham chiếu TowerController thì DPS = CurrentDamage * dpsMultiplier")]
    public TowerController towerRef;              // optional: lấy CurrentDamage
    [Tooltip("Hệ số DPS so với CurrentDamage (1.0 nghĩa là 1 *damage mỗi giây)")]
    public float dpsMultiplier = 1.0f;
    [Tooltip("Nếu > 0 sẽ thay thế tick damage cố định, bỏ qua towerRef/dpsMultiplier.")]
    public float damagePerTickOverride = 0f;

    [Header("Detection")]
    public LayerMask enemyMask;
    public string enemyTag = "Enemy";             // fallback nếu chưa set layer

    

    [Header("VFX (optional)")]
    public ParticleSystem hitVfx;                 // nổ ở điểm cuối tia
    public float hitVfxScale = 1f;

    [Header("Debug")]
    public bool debugLog = false;
    [Header("FirePoint Follower")]
    public bool followAnimatedBone = true;          // bật/tắt bám xương
    public Transform fireBone;                      // kéo đúng “đầu nòng”/muzzle trong prefab
    [Tooltip("Nếu không kéo trực tiếp, tìm theo path (so với Animator root). Ví dụ: Armature/Head/Muzzle")]
    public string fireBonePath = "";
    public Vector3 fireLocalOffset = Vector3.zero;  // tinh chỉnh vị trí so với xương
    public Vector3 fireLocalEuler = Vector3.zero;   // tinh chỉnh rotation so với xương

    [SerializeField] Animator anim;                 // auto-find nếu để trống

    // ——— runtime ———
    EnemyController _target;
    bool _beaming;
    float _nextShotTime;
    Coroutine _beamCo;

    static readonly Collider[] _overlapBuf = new Collider[32];

    void Awake()
    {
        if (!anim) anim = GetComponentInChildren<Animator>(true);

        if (followAnimatedBone && fireBone == null && anim && !string.IsNullOrEmpty(fireBonePath))
        {
            // tìm theo đường dẫn tương đối từ Animator
            fireBone = anim.transform.Find(fireBonePath);
            if (!fireBone) Debug.LogWarning($"[{name}] fireBonePath not found: {fireBonePath}");
        }

        if (line)
        {
            line.positionCount = 2;
            line.enabled = false;
        }
    }
    void LateUpdate()
    {
        // 1) Bám xương để firePoint theo đúng đầu nòng đang lắc
        if (followAnimatedBone && fireBone && firePoint)
        {
            var pos = fireBone.TransformPoint(fireLocalOffset);
            var rot = fireBone.rotation * Quaternion.Euler(fireLocalEuler);
            firePoint.SetPositionAndRotation(pos, rot);
        }

        // 2) Nếu đang bắn, cập nhật LineRenderer theo vị trí firePoint mới
        if (_beaming && line)
        {
            Vector3 from = firePoint ? firePoint.position : transform.position;
            Vector3 to = GetLaserEndPoint();
            line.SetPosition(0, from);
            line.SetPosition(1, to);

            // (optional) dịch hitVFX bám đuôi
            if (hitVfx)
            {
                hitVfx.transform.position = to;
                if (!hitVfx.isPlaying) hitVfx.Play();
            }
        }
    }


    void Update()
    {
        // Giữ mục tiêu / chọn mới
        MaintainTarget();

        float aimErr;
        bool aimed = FaceTarget(out aimErr);

        if (!_beaming && _target != null && Time.time >= _nextShotTime)
        {
            if (!requireAimBeforeFiring || aimed)
            {
                _beamCo = StartCoroutine(BeamRoutine());
            }
            // nếu yêu cầu ngắm trước nhưng chưa “aimed”: đợi khung sau, tower tiếp tục xoay tới
        }

        // Xoay nhẹ về mục tiêu (cho đẹp)
        //FaceTarget();

        // Chờ đến chu kỳ kế tiếp & có mục tiêu & chưa có beam
        if (!_beaming && _target != null && Time.time >= _nextShotTime)
        {
            _beamCo = StartCoroutine(BeamRoutine());
        }

        // Khi đang bắn, cập nhật đầu/đuôi tia mỗi frame
        if (_beaming && line)
        {
            Vector3 from = firePoint ? firePoint.position : transform.position;
            Vector3 to = GetLaserEndPoint();
            //line.SetPosition(0, from);
            //line.SetPosition(1, to);
        }
    }

    // ====== Core ======
    IEnumerator BeamRoutine()
    {
        _beaming = true;
        float start = Time.time;
        float end = start + Mathf.Max(0.05f, beamDuration);
        float nextTick = Time.time;

        if (line) line.enabled = true;
        if (debugLog) Debug.Log($"[{name}] Laser START");

        bool earlyStop = false;

        while (true)
        {
            // 1) Nếu target hiện tại chết/ra khỏi tầm → (có thể) retarget
            if (_target == null || _target.IsDead || !IsInsideRange(_target.transform.position))
            {
                if (retargetDuringBeam)
                    _target = AcquireClosestTargetInRange();

                // Không còn enemy trong tầm → dừng tia NGAY LẬP TỨC
                if (_target == null)
                {
                    earlyStop = true;
                    break;
                }
            }
            float _; FaceTarget(out _);
            // 2) Tick damage theo interval
            if (Time.time >= nextTick)
            {
                ApplyTickDamage();                              // sẽ no-op nếu _target null ở trên
                nextTick += Mathf.Max(0.01f, tickInterval);
            }

            // 3) Hết thời gian beam → dừng bình thường
            if (Time.time >= end) break;

            yield return null;
        }

        // Tắt beam
        if (line) line.enabled = false;
        if (hitVfx) hitVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        _beaming = false;

        // Khóa nhịp cho lần bắn sau:
        // - Early stop (không còn enemy): cho phép bắn lại NGAY khi có mục tiêu (không delay)
        // - Dừng bình thường: chờ period theo fireRate
        if (earlyStop)
        {
            _nextShotTime = Time.time;                         // sẵn sàng bắn lại ngay khi kiếm được mục tiêu
            if (debugLog) Debug.Log($"[{name}] Laser EARLY STOP (no enemy).");
        }
        else
        {
            float period = 1f / Mathf.Max(0.0001f, fireRate);
            _nextShotTime = Time.time + period;
            if (debugLog) Debug.Log($"[{name}] Laser STOP, next in {period:F2}s.");
        }
    }


    void ApplyTickDamage()
    {
        if (_target == null || _target.IsDead) return;

        float tickDmg;
        if (damagePerTickOverride > 0f)
        {
            tickDmg = damagePerTickOverride;
        }
        else
        {
            // DPS = CurrentDamage * dpsMultiplier, nên mỗi tick: DPS * tickInterval
            float dps = (towerRef ? towerRef.CurrentDamage : 10f) * dpsMultiplier;
            tickDmg = dps * Mathf.Max(0.01f, tickInterval);
        }

        _target.TakeDamage(tickDmg);

        // Hit VFX ở điểm kết thúc tia
        if (hitVfx)
        {
            var to = GetLaserEndPoint();
            hitVfx.transform.position = to;
            hitVfx.transform.localScale = Vector3.one * hitVfxScale;
            if (!hitVfx.isPlaying) hitVfx.Play();
        }
    }

    // ====== Targeting ======
    void MaintainTarget()
    {
        if (_target == null || _target.IsDead || !IsInsideRange(_target.transform.position))
        {
            _target = AcquireClosestTargetInRange();
        }
    }

    EnemyController AcquireClosestTargetInRange()
    {
        EnemyController best = null;
        float bestSqr = float.MaxValue;
        int n = Physics.OverlapSphereNonAlloc(transform.position, range, _overlapBuf, enemyMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < n; i++)
        {
            var c = _overlapBuf[i];
            var ec = c.GetComponentInParent<EnemyController>() ?? c.GetComponent<EnemyController>();
            if (ec == null || ec.IsDead) continue;

            float d2 = (ec.transform.position - transform.position).sqrMagnitude;
            if (d2 <= range * range && d2 < bestSqr) { bestSqr = d2; best = ec; }
        }

        if (best == null)
        {
            var tagged = GameObject.FindGameObjectsWithTag(enemyTag);
            foreach (var go in tagged)
            {
                var ec = go.GetComponent<EnemyController>();
                if (ec == null || ec.IsDead) continue;
                float d2 = (ec.transform.position - transform.position).sqrMagnitude;
                if (d2 <= range * range && d2 < bestSqr) { bestSqr = d2; best = ec; }
            }
        }
        return best;
    }

    bool IsInsideRange(Vector3 pos) => (pos - transform.position).sqrMagnitude <= range * range;

    // ====== Aiming/Visual ======
    Vector3 GetLaserEndPoint()
    {
        if (_target != null)
        {
            // ngắm hơi lên 1 chút để đẹp (như bullet)
            var t = _target.transform;
            // nếu Enemy có child "Target" thì có thể tìm và lấy vị trí đó thay vì cộng y
            return t.position + Vector3.up * 0.6f;
        }
        // nếu không có mục tiêu, vẽ ra xa theo hướng nhìn
        var from = firePoint ? firePoint.position : transform.position;
        var fwd = (yawPivot ? yawPivot.forward : transform.forward);
        return from + fwd * range;
    }

    // ❗ REPLACE toàn bộ FaceTarget() cũ:
    // Giữ helper này như cũ:
    Transform FindChildDeep(Transform root, string childName)
    {
        if (!root || string.IsNullOrEmpty(targetChildName)) return null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t && t.name == targetChildName) return t;
        return null;
    }
    Vector3 GetAimPos()
    {
        if (_target == null) return (firePoint ? firePoint.position + (yawPivot ? yawPivot.forward : transform.forward) * 1f : transform.position);
        var t = _target.transform;
        var tp = (!string.IsNullOrEmpty(targetChildName)) ? FindChildDeep(t, targetChildName) : null;
        return tp ? tp.position : t.position + Vector3.up * aimYOffset;
    }

    // ❗ REPLACE toàn bộ FaceTarget() cũ:
    bool FaceTarget(out float errDeg)
    {
        errDeg = 0f;
        if (_target == null) return false;

        Vector3 aimPos = GetAimPos();

        if (rotateWholeTower)
        {
            // ——— Xoay cả cây (root) ———
            Vector3 to = aimPos - transform.position;
            if (lockUpright) to.y = 0f; // chỉ yaw nếu cần giữ thẳng cây

            if (to.sqrMagnitude < 1e-6f) { errDeg = 0f; return true; }

            Quaternion want = Quaternion.LookRotation(to.normalized, Vector3.up);
            // Tính sai số trước khi quay để so sánh
            errDeg = Quaternion.Angle(transform.rotation, want);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, wholeTurnDegPerSec * Time.deltaTime);

            return errDeg <= aimToleranceDeg;
        }
        else
        {
            // ——— Giữ logic yaw/pitch pivot như bạn đang có ———
            // YAW
            Transform yaw = yawPivot ? yawPivot : transform;
            Vector3 to = aimPos - yaw.position; Vector3 flat = to; flat.y = 0f;
            if (flat.sqrMagnitude > 1e-6f)
            {
                Quaternion wantYaw = Quaternion.LookRotation(flat.normalized, Vector3.up);
                yaw.rotation = Quaternion.RotateTowards(yaw.rotation, wantYaw, yawDegPerSec * Time.deltaTime);
            }

            // PITCH (nếu cấu hình)
            if (usePitch && pitchPivot)
            {
                Vector3 localDir = pitchPivot.InverseTransformDirection(aimPos - pitchPivot.position);
                float desiredPitch = Mathf.Atan2(localDir.y, new Vector2(localDir.x, localDir.z).magnitude) * Mathf.Rad2Deg;
                desiredPitch = Mathf.Clamp(desiredPitch, pitchClamp.x, pitchClamp.y);

                Quaternion currentLocal = pitchPivot.localRotation;
                Quaternion targetLocal = Quaternion.Euler(desiredPitch, 0f, 0f);
                pitchPivot.localRotation = Quaternion.RotateTowards(currentLocal, targetLocal, pitchDegPerSec * Time.deltaTime);
            }

            // Ước sai số theo góc giữa forward và hướng mục tiêu (mặt phẳng XZ)
            Vector3 fwd = (yawPivot ? yawPivot.forward : transform.forward);
            Vector3 flatFwd = fwd; flatFwd.y = 0f;
            Vector3 flatTo = (aimPos - (yawPivot ? yawPivot.position : transform.position)); flatTo.y = 0f;
            if (flatFwd.sqrMagnitude > 1e-6f && flatTo.sqrMagnitude > 1e-6f)
                errDeg = Vector3.Angle(flatFwd, flatTo);

            return errDeg <= aimToleranceDeg;
        }
    }



    void OnDisable()
    {
        _beaming = false;
        if (_beamCo != null) StopCoroutine(_beamCo);
        if (line) line.enabled = false;
        if (hitVfx) hitVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
