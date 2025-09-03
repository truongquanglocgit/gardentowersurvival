using UnityEngine;
using UnityEngine.UI;

public class EquipSlot : MonoBehaviour
{
    public Image icon;
    public GameObject highlight; // assign to Highlight object

    private int slotIndex;
    private TowerEquipPanel panelRef;

    public void Setup(int index, TowerEquipPanel panel)
    {
        slotIndex = index;
        panelRef = panel;

        GetComponent<Button>().onClick.RemoveAllListeners();
        GetComponent<Button>().onClick.AddListener(() =>
        {
            panelRef.SelectEquipSlot(index);
        });
    }

    public void SetIcon(Sprite sprite)
    {
        icon.sprite = sprite;
        icon.enabled = (sprite != null);
    }

    public void SetHighlight(bool on)
    {
        if (highlight != null)
            highlight.SetActive(on);
    }
}
