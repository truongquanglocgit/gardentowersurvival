using UnityEngine;
using UnityEngine.UI;

public class TowerSlotUI : MonoBehaviour
{
    public Image icon;
    private TowerData currentTower;

    public void SetTower(TowerData data)
    {
        currentTower = data;
        icon.sprite = data.icon;
        icon.enabled = true;
    }

    public bool IsEmpty() => currentTower == null;
    public TowerData GetTower() => currentTower;
}
