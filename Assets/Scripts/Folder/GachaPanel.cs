using System.Collections.Generic;
using UnityEngine;

public class GachaPanel : MonoBehaviour
{
    public GachaBannerData currentBanner;

    void Start()
    {
        // Ẩn Panel Gacha khi mới bắt đầu
        gameObject.SetActive(false);
    }
    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }
    public void OpenPanel()
    {
        gameObject.SetActive(true);
    }

    public void RollOnce()
    {
        Debug.Log("🎲 Roll Once clicked");
        TowerData result = currentBanner.GetRandomTower();
        if (result != null)
        {
            UnlockTower(result);
            ShowResult(new List<TowerData> { result });
        }
        else
        {
            Debug.LogWarning("❌ RollOnce failed: No tower returned");
        }
    }

    public void RollTen()
    {
        Debug.Log("🎲 Roll Ten clicked");
        List<TowerData> results = currentBanner.RollMulti(10);
        foreach (var tower in results)
        {
            UnlockTower(tower);
        }
        ShowResult(results);
    }

    void UnlockTower(TowerData tower)
    {
        var playerData = PlayerDataManager.Instance.playerData;
        if (!playerData.unlockedTowerIds.Contains(tower.towerId))
        {
            playerData.unlockedTowerIds.Add(tower.towerId);
            PlayerDataManager.Instance.SavePlayerData();
            Debug.Log($"🔓 Unlocked: {tower.displayName}");
        }
        else
        {
            Debug.Log($"Already unlocked: {tower.displayName}");
        }
    }

    void ShowResult(List<TowerData> towers)
    {
        foreach (var t in towers)
        {
            Debug.Log($"🌱 Result: {t.displayName} ({t.rarity})");
            // TODO: Gọi GachaResultPanel.ShowResults nếu muốn UI
        }
    }
}
