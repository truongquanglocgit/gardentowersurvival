using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Linq;

public class GachaController : MonoBehaviour
{
    public GachaBannerData currentBanner;
    public int costPerRoll = 10;
    public Transform resultParent;
    public GameObject resultUIPrefab;
    public GachaResultPanel gachaResultPanel;

    public void RollOnce()
    {
        Debug.Log("👉 RollOnce button clicked");

        if (!CurrencyManager.Instance.TrySpendSeed(costPerRoll))
        {
            Debug.Log("❌ Not enough seed for 1 roll");
            return;
        }

        var tower = currentBanner.GetRandomTower();
        Debug.Log($"🎯 Rolled one: {tower?.displayName} ({tower?.rarity})");

        UnlockTower(tower);
        gachaResultPanel.ShowResults(new List<TowerData> { tower });
    }

    public void RollTen()
    {
        Debug.Log("👉 RollTen button clicked");

        int totalCost = costPerRoll * 10;
        if (!CurrencyManager.Instance.TrySpendSeed(totalCost))
        {
            Debug.Log("❌ Not enough seed for 10 rolls");
            return;
        }

        List<TowerData> results = currentBanner.RollMulti(10);
        Debug.Log($"🎯 Rolled ten: {string.Join(", ", results.Select(r => r.displayName))}");

        foreach (var tower in results)
            UnlockTower(tower);

        gachaResultPanel.ShowResults(results);
    }



    void UnlockTower(TowerData tower)
    {
        var pd = PlayerDataManager.Instance.playerData;
        if (!pd.unlockedTowerIds.Contains(tower.towerId))
        {
            pd.unlockedTowerIds.Add(tower.towerId);
            PlayerDataManager.Instance.SavePlayerData();
            Debug.Log($"🔓 Unlocked: {tower.displayName}");
        }
        else
        {
            Debug.Log($"Already unlocked: {tower.displayName}");
        }
    }

    void ShowResult(TowerData tower)
    {
        GameObject go = Instantiate(resultUIPrefab, resultParent);
        Image img = go.GetComponent<Image>();
        if (img != null && tower.icon != null)
        {
            img.sprite = tower.icon;
        }

        // TODO: bạn có thể mở rộng phần hiển thị icon độ hiếm, hiệu ứng, tên, border theo rarity ở đây
    }
}
