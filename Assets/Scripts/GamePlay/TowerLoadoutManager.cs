using UnityEngine;
using System.Collections.Generic;

public class TowerLoadoutManager : MonoBehaviour
{
    public static TowerLoadoutManager I;

    public List<TowerData> equippedTowers = new();
    public int maxTowers = 5;

    public GameObject towerPreviewPanel; // UI hoặc chỗ đặt prefab
    public GameObject towerPrefab;

    private List<TowerData> towerList = new();

    void Awake()
    {
        if (I == null) I = this;
        else Destroy(gameObject);
    }

    // ✅ Giống như waveManager.LoadWaves(...)
    public void LoadTowers(List<TowerData> towers)
    {
        towerList.Clear();
        equippedTowers.Clear();

        foreach (var tower in towers)
        {
            if (!towerList.Contains(tower))
                towerList.Add(tower);
            equippedTowers = towerList;
            TryAddTower(tower); // tự động trang bị (nếu cần)
        }

        Debug.Log($"✅ Loaded {towerList.Count} towers from GameSession");
    }
    public void LoadEquippedFromPlayerData(List<TowerData> allTowerDatabase)
    {
        equippedTowers.Clear();
        foreach (var id in PlayerDataManager.Instance.playerData.equippedTowerIds)
        {
            TowerData data = allTowerDatabase.Find(t => t.towerId == id);
            if (data != null) TryAddTower(data);
        }
    }

    public void RegisterTower(TowerData tower)
    {
        if (!towerList.Contains(tower))
            towerList.Add(tower);
    }

    public void Setup(TowerData towerData)
    {
        towerPrefab = towerData.prefab;
    }

    public bool TryAddTower(TowerData data)
    {
        if (equippedTowers.Contains(data)) return false;
        if (equippedTowers.Count >= maxTowers) return false;

        equippedTowers.Add(data);
        return true;
    }

    public void RemoveTower(TowerData data)
    {
        if (equippedTowers.Contains(data))
            equippedTowers.Remove(data);
    }

    public bool IsEquipped(TowerData data)
    {
        return equippedTowers.Contains(data);
    }
}
