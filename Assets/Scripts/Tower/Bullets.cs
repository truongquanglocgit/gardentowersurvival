using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class Bullets : MonoBehaviour
{
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
                Transform parent = parentStartVFXToFirePoint ? firePoint : null;
                SpawnVFX(startVFXPrefab, firePoint.position, firePoint.rotation, parent);
            }
            if (startSFX)
            {
                PlayOneShotAt(startSFX, firePoint.position, sfxVolume);
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
                PlayOneShotAt(startSFX, firePoint.position, sfxVolume);
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

        mode = FlightMode.ArcAscend;
        arcTimer = 0f;
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
        Vector3 aimPos = lastKnownTargetPos;
        Vector3 dir = aimPos - transform.position;
        float step = speed * descendSpeedMul * Time.deltaTime;

        if (dir.magnitude <= step)
        {
            Hit();
            return;
        }

        Vector3 move = dir.normalized * step;
        transform.position += move;

        if (orientToVelocity && move.sqrMagnitude > 0.000001f)
            transform.rotation = Quaternion.LookRotation(move, Vector3.up);

        if ((target == null || !target.gameObject.activeInHierarchy) &&
            (transform.position - aimPos).sqrMagnitude <= step * step * 1.5f)
        {
            Hit();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_spent) return;
        if (other.CompareTag("Enemy"))
        {
            Hit();
        }
    }

    void Hit()
    {
        if (_spent) return;
        _spent = true;

        Vector3 impactPos = transform.position;
        Quaternion impactRot = transform.rotation;

        EnemyController enemy = null;
        if (target != null) enemy = target.GetComponentInParent<EnemyController>();

        if (enemy != null && !enemy.IsDead)
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

        // --- HIT VFX/SFX ---
        if (hitVFXPrefab) SpawnVFX(hitVFXPrefab, impactPos, impactRot, null);
        if (hitSFX) PlayOneShotAt(hitSFX, impactPos, sfxVolume);

        ResetBullet();
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
        if (!clip) return;

        if (useAudioPool && audioOneShotPrefab && _audioPool != null && _audioPool.Count > 0)
        {
            var src = _audioPool.Dequeue();
            src.transform.position = pos;
            src.gameObject.SetActive(true);
            src.spatialBlend = 1f;         // 3D
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.PlayOneShot(clip, Mathf.Clamp01(volume));
            StartCoroutine(ReturnAudioAfter(src, clip.length));
        }
        else
        {
            // Fallback nhanh gọn (có tạo GO tạm)
            AudioSource.PlayClipAtPoint(clip, pos, Mathf.Clamp01(volume));
        }
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

    public enum BulletType { Normal, Fire, Ice, Poison, Stun }
}
