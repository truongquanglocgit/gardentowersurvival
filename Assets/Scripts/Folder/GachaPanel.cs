using System.Collections.Generic;
using UnityEngine;

public class GachaPanel : MonoBehaviour
{
    [Header("Banners")]
    [Tooltip("Banner thường (cost tuỳ bạn, ví dụ dùng Water).")]
    public GachaBannerData normalBanner;

    [Tooltip("Banner Special (roll sẽ dùng Rainbow Water).")]
    public GachaBannerData specialBanner;

    [Header("Current Selection")]
    public GachaBannerData currentBanner;

    [Header("Special Costs (Rainbow Water)")]
    [Min(0)] public int specialCostOne = 1;
    [Min(0)] public int specialCostTen = 10;

    void Start()
    {
        // Ẩn Panel Gacha khi mới bắt đầu
        gameObject.SetActive(false);

        // Nếu chưa set, mặc định chọn normal
        if (currentBanner == null) currentBanner = normalBanner;
    }

    public void OpenPanel() => gameObject.SetActive(true);

    public void ClosePanel()
    {
#if ON_ADS
        gameObject.SetActive(false);
        MAXUtil.Instance.ShowInterAd(FirebaseEventMKTEnum.popup_close_gacha.ToString());
#else
        gameObject.SetActive(false);
#endif
    }

    // ===================== Banner switching =====================
    public void SelectNormalBanner()
    {
        if (!normalBanner) { Debug.LogWarning("Normal banner is NULL"); return; }
        currentBanner = normalBanner;
        Debug.Log("📜 Selected NORMAL banner");
        // TODO: cập nhật UI highlight nút tab nếu cần
    }

    public void SelectSpecialBanner()
    {
        if (!specialBanner) { Debug.LogWarning("Special banner is NULL"); return; }
        currentBanner = specialBanner;
        Debug.Log("🌟 Selected SPECIAL banner");
        // TODO: cập nhật UI highlight nút tab nếu cần
    }

    // ===================== Normal rolls (giữ y như cũ) =====================
    public void RollOnce()
    {
        if (currentBanner == null) { Debug.LogWarning("❌ No current banner selected"); return; }
        Debug.Log("🎲 Roll Once clicked (current banner)");

        // (Nếu normal có cost riêng, bạn thêm TrySpendWater ở đây)
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
        if (currentBanner == null) { Debug.LogWarning("❌ No current banner selected"); return; }
        Debug.Log("🎲 Roll Ten clicked (current banner)");

        // (Nếu normal có cost riêng, bạn thêm TrySpendWater ở đây)
        List<TowerData> results = currentBanner.RollMulti(10);
        foreach (var tower in results) UnlockTower(tower);
        ShowResult(results);
    }

    // ===================== Special rolls (Rainbow Water) =====================
    public void RollOnceSpecial()
    {
        if (!specialBanner) { Debug.LogWarning("❌ Special banner missing"); return; }
        if (!TrySpendRainbow(specialCostOne)) return;

        Debug.Log($"🌈 Roll SPECIAL x1 (cost {specialCostOne} Rainbow)");
        
        var result = specialBanner.GetRandomTower();
        if (result != null)
        {
            UnlockTower(result);
            ShowResult(new List<TowerData> { result });
        }
        else Debug.LogWarning("❌ Special RollOnce failed: No tower returned");
    }

    public void RollTenSpecial()
    {
        if (!specialBanner) { Debug.LogWarning("❌ Special banner missing"); return; }
        if (!TrySpendRainbow(specialCostTen)) return;

        Debug.Log($"🌈 Roll SPECIAL x10 (cost {specialCostTen} Rainbow)");
        var results = specialBanner.RollMulti(10);
        foreach (var t in results) UnlockTower(t);
        ShowResult(results);
    }

    // ===================== Helpers =====================
    bool TrySpendRainbow(int cost)
    {
        if (cost <= 0) return true; // free
        var cur = CurrencyManager.Instance;
        if (cur == null)
        {
            Debug.LogError("CurrencyManager.Instance is NULL");
            return false;
        }
        if (!cur.TrySpendRainbowWater(cost))
        {
            Debug.Log("❌ Not enough Rainbow Water");
            // TODO: bật popup “Không đủ Rainbow Water”
            return false;
        }
        return true;
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
        }
        // TODO: GachaResultPanel.ShowResults(towers) nếu có UI
    }
}
