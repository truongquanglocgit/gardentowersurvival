using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Linq;

public class GachaController : MonoBehaviour
{
    public GachaBannerData currentBanner;
    public int costPerRoll = 10;
    public TowerEquipPanel equipPanel;
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

        

        var tower = currentBanner?.GetRandomTower();
        if (tower == null)
        {
            Debug.LogError("[Gacha] GetRandomTower() trả về null. Kiểm tra config banner!");
            return;
        }

        Debug.Log($"🎯 Rolled one: {tower.displayName} ({tower.rarity})");
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

        

        List<TowerData> results = currentBanner?.RollMulti(10);
        if (results == null || results.Count == 0)
        {
            Debug.LogError("[Gacha] RollMulti trả về null/empty. Kiểm tra config banner!");
            return;
        }

        // Lọc null, thay bằng fallback để không crash
        int nullCount = results.Count(r => r == null);
        if (nullCount > 0)
        {
            Debug.LogWarning($"[Gacha] Có {nullCount} kết quả null. Sẽ thay bằng fallback.");
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i] == null)
                {
                    var fb = currentBanner.GetRandomTower();
                    if (fb == null)
                    {
                        Debug.LogError("[Gacha] Fallback cũng null. Bỏ qua slot này.");
                        continue;
                    }
                    results[i] = fb;
                }
            }
        }

        // KHÔNG gọi r.displayName trực tiếp nữa → tránh NRE
        var names = string.Join(", ", results.Where(r => r != null).Select(r => r.displayName));
        Debug.Log($"🎯 Rolled ten: {names}");

        foreach (var tower in results.Where(t => t != null))
            UnlockTower(tower);

        gachaResultPanel.ShowResults(results.Where(t => t != null).ToList());
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
        equipPanel.Rerender();
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
