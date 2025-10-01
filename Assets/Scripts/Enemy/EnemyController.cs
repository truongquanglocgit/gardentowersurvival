using UnityEngine;
using System.Collections;
using UnityEngine.AI;

// ❌ BỎ RequireComponent Animator (để cho phép Animator ở child)
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    [Header("Death Physics")]
    [Tooltip("Raycast từ trên xuống để kẹp enemy lên mặt đất khi chết.")]
    public float groundStickRayHeight = 3f;
    public float groundStickYOffset = 0.02f;
    public LayerMask groundStickMask = ~0;

    [Tooltip("Khi chết có tắt collider để không cản đường không.")]
    public bool disableCollidersOnDeath = true;

    [Tooltip("Chuyển enemy sang layer khác khi chết để đạn/agent bỏ qua (nếu cần).")]
    public string deadLayerName = "DeadEnemy"; // tạo layer này nếu muốn, hoặc để trống = không đổi

    [Header("Death VFX (Shared)")]
    [Tooltip("Điểm tham chiếu để đặt VFX chết. Để trống sẽ dùng transform của enemy.")]
    public Transform deathVFXAnchor;
    public Vector3 deathVFXPositionOffset = Vector3.zero;
    public Vector3 deathVFXEulerOffset = Vector3.zero;
    public Vector3 deathVFXScale = Vector3.one;
    [Header("Death VFX (Embedded / Pool)")]
    [Tooltip("Nếu gán: ưu tiên dùng VFX có sẵn trong prefab enemy (để sẵn ở child và DISABLE).")]
    [SerializeField] private ParticleSystem embeddedDeathVFX;
    [Tooltip("Home (parent) để trả VFX về sau khi phát xong. Để trống sẽ dùng deathVFXAnchor hoặc chính enemy.")]
    [SerializeField] private Transform embeddedVFXHome;
    [Header("Death VFX")]
    [Tooltip("Particle VFX khi enemy chết.")]
    public ParticleSystem deathVFXPrefab;
    [Tooltip("Bắt buộc tách nguồn Death SFX ra khỏi enemy để không bị cắt khi disable.")]
    public bool forceDetachDeathSFX = true;
    

    [Tooltip("Nếu true: parent VFX vào anchor (sẽ tắt nếu enemy bị disable). Nếu false: world-space, độc lập.")]
    public bool deathVFXParentToAnchor = false;

    [Tooltip("Tự hủy object VFX dựa trên lifetime của particle.")]
    public bool deathVFXAutoDestroy = true;

    [Header("Attack FX / SFX")]
    [Tooltip("Âm khi đánh trúng player (impact). Bỏ trống => không phát.")]
    public AudioClip attackImpactSFX;
    [Range(0f, 1f)] public float attackSFXVolume = 0.9f;
    [Tooltip("Random pitch cho SFX cho tự nhiên hơn.")]
    public bool sfxRandomizePitch = true;
    public Vector2 sfxPitchRange = new Vector2(0.95f, 1.05f);
    [Header("Death SFX")]
    [Tooltip("Âm khi enemy chết. Bỏ trống => không phát.")]
    public AudioClip deathSFX;
    [Range(0f, 1f)] public float deathSFXVolume = 0.9f;

    [Tooltip("Ngẫu nhiên pitch khi chết cho tự nhiên hơn.")]
    public bool deathSFXRandomizePitch = true;
    public Vector2 deathSFXPitchRange = new Vector2(0.95f, 1.05f);

    [Tooltip("Nguồn phát one-shot. Để trống sẽ auto tạo. (Chỉ dùng khi không detach)")]
    public AudioSource deathAudioSource;

    [Tooltip("Nếu true: tạo 1 AudioSource rời tại vị trí chết rồi tự hủy (không bị cắt khi enemy tắt).")]
    public bool detachDeathSFX = true;
    [Tooltip("AudioSource cục bộ để phát one-shot. Nếu trống sẽ tự tạo.")]
    public AudioSource attackAudioSource; // gắn sẵn hoặc để null cho auto-create
    [Tooltip("Vị trí phát âm thanh/VFX khi impact (xương tay, miệng...). Nếu trống dùng transform của enemy).")]
    public Transform attackFXAnchor;

    [Tooltip("Hiệu ứng va chạm (ParticleSystem) khi đánh trúng player).")]
    public ParticleSystem attackImpactVFXPrefab;
    [Tooltip("Parent VFX vào anchor trong 1 frame ngắn. Nếu false -> spawn world-space không parent.")]
    public bool vfxParentToAnchor = false;
    [Tooltip("Auto destroy VFX theo duration của particle.")]
    public bool vfxAutoDestroy = true;

    // runtime
    private bool _playedImpactFXThisAttack;

    [Header("Base Stats")]
    public float hp = 1;
    public float damage = 1;
    public int seedReward = 1;

    [Header("Combat")]
    [Tooltip("Khoảng cách được coi là trong tầm tấn công")]
    public float attackRange = 2f;
    [Tooltip("Số đòn / giây")]
    public float attackSpeed = 1f;
    [Tooltip("Enemy ra khỏi tầm attackRange + buffer này mới quay lại CHASE")]
    public float attackExitBuffer = 0.5f;

    [Header("Turning")]
    [Tooltip("Độ/giây khi CHASE (xoay theo hướng chạy)")]
    public float turnSpeedRunDeg = 240f;
    [Tooltip("Độ/giây khi ATTACK (xoay mặt nhìn player)")]
    public float turnSpeedAtkDeg = 720f;
    [Range(0f, 1f)] public float dirLerp = 0.2f; // lọc hướng chạy

    [Header("Animator (on child)")]
    [Tooltip("Đặt Animator ở CHILD (model). Nếu để trống, script sẽ tự tìm Animator trong con.")]
    public Animator anim;                // ✅ Animator ở child
    public string isMovingParam = "IsMoving";
    public string attackTrigger = "Attack";
    public string dieTrigger = "Die";

    [Header("Death")]
    public float deathHideDelay = 0.8f;

    [Header("Damage Check")]
    [Tooltip("Bán kính kiểm tra player ở frame impact (OverlapSphere)")]
    public float impactCheckRadius = 1.5f;
    [Tooltip("Nếu không đặt Animation Event, dùng fallback normalized time trong clip Attack")]
    [Range(0f, 0.95f)] public float fallbackImpactNormalizedTime = 0.35f;
    const string LOGP = "[Enemy]";

    public bool IsDead { get; private set; }

    // wave / status
    private bool _countedDown;
    private bool _inWaveLifetime;
    private bool isSlowed;
    private float originalSpeed;

    private GameController gameController;
    private NavMeshAgent _agent;

    // Animator hashes
    private int _hIsMoving, _hAttack, _hDie;
    private bool _hasIsMoving, _hasDie; // cache tồn tại param

    // nhịp & state
    private float _nextAttackTime;
    private enum State { Chase, Attack, Cooldown, Dead }
    private State _state;
    private bool _attackInProgress;

    // target & facing
    private Transform _player;
    private PlayerController _playerCtrl;
    private Vector3 _lastMoveDir = Vector3.forward;
    [Header("Damage Text (local pool)")]
    [SerializeField] private DamageTextPool damageTextPool; // pool cục bộ của enemy
    [SerializeField] private Transform damageTextAnchor;          // có thể trùng anchor của pool
    [SerializeField] private Color normalDamageColor = new Color(0.95f, 0.15f, 0.15f);
    [SerializeField] private float damageTextYOffset = 0.05f;

    [Header("Model Root (for death pose)")]
    [SerializeField] private Transform modelRoot;   // <-- drag child model/Animator vào đây
    [SerializeField] private float deathTiltDegX = 180f; // xoay ngửa theo trục X (nằm ngửa)
    void Awake()
    {
        // ---- NavMeshAgent config ----
        _agent = GetComponent<NavMeshAgent>();
        _agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        _agent.avoidancePriority = 50;
        _agent.radius = 0.05f;
        _agent.stoppingDistance = Mathf.Max(0.01f, _agent.stoppingDistance);
        if (!damageTextPool) damageTextPool = GetComponentInChildren<DamageTextPool>(true);
        if (!damageTextAnchor && damageTextPool) damageTextAnchor = damageTextPool.damageAnchor;
        if (!deathVFXAnchor) deathVFXAnchor = transform;

        // ta tự xoay
        _agent.updateRotation = false;
        _agent.autoBraking = true;
        if (!embeddedVFXHome) embeddedVFXHome = deathVFXAnchor ? deathVFXAnchor : transform;
        if (embeddedDeathVFX) embeddedDeathVFX.gameObject.SetActive(false);

        // ✅ LẤY ANIMATOR Ở CHILD
        if (!anim) anim = GetComponentInChildren<Animator>(true);
        
        else
        {
            // tắt root motion để không kéo root khi chạy clip
            anim.applyRootMotion = false;

            _hIsMoving = Animator.StringToHash(isMovingParam);
            _hAttack = Animator.StringToHash(attackTrigger);
            _hDie = Animator.StringToHash(dieTrigger);

            // cache xem có param không để khỏi SetBool/Trigger sai
            foreach (var p in anim.parameters)
            {
                if (p.nameHash == _hIsMoving && p.type == AnimatorControllerParameterType.Bool) _hasIsMoving = true;
                if (p.nameHash == _hDie && p.type == AnimatorControllerParameterType.Trigger) _hasDie = true;
            }
        }
        // --- Attack audio init ---
        if (!attackAudioSource)
        {
            var go = new GameObject("AttackAudioSource");
            go.transform.SetParent(transform, false);
            attackAudioSource = go.AddComponent<AudioSource>();
            attackAudioSource.playOnAwake = false;
            attackAudioSource.loop = false;
            attackAudioSource.spatialBlend = 1f;               // 3D
            attackAudioSource.rolloffMode = AudioRolloffMode.Linear;
            attackAudioSource.minDistance = 3f;                // nghe ổn khi gần
            attackAudioSource.maxDistance = 25f;
            attackAudioSource.dopplerLevel = 0f;
            attackAudioSource.reverbZoneMix = 0f;
            attackAudioSource.volume = 1f;                     // tổng volume nguồn
        }
        if (deathSFX)
        {
            // Đảm bảo clip đã nạp vào bộ nhớ (Import: Decompress On Load + Preload Audio Data ON)
            if (!deathSFX.preloadAudioData) deathSFX.LoadAudioData();
        }
        if (!attackFXAnchor) attackFXAnchor = transform;
    }
    void EnsureDeathAudioSource()
    {
        if (deathAudioSource) return;

        var go = new GameObject("DeathAudioSource");
        go.transform.SetParent(transform, false);
        deathAudioSource = go.AddComponent<AudioSource>();
        deathAudioSource.playOnAwake = false;
        deathAudioSource.loop = false;
        deathAudioSource.spatialBlend = 1f;                // 3D
        deathAudioSource.rolloffMode = AudioRolloffMode.Linear;
        deathAudioSource.minDistance = 3f;
        deathAudioSource.maxDistance = 25f;
        deathAudioSource.dopplerLevel = 0f;
        deathAudioSource.reverbZoneMix = 0f;
        deathAudioSource.volume = 1f;
    }
    
    float EstimateLife(ParticleSystem ps)
    {
        var main = ps.main;
        float life = main.duration;
        switch (main.startLifetime.mode)
        {
            case ParticleSystemCurveMode.Constant:
                life += main.startLifetime.constant; break;
            case ParticleSystemCurveMode.TwoConstants:
                life += Mathf.Max(main.startLifetime.constantMin, main.startLifetime.constantMax); break;
            default:
                life += 1f; break;
        }
        return Mathf.Max(0.05f, life + 0.1f);
    }


    void OnDisable()
    {
        // Khi bị Disable mà chưa gọi Die() → vẫn phải trừ AliveCount
        // tránh trừ khi unload scene (scene invalid)
        if (!_countedDown && gameObject.scene.IsValid())
        {
            _countedDown = true;
            WaveRuntime.RegisterDeath(gameObject);
            //Debug.LogWarning($"{LOGP} DISABLE (no Die) {name} id={GetInstanceID()} -> alive={WaveRuntime.AliveCount}");
        }
    }
    void OnEnable()
    {
        IsDead = false;
        _countedDown = false;                  // ✅ reset để trừ 1 lần duy nhất
        _state = State.Chase;
        _attackInProgress = false;
        _nextAttackTime = 0f;

        var p = GameObject.FindGameObjectWithTag("Player");
        if (p) { _player = p.transform; _playerCtrl = p.GetComponent<PlayerController>(); }

        _agent.stoppingDistance = Mathf.Max(0.1f, attackRange * 0.9f);

        if (anim)
        {
            anim.ResetTrigger(_hAttack);
            if (_hasIsMoving) anim.SetBool(_hIsMoving, false);
        }

        //Debug.Log($"{LOGP} ENABLE {name} id={GetInstanceID()}");
    }
    static float GetTotalLifetime(ParticleSystem ps)
    {
        var main = ps.main;
        float life = main.duration;
        switch (main.startLifetime.mode)
        {
            case ParticleSystemCurveMode.Constant:
                life += main.startLifetime.constant; break;
            case ParticleSystemCurveMode.TwoConstants:
                life += Mathf.Max(main.startLifetime.constantMin, main.startLifetime.constantMax); break;
            default:
                life += 1f; break; // an toàn
        }
        return Mathf.Max(0.05f, life);
    }
    void TriggerDeathVFX()
    {
        // Lấy prefab VFX CHUNG từ WaveManager
        var shared = WaveManager.GetSharedDeathVFX();
        if (!shared || PoolManager.I == null) return;

        // Tính world transform
        Transform anchor = deathVFXAnchor ? deathVFXAnchor : transform;
        Vector3 worldPos = anchor.TransformPoint(deathVFXPositionOffset);
        Quaternion worldRot = anchor.rotation * Quaternion.Euler(deathVFXEulerOffset);

        // ✅ Lấy từ pool (KHÔNG parent ra ngoài, vẫn nằm dưới Pool_VFXName)
        var vfxGO = PoolManager.I.Get(shared.gameObject, worldPos, worldRot);
        var ps = vfxGO.GetComponent<ParticleSystem>();

        // Set scale world (vì parent là folder Pool, ta set trực tiếp localScale = world scale mong muốn)
        vfxGO.transform.localScale = deathVFXScale;

        ps.Clear(true);
        ps.Play(true);

        // ✅ Sau lifetime → Stop/Clear và set inactive, GIỮ NGUYÊN Ở THƯ MỤC POOL
        float life = GetTotalLifetime(ps);
        StartCoroutine(ReturnVFXToPoolAfter(ps, life));
    }

    IEnumerator ReturnVFXToPoolAfter(ParticleSystem ps, float wait)
    {
        yield return new WaitForSeconds(wait);
        if (!ps) yield break;

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        PoolManager.I.Return(ps.gameObject);   // ✅ không Destroy
    }
    void TriggerDeathSFX()
    {
        if (!deathSFX) return;

        // Vị trí phát: ưu tiên anchor của death VFX (trùng vị trí hiệu ứng chết)
        Transform anchor = deathVFXAnchor ? deathVFXAnchor : transform;
        Vector3 worldPos = anchor.TransformPoint(deathVFXPositionOffset);

        // Pitch ngẫu nhiên nhẹ cho tự nhiên
        float pitch = deathSFXRandomizePitch ? Random.Range(deathSFXPitchRange.x, deathSFXPitchRange.y) : 1f;

        if (forceDetachDeathSFX)
        {
            // ► Tạo nguồn âm thanh tách rời để không bị cắt khi enemy disable
            AudioSource src = CreateDetachedOneShotSource(worldPos);
            src.pitch = pitch;
            src.PlayOneShot(deathSFX, Mathf.Clamp01(deathSFXVolume));

            // Tính thời lượng sống ước lượng theo clip/pitch (clip.length có thể 0 nếu stream)
            float life = deathSFX.length > 0f ? deathSFX.length / Mathf.Max(0.01f, Mathf.Abs(pitch)) : 1.2f;
            StartCoroutine(ReleaseDetachedAfter(src, life + 0.05f));
        }
        else
        {
            // ► Phát bằng AudioSource gắn trên enemy (có thể bị cắt nếu disable sớm)
            EnsureDeathAudioSource();
            deathAudioSource.transform.position = worldPos;
            deathAudioSource.pitch = pitch;
            deathAudioSource.PlayOneShot(deathSFX, Mathf.Clamp01(deathSFXVolume));
        }
    }
    // === Helpers ===
    AudioSource CreateDetachedOneShotSource(Vector3 pos)
    {
        var go = new GameObject("DeathSFX_OneShot");
        go.transform.position = pos;

        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;
        src.spatialBlend = 1f;                     // 3D
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 3f;
        src.maxDistance = 25f;
        src.dopplerLevel = 0f;
        src.reverbZoneMix = 0f;
        src.volume = 1f;                           // volume tổng, mức clip nằm ở PlayOneShot()
        return src;
    }

    IEnumerator ReleaseDetachedAfter(AudioSource src, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (src) Destroy(src.gameObject);
    }
    void Update()
    {
        if (IsDead || !_player) return;

        float dist = Vector3.Distance(transform.position, _player.position);
        bool inEnterRange = dist <= attackRange;
        bool inExitRange = dist <= (attackRange + attackExitBuffer);

        switch (_state)
        {
            case State.Chase:
                if (_agent.enabled) _agent.isStopped = false;
                if (_agent.enabled) _agent.SetDestination(_player.position);
                UpdateMoveFacing(turnSpeedRunDeg);

                if (inEnterRange && Time.time >= _nextAttackTime)
                    EnterAttack();
                break;

            case State.Attack:
                if (_agent.enabled) _agent.isStopped = true;
                FacePlayer(turnSpeedAtkDeg);

                if (!inExitRange)
                {
                    _attackInProgress = false; // huỷ đòn hiện tại
                    _state = State.Chase;
                }
                break;

            case State.Cooldown:
                if (!inExitRange) { _state = State.Chase; break; }
                FacePlayer(turnSpeedAtkDeg);

                if (Time.time >= _nextAttackTime)
                {
                    EnterAttack();
                    return;
                }
                break;
        }

        // Lưới an toàn: hết state Attack mà chưa impact → cho 1 hit và vào cooldown
        if (_state == State.Attack && _attackInProgress && anim)
        {
            var st = anim.GetCurrentAnimatorStateInfo(0);
            if (st.normalizedTime >= 0.98f)
            {
                TriggerAttackImpactFX();     // NEW
                DoDamageIfInRange();
                _attackInProgress = false;
                _state = State.Cooldown;
            }
        }


        // cập nhật IsMoving cho animator (ở CHILD)
        if (anim && _hasIsMoving)
        {
            bool moving = _state == State.Chase && _agent.enabled && _agent.velocity.sqrMagnitude > 0.005f;
            anim.SetBool(_hIsMoving, moving);
        }
    }

    // ====== State helpers ======
    void EnterAttack()
    {
        _playedImpactFXThisAttack = false; // reset mỗi đòn
        _state = State.Attack;
        _attackInProgress = true;

        FacePlayer(turnSpeedAtkDeg);

        if (anim)
        {
            anim.ResetTrigger(_hAttack);
            anim.SetTrigger(_hAttack);
        }

        // đặt mốc cooldown cho lần kế tiếp
        float cd = 1f / Mathf.Max(attackSpeed, 0.0001f);
        _nextAttackTime = Time.time + cd;

        StopCoroutine(nameof(FallbackImpactRoutine));
        StartCoroutine(nameof(FallbackImpactRoutine));
    }

    IEnumerator FallbackImpactRoutine()
    {
        // chờ animator ổn định tối đa 0.25s
        float t = 0f;
        while (_attackInProgress && t < 0.25f) { t += Time.deltaTime; yield return null; }

        while (_attackInProgress)
        {
            if (!anim) { DoDamageIfInRange(); _attackInProgress = false; _state = State.Cooldown; yield break; }

            var st = anim.GetCurrentAnimatorStateInfo(0);
            if (st.normalizedTime >= fallbackImpactNormalizedTime && st.normalizedTime < 0.98f)
            {
                TriggerAttackImpactFX();     // NEW
                DoDamageIfInRange();
                _attackInProgress = false;
                _state = State.Cooldown;
                yield break;
            }
            if (st.normalizedTime >= 0.98f)
            {
                TriggerAttackImpactFX();     // NEW
                DoDamageIfInRange();
                _attackInProgress = false;
                _state = State.Cooldown;
                yield break;
            }
            yield return null;
        }
    }

    // ====== Animation Event (gọi trong clip Attack của CHILD Animator) ======
    public void OnAttackImpact()
    {
        TriggerAttackImpactFX();       // NEW: phát SFX/VFX một lần
        DoDamageIfInRange();
        _attackInProgress = false;
        _state = State.Cooldown;
    }
    void TriggerAttackImpactFX()
    {
        if (_playedImpactFXThisAttack) return;
        _playedImpactFXThisAttack = true;

        Vector3 pos = attackFXAnchor ? attackFXAnchor.position : transform.position;
        Quaternion rot = attackFXAnchor ? attackFXAnchor.rotation : transform.rotation;

        // --- SFX ---
        if (attackImpactSFX && attackAudioSource)
        {
            attackAudioSource.transform.position = pos;
            attackAudioSource.pitch = sfxRandomizePitch ? Random.Range(sfxPitchRange.x, sfxPitchRange.y) : 1f;
            attackAudioSource.PlayOneShot(attackImpactSFX, Mathf.Clamp01(attackSFXVolume));
        }

        // --- VFX ---
        if (attackImpactVFXPrefab)
        {
            Transform parent = vfxParentToAnchor ? attackFXAnchor : null;
            var vfx = Instantiate(attackImpactVFXPrefab, pos, rot, parent);
            var main = vfx.main;
            vfx.Play(true);

            if (vfxAutoDestroy)
            {
                float life = main.duration;
                // cộng thêm startLifetime nếu là hằng
                if (main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants)
                    life += Mathf.Max(main.startLifetime.constantMin, main.startLifetime.constantMax);
                else
                    life += main.startLifetime.constant;

                Destroy(vfx.gameObject, life + 0.1f);
            }
        }
    }

    // ====== Damage ======
    void DoDamageIfInRange()
    {
        if (IsDead || _playerCtrl == null) return;

        float dist = Vector3.Distance(transform.position, _player.position);
        bool inRange = dist <= attackRange + 0.1f;

        if (!inRange)
        {
            var hits = Physics.OverlapSphere(transform.position, impactCheckRadius, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
                if (h.transform == _player || h.transform.root == _player)
                { inRange = true; break; }
        }

        if (inRange)
            _playerCtrl.TakeDamage(Mathf.RoundToInt(damage));
    }

    // ====== Facing helpers (xoay ROOT; child Animator sẽ xoay theo) ======
    void UpdateMoveFacing(float turnSpeedDeg)
    {
        Vector3 vel = _agent.velocity; vel.y = 0f;
        Vector3 wantDir = vel.sqrMagnitude > 0.0001f
            ? vel.normalized
            : (_agent.steeringTarget - transform.position).normalized;

        if (wantDir.sqrMagnitude > 0.0001f)
        {
            _lastMoveDir = Vector3.Slerp(_lastMoveDir, wantDir, dirLerp);
            float maxStep = turnSpeedDeg * Time.deltaTime;
            Quaternion target = Quaternion.LookRotation(_lastMoveDir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, maxStep);
        }
    }

    void FacePlayer(float turnSpeedDeg)
    {
        if (!_player) return;
        Vector3 dir = _player.position - transform.position; dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        float maxStep = turnSpeedDeg * Time.deltaTime;
        Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, maxStep);
    }

    // ====== HP / Die ======
    public void TakeDamage(float dmg)
    {
        if (IsDead) return;
        // Hiện số damage bằng pool local trước khi trừ máu
        TryShowDamageTextLocal(dmg);
        hp -= dmg;
        if (hp <= 0f) Die();
    }
    private void TryShowDamageTextLocal(float amount)
    {
        if (amount <= 0f || damageTextPool == null) return;

        // đồng bộ anchor nếu bạn muốn override tại EnemyController
        if (damageTextAnchor && damageTextPool.damageAnchor != damageTextAnchor)
            damageTextPool.damageAnchor = damageTextAnchor;

        damageTextPool.Spawn(amount, normalDamageColor, extraYOffset: damageTextYOffset);
    }
    public void Die()
    {
        if (IsDead) return;
        IsDead = true;
        _state = State.Dead;

        TriggerDeathSFX();
        TriggerDeathVFX();

        if (_agent) { _agent.isStopped = true; _agent.enabled = false; }

        if (anim)
        {
            if (_hasIsMoving) anim.SetBool(_hIsMoving, false);
            if (_hasDie) { anim.ResetTrigger(_hDie); anim.SetTrigger(_hDie); }
        }

        GameObject.FindWithTag("GameController")?
            .GetComponent<GameController>()?.AddSeed(seedReward);

        GetComponent<EnemyNavChase>()?.Die();

        if (!_countedDown)
        {
            _countedDown = true;
            WaveRuntime.RegisterDeath(gameObject);
        }

        // 🆕 Xoay ngược enemy khi chết (nằm ngửa ra sau)
        // Xoay NGỬA chỉ phần model, không xoay root
        if (!modelRoot && anim) modelRoot = anim.transform; // auto-fill
        if (modelRoot)
        {
            // giữ nguyên hướng Y hiện tại, chỉ ngửa ra sau theo X
            var r = modelRoot.rotation.eulerAngles; // world rot hiện tại
                                                    // cách 1: đặt tuyệt đối theo root + tilt X
            modelRoot.rotation = Quaternion.Euler(deathTiltDegX, transform.eulerAngles.y, 0f);
        }
        else
        {
            // fallback: nếu không có modelRoot, vẫn xoay root nhưng ít lệch hơn (xoay X thay vì Z)
            transform.rotation = Quaternion.Euler(deathTiltDegX, transform.eulerAngles.y, 0f);
        }
        // 🆕 Disable EnemyController để dừng toàn bộ Update/logic
        this.enabled = false;

        StartCoroutine(HideAfterDelay(deathHideDelay));
    


    // 🆕 Giữ xác đứng yên trên mặt đất
    FreezePhysicsAndStickToGround();
        FreezePhysics();     // như cũ nhưng KHÔNG đổi vị trí ở đây
        StickBoundsOnGround(); // kẹp theo đáy AABB thay vì theo pivot
        SetDeadLayerIfAny();
        StartCoroutine(HideAfterDelay(deathHideDelay));
    }

    void FreezePhysics()
    {
        var rb = GetComponent<Rigidbody>();
        if (rb)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
        if (disableCollidersOnDeath)
        {
            var cols = GetComponentsInChildren<Collider>(true);
            foreach (var c in cols) c.enabled = false;
        }
    }

    // GỘP bounds của toàn bộ Renderer để xác định đáy thực sự
    Bounds GetCombinedRenderBounds()
    {
        var rends = GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) return new Bounds(transform.position, Vector3.one * 0.2f);
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
    }

    // Kẹp sao cho đáy bounds dính mặt đất (min.y = groundY + offset)
    void StickBoundsOnGround()
    {
        // ray từ trên xuống để lấy groundY
        Vector3 rayOrigin = transform.position + Vector3.up * groundStickRayHeight;
        if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, groundStickRayHeight * 2f, groundStickMask, QueryTriggerInteraction.Ignore))
        {
            float groundY = hit.point.y;
            Bounds b = GetCombinedRenderBounds();   // đã tính theo world sau khi xoay

            float deltaUp = (groundY + groundStickYOffset) - b.min.y; // cần nâng lên bao nhiêu
            if (Mathf.Abs(deltaUp) > 0.0001f)
            {
                transform.position += new Vector3(0f, deltaUp, 0f);
            }
        }
        else
        {
            // fallback theo pivot nếu không bắn trúng đất (hiếm)
            Vector3 p = transform.position;
            p.y += groundStickYOffset;
            transform.position = p;
        }
    }
    void FreezePhysicsAndStickToGround()
    {
        // 🧊 Đóng băng vật lý để không rơi xuyên
        var rb = GetComponent<Rigidbody>();
        if (rb)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;         // kinematic = không bị physics kéo xuống
            rb.detectCollisions = false;   // tuỳ chọn, vì ta có thể tắt collider bên dưới
        }

        // 🔇 Tắt collider để không cản đường (tuỳ chọn)
        if (disableCollidersOnDeath)
        {
            var cols = GetComponentsInChildren<Collider>(true);
            foreach (var c in cols) c.enabled = false;
        }

        // 📌 Kẹp lên mặt đất bằng raycast từ trên xuống
        Vector3 rayOrigin = transform.position + Vector3.up * groundStickRayHeight;
        if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, groundStickRayHeight * 2f, groundStickMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 p = transform.position;
            p.y = hit.point.y + groundStickYOffset;
            transform.position = p;
        }
    }

    void SetDeadLayerIfAny()
    {
        if (string.IsNullOrEmpty(deadLayerName)) return;
        int deadLayer = LayerMask.NameToLayer(deadLayerName);
        if (deadLayer < 0) return; // layer chưa tồn tại → bỏ qua

        // đổi layer cho root + toàn bộ children
        SetLayerRecursively(transform, deadLayer);
    }

    static void SetLayerRecursively(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i), layer);
    }


    public void Init(float powerMultiplier)
    {
        hp *= powerMultiplier;
        damage *= powerMultiplier;
        seedReward = Mathf.RoundToInt(seedReward * powerMultiplier);
    }

    public void BeginWaveLifetime() => _inWaveLifetime = true;

    IEnumerator HideAfterDelay(float t)
    {
        yield return new WaitForSeconds(t);
        gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0.3f, 0.2f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, impactCheckRadius);
    }
