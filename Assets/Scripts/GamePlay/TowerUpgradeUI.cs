using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TowerUpgradeUI : MonoBehaviour
{
    public static TowerUpgradeUI Instance;

    [Header("Root")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Image backBlocker;

    [Header("Header")]
    [SerializeField] private Image imagePreview;
    [SerializeField] private TextMeshProUGUI textTowerName;
    [SerializeField] private TextMeshProUGUI textTargetMode;
    [SerializeField] private Button btnTargetMode;   // <-- gán nút Target Mode ở đây

    [Header("Stats")]
    [SerializeField] private TextMeshProUGUI textLevel;
    [SerializeField] private TextMeshProUGUI textDamage;
    [SerializeField] private TextMeshProUGUI textRange;
    [SerializeField] private TextMeshProUGUI textAtkSpeed;

    [Header("Buttons")]
    [SerializeField] private Button btnUpgrade;
    [SerializeField] private TextMeshProUGUI txtUpgradePrice;
    [SerializeField] private Button btnSell;
    [SerializeField] private TextMeshProUGUI txtSellPrice;
    [SerializeField] private Button btnClose;

    private TowerController current;

    void Awake()
    {
        Instance = this;
        if (panel) panel.SetActive(false);

        if (btnUpgrade) btnUpgrade.onClick.AddListener(OnClickUpgrade);
        if (btnSell) btnSell.onClick.AddListener(OnClickSell);
        if (btnClose) btnClose.onClick.AddListener(Hide);
        if (btnTargetMode) btnTargetMode.onClick.AddListener(OnClickTargetMode);

        if (backBlocker) backBlocker.raycastTarget = true;
    }

    public void Show(TowerController tower)
    {
        current = tower;
        if (panel) panel.SetActive(true);
        Refresh();
    }

    public void Hide()
    {
        current = null;
        if (panel) panel.SetActive(false);
    }

    void Refresh()
    {
        if (current == null) return;

        float lvlNow = current.Level;
        float dmgNow = current.CurrentDamage;
        float rngNow = current.CurrentRange;
        float frNow = current.CurrentFireRate;

        float? lvlNext = current.CanUpgrade ? current.Level + 1 : (float?)null;
        float? dmgNext = current.CanUpgrade ? current.GetDamageAtLevel(current.Level + 1) : (float?)null;
        float? rngNext = current.CanUpgrade ? current.GetRangeAtLevel(current.Level + 1) : (float?)null;
        float? frNext = current.CanUpgrade ? current.GetFireRateAtLevel(current.Level + 1) : (float?)null;

        if (imagePreview) imagePreview.sprite = current.previewSprite;
        if (textTowerName) textTowerName.text = $"{current.towerName} (Level {current.Level}/{current.MaxLevel})";
        if (textTargetMode) textTargetMode.text = $"Target: {current.TargetingMode}";

        if (textLevel) textLevel.text = lvlNext != null ? $"Level:  {lvlNow} → {lvlNext}" : $"Level:  {lvlNow}";
        if (textDamage) textDamage.text = dmgNext != null ? $"Damage: {dmgNow} → {dmgNext}" : $"Damage: {dmgNow}";
        if (textRange) textRange.text = rngNext != null ? $"Range:  {rngNow} → {rngNext}" : $"Range:  {rngNow}";
        if (textAtkSpeed) textAtkSpeed.text = frNext != null ? $"Fire/s: {frNow:0.##} → {frNext:0.##}" : $"Fire/s: {frNow:0.##}";

        int upCost = current.GetUpgradeCost();
        int sellVal = current.GetSellPrice();

        if (txtUpgradePrice) txtUpgradePrice.text = current.CanUpgrade ? $"Upgrade: {upCost}" : "Max Level";
        if (txtSellPrice) txtSellPrice.text = $"Sell: +{sellVal}";
        if (btnUpgrade) btnUpgrade.interactable = current.CanUpgrade && GameController.Instance.Seed >= upCost;
    }

    public void OnClickUpgrade()
    {
        if (current == null || !current.CanUpgrade) return;
        if (current.Upgrade())
        {
            Refresh();
        }
    }

    public void OnClickSell()
    {
        if (!current) return;
        GameController.Instance.AddSeed(current.GetSellPrice());
        current.SellAndDestroy();
        Hide();
    }

    // === Nút đổi Target Mode trong UI ===
    public void OnClickTargetMode()
    {
        if (current == null) return;

        int n = System.Enum.GetValues(typeof(TowerController.TargetMode)).Length;
        current.TargetingMode = (TowerController.TargetMode)(((int)current.TargetingMode + 1) % n);

        // force retarget ngay
        current.ForceRetargetNow();

        if (textTargetMode) textTargetMode.text = $"Target: {current.TargetingMode}";
    }
}
