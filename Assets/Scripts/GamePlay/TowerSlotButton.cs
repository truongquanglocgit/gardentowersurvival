using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TowerSlotButton : MonoBehaviour
{
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text costText;

    private TowerData data;
    private System.Action<TowerData> onClick;

    void Awake()
    {
        // Tự động tìm nếu chưa gán thủ công
        if (icon == null)
            icon = GetComponentInChildren<Image>();

        if (nameText == null)
            nameText = GetComponentInChildren<TMP_Text>();
    }

    public void Setup(TowerData towerData, System.Action<TowerData> onClicked)
    {
        data = towerData;
        onClick = onClicked;

        if (icon != null && data.icon != null)
            icon.sprite = data.icon;

        if (nameText != null)
            nameText.text = data.displayName;
        if (costText != null)
            costText.text = data.cost.ToString();
        GetComponent<Button>().onClick.RemoveAllListeners();
        GetComponent<Button>().onClick.AddListener(() => onClick?.Invoke(data));
    }
}
