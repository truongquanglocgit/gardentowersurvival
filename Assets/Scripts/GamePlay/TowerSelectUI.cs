// TowerSelectUI.cs - Sử dụng danh sách tower đã chọn trước trận để gán vào UI khi vào game
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class TowerSelectUI : MonoBehaviour
{
    [Header("Prefabs & References")]
    public GameObject towerSlotButtonPrefab; // Prefab 1 button đại diện 1 tower
    public Transform buttonContainer;        // Nơi chứa các button sinh ra
    public List<Button> preconfiguredButtons; // Hoặc dùng sẵn các nút gán sẵn

    [Header("Runtime Data")]
    public List<TowerData> towersInMatch = new(); // Gán sẵn trong Inspector hoặc thông qua code khi chuyển scene

    void Start()
    {
        // Nếu towersInMatch đang rỗng, tự lấy từ LoadoutManager
        if (towersInMatch == null || towersInMatch.Count == 0)
        {
            towersInMatch = TowerLoadoutManager.I.equippedTowers;
        }
        GenerateTowerButtons();
    }

    void GenerateTowerButtons()
    {
        if (preconfiguredButtons != null && preconfiguredButtons.Count >= towersInMatch.Count)
        {
            // Gắn vào nút có sẵn
            for (int i = 0; i < towersInMatch.Count; i++)
            {
                var data = towersInMatch[i];
                var btn = preconfiguredButtons[i];

                btn.GetComponentInChildren<Image>().sprite = data.icon;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => TowerPlacer.I.SetCurrentTower(data));
            }
        }
        else
        {
            // Tạo nút động nếu không có sẵn
            foreach (var data in towersInMatch)
            {
                GameObject btnObj = Instantiate(towerSlotButtonPrefab, buttonContainer);
                TowerSlotButton btn = btnObj.GetComponent<TowerSlotButton>();
                btn.Setup(data, TowerPlacer.I.SetCurrentTower);
            }
        }
    }
}
