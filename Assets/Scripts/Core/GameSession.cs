using System.Collections.Generic;
using UnityEngine;

public class GameSession : MonoBehaviour
{
    public static GameSession Instance;
    public AllTowerDatabase allTowerDatabase;
    public List<WaveDef> selectedWaveList;
    public List<TowerData> startingTowers;

    public MapData currentMapData; // 👈 Thêm biến lưu map đang chọn

    void Awake()
    {
        if (Instance != null) Destroy(gameObject);
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    // 👇 Hàm gọi khi nhấn Play (từ MainMenu)
    public void LoadFromPlayerData()
    {
        selectedWaveList = currentMapData.waveList;
        startingTowers = new List<TowerData>();

        var equippedIds = PlayerDataManager.Instance.playerData.equippedTowerIds;

        foreach (var id in equippedIds)
        {
            if (string.IsNullOrEmpty(id)) continue;

            // ✅ Tìm từ AllTowerDatabase thay vì MapData
            TowerData tower = allTowerDatabase.allTowers.Find(t => t.towerId == id);
            if (tower != null)
                startingTowers.Add(tower);
        }

        Debug.Log($"✅ GameSession loaded {startingTowers.Count} equipped towers from PlayerData.");
    }

}
