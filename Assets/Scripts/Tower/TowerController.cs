using UnityEngine;
using UnityEngine.UI;

public class TowerController : MonoBehaviour
{
    [Header("Combat")]
    public float range = 5f;      // sẽ sync theo level mỗi frame
    public float fireRate = 1f;   // sẽ sync theo level mỗi frame

    [Header("Bullet")]
    public GameObject bulletObject;   // instance bật/tắt
    public Transform firePoint;
    public float minBulletSpeed = 15f;
    public float maxBulletSpeed = 15f;

    [Header("Stats (Min/Max)")]
    public float minTowerDamage = 3f;
    public float maxTowerDamage = 20f;

    public float minRange = 5f;
    public float maxRange = 8f;

    public float minFireRate = 1f;
    public float maxFireRate = 2f;

    [Header("Economy")]
    public int seedCost = 100;
    public float sellRoti = 0.4f;     // tỉ lệ hoàn seed khi bán
    public float upgradeRoti = 0.8f;  // tỉ lệ tính giá upgrade
    public float currentValue;
    [Header("Meta")]
    public string towerName = "Tomato";
    public int Level = 1;
    public int MaxLevel = 3;

    [Header("(Optional) Arrays override min/max")]
    public float[] damageLevels;   // size = MaxLevel
    public float[] rangeLevels;
    public float[] atkSpeedLevels; // fireRate per level
    [Header("Level as Stars (over head)")]
    [SerializeField] private RectTransform currentStarsContainer; // World-Space holder
    [SerializeField] private GameObject starPrefab;               // prefab UI/Image 1 ngôi sao
    [SerializeField] private Sprite starFilled;                   // sprite sao đầy
    [SerializeField] private float starWorldSize = 0.25f;         // kích thước world scale
    [SerializeField] private float starOffsetY = 2.0f;            // độ cao so với tower
    [SerializeField] private float starSpacing = 6f;

    public enum TargetMode { First, Last, Strongest, Weakest }
    public TargetMode TargetingMode = TargetMode.First;
    [SerializeField] private bool debugTargeting = false;
    public float retargetInterval = 0.15f; // giãn nhịp retarget


    // refs & runtime
    private Bullets bulletScript;
    public Bullets bullet; // nếu bạn dùng Bullet component khác để GetStat()
    private float fireCooldown = 0f;
    private float retargetTimer = 0f;
    private Transform target;
    private TargetMode lastMode;
    private TowerController current;

    // ======= Helpers =======
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

    public int GetUpgradeCost()
    {
        // ví dụ: base theo upgradeRoti + tăng theo cấp
        return Mathf.RoundToInt(seedCost * upgradeRoti) + (Level * Mathf.RoundToInt(seedCost * 0.6f));
    }
    public int GetSellPrice()
    {
        return Mathf.RoundToInt(currentValue * sellRoti) + (Level - 1) * Mathf.RoundToInt(currentValue * 0.2f);
    }
    public void ShowStar()
    {
        EnsureStarUI();
        RenderStars(currentStarsContainer, Level);
    }



    public static System.Action<TowerController> OnTowerUpgraded;

    public void SellAndDestroy()
    {
        GameController.Instance.AddSeed(GetSellPrice());
        Destroy(gameObject);
    }

    // ======= Targeting control =======
    public void ForceRetargetNow()
    {
        if (debugTargeting) Debug.Log($"[Targeting] ForceRetargetNow() mode={TargetingMode}");
        target = null;
        retargetTimer = 0f;
        FindTarget(true);
    }

    public void CycleTargetMode()
    {
        int n = System.Enum.GetValues(typeof(TargetMode)).Length;
        TargetingMode = (TargetMode)(((int)TargetingMode + 1) % n);
        if (debugTargeting) Debug.Log($"[Targeting] Mode switched to {TargetingMode}");
        ForceRetargetNow();
    }
    // =================== STARS UI ===================


