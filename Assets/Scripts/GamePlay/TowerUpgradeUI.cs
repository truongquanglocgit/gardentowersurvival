using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class TowerUpgradeUI : MonoBehaviour
{
    public static TowerUpgradeUI Instance;

    [Header("Root")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Image backBlocker;

    [Header("Header")]
    [SerializeField] private TextMeshProUGUI textTowerName;
    [SerializeField] private TextMeshProUGUI textTargetMode;

    [Header("Stats")]
    [SerializeField] private TextMeshProUGUI textDamage;
    [SerializeField] private TextMeshProUGUI textAtkSpeed;

    [Header("Level as Stars")]
    [SerializeField] private RectTransform currentStarsContainer; // group sao hiện tại
    [SerializeField] private RectTransform nextStarsContainer;    // group sao nâng cấp
    [SerializeField] private GameObject starPrefab;               // prefab 1 ngôi sao (Image)
    [SerializeField] private Sprite starFilled;                   // sao đầy
    [SerializeField] private Sprite starEmpty;                    // sao rỗng (optional)
    [SerializeField] private TextMeshProUGUI arrowText;           // TMP “→” giữa 2 dãy

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

        if (backBlocker) backBlocker.raycastTarget = true;
    }

    public void Show(TowerController tower)
    {
        current = tower;
        if (panel) panel.SetActive(true);
        if (backBlocker) backBlocker.raycastTarget = true;
        PauseManager.PushPause();
        Refresh();
    }

    public void Hide()
    {
        current = null;
        if (panel) panel.SetActive(false);
        PauseManager.PopPause();
    }

    void OnDestroy()
    {
        if (panel && panel.activeSelf) PauseManager.PopPause();
    }

    void Refresh()
    {
        if (!current) return;

        // Header
        if (textTowerName) textTowerName.text = $"{current.towerName}";
        if (textTargetMode) textTargetMode.text = $"Target: {current.TargetingMode}";

        // Level → ⭐
        int lvlNow = Mathf.RoundToInt(current.Level);
        int maxLvl = Mathf.RoundToInt(current.MaxLevel);
        int? lvlNext = current.CanUpgrade ? lvlNow + 1 : (int?)null;

        RenderStars(currentStarsContainer, lvlNow, maxLvl);
        if (lvlNext != null)
        {
            if (arrowText) arrowText.gameObject.SetActive(true);
            RenderStars(nextStarsContainer, lvlNext.Value, maxLvl);
        }
        else
        {
            if (arrowText) arrowText.gameObject.SetActive(false);
            ClearContainer(nextStarsContainer);
        }

        // Other stats
        float dmgNow = current.CurrentDamage;
        float frNow = current.CurrentFireRate;
        float? dmgNext = current.CanUpgrade ? current.GetDamageAtLevel(lvlNow + 1) : (float?)null;
        float? frNext = current.CanUpgrade ? current.GetFireRateAtLevel(lvlNow + 1) : (float?)null;

        if (textDamage) textDamage.text = dmgNext != null ? $"{dmgNext}" : $"{dmgNow}";
        if (textAtkSpeed) textAtkSpeed.text = frNext != null ? $" {frNext:0.##}" : $"{frNow:0.##}";

        int upCost = current.GetUpgradeCost();
        bool canUpgrade = current.CanUpgrade && GameController.Instance.Seed >= upCost;
        int sellVal = current.GetSellPrice();
        if (txtUpgradePrice) txtUpgradePrice.text = current.CanUpgrade ? $"-{upCost}" : "Max Level";
        if (txtSellPrice) txtSellPrice.text = $"+{sellVal}";

        // button + toàn bộ child cùng dim
        if (btnUpgrade)
        {
            btnUpgrade.interactable = canUpgrade;

            var cg = btnUpgrade.GetComponent<CanvasGroup>();
            if (!cg) cg = btnUpgrade.gameObject.AddComponent<CanvasGroup>();

            cg.alpha = canUpgrade ? 1f : 0.45f;   // độ tối mong muốn
            cg.interactable = canUpgrade;         // chặn tương tác toàn bộ con
            cg.blocksRaycasts = canUpgrade;       // chặn raycast khi khóa
        }
    }

    void RenderStars(RectTransform container, int filledCount, int maxCount)
    {
        if (!container || !starPrefab) return;

        EnsureStarChildren(container, maxCount);

        for (int i = 0; i < container.childCount; i++)
        {
            var go = container.GetChild(i).gameObject;
            var img = go.GetComponent<Image>();
            bool filled = i < filledCount;

            if (img)
            {
                if (filled && starFilled) img.sprite = starFilled;
                else if (!filled && starEmpty) img.sprite = starEmpty;
                // Nếu không có starEmpty, dùng starFilled + giảm alpha cho ô “rỗng”
                img.color = (filled || starEmpty != null) ? Color.white : new Color(1, 1, 1, 0.25f);
                img.enabled = true;
            }

            go.SetActive(i < maxCount); // ẩn bớt sao nếu maxCount nhỏ hơn số child
        }
    }

    void EnsureStarChildren(RectTransform container, int needed)
    {
        int have = container.childCount;
        for (int i = have; i < needed; i++)
        {
            var go = Instantiate(starPrefab, container);
            go.name = $"Star_{i + 1}";
        }
        // Nếu thừa có thể giữ lại để tái sử dụng; không cần destroy
    }

    void ClearContainer(RectTransform container)
    {
        if (!container) return;
        for (int i = 0; i < container.childCount; i++)
            container.GetChild(i).gameObject.SetActive(false);
    }

    public void OnClickUpgrade()
    {
        if (current == null || !current.CanUpgrade) return;
        if (current.Upgrade())
        {
            Refresh();
            PauseManager.PopPause();
        }
    }

    public void OnClickSell()
    {
        if (!current) return;
        GameController.Instance.AddSeed(current.GetSellPrice());
        current.SellAndDestroy();
        PauseManager.PopPause();
        Hide();
    }
}
