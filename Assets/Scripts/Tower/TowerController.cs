using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TowerController hợp nhất: Bullet (thẳng/arc), Melee, Laser
/// - Chọn kiểu bằng bool: isMelee, isLaser (còn lại = Bullet)
/// - Dùng Animator "Attack" cho Bullet/Melee, Laser không bắt buộc Animator
/// - Targeting, Level/Stat, UI sao, Upgrade/Sell giữ chung
/// </summary>
public class TowerController : MonoBehaviour, IUpgradableTower
{
    // ====================== LASER & MELEE FX/SOUND ======================
    [Header("Laser Tick FX / SFX")]
    [SerializeField] private GameObject laserMuzzleVFX;      // VFX xuất phát ở firePoint
    [SerializeField] private GameObject laserImpactVFX;      // VFX tại mục tiêu mỗi tick
    [SerializeField] private float laserVFXLifetime = 0.8f;  // tự hủy sau X giây
    [SerializeField] private AudioClip laserTickSFX;         // âm tick (bắn trúng)
    [SerializeField] private float laserSFXVolume = 0.8f;
    [Header("Laser Visual")]
    [SerializeField] private float laserStartWidth = 0.05f;
    [SerializeField] private float laserEndWidth = 0.05f;
    [Header("Melee Hit SFX")]
    [SerializeField] private AudioClip meleeHitSFX;          // âm khi vung trúng 1 mục tiêu
    [SerializeField] private float meleeSFXVolume = 0.9f;
    // ======= BULLET POOL (round-robin) =======
    [Header("Multi Bullet Pool (optional)")]
    [SerializeField] private bool useMultiBulletPool = false;     // BẬT: dùng luân phiên nhiều viên
    [SerializeField] private List<GameObject> bulletPool = new List<GameObject>();
    [SerializeField] private bool autoCollectFromChildren = true; // tự gom các child làm pool
    private int _poolIndex = 0;

    // ====================== CHỌN KIỂU TẤN CÔNG ======================
    [Header("Attack Mode")]
    public bool isMelee = false;
    [Header("Melee")]
    public LayerMask enemyMask = ~0;          // Nên set trong Inspector chỉ layer Enemy
    public Transform meleeCenter;             // Optional: nếu null => dùng transform.position
    public float meleeYOffset = 0f;           // Nếu muốn đánh ở cao hơn gốc

    static readonly Collider[] _overlapBuf = new Collider[64];
    static readonly HashSet<EnemyController> _uniqueVictims = new HashSet<EnemyController>();
    [Header("Melee (single target)")]
    public LayerMask meleeEnemyMask = ~0;   // NÊN set chỉ Layer "Enemy" trong Inspector
    

    public bool isLaser = false;   // nếu cả hai false -> Bullet (straight hoặc arc)

    // ====================== BULLET (STRAIGHT/ARC) ======================
    [Header("Bullet / Arc Shot")]
    public bool arcShot = false;                  // Bật: bắn lobbing (parabol)
    [Tooltip("Thời gian bay mong muốn (giây). 0 sẽ tự suy theo fireRate.")]
    public float arcFlightTime = 0f;
    [Tooltip("Độ cao đỉnh parabol (m) khi không dùng Rigidbody.")]
    public float arcApexHeight = 2.5f;
    [Tooltip("Bán kính nổ khi chạm đất (Bezier). 0 = chỉ mục tiêu chính.")]
    public float arcImpactRadius = 0.6f;
    [Tooltip("Layer Enemy để Overlap khi Bezier kết thúc.")]
    public LayerMask arcEnemyMask;                // để 0 sẽ fallback theo tag

    [Header("Bullet Prefab/FirePoint")]
    public GameObject bulletObject;               // prefab instance/pool item (giữ nguyên behavior cũ)
    public Transform firePoint;
    public float minBulletSpeed = 15f;
    public float maxBulletSpeed = 15f;

    // ====================== LASER ======================
    [Header("Laser")]
    [Tooltip("LineRenderer 2 point cho laser (optional).")]
    public LineRenderer line;
    [Tooltip("Thời gian giữ tia (giây).")]
    public float beamDuration = 1.5f;
    [Tooltip("Bao lâu tick dame 1 lần.")]
    public float tickInterval = 0.2f;
    [Tooltip("Có đổi mục tiêu trong lúc tia đang bắn không.")]
    public bool retargetDuringBeam = true;

    [Header("Laser Aiming")]
    public bool requireAimBeforeFiring = true;
    [Range(0f, 30f)] public float aimToleranceDeg = 4f;

    [Header("Laser Damage")]
    [Tooltip("DPS = CurrentDamage * dpsMultiplier (nếu không dùng override).")]
    public float dpsMultiplier = 1.0f;
    [Tooltip("Nếu > 0 sẽ dùng dame/tick này, bỏ qua DPS.")]
    public float damagePerTickOverride = 0f;

    [Header("Laser FirePoint follow bone (optional)")]
    public bool followAnimatedBone = false;
    public Transform fireBone;
    public string fireBonePath = "";       // tìm dưới Animator
    public Vector3 fireLocalOffset = Vector3.zero;
    public Vector3 fireLocalEuler = Vector3.zero;

    // ====================== COMBAT CHUNG ======================
    [Header("Combat")]
    public float range = 5f;      // sync theo level
    public float fireRate = 1f;   // sync theo level

    [Header("Aiming Target Point")]
    [Tooltip("Tên child trong Enemy để ngắm vào (ví dụ: Target)")]
    public string aimPointChildName = "Target";
    [Tooltip("Nếu không có AimPoint, dùng Collider để ước lượng (ngực/đầu).")]
    public bool useColliderForAim = true;
    [Tooltip("Tỉ lệ từ tâm collider lên phía đỉnh (0=giữa, 0.5=nửa lên).")]
    [Range(0f, 0.6f)] public float colliderTopBias = 0.25f;
    [Tooltip("Fallback khi không có Collider/AimPoint (m).")]
    public float defaultAimYOffset = 0.6f;

    [Header("Aiming/Rotation")]
    [Tooltip("Phần tháp xoay theo mục tiêu (yaw). Trống = xoay root.")]
    public Transform yawPivot;
    public float yawDegPerSec = 540f;

    // ====================== STATS & ECONOMY ======================
    [Header("Stats (Min/Max)")]
    public float minTowerDamage = 3f;
    public float maxTowerDamage = 20f;
    public float minRange = 5f;
    public float maxRange = 8f;
    public float minFireRate = 1f;
    public float maxFireRate = 2f;

    [Header("Economy")]
    public int seedCost = 100;
    public float sellRoti = 0.4f;
    public float upgradeRoti = 0.8f;
    public float currentValue;