    void EnsureStarUI()
    {
        if (currentStarsContainer != null) return;

        // Tạo Canvas world-space + holder
        var canvasGO = new GameObject("StarCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        canvasGO.transform.localPosition = Vector3.up * starOffsetY;

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        canvasGO.transform.localScale = Vector3.one * starWorldSize;

        // Holder ngang để đặt sao
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

    // =================== UPGRADE ===================
    public bool Upgrade()
    {
        if (!CanUpgrade) return false;

        int cost = GetUpgradeCost();
        currentValue = currentValue + cost;
        if (!GameController.Instance.TrySpendSeed(cost))
        {
            if (debugTargeting) Debug.Log($"[Upgrade] Not enough seed. Need {cost}, have {GameController.Instance.Seed}");
            return false;
        }

        Level++;
        ShowStar();
        if (debugTargeting) Debug.Log($"[Upgrade] {towerName} -> Level {Level}");

        OnTowerUpgraded?.Invoke(this);

        // nếu có bullet stat riêng
        if (bullet != null)
        {
            try { bullet.GetStat(); } catch { /* bỏ qua nếu không có */ }
        }

        // sau nâng cấp có thể muốn retarget lại
        ForceRetargetNow();
        return true;
    }



    // ======= Unity =======
    void Start()
    {
        lastMode = TargetingMode;

        if (bulletObject != null)
        {
            bulletScript = bulletObject.GetComponent<Bullets>();
            bulletObject.SetActive(false);
        }
        currentValue = seedCost;
        ShowStar(); // hiển thị sao ban đầu
    }

    void Update()
    {
        // sync stat theo level
        range = CurrentRange;
        fireRate = CurrentFireRate;

        // phát hiện đổi mode từ UI ngoài
        if (TargetingMode != lastMode)
        {
            if (debugTargeting) Debug.Log($"[Targeting] Detected mode change {lastMode} -> {TargetingMode}");
            lastMode = TargetingMode;
            ForceRetargetNow();
        }

        // retarget định kỳ
        retargetTimer -= Time.deltaTime;
        if (retargetTimer <= 0f)
        {
            retargetTimer = retargetInterval;
            FindTarget(false);
        }

        fireCooldown -= Time.deltaTime;

        // target chết hoặc null → retarget ngay
        if (target == null || target.GetComponent<EnemyController>()?.IsDead == true)
        {
            FindTarget(true);
            return;
        }

        if (!bulletObject.activeInHierarchy && fireCooldown <= 0f)
        {
            Shoot();
            fireCooldown = 1f / fireRate;
        }
    }

    // ======= Target selection =======
    void FindTarget(bool force)
    {
        // không force: giữ target cũ khi hợp lệ & trong tầm (để tránh nhấp nháy)
        if (!force && target != null)
        {
            var ec = target.GetComponent<EnemyController>();
            float distSqr = (target.position - transform.position).sqrMagnitude;
            if (IsAlive(ec) && distSqr <= CurrentRange * CurrentRange)
            {
                if (debugTargeting) Debug.Log($"[Targeting] Keep current target: {target.name} (mode={TargetingMode})");
                return;
            }
        }

        Transform newT = AcquireTarget();
        if (debugTargeting)
        {
            if (newT) Debug.Log($"[Targeting] Selected: {newT.name} (mode={TargetingMode})");
            else Debug.Log($"[Targeting] Selected: NULL (mode={TargetingMode})");
        }
        target = newT;
    }

    Transform AcquireTarget()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        float rangeSqr = CurrentRange * CurrentRange;

        if (debugTargeting)
            Debug.Log($"[Targeting] AcquireTarget() mode={TargetingMode}, enemies={enemies.Length}, range={CurrentRange:F2}");

        Transform best = null;
        float bestMetric = float.NegativeInfinity; // so sánh bằng 'metric > bestMetric'
        Vector3 myPos = transform.position;

        foreach (var go in enemies)
        {
            if (!go) continue;
            var ec = go.GetComponent<EnemyController>();
            if (!IsAlive(ec))
            {
                if (debugTargeting) Debug.Log($"[Targeting] skip {go.name} (dead)");
                continue;
            }

            Vector3 to = go.transform.position - myPos;
            float distSqr = to.sqrMagnitude;

            if (distSqr > rangeSqr)
            {
                //if (debugTargeting) Debug.Log($"[Targeting] skip {go.name} (out of range) dist={Mathf.Sqrt(distSqr):F2}");
                continue;
            }

            float hp = GetEnemyHP(ec);
            float metric = 0f;

            switch (TargetingMode)
            {
                case TargetMode.First:
                    // gần nhất → metric lớn hơn khi khoảng cách nhỏ: dùng -distSqr
                    metric = -distSqr;
                    break;

                case TargetMode.Last:
                    // xa nhất trong tầm → metric = +distSqr
                    metric = distSqr;
                    break;

                case TargetMode.Strongest:
                    // HP cao ưu tiên, tie-break: gần hơn một chút
                    metric = hp * 100000f - distSqr;
                    break;

                case TargetMode.Weakest:
                    // HP thấp ưu tiên, tie-break: gần hơn
                    metric = -hp * 100000f - distSqr;
                    break;
            }

            if (debugTargeting)
                Debug.Log($"[Targeting] cand={go.name} dist={Mathf.Sqrt(distSqr):F2} hp={hp:F0} metric={metric}");

            if (metric > bestMetric)
            {
                bestMetric = metric;
                best = go.transform;
                if (debugTargeting) Debug.Log($"[Targeting]   -> best now {best.name} (metric={bestMetric})");
            }
        }

        return best;
    }

    // ======= Combat =======
    void Shoot()
    {
        if (bulletScript == null || target == null) return;

        bulletObject.transform.position = firePoint.position;
        bulletObject.transform.rotation = firePoint.rotation;

        float distance = Vector3.Distance(firePoint.position, target.position);
        float timeBuffer = 0.9f / fireRate;
        float recommendedSpeed = distance / timeBuffer;

        bulletScript.speed = Mathf.Clamp(recommendedSpeed, minBulletSpeed, maxBulletSpeed);

        // Ví dụ truyền damage hiện tại:
        // bulletScript.damage = CurrentDamage;
        // hoặc bulletScript.SetDamage(CurrentDamage);

        bulletObject.SetActive(true);
        bulletScript.SetTarget(target, firePoint);
    }
}
