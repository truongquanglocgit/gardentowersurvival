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

    [Header("Stats")]
    [SerializeField] TMP_Text damageText;
    [SerializeField] TMP_Text rangeText;
    [SerializeField] TMP_Text fireRateText;

    [Header("Buttons")]
    [SerializeField] Button closeBtn;

    TowerData current;

    void Awake()
    {
        if (panelRoot) panelRoot.SetActive(false);
        if (closeBtn) closeBtn.onClick.AddListener(Close);
    }

    public void OpenFor(TowerData data)
    {
        current = data;
        if (!current || !current.prefab)
        {
            Debug.LogWarning("TowerInfoPanel: TowerData/prefab null");
            return;
        }

        var tc = current.prefab.GetComponent<TowerController>();
        if (!tc)
        {
            Debug.LogWarning("TowerInfoPanel: Prefab không có TowerController");
            return;
        }

        // Header
        if (icon) icon.sprite = current.icon;
        if (towerName) towerName.text = $"{tc.towerName}";

        // Stats hiển thị min → max
        if (damageText) damageText.text = $"Damage: {tc.minTowerDamage} → {tc.maxTowerDamage}";
        if (rangeText) rangeText.text = $"Range: {tc.minRange} → {tc.maxRange}";
        if (fireRateText) fireRateText.text = $"FireRate: {tc.minFireRate:0.##}/s → {tc.maxFireRate:0.##}/s";

        if (panelRoot) panelRoot.SetActive(true);
    }

    public void Close()
    {
        if (panelRoot) panelRoot.SetActive(false);
        current = null;
    }
}
