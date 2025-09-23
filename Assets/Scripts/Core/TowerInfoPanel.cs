using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TowerInfoPanel : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] GameObject panelRoot;

    [Header("Header")]
    [SerializeField] Image icon;
    [SerializeField] TMP_Text towerName;
    [SerializeField] TMP_Text rarityText;   // ✅ Text hiển thị rarity (đổi màu theo rarity)

    [Header("Stats")]
    [SerializeField] TMP_Text damageText;
    [SerializeField] TMP_Text rangeText;
    [SerializeField] TMP_Text fireRateText;

    [Header("Buttons")]
    [SerializeField] Button closeBtn;

    [Header("Rarity Colors (chỉnh trong Inspector)")]
    [SerializeField] Color commonColor = new Color32(180, 180, 180, 255);
    [SerializeField] Color uncommonColor = new Color32(80, 200, 120, 255);
    [SerializeField] Color rareColor = new Color32(80, 120, 255, 255);
    [SerializeField] Color epicColor = new Color32(170, 90, 255, 255);
    [SerializeField] Color legendaryColor = new Color32(255, 170, 40, 255);
    [SerializeField] Color mythicColor = new Color32(255, 60, 60, 255);
    [SerializeField] Color voidColor = new Color32(80, 220, 220, 255);

    TowerData current;

    void Awake()
    {
        if (panelRoot) panelRoot.SetActive(false);
        if (closeBtn) closeBtn.onClick.AddListener(Close);
    }

    // Giữ API cũ
    public void OpenFor(TowerData data) => OpenFor(data, null);

    // Overload: cho phép nhận sẵn controller (vd. từ TowerSlotButton)
    public void OpenFor(TowerData data, TowerController controllerFromPrefab)
    {
        current = data;
        if (current == null)
        {
            Debug.LogWarning("TowerInfoPanel: TowerData null");
            return;
        }

        // ===== Resolve TowerController =====
        TowerController tc = controllerFromPrefab;

        if (tc == null)
        {
            if (current.prefab == null)
            {
                Debug.LogWarning("TowerInfoPanel: TowerData.prefab null");
                return;
            }

            // 1) root
            tc = current.prefab.GetComponent<TowerController>();

            // 2) direct children
            if (tc == null)
            {
                var root = current.prefab.transform;
                for (int i = 0; i < root.childCount; i++)
                {
                    var c = root.GetChild(i).GetComponent<TowerController>();
                    if (c != null) { tc = c; break; }
                }
            }

            // 3) sâu toàn bộ (kể cả inactive)
            if (tc == null)
            {
                var candidates = current.prefab.GetComponentsInChildren<TowerController>(true);
                if (candidates != null && candidates.Length > 0)
                {
                    tc = candidates[0];
                    if (candidates.Length > 1)
                        Debug.LogWarning($"TowerInfoPanel: Có {candidates.Length} TowerController trong prefab '{current.prefab.name}', dùng '{tc.name}'.");
                }
            }
        }

        if (tc == null)
        {
            Debug.LogWarning($"TowerInfoPanel: Không tìm thấy TowerController trong prefab '{current.prefab?.name}'.");
            return;
        }
        // ===================================

        // Header
        if (icon) { icon.sprite = current.icon; icon.preserveAspect = true; }
        var rarityColor = GetRarityColor(current.rarity);

        if (towerName)
        {
            towerName.text = current.name;
            towerName.color = rarityColor;        // ✅ tên đổi màu theo rarity
        }

        if (rarityText)
        {
            rarityText.text = current.rarity.ToString().ToUpper(); // ví dụ: LEGENDARY
            rarityText.color = rarityColor;                         // ✅ rarity text cùng màu
        }

        // Stats hiển thị min → max
        if (damageText) damageText.text = $"Damage: {tc.minTowerDamage} - {tc.maxTowerDamage}";
        if (rangeText) rangeText.text = $"Range: {tc.minRange} - {tc.maxRange}";
        if (fireRateText) fireRateText.text = $"FireRate: {tc.minFireRate:0.##}/s - {tc.maxFireRate:0.##}/s";

        if (panelRoot) panelRoot.SetActive(true);
    }

    public void Close()
    {
        if (panelRoot) panelRoot.SetActive(false);
        current = null;
    }

    // -------- Helpers --------
    private Color GetRarityColor(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Common: return commonColor;
            case Rarity.Uncommon: return uncommonColor;
            case Rarity.Rare: return rareColor;
            case Rarity.Epic: return epicColor;
            case Rarity.Legendary: return legendaryColor;
            case Rarity.Mythic: return mythicColor;
            case Rarity.Void: return voidColor;
            default: return commonColor;
        }
    }
}