    [Header("Meta")]
    public string towerName = "Tomato";
    public int Level = 1;
    public int MaxLevel = 3;

    [Header("(Optional) Arrays override min/max")]
    public float[] damageLevels;   // size = MaxLevel
    public float[] rangeLevels;
    public float[] atkSpeedLevels; // fireRate per level

    // ====================== ANIMATOR (cho Bullet & Melee) ======================
    [Header("Animator")]
    public Animator anim;                         // auto-find nếu để trống
    public string attackTrigger = "Attack";       // Trigger
    public string attackSpeedParam = "AttackSpeed"; // Float speed multiplier
    public string attackStateName = "Attack";     // tên state/clip
    [Tooltip("Độ dài clip Attack speed=1 (giây). 0 = auto detect theo clip.")]
    public float attackClipLength = 0f;
    [Range(0f, 0.95f)]
    [Tooltip("Fallback thời điểm impact nếu chưa đặt Animation Event.")]
    public float fallbackImpactNormTime = 0.25f;

    // ====================== STAR UI ======================
    [Header("Level as Stars (over head)")]
    [SerializeField] private RectTransform currentStarsContainer;
    [SerializeField] private GameObject starPrefab;
    [SerializeField] private Sprite starFilled;
    [SerializeField] private float starWorldSize = 0.25f;
    [SerializeField] private float starOffsetY = 2.0f;
    [SerializeField] private float starSpacing = 6f;

    // ====================== TARGETING ======================
    public enum TargetMode { First, Last, Strongest, Weakest }
    public TargetMode TargetingMode = TargetMode.First;
    [SerializeField] private bool debugTargeting = false;
    public float retargetInterval = 0.15f;

    // ====================== RUNTIME STATE ======================
    private Bullets bulletScript;
    private float nextShotTime = 0f;        // dùng cho Bullet/Melee/Laser
    private const float MAX_CATCHUP = 0.2f; // giới hạn bù nhịp
    private float retargetTimer = 0f;
    private Transform target;
    private TargetMode lastMode;
    private bool shotQueued;                // Bullet/Melee: đã trigger và đang chờ impact
    private Coroutine fallbackCo;

    // Laser runtime
    private bool _beaming;
    private Coroutine _beamCo;

    // ====================== VISUAL / MATERIAL BY LEVEL ======================
    [Header("Visual / Level Skin")]
    [SerializeField] private Transform modelRoot;          // 👈 kéo root của 3D model (con) vào đây
    [SerializeField] private string modelChildName = "";   // hoặc ghi tên child, nếu không kéo được

    [SerializeField] private Material baseMaterial;        // (tuỳ chọn) skin lv1
    [SerializeField] private Material goldMaterial;        // lv2: vàng kim loại
    [SerializeField] private Material diamondMaterial;     // lv3: “kim cương”

