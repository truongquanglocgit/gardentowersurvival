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
    [SerializeField] private RectTransform currentStarsContainer;
    [SerializeField] private RectTransform nextStarsContainer;
    [SerializeField] private GameObject starPrefab;
    [SerializeField] private Sprite starFilled;
    [SerializeField] private Sprite starEmpty;
    [SerializeField] private TextMeshProUGUI arrowText;

    [Header("Buttons")]
    [SerializeField] private Button btnUpgrade;
    [SerializeField] private TextMeshProUGUI txtUpgradePrice;
    [SerializeField] private Button btnSell;
    [SerializeField] private TextMeshProUGUI txtSellPrice;
    [SerializeField] private Button btnClose;

    private TowerController current;
    private MeleeTowerController currentMele;

    // Để tránh pop slow trùng khi panel bị disable từ nơi khác
    private bool _ownsSlowMotion = false;

    void Awake()
    {
        Instance = this;

        if (panel) panel.SetActive(false);

        if (btnUpgrade) btnUpgrade.onClick.AddListener(OnClickUpgrade);
        if (btnSell) btnSell.onClick.AddListener(OnClickSell);
        if (btnClose) btnClose.onClick.AddListener(Hide);

        // Cho backBlocker bấm để đóng (nếu muốn)
        if (backBlocker)
        {
            backBlocker.raycastTarget = true;
            var bbBtn = backBlocker.GetComponent<Button>();
            if (!bbBtn) bbBtn = backBlocker.gameObject.AddComponent<Button>();
            bbBtn.onClick.RemoveAllListeners();
            bbBtn.onClick.AddListener(Hide);
        }
    }

    void OnEnable()
    {
        // Reset nhẹ UI mỗi khi panel bật
        if (arrowText) arrowText.gameObject.SetActive(false);
        if (textTowerName) textTowerName.text = "";
        if (textTargetMode) textTargetMode.text = "";
        if (textDamage) textDamage.text = "";
        if (textAtkSpeed) textAtkSpeed.text = "";
        ClearContainer(currentStarsContainer);
        ClearContainer(nextStarsContainer);
    }

    void OnDisable()
    {
        // Nếu panel bị tắt từ nơi khác (không đi qua Hide), đảm bảo trả slow về như cũ
        if (_ownsSlowMotion)
        {
            //PauseManager.PopSlow();
            _ownsSlowMotion = false;
        }
        current = null;
    }

    void OnDestroy()
    {
        // Phòng ngừa lần cuối
        if (_ownsSlowMotion)
        {
            //PauseManager.PopSlow();
            _ownsSlowMotion = false;
        }
    }

    public void Show(TowerController tower)
    {
        current = tower;
        TowerPlacer.I.CancelPlacement();
        if (panel && !panel.activeSelf)
        {
            panel.SetActive(true);
        }

        // Bật slow nếu chưa bật ở phiên này
        if (!_ownsSlowMotion)
        {
            //PauseManager.PushSlow(0.3f);
            _ownsSlowMotion = true;
        }

        Refresh();
    }
    public void Show(MeleeTowerController tower)
    {
        currentMele = tower;
        TowerPlacer.I.CancelPlacement();
        if (panel && !panel.activeSelf)
        {
            panel.SetActive(true);
        }

        // Bật slow nếu chưa bật ở phiên này
        if (!_ownsSlowMotion)
        {
            //PauseManager.PushSlow(0.3f);
            _ownsSlowMotion = true;
        }

        Refresh();
    }
    public void Hide()
    {
        
        OnDisable();
        this.panel.SetActive(false);
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
        if (textAtkSpeed) textAtkSpeed.text = frNext != null ? $"{frNext:0.##}" : $"{frNow:0.##}";

        int upCost = current.GetUpgradeCost();
        bool canUpgrade = current.CanUpgrade && GameController.Instance.Seed >= upCost;
        int sellVal = current.GetSellPrice();

        if (txtUpgradePrice) txtUpgradePrice.text = current.CanUpgrade ? $"-{upCost}" : "Max Level";
        if (txtSellPrice) txtSellPrice.text = $"+{sellVal}";

        // Dim toàn bộ cụm upgrade khi không đủ điều kiện
        if (btnUpgrade)
        {
            btnUpgrade.interactable = canUpgrade;

            var cg = btnUpgrade.GetComponent<CanvasGroup>();
            if (!cg) cg = btnUpgrade.gameObject.AddComponent<CanvasGroup>();

            cg.alpha = canUpgrade ? 1f : 0.45f;
            cg.interactable = canUpgrade;
            cg.blocksRaycasts = canUpgrade;
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

                img.color = (filled || starEmpty != null) ? Color.white : new Color(1, 1, 1, 0.25f);
                img.enabled = true;
            }

            go.SetActive(i < maxCount);
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
            // KHÔNG pop pause/slow ở đây; UI vẫn đang mở để xem chỉ số mới
        }
    }

    public void OnClickSell()
    {
        if (!current) return;
        GameController.Instance.AddSeed(current.GetSellPrice());
        current.SellAndDestroy();
        Hide(); // Hide sẽ pop slow nếu đang giữ
    }
}
