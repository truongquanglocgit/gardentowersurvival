using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MeleeTowerController : MonoBehaviour
{
    [Header("Combat")]
    public float range = 2.5f;
    public float fireRate = 1.5f;

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

    float fireCooldown;

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

    public int GetUpgradeCost() => Mathf.RoundToInt(seedCost * upgradeRoti) + (Level * Mathf.RoundToInt(seedCost * 0.6f));
    public int GetSellPrice() => Mathf.RoundToInt(currentValue * sellRoti) + (Level - 1) * Mathf.RoundToInt(currentValue * 0.2f);

    void Start()
    {
        currentValue = seedCost;
        fireCooldown = 0f;
        ShowStar();
    }

    void Update()
    {
        range = CurrentRange;
        fireRate = CurrentFireRate;

        fireCooldown -= Time.deltaTime;
        if (fireCooldown <= 0f)
        {
            DoMelee();
            fireCooldown = 1f / Mathf.Max(0.0001f, fireRate);
        }
    }

    void DoMelee()
    {
        var hits = Physics.OverlapSphere(transform.position, range, enemyMask);
        var victims = new List<EnemyController>();

        if (hits.Length > 0)
        {
            foreach (var c in hits)
            {
                var ec = c.GetComponentInParent<EnemyController>() ?? c.GetComponent<EnemyController>();
                if (ec != null && !ec.IsDead) victims.Add(ec);
            }
        }
        else
        {
            // fallback theo tag
            var tagged = GameObject.FindGameObjectsWithTag(enemyTag);
            float r2 = range * range;
            foreach (var go in tagged)
            {
                var ec = go.GetComponent<EnemyController>();
                if (!ec || ec.IsDead) continue;
                if ((go.transform.position - transform.position).sqrMagnitude <= r2)
                    victims.Add(ec);
            }
        }

        if (victims.Count == 0) return;

        float dmg = CurrentDamage;
        foreach (var ec in victims) ec.TakeDamage(dmg);

        if (hitVfx) hitVfx.Play();
        if (hitSfx) hitSfx.Play();
    }

    // === Stars (tương tự TowerController) ===
    public void ShowStar()
    {
        EnsureStarUI();
        RenderStars(starsContainer, Level);
    }
    void EnsureStarUI()
    {
        if (starsContainer != null) return;

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