#endif

    #region Status Effects
    public void ApplySlow(float slowFactor, float duration)
    {
        if (!isSlowed)
        {
            originalSpeed = _agent ? _agent.speed : 0f;
            if (_agent) _agent.speed *= slowFactor;
            isSlowed = true;
            StartCoroutine(RemoveSlow(duration));
        }
    }

    IEnumerator RemoveSlow(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (_agent) _agent.speed = originalSpeed;
        isSlowed = false;
    }

    public void ApplyBurn(float duration, float interval) => ApplyBurn(duration, interval, damage*25/100 );
    public void ApplyBurn(float duration, float interval, float damagePerTick)
    {
        StartCoroutine(BurnCoroutine(duration, interval, damagePerTick));
    }

    IEnumerator BurnCoroutine(float duration, float interval, float damagePerTick)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            TakeDamage(damagePerTick);
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }
    }

    public void ApplyStun(float duration)
    {
        if (!isSlowed && _agent)
        {
            originalSpeed = _agent.speed;
            _agent.speed = 0f;
            isSlowed = true;
            StartCoroutine(RemoveStun(duration));
        }
    }

    IEnumerator RemoveStun(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (_agent) _agent.speed = originalSpeed;
        isSlowed = false;
    }

    public void ApplyPoison(float duration, float interval, float damage)
    {
        StartCoroutine(PoisonCoroutine(duration, interval, damage));
    }

    IEnumerator PoisonCoroutine(float duration, float interval, float damage)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            TakeDamage(damage);
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }
    }
    #endregion
}