    // fallback nếu bạn không có material riêng
    [SerializeField] private bool usePropertyBlockIfNoMaterial = true;
    [SerializeField] private Color goldColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField, Range(0f, 1f)] private float goldMetallic = 0.9f;
    [SerializeField, Range(0f, 1f)] private float goldSmoothness = 0.8f;

    [SerializeField] private Color diamondColor = new Color(0.8f, 0.95f, 1f);
    [SerializeField, Range(0f, 1f)] private float diamondMetallic = 0.6f;
    [SerializeField, Range(0f, 1f)] private float diamondSmoothness = 1f;

    static readonly int _ColorID = Shader.PropertyToID("_Color");
    static readonly int _MetallicID = Shader.PropertyToID("_Metallic");
    static readonly int _GlossinessID = Shader.PropertyToID("_Glossiness");

    private Renderer[] _skinRenderers;                 // auto-fill từ modelRoot
    private MaterialPropertyBlock _mpb;

    [Header("Upgrade Aura FX")]
    [SerializeField] private GameObject upgradeAuraPrefab; // kéo prefab aura vào đây
    [SerializeField] private float upgradeAuraDuration = 1.0f;   // thời gian show trước khi fade
    [SerializeField] private float upgradeAuraFadeTime = 0.6f;   // thời gian fade out
    [SerializeField] private bool auraAttachToModel = true;      // true = làm con của model (đỡ lệch)
    [SerializeField] private Vector3 auraLocalOffset = Vector3.zero;

    // === Upgrade Aura FX (rotation options) ===
    public enum AuraRotationMode { KeepPrefab, UseLocalEuler, UseWorldEuler, MatchYawOnly, MatchTransform }

    [Header("Upgrade Aura FX (Rotation)")]
    [SerializeField] private AuraRotationMode auraRotationMode = AuraRotationMode.KeepPrefab;

    // Dùng khi UseLocalEuler hoặc UseWorldEuler
    [SerializeField] private Vector3 auraEuler = Vector3.zero;

    // Dùng khi MatchYawOnly / MatchTransform
    [SerializeField] private Transform auraMatchTransform; // ví dụ: yawPivot / modelRoot / firePoint
    [SerializeField] private bool auraOnlyYaw = true;
    [SerializeField] private Vector3 upgradeAuraEuler = Vector3.zero; // góc xoay tùy chỉnh
    [SerializeField] private Vector3 upgradeAuraScale = Vector3.one;




    // ====================== HELPERS ======================
    public bool CanUpgrade => Level < MaxLevel;
    int ClampLevel(int lvl) => Mathf.Clamp(lvl, 1, MaxLevel);
    float T(int lvl) => (MaxLevel <= 1) ? 1f : (ClampLevel(lvl) - 1f) / (MaxLevel - 1f);
    float LerpStat(float min, float max, int lvl) => Mathf.Lerp(min, max, T(lvl));
    bool HasArray(float[] arr) => arr != null && arr.Length >= MaxLevel;
    bool IsAlive(EnemyController ec) => ec != null && !ec.IsDead;
    float GetEnemyHP(EnemyController ec) => ec != null ? ec.hp : 0f;

    public float GetDamageAtLevel(int lvl) => HasArray(damageLevels) ? damageLevels[ClampLevel(lvl) - 1] : LerpStat(minTowerDamage, maxTowerDamage, lvl);
    public float GetRangeAtLevel(int lvl) => HasArray(rangeLevels) ? rangeLevels[ClampLevel(lvl) - 1] : LerpStat(minRange, maxRange, lvl);
    public float GetFireRateAtLevel(int lvl) => HasArray(atkSpeedLevels) ? atkSpeedLevels[ClampLevel(lvl) - 1] : LerpStat(minFireRate, maxFireRate, lvl);

    public float CurrentDamage => GetDamageAtLevel(Level);
    public float CurrentRange => GetRangeAtLevel(Level);
    public float CurrentFireRate => GetFireRateAtLevel(Level);

    // IUpgradableTower triển khai chuẩn — KHÔNG throw
    public string TowerName => towerName;
    public string TargetingModeDisplay => TargetingMode.ToString();
    int IUpgradableTower.Level => Level;
    int IUpgradableTower.MaxLevel => MaxLevel;

    // ====================== LIFECYCLE ======================
    void Awake()
    {
        
        if (!anim) anim = GetComponentInChildren<Animator>(true);

        if (bulletObject != null)
        {
            bulletScript = bulletObject.GetComponent<Bullets>();
            SetActiveRecursively(bulletObject, true);
            bulletObject.SetActive(true);
        }

        // Laser init
        if (isLaser)
        {
            if (line)
            {
                line.positionCount = 2;
                line.enabled = false;

                // 👇 chỉnh độ dày laser
                line.startWidth = laserStartWidth;
                line.endWidth = laserEndWidth;
            }
            if (followAnimatedBone && fireBone == null && anim && !string.IsNullOrEmpty(fireBonePath))
            {
                fireBone = anim.transform.Find(fireBonePath);
                if (!fireBone) Debug.LogWarning($"[{name}] fireBonePath not found: {fireBonePath}");
            }
            if (line) { line.positionCount = 2; line.enabled = false; }
        }
        
    }

    void Start()
    {
        lastMode = TargetingMode;

        if (!isLaser) // Animator chỉ cần chính cho Bullet/Melee
        {
            if (attackClipLength <= 0f && anim && anim.runtimeAnimatorController)
            {
                foreach (var c in anim.runtimeAnimatorController.animationClips)
                    if (c.name == attackStateName || c.name.Contains(attackStateName))
                    { attackClipLength = c.length; break; }
            }
        }

        currentValue = seedCost;
        CacheModelRenderers();     // 👈 thêm dòng này
        ShowStar();
        ApplyLevelSkin();          // 👈 áp skin theo Level hiện tại
        if (autoCollectFromChildren && bulletPool.Count == 0 && bulletObject != null)
        {
            // Nếu bulletObject là parent chứa nhiều viên, gom tất cả con không phải root
            var trs = bulletObject.GetComponentsInChildren<Transform>(true);
            foreach (var t in trs)
            {
                if (!t || t == bulletObject.transform) continue;
                if (!bulletPool.Contains(t.gameObject)) bulletPool.Add(t.gameObject);
            }
            // Nếu không có child → coi chính bulletObject là 1 viên
            if (bulletPool.Count == 0) bulletPool.Add(bulletObject);
        }

    }
    GameObject GetBulletRoundRobin()
    {
        if (!useMultiBulletPool || bulletPool == null || bulletPool.Count == 0)
            return bulletObject; // cách cũ: luôn bắn bằng 1 viên

        int n = bulletPool.Count;
        // thử n lần để tìm viên đang rảnh (inactive)
        for (int k = 0; k < n; k++)
        {
            var idx = _poolIndex;
            _poolIndex = (_poolIndex + 1) % n;

            var go = bulletPool[idx];
            if (go == null) continue;

            // Ưu tiên chọn viên đang rảnh
            if (!go.activeInHierarchy) return go;
        }

        // Nếu tất cả đang bận: cứ lấy lượt tiếp theo (chấp nhận “giật” viên đang bay)
        var fallback = bulletPool[_poolIndex];
        _poolIndex = (_poolIndex + 1) % n;
        return fallback ? fallback : bulletObject;
    }

    void CacheModelRenderers()
    {
        // nếu chưa kéo trực tiếp → tìm theo tên child
        if (modelRoot == null && !string.IsNullOrEmpty(modelChildName))
        {
            var t = transform.Find(modelChildName);
            if (t) modelRoot = t;
        }
        // nếu vẫn chưa có → lấy child đầu tiên có Renderer
        if (modelRoot == null)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.transform == transform) continue; // bỏ root Tower
                modelRoot = r.transform.root == transform ? r.transform : r.transform;
                break;
            }
        }

        if (modelRoot != null)
            _skinRenderers = modelRoot.GetComponentsInChildren<Renderer>(true);
        else
            _skinRenderers = GetComponentsInChildren<Renderer>(true); // fallback: tất cả con
    }

    void Update()
    {
        // 1) Sync stat theo Level
        range = CurrentRange;
        fireRate = CurrentFireRate;

        // 2) Animator playback speed (chỉ áp cho Bullet/Melee)
        if (!isLaser && anim && !string.IsNullOrEmpty(attackSpeedParam))
        {
            float speedMul = Mathf.Max(0.01f, (attackClipLength > 0f ? attackClipLength : 1f) * fireRate);
            anim.SetFloat(attackSpeedParam, speedMul);
        }

        // 3) Mode change → retarget ngay
        if (TargetingMode != lastMode)
        {
            if (debugTargeting) Debug.Log($"[Targeting] Mode {lastMode} -> {TargetingMode}");
            lastMode = TargetingMode;
            ForceRetargetNow();
        }

        // 4) Retarget định kỳ
        retargetTimer -= Time.deltaTime;
        if (retargetTimer <= 0f)
        {
            retargetTimer = retargetInterval;
            FindTarget(false);
        }

        // 5) Mất target → retarget
        if (target == null || target.GetComponent<EnemyController>()?.IsDead == true ||
            (target.position - transform.position).sqrMagnitude > range * range)
        {
            FindTarget(true);
        }

        // 6) Xoay yaw về target (nếu có)
        if (target)
        {
            var aimT = FindChildDeep(target, aimPointChildName) ?? target;
            RotateYawTowards(aimT.position);
        }

        // 7) Hành vi theo từng mode
        if (isLaser)
        {
            UpdateLaserMode();
        }
        else
        {
            UpdateBulletOrMeleeMode();
        }

        // 8) Cập nhật line laser (nếu đang bắn)
        if (isLaser && _beaming && line)
        {
            Vector3 from = firePoint ? firePoint.position : transform.position;
            Vector3 to = GetLaserEndPoint();
            line.SetPosition(0, from);
            line.SetPosition(1, to);
        }

        // 9) Theo xương (nếu có)
        if (isLaser && followAnimatedBone && fireBone && firePoint)
        {
            var pos = fireBone.TransformPoint(fireLocalOffset);
            var rot = fireBone.rotation * Quaternion.Euler(fireLocalEuler);
            firePoint.SetPositionAndRotation(pos, rot);
        }
    }
    void ApplyLevelSkin()
    {
        if (_skinRenderers == null || _skinRenderers.Length == 0) return;

        // Ưu tiên dùng material riêng nếu có
        Material useMat = null;
        if (Level >= 3 && diamondMaterial) useMat = diamondMaterial;
        else if (Level >= 2 && goldMaterial) useMat = goldMaterial;
        else if (baseMaterial) useMat = baseMaterial;

        if (useMat != null)
        {
            for (int i = 0; i < _skinRenderers.Length; i++)
            {
                var r = _skinRenderers[i];
                if (!r) continue;

                // gán cho tất cả submesh
                var mats = r.sharedMaterials;
                for (int m = 0; m < mats.Length; m++) mats[m] = useMat;
                r.sharedMaterials = mats;

                // clear property block nếu có
                r.SetPropertyBlock(null);
            }
            return;
        }

        // Không có material riêng → dùng PropertyBlock để tint & thiết lập thông số vật liệu
        if (!usePropertyBlockIfNoMaterial) return;

        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        Color c; float metal; float smooth;

        if (Level >= 3) { c = diamondColor; metal = diamondMetallic; smooth = diamondSmoothness; }
        else if (Level >= 2) { c = goldColor; metal = goldMetallic; smooth = goldSmoothness; }
        else
        {
            // Lv1: khôi phục mặc định
            for (int i = 0; i < _skinRenderers.Length; i++)
                if (_skinRenderers[i]) _skinRenderers[i].SetPropertyBlock(null);
            return;
        }

        for (int i = 0; i < _skinRenderers.Length; i++)
        {
            var r = _skinRenderers[i];
            if (!r) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(_ColorID, c);
            _mpb.SetFloat(_MetallicID, metal);
            _mpb.SetFloat(_GlossinessID, smooth); // Standard shader uses _Glossiness
            r.SetPropertyBlock(_mpb);
        }
    }


    // ====================== BULLET/MELEE FLOW ======================
    void UpdateBulletOrMeleeMode()
    {
        // Chỉ ra lệnh bắn khi đến giờ kế tiếp (dùng Animator)
        if (target && !shotQueued && Time.time >= nextShotTime)
        {
            StartAttack();   // set trigger; impact sẽ xử lý trong OnAttackImpact/Fallback
        }
    }

    void StartAttack()
    {
        shotQueued = true;

        if (anim)
        {
            anim.ResetTrigger(attackTrigger);
            anim.SetTrigger(attackTrigger);
        }
        else
        {
            // Không có Animator → impact ngay
            OnAttackImpact();
            return;
        }

        // Fallback nếu quên đặt Animation Event
        if (fallbackCo != null) StopCoroutine(fallbackCo);
        fallbackCo = StartCoroutine(FallbackImpact());
    }

    IEnumerator FallbackImpact()
    {
        yield return null; // chờ vào state Attack

        while (shotQueued && anim)
        {
            if (target)
            {
                var aimT = FindChildDeep(target, aimPointChildName) ?? target;
                RotateYawTowards(aimT.position);
            }

            var st = anim.GetCurrentAnimatorStateInfo(0);
            if (st.IsName(attackStateName) || st.IsTag("Attack"))
            {
                if (st.normalizedTime >= fallbackImpactNormTime && st.normalizedTime < 0.98f)
                { OnAttackImpact(); yield break; }
            }
            if (st.normalizedTime >= 0.98f)
            { OnAttackImpact(); yield break; } // an toàn
            yield return null;
        }
    }

    /// <summary> Animation Event sẽ gọi hàm này đúng khung đánh </summary>
    public void OnAttackImpact()
    {
        if (!shotQueued) return;
        shotQueued = false;
        if (fallbackCo != null) { StopCoroutine(fallbackCo); fallbackCo = null; }

        if (isMelee)
        {
            DoMelee();
            // đặt nhịp cho phát sau theo IMPACT
            ScheduleNextShot(1f / Mathf.Max(0.0001f, fireRate));
            return;
        }

        // Bullet
        ShootBulletNow();
    }

    void ShootBulletNow()
    {
        if ((bulletObject == null && (bulletPool == null || bulletPool.Count == 0)) || target == null || firePoint == null)
            return;

        Transform aimT = FindChildDeep(target, aimPointChildName) ?? target;
        Vector3 aimPos = aimT.position;

        // >>> LẤY VIÊN SẼ BẮN (1 viên nếu off, round-robin nếu on)
        GameObject proj = GetBulletRoundRobin();
        if (!proj) return;

        // script/rigidbody THEO VIÊN NÀY
        var bs = proj.GetComponent<Bullets>();
        var rb = proj.GetComponent<Rigidbody>();

        // --- ARC SHOT ---
        if (arcShot)
        {
            float period = (arcFlightTime > 0f)
                ? arcFlightTime
                : Mathf.Max(0.25f, 0.9f / Mathf.Max(0.0001f, fireRate));

            proj.transform.position = firePoint.position;
            proj.transform.rotation = Quaternion.LookRotation((aimPos - firePoint.position).normalized, Vector3.up);

            // set damage cho viên này
            SetBulletDamageOn(bs, proj, CurrentDamage);

            ActivateHierarchy(proj); // bật cả cây con

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.velocity = ComputeBallisticVelocity(firePoint.position, aimPos, period, Physics.gravity.y);
            }

            if (bs != null)
            {
                // truyền lại TowerController (nếu prefab chưa set)
                if (!bs.towerController) bs.towerController = this;
                bs.SetTarget(aimT, firePoint);
                // nếu dùng flight nội bộ của Bullets
                bs.StartArcFlight(
                    apexHeight: Mathf.Max(0.1f, arcApexHeight),
                    upTime: Mathf.Clamp(arcFlightTime * 0.4f, 0.08f, 0.6f),
                    descendSpeedMultiplier: 1.2f,
                    terminalHoming: true
                );
            }
            else
            {
                // không có script -> dùng bezier coroutine hiện có
                StartCoroutine(ArcBezierFlight(proj, firePoint.position, aimPos, period, arcApexHeight, CurrentDamage));
            }

            ScheduleNextShot(period);
            return;
        }

        // --- BẮN THẲNG ---
        Quaternion look = Quaternion.LookRotation((aimPos - firePoint.position).normalized, Vector3.up);
        proj.transform.SetPositionAndRotation(firePoint.position, look);

        // damage cho viên này
        SetBulletDamageOn(bs, proj, CurrentDamage);

        float dist = Vector3.Distance(firePoint.position, aimPos);
        float tFly = Mathf.Max(0.01f, 0.9f / Mathf.Max(0.0001f, fireRate));

        if (bs != null)
        {
            // truyền speed → script Bullets sẽ di chuyển
            bs.speed = Mathf.Clamp(dist / tFly, minBulletSpeed, maxBulletSpeed);
            if (!bs.towerController) bs.towerController = this;
            ActivateHierarchy(proj);
            bs.SetTarget(aimT, firePoint);
        }
        else
        {
            ActivateHierarchy(proj);
            // nếu không có script, tự tắt theo thời gian bay gần đúng
            StartCoroutine(DeactivateAfter(proj, tFly + 0.3f));
        }

        // khoá nhịp như cũ
        float periodStraight = 1f / Mathf.Max(0.0001f, fireRate);
        if (nextShotTime == 0f || Time.time - nextShotTime > MAX_CATCHUP)
            nextShotTime = Time.time + periodStraight;
        else
            nextShotTime += periodStraight;
    }
    void SetBulletDamageOn(MonoBehaviour bulletScript, GameObject proj, float dmg)
    {
        if (bulletScript != null)
        {
            var t = bulletScript.GetType();

            // Field: public float damage
            var f = t.GetField("damage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(float))
            {
                f.SetValue(bulletScript, dmg);
                return;
            }

            // Property: public float Damage { get; set; }
            var p = t.GetProperty("Damage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanWrite && p.PropertyType == typeof(float))
            {
                p.SetValue(bulletScript, dmg);
                return;
            }

            // Method: public void SetDamage(float x)
            var m = t.GetMethod("SetDamage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                null, new[] { typeof(float) }, null);
            if (m != null)
            {
                m.Invoke(bulletScript, new object[] { dmg });
                return;
            }
        }

        // Fallback nếu không tìm thấy
        if (proj != null)
            proj.SendMessage("SetDamage", dmg, SendMessageOptions.DontRequireReceiver);
    }

    /// <summary>
    /// Tắt 1 GameObject (ví dụ: bullet) sau thời gian t (giây).
    /// Dùng cho bullet bắn thẳng không có script tự hủy.
    /// </summary>
    IEnumerator DeactivateAfter(GameObject go, float t)
    {
        yield return new WaitForSeconds(t);
        if (go) go.SetActive(false);
    }

    // Spawn 1 VFX và tự hủy
    GameObject SpawnVFX(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent = null, float lifetime = 1f)
    {
        if (!prefab) return null;
        var go = Instantiate(prefab, pos, rot, parent);
        go.SetActive(true);

        // nếu có particle → Play toàn bộ
        var pss = go.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < pss.Length; i++)
        {
            var ps = pss[i];
            if (!ps) continue;
            ps.Clear(true);
            ps.Play(true);
        }

        if (lifetime > 0f) Destroy(go, lifetime);
        return go;
    }

    // Play 1 clip 3D tại vị trí (không cần AudioSource sẵn)
    void PlayOneShot3D(AudioClip clip, Vector3 pos, float volume = 1f)
    {
        if (!clip) return;
        AudioSource.PlayClipAtPoint(clip, pos, volume);
    }

    // ====================== LASER FLOW ======================
    void UpdateLaserMode()
    {
        // Đủ điều kiện bắt đầu beam?
        if (!_beaming && target != null && Time.time >= nextShotTime)
        {
            bool aimed = IsAimedToTarget(aimToleranceDeg);
            if (!requireAimBeforeFiring || aimed)
            {
                _beamCo = StartCoroutine(BeamRoutine());
            }
        }
    }

    IEnumerator BeamRoutine()
    {
        _beaming = true;
        float start = Time.time;
        float end = start + Mathf.Max(0.05f, beamDuration);
        float nextTick = Time.time;

        if (line) line.enabled = true;

        bool earlyStop = false;

        while (true)
        {
            // target chết/ra ngoài → có thể retarget
            if (target == null || target.GetComponent<EnemyController>()?.IsDead == true ||
                (target.position - transform.position).sqrMagnitude > range * range)
            {
                if (retargetDuringBeam)
                    FindTarget(true);

                if (target == null) { earlyStop = true; break; }
            }

            // Tick damage
            if (Time.time >= nextTick)
            {
                ApplyLaserTickDamage();
                nextTick += Mathf.Max(0.01f, tickInterval);
            }

            if (Time.time >= end) break;
            yield return null;
        }

        // Tắt beam
        if (line) line.enabled = false;
        _beaming = false;

        // Khóa nhịp
        if (earlyStop)
        {
            nextShotTime = Time.time; // sẵn sàng bắn lại ngay khi có mục tiêu
        }
        else
        {
            float period = 1f / Mathf.Max(0.0001f, fireRate);
            nextShotTime = Time.time + period;
        }
    }

    void ApplyLaserTickDamage()
    {
        var ec = target ? target.GetComponent<EnemyController>() : null;
        if (ec == null || ec.IsDead) return;

        float tickDmg = (damagePerTickOverride > 0f)
            ? damagePerTickOverride
            : (CurrentDamage * dpsMultiplier * Mathf.Max(0.01f, tickInterval));

        ec.TakeDamage(tickDmg);

        // === ONLY LASER: FX + SFX mỗi tick ===
        if (isLaser)
        {
            // Muzzle VFX tại firePoint
            if (laserMuzzleVFX && firePoint)
                SpawnVFX(laserMuzzleVFX, firePoint.position, firePoint.rotation, null, laserVFXLifetime);

            // Impact VFX tại điểm aim của target (ưu tiên child AimPoint nếu có)
            Transform aimT = FindChildDeep(ec.transform, aimPointChildName) ?? ec.transform;
            Vector3 hitPos = GetAimWorldPos(aimT);
            Quaternion hitRot = Quaternion.LookRotation((hitPos - (firePoint ? firePoint.position : transform.position)).normalized, Vector3.up);

            if (laserImpactVFX)
                SpawnVFX(laserImpactVFX, hitPos, hitRot, null, laserVFXLifetime);

            // Âm thanh tick (tại vị trí va chạm để nghe dễ)
            if (laserTickSFX)
                PlayOneShot3D(laserTickSFX, hitPos, laserSFXVolume);
        }
    }



    // ====================== AIMING/ROTATION/HELPERS ======================
    void RotateYawTowards(Vector3 worldPos)
    {
        var pivot = yawPivot ? yawPivot : transform;
        Vector3 to = worldPos - pivot.position; to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return;

        Quaternion targetYaw = Quaternion.LookRotation(to, Vector3.up);
        Vector3 e = targetYaw.eulerAngles;
        targetYaw = Quaternion.Euler(0f, e.y, 0f);

        pivot.rotation = Quaternion.RotateTowards(pivot.rotation, targetYaw, yawDegPerSec * Time.deltaTime);
    }

    bool IsAimedToTarget(float toleranceDeg)
    {
        if (!target) return false;
        var pivot = yawPivot ? yawPivot : transform;
        Vector3 fwd = pivot.forward; fwd.y = 0f;
        Vector3 to = (GetAimWorldPos(target) - pivot.position); to.y = 0f;
        if (fwd.sqrMagnitude < 1e-6f || to.sqrMagnitude < 1e-6f) return true;
        float err = Vector3.Angle(fwd, to);
        return err <= toleranceDeg;
    }

    public void ForceRetargetNow()
    {
        target = null;
        retargetTimer = 0f;
        FindTarget(true);
    }

    public void CycleTargetMode()
    {
        int n = System.Enum.GetValues(typeof(TargetMode)).Length;
        TargetingMode = (TargetMode)(((int)TargetingMode + 1) % n);
        ForceRetargetNow();
    }

    void FindTarget(bool force)
    {
        if (!force && target != null)
        {
            var ec = target.GetComponent<EnemyController>();
            float distSqr = (target.position - transform.position).sqrMagnitude;
            if (IsAlive(ec) && distSqr <= range * range) return; // giữ target cũ
        }
        target = AcquireTarget();
    }

    Transform AcquireTarget()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        float rangeSqr = range * range;
        Transform best = null;
        float bestMetric = float.NegativeInfinity;
        Vector3 myPos = transform.position;

        foreach (var go in enemies)
        {
            if (!go) continue;
            var ec = go.GetComponent<EnemyController>();
            if (!IsAlive(ec)) continue;

            float distSqr = (go.transform.position - myPos).sqrMagnitude;
            if (distSqr > rangeSqr) continue;

            float hp = GetEnemyHP(ec);
            float metric = 0f;

            switch (TargetingMode)
            {
                case TargetMode.First: metric = -distSqr; break;                 // gần nhất
                case TargetMode.Last: metric = distSqr; break;                   // xa nhất
                case TargetMode.Strongest: metric = hp * 1e5f - distSqr; break;
                case TargetMode.Weakest: metric = -hp * 1e5f - distSqr; break;
            }

            if (metric > bestMetric) { bestMetric = metric; best = go.transform; }
        }
        return best;
    }

    // Tính vận tốc ban đầu để từ p0 tới p1 trong thời gian T với gia tốc gY
    Vector3 ComputeBallisticVelocity(Vector3 p0, Vector3 p1, float T, float gY)
    {
        T = Mathf.Max(0.001f, T);
        Vector3 to = p1 - p0;
        Vector3 toXZ = new Vector3(to.x, 0f, to.z);
        float xz = toXZ.magnitude;
        Vector3 dirXZ = (xz > 0.0001f) ? (toXZ / xz) : Vector3.forward;

        float vxz = xz / T;
        float vy = (to.y - 0.5f * gY * T * T) / T;
        return dirXZ * vxz + Vector3.up * vy;
    }

    void ScheduleNextShot(float period)
    {
        if (nextShotTime == 0f || Time.time - nextShotTime > MAX_CATCHUP)
            nextShotTime = Time.time + period;
        else
            nextShotTime += period;
    }

    IEnumerator ArcBezierFlight(GameObject proj, Vector3 start, Vector3 end, float duration, float apexHeight, float damage)
    {
        duration = Mathf.Max(0.05f, duration);
        Vector3 mid = (start + end) * 0.5f;
        Vector3 apex = mid + Vector3.up * Mathf.Max(0.1f, apexHeight);

        float t = 0f;
        while (t < duration && proj != null)
        {
            float u = t / duration;
            Vector3 a = Vector3.Lerp(start, apex, u);
            Vector3 b = Vector3.Lerp(apex, end, u);
            Vector3 pos = Vector3.Lerp(a, b, u);

            Vector3 da = (apex - start);
            Vector3 db = (end - apex);
            Vector3 tangent = Vector3.Lerp(da, db, u);
            if (tangent.sqrMagnitude > 1e-4f)
                proj.transform.rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);

            proj.transform.position = pos;
            t += Time.deltaTime;
            yield return null;
        }

        if (proj != null) proj.transform.position = end;

        // Impact
        bool usedMask = arcEnemyMask.value != 0;
        int hitCount = 0;

        if (arcImpactRadius > 0.01f)
        {
            Collider[] buf = Physics.OverlapSphere(end, arcImpactRadius,
                usedMask ? arcEnemyMask : ~0, QueryTriggerInteraction.Ignore);

            if (buf != null && buf.Length > 0)
            {
                for (int i = 0; i < buf.Length; i++)
                {
                    var ec = buf[i].GetComponentInParent<EnemyController>() ?? buf[i].GetComponent<EnemyController>();
                    if (ec != null && !ec.IsDead)
                    {
                        ec.TakeDamage(damage);
                        hitCount++;
                    }
                }
            }
            if (!usedMask && hitCount == 0)
            {
                var tagged = GameObject.FindGameObjectsWithTag("Enemy");
                foreach (var go in tagged)
                {
                    var ec = go.GetComponent<EnemyController>();
                    if (ec && !ec.IsDead && (go.transform.position - end).sqrMagnitude <= arcImpactRadius * arcImpactRadius)
                    {
                        ec.TakeDamage(damage);
                        hitCount++;
                    }
                }
            }
        }
        else
        {
            var ec = target ? target.GetComponent<EnemyController>() : null;
            if (ec != null && !ec.IsDead && (ec.transform.position - end).sqrMagnitude <= 1.0f)
            {
                ec.TakeDamage(damage);
                hitCount = 1;
            }
        }

        if (proj != null) proj.SetActive(false);
        if (bulletScript != null) bulletScript.enabled = true; // trả lại nếu cần dùng sau
    }

    Transform FindChildDeep(Transform root, string childName)
    {
        if (!root || string.IsNullOrEmpty(childName)) return null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t && t.name == childName) return t;
        return null;
    }

    Vector3 GetAimWorldPos(Transform t)
    {
        float _; return GetAimWorldPosAndOffsetY(t, out _);
    }

    // Trả về điểm aim (world) & offsetY so với target.root
    Vector3 GetAimWorldPosAndOffsetY(Transform t, out float offsetY)
    {
        offsetY = defaultAimYOffset;
        if (t == null) return Vector3.zero;

        // 1) Ưu tiên child "AimPoint"
        if (!string.IsNullOrEmpty(aimPointChildName))
        {
            var child = t.Find(aimPointChildName);
            if (child != null)
            {
                offsetY = child.position.y - t.position.y;
                return child.position;
            }
        }

        // 2) Ước lượng từ Collider
        if (useColliderForAim)
        {
            Collider col = t.GetComponentInChildren<CapsuleCollider>();
            if (!col) col = t.GetComponentInChildren<CharacterController>();
            if (!col) col = t.GetComponentInChildren<Collider>();

            if (col != null)
            {
                var b = col.bounds; // world
                float chestY = b.center.y + b.extents.y * colliderTopBias;
                offsetY = chestY - t.position.y;
                return new Vector3(b.center.x, chestY, b.center.z);
            }
        }

        // 3) Fallback
        return t.position + Vector3.up * defaultAimYOffset;
    }

    Vector3 GetLaserEndPoint()
    {
        if (target != null) return GetAimWorldPos(target);
        var from = firePoint ? firePoint.position : transform.position;
        var fwd = (yawPivot ? yawPivot.forward : transform.forward);
        return from + fwd * range;
    }

    static void SetActiveRecursively(GameObject root, bool active)
    {
        var all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
            all[i].gameObject.SetActive(active);
    }

    // ====================== MELEE ======================
    void DoMelee()
    {
        // Tâm vùng chém
        Vector3 center = meleeCenter ? meleeCenter.position : transform.position;
        if (meleeYOffset != 0f) center += Vector3.up * meleeYOffset;

        float r = CurrentRange;                // hoặc dùng 'range' nếu bạn sync sẵn
        float r2 = r * r;
        float dmg = CurrentDamage;

        EnemyController chosen = null;

        // 1) ƯU TIÊN: target hiện tại (đang lock để xoay/anim)
        if (target != null)
        {
            var ec = target.GetComponent<EnemyController>();
            if (ec != null && !ec.IsDead)
            {
                // kiểm tra thật sự trong tầm bằng ClosestPoint của collider
                Collider col = target.GetComponentInChildren<Collider>();
                Vector3 p = col ? col.ClosestPoint(center) : target.position;
                if ((p - center).sqrMagnitude <= r2)
                {
                    chosen = ec;
                }
            }
        }

        // 2) Nếu chưa có, chọn enemy gần nhất trong bán kính (có cả trigger)
        if (chosen == null)
        {
            // dùng buffer sẵn có của class nếu bạn đã khai báo _overlapBuf; nếu chưa:
            // static readonly Collider[] _overlapBuf = new Collider[64];

            int n = Physics.OverlapSphereNonAlloc(
                center, r,
                _overlapBuf,
                meleeEnemyMask,
                QueryTriggerInteraction.Collide // LẤY CẢ TRIGGER
            );

            float bestD2 = float.MaxValue;

            for (int i = 0; i < n; i++)
            {
                var c = _overlapBuf[i];
                if (!c) continue;

                EnemyController ec = null;

                // Ưu tiên lấy từ attachedRigidbody (ổn với enemy có nhiều child colliders)
                if (c.attachedRigidbody)
                    ec = c.attachedRigidbody.GetComponent<EnemyController>();

                if (!ec)
                    ec = c.GetComponentInParent<EnemyController>() ?? c.GetComponent<EnemyController>();

                if (ec == null || ec.IsDead) continue;

                Vector3 p = c.ClosestPoint(center);
                float d2 = (p - center).sqrMagnitude;

                if (d2 <= r2 && d2 < bestD2)
                {
                    bestD2 = d2;
                    chosen = ec;
                }
            }
        }

        // 3) (Tùy chọn) fallback theo tag nếu layer chưa set đúng
        if (chosen == null)
        {
            var tagged = GameObject.FindGameObjectsWithTag("Enemy");
            float bestD2 = float.MaxValue;
            foreach (var go in tagged)
            {
                var ec = go.GetComponent<EnemyController>();
                if (!ec || ec.IsDead) continue;

                Collider col = go.GetComponentInChildren<Collider>();
                Vector3 p = col ? col.ClosestPoint(center) : go.transform.position;
                float d2 = (p - center).sqrMagnitude;

                if (d2 <= r2 && d2 < bestD2)
                {
                    bestD2 = d2;
                    chosen = ec;
                }
            }
        }

        // 4) Gây damage CHỈ 1 MỤC TIÊU
        if (chosen != null)
        {
            chosen.TakeDamage(dmg);

            // SFX chỉ cho melee
            if (meleeHitSFX)
            {
                // phát tại chỗ chém (center) hoặc tại mục tiêu
                Vector3 sfxPos = (meleeCenter ? meleeCenter.position : transform.position);
                PlayOneShot3D(meleeHitSFX, sfxPos, meleeSFXVolume);
            }
        }

        // else Debug.Log("Melee: no target in range.");
    }

    // ====================== UI SAO ======================
    void SetBulletDamage(float dmg)
    {
        if (bulletScript == null)
        {
            // nếu bạn có nhiều prefab/pool, có thể set qua SendMessage trực tiếp
            if (bulletObject) bulletObject.SendMessage("SetDamage", dmg, SendMessageOptions.DontRequireReceiver);
            return;
        }

        var t = bulletScript.GetType();

        // Field: public float damage;
        var f = t.GetField("damage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(float)) { f.SetValue(bulletScript, dmg); return; }

        // Property: public float Damage { get; set; }
        var p = t.GetProperty("Damage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.CanWrite && p.PropertyType == typeof(float)) { p.SetValue(bulletScript, dmg); return; }

        // Method: public void SetDamage(float x)
        var m = t.GetMethod("SetDamage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(float) }, null);
        if (m != null) { m.Invoke(bulletScript, new object[] { dmg }); return; }

        // Fallback
        bulletObject.SendMessage("SetDamage", dmg, SendMessageOptions.DontRequireReceiver);
    }
    public void ShowStar()
    {
        EnsureStarUI();
        RenderStars(currentStarsContainer, Level);
    }

    void EnsureStarUI()
    {
        if (currentStarsContainer != null) return;

        var canvasGO = new GameObject("StarCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        canvasGO.transform.localPosition = Vector3.up * starOffsetY;

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        canvasGO.transform.localScale = Vector3.one * starWorldSize;

        var holder = new GameObject("Stars", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        holder.transform.SetParent(canvasGO.transform, false);
        var rt = holder.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200, 50);
        rt.anchoredPosition = Vector2.zero;
        var hlg = holder.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = starSpacing;
        hlg.childForceExpandHeight = hlg.childForceExpandWidth = false;

        currentStarsContainer = rt;
    }

    void RenderStars(RectTransform container, int count)
    {
        if (!container || !starPrefab || !starFilled) return;
        int have = container.childCount;

        for (int i = 0; i < count; i++)
        {
            GameObject star = (i < have) ? container.GetChild(i).gameObject : Instantiate(starPrefab, container);
            star.name = $"Star_{i + 1}";
            var img = star.GetComponent<Image>();
            if (img) { img.sprite = starFilled; img.color = Color.white; }
            star.SetActive(true);
        }
        for (int i = count; i < have; i++)
            container.GetChild(i).gameObject.SetActive(false);
    }

    // ====================== UPGRADE/SELL ======================
    public int GetUpgradeCost() =>
        Mathf.RoundToInt(seedCost * upgradeRoti) + (Level * Mathf.RoundToInt(seedCost * 0.6f));
    public int GetSellPrice() =>
        Mathf.RoundToInt(currentValue * sellRoti) + (Level - 1) * Mathf.RoundToInt(currentValue * 0.2f);

    public bool Upgrade()
    {
        if (!CanUpgrade) return false;
        int cost = GetUpgradeCost();
        currentValue += cost;
        if (!GameController.Instance.TrySpendSeed(cost))
            return false;

        Level++;

        ShowStar();
        ApplyLevelSkin();                 // skin theo level
        StartCoroutine(PlayUpgradeAura()); // <<<<<<<<<< thêm dòng này
        ForceRetargetNow();
        return true;
    }

    IEnumerator PlayUpgradeAura()
    {
        if (!upgradeAuraPrefab) yield break;

        // Spawn
        Transform parent = (auraAttachToModel && modelRoot) ? modelRoot : transform;
        GameObject fx = Instantiate(upgradeAuraPrefab, parent);
        fx.transform.localPosition = auraLocalOffset;
        fx.transform.localRotation = Quaternion.Euler(upgradeAuraEuler); // 👈 xoay theo input
        fx.transform.localScale = upgradeAuraScale;                      // 👈 scale theo input
        fx.SetActive(true);

        // Nếu có ParticleSystem → Play tất cả
        var pss = fx.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < pss.Length; i++)
        {
            var ps = pss[i];
            ps.Clear(true);
            ps.Play(true);
        }

        // Nếu có Audio → play
        var audio = fx.GetComponentInChildren<AudioSource>();
        if (audio) audio.Play();

        // Thời gian hiển thị chính
        yield return new WaitForSeconds(Mathf.Max(0f, upgradeAuraDuration));

        // Fade out / destroy như cũ...
        if (pss != null && pss.Length > 0)
        {
            for (int i = 0; i < pss.Length; i++)
                pss[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
            yield return new WaitForSeconds(Mathf.Max(0f, upgradeAuraFadeTime));
            Destroy(fx);
            yield break;
        }

        // Fallback fade alpha cho Renderer...
        var rends = fx.GetComponentsInChildren<Renderer>(true);
        if (rends != null && rends.Length > 0 && upgradeAuraFadeTime > 0f)
        {
            var mpb = new MaterialPropertyBlock();
            int _ColorID = Shader.PropertyToID("_Color");

            Color[] baseColors = new Color[rends.Length];
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                r.GetPropertyBlock(mpb);
                Color c = Color.white;
                if (r.sharedMaterial && r.sharedMaterial.HasProperty(_ColorID))
                    c = r.sharedMaterial.color;
                baseColors[i] = c;
            }

            float t = 0f;
            while (t < upgradeAuraFadeTime && fx)
            {
                float k = 1f - (t / upgradeAuraFadeTime);
                for (int i = 0; i < rends.Length; i++)
                {
                    var r = rends[i]; if (!r) continue;
                    r.GetPropertyBlock(mpb);
                    Color c = baseColors[i];
                    c.a = c.a * k;
                    mpb.SetColor(_ColorID, c);
                    r.SetPropertyBlock(mpb);
                }
                t += Time.deltaTime;
                yield return null;
            }
        }

        if (fx) Destroy(fx);
    }

    void ApplyAuraRotation(Transform aura, Transform defaultParent)
    {
        if (!aura) return;

        switch (auraRotationMode)
        {
            case AuraRotationMode.KeepPrefab:
                // Giữ nguyên rotation prefab
                break;

            case AuraRotationMode.UseLocalEuler:
                // Xoay theo local của parent (giữ nguyên parent)
                aura.localRotation = Quaternion.Euler(auraEuler);
                break;

            case AuraRotationMode.UseWorldEuler:
                // Ép world rotation
                aura.rotation = Quaternion.Euler(auraEuler);
                break;

            case AuraRotationMode.MatchYawOnly:
                {
                    Transform src = auraMatchTransform ? auraMatchTransform : defaultParent;
                    // Lấy yaw của src rồi cộng offset (local)
                    Vector3 e = src.rotation.eulerAngles;
                    Quaternion yaw = Quaternion.Euler(0f, e.y, 0f);
                    aura.rotation = yaw;
                    if (auraEuler != Vector3.zero)
                        aura.rotation *= Quaternion.Euler(auraEuler);
                    break;
                }

            case AuraRotationMode.MatchTransform:
                {
                    Transform src = auraMatchTransform ? auraMatchTransform : defaultParent;
                    if (auraOnlyYaw)
                    {
                        Vector3 e = src.rotation.eulerAngles;
                        aura.rotation = Quaternion.Euler(0f, e.y, 0f);
                    }
                    else
                    {
                        aura.rotation = src.rotation;
                    }
                    if (auraEuler != Vector3.zero)
                        aura.rotation *= Quaternion.Euler(auraEuler);
                    break;
                }
        }
    }



    // Method bắt buộc theo interface
    public void SellAndDestroy()
    {
        // Gọi sang hàm overload mặc định destroy cha 1 lớp
        SellAndDestroy(1);
    }

    // Overload cho phép chỉ định số lớp cha
    public void SellAndDestroy(int destroyParentLevels)
    {
        GameController.Instance.AddSeed(GetSellPrice());

        // Đi lên cha
        Transform target = transform;
        for (int i = 0; i < destroyParentLevels && target.parent != null; i++)
        {
            target = target.parent;
        }

        Destroy(target.gameObject);
        GameController.Instance.UpdateTowerCount(-1);
    }

    // ====================== GIZMOS ======================
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, Application.isPlaying ? CurrentRange : range);
    }
    static void ActivateHierarchy(GameObject root)
    {
        if (!root) return;
        // Bật toàn bộ node
        var trs = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < trs.Length; i++)
            trs[i].gameObject.SetActive(true);

        // Nếu có VFX, bật lại cho chắc
        var pss = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < pss.Length; i++)
        {
            pss[i].Clear(true);
            pss[i].Play(true);
        }
        // Nếu trước đó từng disable Collider/Renderer:
        var cols = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++) cols[i].enabled = true;

        var rends = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++) rends[i].enabled = true;
    }

}
