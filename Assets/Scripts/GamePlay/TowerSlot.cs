using UnityEngine;
using UnityEngine.UI;

public class TowerSlot : MonoBehaviour
{
    public TowerData data;
    public Image icon;
    public Button selectButton;

    void Start()
    {
        icon.sprite = data.icon;
        selectButton.onClick.AddListener(OnClick);
        UpdateVisual();
    }

    public void OnClick()
    {
        if (TowerLoadoutManager.I.IsEquipped(data))
        {
            TowerLoadoutManager.I.RemoveTower(data);
        }
        else
        {
            TowerLoadoutManager.I.TryAddTower(data);
        }

        UpdateVisual();
    }

    void UpdateVisual()
    {
        // Đổi màu nút khi được chọn
        bool selected = TowerLoadoutManager.I.IsEquipped(data);
        icon.color = selected ? Color.green : Color.white;
    }
}
