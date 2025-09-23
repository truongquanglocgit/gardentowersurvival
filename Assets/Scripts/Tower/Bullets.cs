using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class Bullets : MonoBehaviour
{
    [Header("AOE")]
    
    public bool isAoeDamage = false;
    [Min(0f)] public float aoeRadius = 0f;
    public LayerMask enemyMask = ~0;

    [Header("AOE Fallback (no-collider)")]
    [SerializeField] bool aoeEnableFallbackNoCollider = true; // ✅ bật Fallback theo EnemyController
    [SerializeField] int aoeMaxPerHit = 64;                  // giới hạn nạn nhân/lần nổ
    [SerializeField] bool debugAoe = false;                   // log & vẽ debug

    [Header("AOE Safeguards")]
    [SerializeField] bool aoeUsePhysicsOnly = true; // ✅ chỉ dựa vào OverlapSphere, KHÔNG fallback theo tag
    [SerializeField] bool aoeClampByDistanceOnEach = true; // ✅ double-check từng enemy theo distance
    
    // --- Arc impact lock ---
    Vector3 impactLockPos;      // điểm rơi được "khóa" khi bắt đầu hạ
    float descendTimeout = 2.5f;   // tối đa X giây cho pha hạ
    float descendTimer = 0f;
    // ====== VFX Prefabs ======
    [Header("VFX")]
    [Tooltip("Hiệu ứng lúc bắn ở nòng (muzzle flash / start explosion)")]
    [SerializeField] private ParticleSystem startVFXPrefab;
    [Tooltip("Hiệu ứng va chạm khi đạn trúng mục tiêu")]
    [SerializeField] private ParticleSystem hitVFXPrefab;

    [Tooltip("Có nên parent Start VFX vào firePoint để bám nòng trong khoảnh khắc?")]
    [SerializeField] private bool parentStartVFXToFirePoint = false;

    [Tooltip("Nếu không dùng pool VFX: Auto destroy object sau thời lượng particle")]
    [SerializeField] private bool autoDestroyVFX = true;
    // +++++ AOE → VFX SCALING +++++
    [Header("AOE VFX Scaling")]
    [SerializeField] bool scaleStartVFXWithAoe = false;   // bật nếu muốn muzzle cũng to ra theo AOE
    [SerializeField] bool scaleHitVFXWithAoe = true;    // thường bật: vụ nổ to bằng bán kính AOE
    [Min(0.01f)][SerializeField] float baseVFXScale = 1f;   // scale gốc
    [Min(0f)][SerializeField] float vfxScalePerMeter = 1.0f; // mỗi 1m radius tăng thêm bao nhiêu scale
    [SerializeField] float vfxMinScale = 0.4f;
    [SerializeField] float vfxMaxScale = 8f;

    [SerializeField] bool setShapeRadius = true;          // nếu prefab dùng Shape (Sphere/Circle), set radius luôn
    [Range(0f, 2f)][SerializeField] float shapeRadiusFactor = 1.0f; // nhân lên/giảm bớt so với aoeRadius

    // ====== SFX Clips ======
    [Header("SFX")]
    [Tooltip("Âm lúc bắn ở nòng (muzzle/shot). Bỏ trống thì không phát.")]
    [SerializeField] private AudioClip startSFX;
    [Tooltip("Âm va chạm khi trúng mục tiêu. Bỏ trống thì không phát.")]
    [SerializeField] private AudioClip hitSFX;
    [Range(0f, 1f)][SerializeField] private float sfxVolume = 0.9f;

    [Tooltip("Dùng pool AudioSource để tránh GC khi phát âm thanh 1-shot.")]
    [SerializeField] private bool useAudioPool = true;
    [Tooltip("Prefab AudioSource 1-shot (spatialBlend=1). Bỏ trống sẽ fallback PlayClipAtPoint.")]
    [SerializeField] private AudioSource audioOneShotPrefab;
    [SerializeField] private int audioPoolCapacity = 8;
    // === ROUTE SFX QUA AUDIOSOURCE (thay vì PlayClipAtPoint) ===
    [Header("SFX Routing (AudioSource)")]
    [SerializeField] private AudioSource startSFXSource; // phát tiếng ở nòng súng
    [SerializeField] private AudioSource hitSFXSource;   // phát tiếng va chạm
    // ====== Aim & Flight ======
    [Header("Aim")]
    [SerializeField] float defaultAimYOffset = 0.6f;
    [SerializeField] bool orientToVelocity = true;

    [Header("Speed")]
    [SerializeField] float baseSpeed = 15f;

    [Header("Activation")]
    [Tooltip("Khi OnEnable, bật lại toàn bộ child, renderer, collider, particle, trail.")]
    [SerializeField] bool forceEnableHierarchyOnEnable = true;

    // ====== Runtime ======
    private Transform target;
    private Transform firePoint;
    private Vector3 shotAimOffset;
    private Vector3 lastKnownTargetPos;

    public TowerController towerController;
    public float speed;
    public float damage;
    public BulletType bulletType = BulletType.Normal;

    private Rigidbody rb;

    // chống double-hit trong 1 vòng đời
    private bool _spent;

    // cache hierarchy
    Renderer[] _renderers;
    Collider[] _colliders;
    ParticleSystem[] _particles;
    TrailRenderer[] _trails;

    enum FlightMode { Straight, ArcAscend, ArcDescend }
    FlightMode mode = FlightMode.Straight;

    // Arc params
    bool terminalHoming = true;
    float ascendDuration = 0.25f;
    float descendSpeedMul = 1.2f;
    float arcTimer = 0f;
    Vector3 arcStartPos;
    Vector3 arcApexPos;

    // ====== Audio Pool (static cho nhẹ) ======
    private static Queue<AudioSource> _audioPool;
    private static Transform _audioPoolRoot;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        _renderers = GetComponentsInChildren<Renderer>(true);
        _colliders = GetComponentsInChildren<Collider>(true);
        _particles = GetComponentsInChildren<ParticleSystem>(true);
        _trails = GetComponentsInChildren<TrailRenderer>(true);

        var selfCol = GetComponent<Collider>();
        if (selfCol) selfCol.isTrigger = true;

        // Chuẩn bị audio pool nếu cần
        if (useAudioPool && audioOneShotPrefab && _audioPool == null)
        {
            _audioPool = new Queue<AudioSource>(audioPoolCapacity);
            _audioPoolRoot = new GameObject("[AudioOneShotPool]").transform;
            for (int i = 0; i < audioPoolCapacity; i++)
            {
                var src = Instantiate(audioOneShotPrefab, _audioPoolRoot);
                src.playOnAwake = false;
                src.Stop();
                src.gameObject.SetActive(false);
                _audioPool.Enqueue(src);
            }
        }
        EnsureSFXSources(); // 🔊 chuẩn bị 2 AudioSource dùng chung cho viên đạn
    }

    void OnEnable()
    {
        _spent = false;
        mode = FlightMode.Straight;
        arcTimer = 0f;

        if (rb)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (forceEnableHierarchyOnEnable)
            ForceEnableHierarchy();
    }
    float ComputeAoeVFXScale()
    {
        float s = baseVFXScale + aoeRadius * vfxScalePerMeter;
        return Mathf.Clamp(s, vfxMinScale, vfxMaxScale);
    }

    void ApplyAoeScaling(ParticleSystem fx)
    {
        if (!fx) return;

        // đảm bảo transform scale ảnh hưởng tới particle
        var main = fx.main;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        // scale transform theo bán kính AOE
        float s = ComputeAoeVFXScale();
        fx.transform.localScale = Vector3.one * s;

        // nếu VFX có Shape (Sphere/Cone/Circle) → chỉnh bán kính để vòng nổ “chạm mép” AOE
        if (setShapeRadius)
        {
            var shape = fx.shape;
            if (shape.enabled)
            {
                shape.radius = aoeRadius * shapeRadiusFactor;
            }
        }
    }

    void Start()
    {
        GetStat();
        if (!rb) rb = GetComponent<Rigidbody>();
        if (rb)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    public void GetStat()
    {
        if (towerController)
        {
            speed = Mathf.Max(towerController.minBulletSpeed, baseSpeed);
            damage = towerController.CurrentDamage;
        }
        else
        {
            speed = Mathf.Max(baseSpeed, 1f);
        }
    }

    /// <summary>
    /// Gọi ngay khi spawn đạn. Phát Start VFX/SFX tại firePoint nếu có.
    /// </summary>
    public void SetTarget(Transform t, Transform fireOrigin)
    {
        target = t;
        firePoint = fireOrigin;
        shotAimOffset = new Vector3(0f, defaultAimYOffset, 0f);
        if (t) lastKnownTargetPos = t.position + shotAimOffset;

        // --- START VFX/SFX ---
        if (firePoint)
        {
            if (startVFXPrefab)
            {
                var v = Instantiate(startVFXPrefab, firePoint.position, firePoint.rotation,
                                    parentStartVFXToFirePoint ? firePoint : null);
                if (scaleStartVFXWithAoe && isAoeDamage) ApplyAoeScaling(v);
                v.Play(true);
                if (autoDestroyVFX) Destroy(v.gameObject, v.main.duration + v.main.startLifetime.constant + 0.1f);
            }
            if (startSFX)
            {
                // was: PlayOneShotAt(startSFX, firePoint.position, sfxVolume);
                EnsureSFXSources();
                PlaySFX(startSFXSource, startSFX, firePoint.position, sfxVolume);
            }
        }

    }

    public void SetTargetWithOffset(Transform t, Transform fireOrigin, float yOffset)
    {
        target = t;
        firePoint = fireOrigin;
        shotAimOffset = new Vector3(0f, yOffset, 0f);
        if (t) lastKnownTargetPos = t.position + shotAimOffset;

        // --- START VFX/SFX ---
        if (firePoint)
        {
            if (startVFXPrefab)
            {
                Transform parent = parentStartVFXToFirePoint ? firePoint : null;
                SpawnVFX(startVFXPrefab, firePoint.position, firePoint.rotation, parent);
            }
            if (startSFX)
            {
                // was: PlayOneShotAt(startSFX, firePoint.position, sfxVolume);
                EnsureSFXSources();
                PlaySFX(startSFXSource, startSFX, firePoint.position, sfxVolume);
            }
        }

    }

    public void StartArcFlight(float apexHeight, float upTime, float descendSpeedMultiplier = 1.2f, bool terminalHoming = true)
    {
        this.ascendDuration = Mathf.Max(0.06f, upTime);
        this.descendSpeedMul = Mathf.Max(0.2f, descendSpeedMultiplier);
        this.terminalHoming = terminalHoming;

        arcStartPos = transform.position;

        Vector3 currentAim = (target ? target.position : transform.position) + shotAimOffset;
        lastKnownTargetPos = currentAim;

        Vector3 mid = (arcStartPos + currentAim) * 0.5f;
        float topY = Mathf.Max(arcStartPos.y, currentAim.y) + Mathf.Max(0.1f, apexHeight);
        arcApexPos = new Vector3(mid.x, topY, mid.z);

        // 🔒 Khóa điểm rơi mặc định ngay bây giờ
        impactLockPos = currentAim;

        mode = FlightMode.ArcAscend;
        arcTimer = 0f;
        descendTimer = 0f;
    }


    void Update()
    {
        if (_spent) return;

        if ((target == null || !target.gameObject.activeInHierarchy) && mode == FlightMode.Straight)
        {
            ResetBullet();
            return;
        }

        if (target != null && target.gameObject.activeInHierarchy)
            lastKnownTargetPos = target.position + shotAimOffset;

        switch (mode)
        {
            case FlightMode.Straight: TickStraight(); break;
            case FlightMode.ArcAscend: TickArcAscend(); break;
            case FlightMode.ArcDescend: TickArcDescend(); break;
        }
    }

    void TickStraight()
    {
        Vector3 aimPos = target.position + shotAimOffset;
        Vector3 dir = aimPos - transform.position;
        float step = speed * Time.deltaTime;

        if (dir.magnitude <= step)
        {
            Hit();
            return;
        }

        Vector3 move = dir.normalized * step;
        transform.position += move;

        if (orientToVelocity && move.sqrMagnitude > 0.000001f)
            transform.rotation = Quaternion.LookRotation(move, Vector3.up);
    }

    void TickArcAscend()
    {
        arcTimer += Time.deltaTime;
        float u = Mathf.Clamp01(arcTimer / ascendDuration);

        Vector3 a = Vector3.Lerp(arcStartPos, arcApexPos, u);
        Vector3 newPos = a;

        Vector3 move = newPos - transform.position;
        transform.position = newPos;

        if (orientToVelocity && move.sqrMagnitude > 0.000001f)
            transform.rotation = Quaternion.LookRotation(move.normalized, Vector3.up);

        if (u >= 1f) mode = FlightMode.ArcDescend;
    }

    void TickArcDescend()
    {
        // Cập nhật "điểm rơi" nếu cho phép homing
        if (terminalHoming && target && target.gameObject.activeInHierarchy)
            impactLockPos = target.position + shotAimOffset;

        Vector3 dir = impactLockPos - transform.position;
        float step = speed * descendSpeedMul * Time.deltaTime;

        // Điều kiện nổ:
        // - chạm/đủ gần điểm rơi
        // - hoặc đã hạ quá lâu (timeout)
        // - hoặc viên đạn rơi thấp hơn điểm rơi 0.1m (tránh lướt qua)
        descendTimer += Time.deltaTime;
        bool timeout = descendTimer >= descendTimeout;
        bool below = transform.position.y <= impactLockPos.y + 0.1f;

        if (dir.magnitude <= step || timeout || below)
        {
            Hit();
            return;
        }

        Vector3 move = dir.normalized * step;
        transform.position += move;

        if (orientToVelocity && move.sqrMagnitude > 0.000001f)
            transform.rotation = Quaternion.LookRotation(move, Vector3.up);
    }


    private void OnTriggerEnter(Collider other)
    {
        if (_spent) return;

        // ❗ Với ARC, bỏ qua va chạm giữa không trung để tránh nổ sớm
        if (mode == FlightMode.ArcAscend || mode == FlightMode.ArcDescend)
            return;

        if (other.CompareTag("Enemy"))
        {
            Hit(); // straight đụng là nổ
        }
    }

    void Hit()
    {
        if (_spent) return;
        _spent = true;

        Vector3 impactPos = transform.position;
        Quaternion impactRot = transform.rotation;

        // --- Gây damage ---
        if (isAoeDamage && aoeRadius > 0.01f)
        {
            LayerMask preferredMask = (enemyMask.value != 0)
                ? enemyMask
                : (LayerMask)LayerMask.GetMask("Enemy");   // 👈 ép kiểu int -> LayerMask

            var victims = _GetAoeVictims(impactPos, aoeRadius, preferredMask);
            foreach (var ec in victims) _ApplyBulletEffect(ec);
        }
        else
        {
            // Single target: ưu tiên target còn sống; nếu không có thì ray tìm gần nhất trong bán kính nhỏ
            EnemyController enemy = null;
            if (target) enemy = target.GetComponentInParent<EnemyController>();

            if (!enemy)
            {
                // fallback: tìm enemy gần điểm nổ nhất trong bán kính nhỏ
                var victims = _GetAoeVictims(impactPos, 0.6f, enemyMask);
                float best = float.PositiveInfinity;
                foreach (var ec in victims)
                {
                    float d2 = (ec.transform.position - impactPos).sqrMagnitude;
                    if (d2 < best) { best = d2; enemy = ec; }
                }
            }
            if (enemy && !enemy.IsDead) _ApplyBulletEffect(enemy);
        }

        // --- VFX/SFX ---
        if (hitVFXPrefab)
        {
            var v = Instantiate(hitVFXPrefab, impactPos, impactRot, null);
            if (scaleHitVFXWithAoe && isAoeDamage) ApplyAoeScaling(v);
            v.Play(true);
            if (autoDestroyVFX) Destroy(v.gameObject, v.main.duration + v.main.startLifetime.constant + 0.1f);
        }
        if (hitSFX) { EnsureSFXSources(); PlaySFX(hitSFXSource, hitSFX, impactPos, sfxVolume); }

        ResetBullet();
    }
    // Buffer/sets dùng chung
    static readonly Collider[] _buf = new Collider[128];
    static readonly HashSet<EnemyController> _unique = new HashSet<EnemyController>();

    List<EnemyController> _GetAoeVictims(Vector3 center, float radius, LayerMask preferredMask)
    {
        _unique.Clear();
        float r2 = radius * radius;
        int preferred = preferredMask.value;

        // 1) Thử physics trước (nếu sau này bạn thêm collider sẽ hoạt động ngay)
        int n = Physics.OverlapSphereNonAlloc(center, radius, _buf, ~0, QueryTriggerInteraction.Collide);
        int max = Mathf.Min(n, _buf.Length);

        for (int i = 0; i < max; i++)
        {
            var col = _buf[i]; if (!col) continue;
            // bỏ chính collider của viên đạn (nếu có)
            if (col.transform.IsChildOf(transform)) continue;

            EnemyController ec = null;
            if (col.attachedRigidbody) ec = col.attachedRigidbody.GetComponent<EnemyController>();
            if (!ec) ec = col.GetComponentInParent<EnemyController>() ?? col.GetComponent<EnemyController>();
            if (ec == null || ec.IsDead) continue;

            // nếu user set mask, yêu cầu collider hoặc root của enemy nằm trong mask
            if (preferred != 0)
            {
                int colBit = 1 << col.gameObject.layer;
                int ecBit = 1 << ec.gameObject.layer;
                if ((colBit & preferred) == 0 && (ecBit & preferred) == 0) continue;
            }

            // chốt bằng khoảng cách thật (closest point nếu có collider; nếu không dùng aim pos)
            Vector3 p = col.enabled ? col.ClosestPoint(center) : _GetEnemyAimPos(ec);
            if ((p - center).sqrMagnitude > r2) continue;

            _unique.Add(ec);
            if (_unique.Count >= aoeMaxPerHit) break;
        }

        // 2) Nếu KHÔNG thu được ai và cho phép fallback → quét theo EnemyController (KHÔNG cần collider)
        if (_unique.Count == 0 && aoeEnableFallbackNoCollider)
        {
            // chỉ lấy enemy đang active trong scene
            var all = FindObjectsOfType<EnemyController>(false);
            for (int i = 0; i < all.Length; i++)
            {
                var ec = all[i]; if (!ec || ec.IsDead) continue;

                // ❌ ĐỪNG lọc theo layer trong fallback (enemy root có thể không ở layer "Enemy")
                // if (preferred != 0) { ... }  ---> BỎ KHỐI NÀY

                // ✅ Lọc theo khoảng cách tới điểm aim/transform
                Vector3 p = _GetEnemyAimPos(ec);
                if ((p - center).sqrMagnitude > r2) continue;

                _unique.Add(ec);
                if (_unique.Count >= aoeMaxPerHit) break;
            }
        }


        if (debugAoe)
            Debug.Log($"[AOE] victims={_unique.Count} radius={radius} (preferredMask={preferredMask.value})");

        var list = new List<EnemyController>(_unique.Count);
        foreach (var ec in _unique) list.Add(ec);
        return list;
    }

    // Lấy điểm aim “đúng” ngay cả khi không có collider:
    // - Ưu tiên AimPoint tên như TowerController đang dùng
    // - Nếu không có, dùng transform.position + defaultYOffset của tower
    Vector3 _GetEnemyAimPos(EnemyController ec)
    {
        Transform t = ec.transform;
        string aimName = (towerController != null) ? towerController.aimPointChildName : "Target";
        if (!string.IsNullOrEmpty(aimName))
        {
            var aim = t.Find(aimName);
            if (aim) return aim.position;
        }
        float yOff = (towerController != null) ? towerController.defaultAimYOffset : 0.6f;
        return t.position + Vector3.up * yOff;
    }


    static Vector3 GetBestClosestPoint(EnemyController ec, Vector3 center)
    {
        Vector3 best = ec.transform.position;
        float bestD2 = float.PositiveInfinity;

        var cols = ec.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            var col = cols[i]; if (!col || !col.enabled) continue;
            Vector3 p = col.ClosestPoint(center);
            float d2 = (p - center).sqrMagnitude;
            if (d2 < bestD2) { bestD2 = d2; best = p; }
        }
        return best;
    }



    // Áp sát thương + hiệu ứng theo BulletType
    void _ApplyBulletEffect(EnemyController enemy)
    {
        switch (bulletType)
        {
            case BulletType.Normal: enemy.TakeDamage(damage); break;
            case BulletType.Fire: enemy.TakeDamage(damage); if (enemy.isActiveAndEnabled) enemy.ApplyBurn(5f, 1f); break;
            case BulletType.Ice: enemy.TakeDamage(damage); if (enemy.isActiveAndEnabled) enemy.ApplySlow(0.5f, 2f); break;
            case BulletType.Poison: if (enemy.isActiveAndEnabled) enemy.ApplyPoison(3f, 1f, 1f); break;
            case BulletType.Stun: enemy.TakeDamage(damage); if (enemy.isActiveAndEnabled) enemy.ApplyStun(2f); break;
        }
    }
    void ResetBullet()
    {
        target = null;
        mode = FlightMode.Straight;
        arcTimer = 0f;

        if (firePoint != null)
        {
            transform.position = firePoint.position;
            if (orientToVelocity) transform.rotation = firePoint.rotation;
        }

        gameObject.SetActive(false);
    }

    // ====== VFX Helpers ======
    private void SpawnVFX(ParticleSystem prefab, Vector3 pos, Quaternion rot, Transform parent)
    {
        if (!prefab) return;

        var vfx = Instantiate(prefab, pos, rot, parent);
        var main = vfx.main;
        vfx.Play(true);

        if (autoDestroyVFX)
        {
            float life = main.duration;
            // Đơn giản: cộng thêm startLifetime nếu là constant
            life += main.startLifetime.constant;
            Destroy(vfx.gameObject, life + 0.1f);
        }
    }

    // ====== SFX Helpers ======
    private void PlayOneShotAt(AudioClip clip, Vector3 pos, float volume)
    {
        EnsureSFXSources();
        PlaySFX(hitSFXSource, clip, pos, volume);
    }

    private System.Collections.IEnumerator ReturnAudioAfter(AudioSource src, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (src)
        {
            src.gameObject.SetActive(false);
            _audioPool.Enqueue(src);
        }
    }

    // ====== Visual Reset Helpers ======
    void ForceEnableHierarchy()
    {
        var trs = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < trs.Length; i++)
            trs[i].gameObject.SetActive(true);

        if (_renderers != null) for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i]) _renderers[i].enabled = true;

        if (_colliders != null) for (int i = 0; i < _colliders.Length; i++)
                if (_colliders[i]) _colliders[i].enabled = true;

        if (_trails != null) for (int i = 0; i < _trails.Length; i++)
            {
                var tr = _trails[i];
                if (!tr) continue;
                tr.Clear();
                tr.emitting = true;
                tr.enabled = true;
            }

        if (_particles != null) for (int i = 0; i < _particles.Length; i++)
            {
                var ps = _particles[i];
                if (!ps) continue;
                ps.Clear(true);
                ps.Play(true);
            }
    }
    void EnsureSFXSources()
    {
        if (!startSFXSource)
        {
            var go = new GameObject("StartSFXSource");
            go.transform.SetParent(transform, false);
            startSFXSource = go.AddComponent<AudioSource>();
            startSFXSource.playOnAwake = false;
            startSFXSource.spatialBlend = 1f;
            startSFXSource.rolloffMode = AudioRolloffMode.Linear;
            startSFXSource.maxDistance = 25f;
        }
        if (!hitSFXSource)
        {
            var go = new GameObject("HitSFXSource");
            go.transform.SetParent(transform, false);
            hitSFXSource = go.AddComponent<AudioSource>();
            hitSFXSource.playOnAwake = false;
            hitSFXSource.spatialBlend = 1f;
            hitSFXSource.rolloffMode = AudioRolloffMode.Linear;
            hitSFXSource.maxDistance = 25f;
        }
    }

    static void PlaySFX(AudioSource src, AudioClip clip, Vector3 worldPos, float volume = 1f)
    {
        if (!src || !clip) return;
        src.transform.position = worldPos;
        src.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    public enum BulletType { Normal, Fire, Ice, Poison, Stun }
}
