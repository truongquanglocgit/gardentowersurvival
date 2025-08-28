using UnityEngine;

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
    public float currentValue ;
    [Header("Meta")]
    public string towerName = "Tomato";
    public int Level = 1;
    public int MaxLevel = 3;

    [Header("(Optional) Arrays override min/max")]
    public float[] damageLevels;   // size = MaxLevel
    public float[] rangeLevels;
    public float[] atkSpeedLevels; // fireRate per level

    
    public enum TargetMode { First, Last, Strongest, Weakest }
    public TargetMode TargetingMode = TargetMode.First;
    [SerializeField] private bool debugTargeting = false;
    public float retargetInterval = 0.15f; // giãn nhịp retarget

    [Header("Visual")]
    public Sprite previewSprite;

    // refs & runtime
    private Bullet bulletScript;
    public Bullet bullet; // nếu bạn dùng Bullet component khác để GetStat()
    private float fireCooldown = 0f;
    private float retargetTimer = 0f;
    private Transform target;
    private TargetMode lastMode;

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

    // ======= Unity =======
    void Start()
    {
        lastMode = TargetingMode;

        if (bulletObject != null)
        {
            bulletScript = bulletObject.GetComponent<Bullet>();
            bulletObject.SetActive(false);
        }
        currentValue = seedCost;
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
