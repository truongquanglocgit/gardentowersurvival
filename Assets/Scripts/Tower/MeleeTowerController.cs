using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class MeleeTowerController : MonoBehaviour, IUpgradableTower
{

    [Header("Combat")]
    public float range = 2.5f;          // bán kính chém
    public float fireRate = 1.5f;       // đòn / giây

    [Header("Stats (Min/Max)")]
    public float minTowerDamage = 5f, maxTowerDamage = 30f;
    public float minRange = 2.5f, maxRange = 4.5f;
    public float minFireRate = 1f, maxFireRate = 3f;

    [Header("Detection")]
    public LayerMask enemyMask;
    public string enemyTag = "Enemy"; // fallback

    [Header("Economy")]
    public int seedCost = 100;
    public float sellRoti = 0.4f;
    public float upgradeRoti = 0.8f;
    public float currentValue;

    [Header("Meta")]
    public string towerName = "Melee Vine";
    public int Level = 1;
    public int MaxLevel = 3;

    [Header("(Optional) Arrays override min/max)")]
    public float[] damageLevels, rangeLevels, atkSpeedLevels;

    [Header("Stars Over Head")]
    [SerializeField] RectTransform starsContainer;
    [SerializeField] GameObject starPrefab;
    [SerializeField] Sprite starFilled;
    [SerializeField] float starWorldSize = 0.25f, starOffsetY = 2f, starSpacing = 6f;

    [Header("VFX/SFX (optional)")]
    public ParticleSystem hitVfx;
    public AudioSource hitSfx;
    float _attackStartedAt;          // thời điểm bắt đầu đòn
    float _attackDeadline;           // thời điểm tối đa phải kết thúc đòn (failsafe)
    bool _impactDoneThisSwing;      // đã gây damage trong đòn này chưa

    // === Animator setup ===
    [Header("Animator")]
    public Animator anim;                    // auto-find nếu để trống
    public string attackTrigger = "Attack";  // Trigger trong Animator
    public string attackSpeedParam = "AttackSpeed"; // Float dùng làm Speed Multiplier
    public string attackStateName = "Attack";       // state/clip tên "Attack"
    [Tooltip("Độ dài clip Attack ở speed = 1 (giây). Để 0 sẽ auto tìm theo tên clip.")]
    public float attackClipLength = 0f;
    [Range(0f, 0.95f), Tooltip("Fallback: thời điểm impact theo normalizedTime nếu chưa đặt Animation Event.")]
    public float fallbackImpactNormalizedTime = 0.35f;

    [Header("Facing")]
    public float turnSpeedDeg = 540f;        // NEW: độ/giây quay về mục tiêu
    public bool keepUpright = true;          // NEW: chỉ quay yaw
    public Transform visual;                 // NEW: child chứa model/Animator

    [Header("Debug")]
    public bool debugLog = false;

    float fireCooldown;
    float _nextAttackTime;
    bool _attackInProgress;
    // --- REPLACE biến cũ nếu muốn ---
    // private float _nextAttackTime;

    
    // Giới hạn bù nếu tụt FPS quá xa mốc (tránh dồn nhịp)
    private const float MAX_CATCHUP = 0.2f;

    // target hiện tại để xoay về
    EnemyController _currentTarget;          // NEW

    // cache overlap để đỡ GC
    static readonly Collider[] _overlapBuf = new Collider[32];

    // helpers
    public bool CanUpgrade => Level < MaxLevel;
    int ClampLevel(int l) => Mathf.Clamp(l, 1, MaxLevel);
    float T(int l) => (MaxLevel <= 1) ? 1f : (ClampLevel(l) - 1f) / (MaxLevel - 1f);
    float Lerp(float a, float b, int l) => Mathf.Lerp(a, b, T(l));
    bool HasArr(float[] a) => a != null && a.Length >= MaxLevel;

    public float GetDamageAtLevel(int l) => HasArr(damageLevels) ? damageLevels[ClampLevel(l) - 1] : Lerp(minTowerDamage, maxTowerDamage, l);
    public float GetRangeAtLevel(int l) => HasArr(rangeLevels) ? rangeLevels[ClampLevel(l) - 1] : Lerp(minRange, maxRange, l);
    public float GetFireRateAtLevel(int l) => HasArr(atkSpeedLevels) ? atkSpeedLevels[ClampLevel(l) - 1] : Lerp(minFireRate, maxFireRate, l);

    public float CurrentDamage => GetDamageAtLevel(Level);
    public float CurrentRange => GetRangeAtLevel(Level);
    public float CurrentFireRate => GetFireRateAtLevel(Level);

    public string TowerName => throw new System.NotImplementedException();

    public string TargetingModeDisplay => throw new System.NotImplementedException();

    int IUpgradableTower.Level => throw new System.NotImplementedException();

    int IUpgradableTower.MaxLevel => throw new System.NotImplementedException();

    public int GetUpgradeCost() => Mathf.RoundToInt(seedCost * upgradeRoti) + (Level * Mathf.RoundToInt(seedCost * 0.6f));
    public int GetSellPrice() => Mathf.RoundToInt(currentValue * sellRoti) + (Level - 1) * Mathf.RoundToInt(currentValue * 0.2f);

    void Awake()
    {
        if (!anim) anim = GetComponentInChildren<Animator>(true);
        if (!visual)
        {
            // NEW: auto-find visual nếu chưa gán
            if (anim) visual = anim.transform;
            else if (transform.childCount > 0) visual = transform.GetChild(0);
        }
    }

    void Start()
    {
        currentValue = seedCost;
        fireCooldown = 0f;
        _nextAttackTime = 0f;
        _attackInProgress = false;

        // auto lấy độ dài clip Attack nếu chưa set tay
        if (attackClipLength <= 0f && anim && anim.runtimeAnimatorController)
        {
            foreach (var c in anim.runtimeAnimatorController.animationClips)
            {
                if (c.name == attackStateName || c.name.Contains(attackStateName))
                {
                    attackClipLength = c.length;
                    break;
                }
            }
        }

        ShowStar();
    }

    void Update()
    {
        // 1) Sync stat theo level
        range = CurrentRange;
        fireRate = CurrentFireRate;

        // 2) Đồng bộ tốc độ anim theo fireRate
        if (anim && !string.IsNullOrEmpty(attackSpeedParam))
        {
            float playbackSpeed = Mathf.Max(0.01f, (attackClipLength > 0 ? attackClipLength : 1f) * fireRate);
            anim.SetFloat(attackSpeedParam, playbackSpeed);
        }

        // 3) Giữ/đổi target
        MaintainTarget();

        // 4) Xoay về target
        FaceCurrentTarget();

        // 5) Failsafe: nếu vì lý do gì đang "tưởng" là đang đánh mà đã quá hạn → kết thúc đòn
        if (_attackInProgress && Time.time > _attackDeadline)
        {
            if (!_impactDoneThisSwing) DoMelee(); // đảm bảo không mất damage
            _attackInProgress = false;
        }

        // 6) Điều kiện ra đòn mới: hết cooldown + có mục tiêu
        if (!_attackInProgress && _currentTarget != null && Time.time >= _nextAttackTime)
        {
            StartAttack();
        }
    }




    // NEW: giữ/validate mục tiêu hiện tại; nếu không hợp lệ thì tìm mục tiêu mới (closest)
    void MaintainTarget()
    {
        if (_currentTarget == null || _currentTarget.IsDead || !IsInsideRange(_currentTarget.transform.position))
        {
            _currentTarget = AcquireClosestTargetInRange();
        }
    }

    // NEW: chọn enemy gần nhất trong range làm mục tiêu
    EnemyController AcquireClosestTargetInRange()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, range, _overlapBuf, enemyMask, QueryTriggerInteraction.Ignore);
        EnemyController best = null;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            var c = _overlapBuf[i];
            var ec = c.GetComponentInParent<EnemyController>() ?? c.GetComponent<EnemyController>();
            if (ec == null || ec.IsDead) continue;

            float d2 = (ec.transform.position - transform.position).sqrMagnitude;
            if (d2 < bestSqr) { bestSqr = d2; best = ec; }
        }

        if (best == null)
        {
            // fallback theo tag nếu LayerMask chưa set
            var tagged = GameObject.FindGameObjectsWithTag(enemyTag);
            float r2 = range * range;
            foreach (var go in tagged)
            {
                var ec = go.GetComponent<EnemyController>();
                if (!ec || ec.IsDead) continue;
                float d2 = (go.transform.position - transform.position).sqrMagnitude;
                if (d2 <= r2 && d2 < bestSqr) { bestSqr = d2; best = ec; }
            }
        }
        return best;
    }

    // NEW: helper kiểm tra trong range
    bool IsInsideRange(Vector3 pos)
    {
        return (pos - transform.position).sqrMagnitude <= range * range;
    }

    void StartAttack()
    {
        if (_currentTarget == null) return;

        _attackInProgress = true;
        _impactDoneThisSwing = false;
        _attackStartedAt = Time.time;

        if (debugLog) Debug.Log($"[{name}] StartAttack on {_currentTarget.name}");

        // Trigger anim
        if (anim)
        {
            anim.ResetTrigger(attackTrigger);
            anim.SetTrigger(attackTrigger);
        }

        // Tính deadline an toàn dựa trên độ dài clip / speed
        float baseLen = (attackClipLength > 0f) ? attackClipLength : Mathf.Max(0.2f, 1f / Mathf.Max(0.0001f, fireRate));
        float speedMul = (anim && !string.IsNullOrEmpty(attackSpeedParam)) ? Mathf.Max(0.01f, anim.GetFloat(attackSpeedParam)) : 1f;
        float estDur = baseLen / Mathf.Max(0.01f, speedMul);
        _attackDeadline = Time.time + estDur + 0.2f; // +0.2s margin

        // Khởi chạy fallback nếu clip chưa có Animation Event
        StopCoroutine(nameof(FallbackImpactRoutine));
        StartCoroutine(nameof(FallbackImpactRoutine));
    }




    IEnumerator FallbackImpactRoutine()
    {
        // chờ 1 frame để Animator vào state mới
        yield return null;

        while (_attackInProgress && anim)
        {
            // luôn bám target khi đang vung đòn
            FaceCurrentTarget();

            var st = anim.GetCurrentAnimatorStateInfo(0);
            // Nếu vào state Attack hoặc state có Tag="Attack"
            bool inAttack = st.IsName(attackStateName) || st.IsTag("Attack");

            // Khi qua mốc impact (normalizedTime) mà chưa gây damage → gây ngay
            if (inAttack && !_impactDoneThisSwing &&
                st.normalizedTime >= fallbackImpactNormalizedTime && st.normalizedTime < 0.98f)
            {
                DoMelee();
                _impactDoneThisSwing = true;
            }

            // Nếu anim gần xong mà vẫn chưa kết thúc → kết thúc đòn (đảm bảo không mất damage)
            if (st.normalizedTime >= 0.98f)
            {
                if (!_impactDoneThisSwing) { DoMelee(); _impactDoneThisSwing = true; }
                _attackInProgress = false;
                yield break;
            }

            // Failsafe phụ: nếu quá deadline thì out vòng
            if (Time.time > _attackDeadline)
            {
                if (!_impactDoneThisSwing) { DoMelee(); _impactDoneThisSwing = true; }
                _attackInProgress = false;
                yield break;
            }

            yield return null;
        }
    }




    // === Animation Event từ clip Attack sẽ gọi hàm này ===
    public void OnAttackImpact()
    {
        // Animation Event từ clip Attack sẽ gọi hàm này đúng khung đánh
        if (!_impactDoneThisSwing)
        {
            DoMelee();
            _impactDoneThisSwing = true;
        }

        // Kết thúc đòn ngay tại impact, và set cooldown từ thời điểm impact để đều nhịp
        _attackInProgress = false;
        _nextAttackTime = Time.time + 1f / Mathf.Max(0.0001f, fireRate);

        if (debugLog) Debug.Log($"[{name}] Impact (event), next at {_nextAttackTime:F2}");
    }

    void TryImpactOnce()
    {
        if (_impactedThisSwing) return;   // đã gây dame trong nhát này
        _impactedThisSwing = true;

        DoMelee();                        // thực sự gây dame

        // Khóa pha theo IMPACT: đặt giờ cho nhát sau tại đây
        float period = 1f / Mathf.Max(0.0001f, fireRate);
        if (_nextAttackTime == 0f || Time.time - _nextAttackTime > MAX_CATCHUP)
            _nextAttackTime = Time.time + period;
        else
            _nextAttackTime += period;
    }

    void EndSwing()
    {
        _attackInProgress = false;
    }

    // Khi đòn bị hủy giữa chừng (mất state hoặc target) -> không spam attack
    void AbortAttackSoft()
    {
        _attackInProgress = false;
        float period = 1f / Mathf.Max(0.0001f, fireRate);
        _nextAttackTime = Mathf.Max(_nextAttackTime, Time.time + period * 0.5f);
    }


    // === Gây sát thương cho tất cả enemy trong bán kính ===
    void DoMelee()
    {
        float dmg = CurrentDamage;

        int count = Physics.OverlapSphereNonAlloc(transform.position, range, _overlapBuf, enemyMask, QueryTriggerInteraction.Ignore);
        List<EnemyController> victims = null;

        if (count > 0)
        {
            victims = new List<EnemyController>(count);
            for (int i = 0; i < count; i++)
            {
                var c = _overlapBuf[i];
                var ec = c.GetComponentInParent<EnemyController>() ?? c.GetComponent<EnemyController>();
                if (ec != null && !ec.IsDead) victims.Add(ec);
            }
        }
        else
        {
            var tagged = GameObject.FindGameObjectsWithTag(enemyTag);
            float r2 = range * range;
            victims = new List<EnemyController>(tagged.Length);
            foreach (var go in tagged)
            {
                var ec = go.GetComponent<EnemyController>();
                if (!ec || ec.IsDead) continue;
                if ((go.transform.position - transform.position).sqrMagnitude <= r2)
                    victims.Add(ec);
            }
        }

        if (victims != null && victims.Count > 0)
        {
            foreach (var ec in victims) ec.TakeDamage(dmg);
            if (hitVfx) hitVfx.Play();
            if (hitSfx) hitSfx.Play();
        }

        // NEW: nếu target hiện tại đã chết sau cú chém → tìm lại ngay
        if (_currentTarget == null || _currentTarget.IsDead) _currentTarget = AcquireClosestTargetInRange();
    }



    bool HasAnyTargetInRange()
    {
        // (giữ lại để không phá cũ) – nhưng Update đã dùng _currentTarget rồi
        int n = Physics.OverlapSphereNonAlloc(transform.position, range, _overlapBuf, enemyMask, QueryTriggerInteraction.Ignore);
        if (n > 0) return true;

        var tagged = GameObject.FindGameObjectsWithTag(enemyTag);
        float r2 = range * range;
        foreach (var go in tagged)
        {
            if ((go.transform.position - transform.position).sqrMagnitude <= r2)
                return true;
        }
        return false;
    }

    // === NEW: xoay về mục tiêu hiện tại ===
    void FaceCurrentTarget()
    {
        if (_currentTarget == null) return;

        Vector3 to = _currentTarget.transform.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(to.normalized, Vector3.up);

        // quay root
        Quaternion newRoot = keepUpright
            ? Quaternion.Euler(0f, target.eulerAngles.y, 0f)
            : target;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, newRoot, turnSpeedDeg * Time.deltaTime);

        // quay visual (nếu có) để chắc chắn model xoay theo
        if (visual)
        {
            Quaternion newVisual = keepUpright
                ? Quaternion.Euler(0f, target.eulerAngles.y, 0f)
                : target;

            // Dùng world rotation để đơn giản, hoặc dùng local nếu bạn muốn giữ offset
            visual.rotation = Quaternion.RotateTowards(visual.rotation, newVisual, turnSpeedDeg * Time.deltaTime);
        }
    }

    // === Stars UI giữ nguyên ===
    public void ShowStar()
    {
        EnsureStarUI();
        RenderStars(starsContainer, Level);
    }
    // Chỉ dùng event hay vẫn cho phép fallback?
    [SerializeField] bool useAnimationEventOnly = true;

    // Debounce: mỗi nhát chém chỉ gây dame 1 lần
    bool _impactedThisSwing = false;

    // Kiểm tra đang ở Attack state (tránh dame ngoài anim)
    bool IsInAttackState()
    {
        if (!anim) return false;
        var st = anim.GetCurrentAnimatorStateInfo(0);
        return st.IsName(attackStateName) || st.IsTag("Attack");
    }

    void EnsureStarUI()
    {
        if (starsContainer != null) return;

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

        starsContainer = rt;
    }
    void RenderStars(RectTransform container, int count)
    {
        if (!container || !starPrefab || !starFilled) return;

        int have = container.childCount;
        for (int i = 0; i < count; i++)
        {
            GameObject star;
            if (i < have) star = container.GetChild(i).gameObject;
            else star = Instantiate(starPrefab, container);

            star.name = $"Star_{i + 1}";
            var img = star.GetComponent<Image>();
            if (img) { img.sprite = starFilled; img.color = Color.white; }
            star.SetActive(true);
        }
        for (int i = count; i < have; i++)
            container.GetChild(i).gameObject.SetActive(false);
    }

    public bool Upgrade()
    {
        if (!CanUpgrade) return false;
        int cost = GetUpgradeCost();
        currentValue += cost;
        if (!GameController.Instance.TrySpendSeed(cost)) return false;
        Level++;
        ShowStar();
        return true;
    }

    public void SellAndDestroy()
    {
        GameController.Instance.AddSeed(GetSellPrice());
        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, Application.isPlaying ? CurrentRange : range);
    }
}
