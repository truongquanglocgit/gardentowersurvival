using UnityEngine;
using System.Collections;
using UnityEngine.AI;

// ❌ BỎ RequireComponent Animator (để cho phép Animator ở child)
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    [Header("Death VFX")]
    [Tooltip("Particle VFX khi enemy chết.")]
    public ParticleSystem deathVFXPrefab;

    [Tooltip("Anchor để căn vị trí/rotation cho death VFX. Để trống sẽ dùng transform của enemy.")]
    public Transform deathVFXAnchor;

    [Tooltip("Offset local theo anchor (m).")]
    public Vector3 deathVFXPositionOffset = Vector3.zero;

    [Tooltip("Offset local rotation theo anchor (deg Euler).")]
    public Vector3 deathVFXEulerOffset = Vector3.zero;

    [Tooltip("Scale áp cho VFX sau khi spawn.")]
    public Vector3 deathVFXScale = Vector3.one;

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

        if (!attackFXAnchor) attackFXAnchor = transform;
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
    void TriggerDeathVFX()
    {
        if (!deathVFXPrefab) return;

        Transform anchor = deathVFXAnchor ? deathVFXAnchor : transform;

        // Tính pos/rot theo local offset của anchor
        Vector3 worldPos = anchor.TransformPoint(deathVFXPositionOffset);
        Quaternion worldRot = anchor.rotation * Quaternion.Euler(deathVFXEulerOffset);

        Transform parent = deathVFXParentToAnchor ? anchor : null;

        // Nếu có pool VFX của bạn thì đổi sang lấy từ pool ở đây
        ParticleSystem vfx = Instantiate(deathVFXPrefab, worldPos, worldRot, parent);

        // Áp scale (local nếu có parent, còn không thì vẫn ok vì object mới không bị ảnh hưởng)
        vfx.transform.localScale = deathVFXScale;

        vfx.Play(true);

        if (deathVFXAutoDestroy)
        {
            var main = vfx.main;
            float life = main.duration;

            // Ước lượng phần startLifetime
            switch (main.startLifetime.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    life += main.startLifetime.constant;
                    break;
                case ParticleSystemCurveMode.TwoConstants:
                    life += Mathf.Max(main.startLifetime.constantMin, main.startLifetime.constantMax);
                    break;
                default:
                    // Trường hợp Curve/TwoCurves: dùng một giá trị an toàn
                    life += 1.0f;
                    break;
            }

            Destroy(vfx.gameObject, Mathf.Max(0.05f, life + 0.1f));
        }
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

        // ▶▶ Spawn VFX chết NGAY TẠI ĐÂY (trước khi disable agent/SetActive)
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

        StartCoroutine(HideAfterDelay(deathHideDelay));
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

    public void ApplyBurn(float duration, float interval) => ApplyBurn(duration, interval, 5f);
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
