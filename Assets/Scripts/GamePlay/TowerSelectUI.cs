using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class TowerSelectUI : MonoBehaviour
{
    [Header("Prefabs & References")]
    public GameObject towerSlotButtonPrefab;
    public Transform buttonContainer;
    public List<Button> preconfiguredButtons;

    [Header("Runtime Data")]
    public List<TowerData> towersInMatch = new();

    public void InitTowerButtons(List<TowerData> towerList)
    {
        towersInMatch = towerList;
        GenerateTowerButtons();
    }

    void GenerateTowerButtons()
    {
        if (preconfiguredButtons != null && preconfiguredButtons.Count >= towersInMatch.Count)
        {
            for (int i = 0; i < towersInMatch.Count; i++)
            {
                var data = towersInMatch[i];
                var btn = preconfiguredButtons[i];

                // ✅ Gán icon vào GameObject con tên "icon"
                var iconImage = btn.transform.Find("icon")?.GetComponent<Image>();
                if (iconImage != null)
                    iconImage.sprite = data.icon;

                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => TowerPlacer.I.SetCurrentTower(data));
            }
        }
        else
        {
            foreach (var data in towersInMatch)
            {
                GameObject btnObj = Instantiate(towerSlotButtonPrefab, buttonContainer);
                TowerSlotButton btn = btnObj.GetComponent<TowerSlotButton>();
                btn.Setup(data, TowerPlacer.I.SetCurrentTower);
            }
        }
    }

}
