using UnityEngine;
using System.Collections.Generic;

public class TowerEquipPanel : MonoBehaviour
{
    [Header("UI References")]
    public Transform towerListContainer;
    public GameObject towerButtonPrefab;

    public List<EquipSlot> equipSlots; // List các slot prefab có highlight, icon

    [Header("Tower Data")]
    public List<TowerData> allTowers; // Gán toàn bộ tower có sẵn
    public int maxEquip = 5;

    private int selectedEquipSlotIndex = -1;

    void Start()
    {
        for (int i = 0; i < equipSlots.Count; i++)
        {
            int index = i;
            equipSlots[i].Setup(index, this); // Gọi vào EquipSlot.cs
        }

        RenderEquippedSlots();
        RenderTowerList();
        ClosePanel();
    }

    public void SelectEquipSlot(int index)
    {
        selectedEquipSlotIndex = index;

        for (int i = 0; i < equipSlots.Count; i++)
        {
            equipSlots[i].SetHighlight(i == index);
        }
    }

    void RenderEquippedSlots()
    {
        var equippedIds = PlayerDataManager.Instance.playerData.equippedTowerIds;

        for (int i = 0; i < equipSlots.Count; i++)
        {
            if (i < equippedIds.Count && !string.IsNullOrEmpty(equippedIds[i]))
            {
                string id = equippedIds[i];
                TowerData tower = allTowers.Find(t => t.towerId == id);
                equipSlots[i].SetIcon(tower?.icon);
            }
            else
            {
                equipSlots[i].SetIcon(null);
            }

            equipSlots[i].SetHighlight(i == selectedEquipSlotIndex);
        }
    }
    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }
    public void OpenPanel()
    {
        gameObject.SetActive(true);
    }
    void RenderTowerList()
    {
        foreach (Transform child in towerListContainer)
        {
            Destroy(child.gameObject);
        }

        var unlockedIds = PlayerDataManager.Instance.playerData.unlockedTowerIds;
        var equippedIds = PlayerDataManager.Instance.playerData.equippedTowerIds;

        foreach (var tower in allTowers)
        {
            GameObject btnObj = Instantiate(towerButtonPrefab, towerListContainer);
            TowerSlotButton btn = btnObj.GetComponent<TowerSlotButton>();

            bool isUnlocked = unlockedIds.Contains(tower.towerId);
            bool isEquipped = equippedIds.Contains(tower.towerId);

            btn.Setup(tower, (towerData) =>
            {
                if (selectedEquipSlotIndex < 0)
                {
                    Debug.Log("⚠️ Chưa chọn ô equip nào");
                    return;
                }

                // Ghi vào slot
                while (equippedIds.Count <= selectedEquipSlotIndex)
                    equippedIds.Add(""); // padding nếu thiếu

                equippedIds[selectedEquipSlotIndex] = towerData.towerId;

                PlayerDataManager.Instance.SavePlayerData();
                RenderEquippedSlots();
            });

            btn.GetComponent<UnityEngine.UI.Button>().interactable = isUnlocked;

            // Màu icon
            if (btn.icon != null)
            {
                if (!isUnlocked)
                    btn.icon.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                else if (isEquipped)
                    btn.icon.color = Color.green;
                else
                    btn.icon.color = Color.white;
            }
        }
    }
}
