using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using System;

public class RewardManager : MonoBehaviour
{
    public static RewardManager I;

    private enum PendingType { None, FreeTower, UpgradeTower, Water }
    private PendingType _pendingType = PendingType.None;
    private TowerData _pendingTower;
    private int _pendingAmount;

    [Header("Panel / UI")]
    public GameObject rewardPanel;        // Panel hiển thị reward
    public Image rewardIcon;              // Ảnh minh hoạ (cây, mũi tên upgrade, nước…)
    public TMP_Text rewardText;           // Nội dung: "Upgrade: <Tên Tower>" / "Water x 100"…
    public Slider timerSlider;            // Slider đếm ngược (unscaled)
    public float showDuration = 2.5f;     // Hết thời gian -> đóng, KHÔNG nhận

    [Header("Sprites (tham số hoá)")]
    public Sprite upgradeSprite;          // Icon khi reward là upgrade
    public Sprite waterSprite;            // Icon khi reward là water
    public Sprite freeTowerSprite;        // Icon khi reward là free tower (nếu có)

    [Header("Fallback khi không còn cây nào upgrade được")]
    public int waterFallbackAmount = 0; // WATER thưởng thay thế

    private Coroutine _showCo;

    // ========= Free placements bank =========
    // Lưu số lần đặt miễn phí theo towerId
    private readonly Dictionary<string, int> _freeBank = new Dictionary<string, int>();

    // UI (như TowerSelectUI) có thể lắng nghe để cập nhật badge xN
    public event Action OnFreeCountsChanged;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        if (rewardPanel) rewardPanel.SetActive(false);
    }

    // =========================================================
    // ==============  ENTRY từ WaveManager gọi  ===============
    // =========================================================

    /// <summary>WaveManager gọi 1 hàm này để roll và present reward.</summary>
    public void RollAndPresentReward()
    {
        // Ví dụ tỉ lệ: 50% free tower, 30% upgrade, 20% water
        float r = UnityEngine.Random.value;
        if (r < 0.5f)
        {
            // FreeTower: 1 trong 5 cây đang equip
            var list = TowerLoadoutManager.I ? TowerLoadoutManager.I.equippedTowers : null;
            if (list == null || list.Count == 0) { ShowWaterPending(waterFallbackAmount); return; }

            TowerData pick = list[UnityEngine.Random.Range(0, list.Count)];
            int amount = 1; // tuỳ bạn tăng x2, x3...
            ShowFreeTowerPending(pick, amount);
        }
        else if (r < 0.8f)
        {
            // Upgrade random plant đang đặt trên map (nếu có)
            var upgradable = GetUpgradableTowersInScene();
            if (upgradable.Count == 0)
            {
                // Không còn cây để upgrade -> đổi sang Water
                ShowWaterPending(waterFallbackAmount);
                return;
            }
            var target = upgradable[UnityEngine.Random.Range(0, upgradable.Count)];
            ShowUpgradePending(target);
        }
        else
        {
            // Water
            ShowWaterPending(waterFallbackAmount);
        }
    }

    // =========================================================
    // ==============  PENDING - CHỈ HIỂN THỊ UI  ==============
    // =========================================================

    public void ShowFreeTowerPending(TowerData tower, int amount)
    {
        if (!tower) return;
        _pendingType = PendingType.FreeTower;
        _pendingTower = tower;
        _pendingAmount = Mathf.Max(1, amount);

        var icon = tower.icon ? tower.icon : freeTowerSprite;
        string name = string.IsNullOrEmpty(tower.displayName) ? tower.name : tower.displayName;
        ShowPanel(icon, $"{name} x{_pendingAmount} ");
    }

    public void ShowUpgradePending(TowerController towerInScene)
    {
        if (!towerInScene) { ShowWaterPending(waterFallbackAmount); return; }

        _pendingType = PendingType.UpgradeTower;
        _pendingTower = towerInScene.GetComponent<TowerData>() ? towerInScene.GetComponent<TowerData>() : null;
        // Lưu reference để biết tên/icon (nếu bạn không gắn TowerData vào Tower trong scene
        // thì chỉ dùng tên từ TowerController)
        string display = !string.IsNullOrEmpty(towerInScene.towerName) ? towerInScene.towerName : towerInScene.name;

        ShowPanel(upgradeSprite, $"Upgrade FREE: {display}");
    }

    public void ShowWaterPending(int amount)
    {
        Debug.Log(amount);
        _pendingType = PendingType.Water;
        _pendingTower = null;
        _pendingAmount = Mathf.Max(1, amount);
        ShowPanel(waterSprite, $"WATER x{_pendingAmount}");
    }

    // =========================================================
    // ===================  BUTTON HANDLERS  ===================
    // =========================================================

    /// <summary>Yes = nhận reward → áp dụng pending rồi đóng panel.</summary>
    public void OnClickYes()
    {
        ApplyPendingReward();
        ClosePanel();
    }

    /// <summary>No = từ chối → không nhận gì, chỉ đóng panel.</summary>
    public void OnClickNo()
    {
        ClearPending();
        ClosePanel();
    }

    // =========================================================
    // =================  ÁP DỤNG PENDING THẬT  =================
    // =========================================================

    private void ApplyPendingReward()
    {
        switch (_pendingType)
        {
            case PendingType.FreeTower:
                if (_pendingTower) GrantFreeTower(_pendingTower, _pendingAmount);
                break;

            case PendingType.UpgradeTower:
                // Tìm 1 tower để upgrade miễn phí
                // Bạn có thể thay bằng cách đã lưu sẵn ref TowerController cần upgrade
                // Ở đây mình cố gắng tìm ngẫu nhiên như lúc present:
                var upgradable = GetUpgradableTowersInScene();
                if (upgradable.Count > 0)
                {
                    // Chọn 1 tower bất kỳ để upgrade free
                    var pick = upgradable[UnityEngine.Random.Range(0, upgradable.Count)];
                    // Gọi UpgradeForFree nếu có, nếu không thì bỏ qua (vì flow yêu cầu free)
                    var mi = typeof(TowerController).GetMethod("UpgradeForFree",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (mi != null) { try { mi.Invoke(pick, null); } catch { } }
                }
                else
                {
                    // Không còn tower để upgrade → có thể chuyển thành Water nếu muốn
                    // GrantWater(waterFallbackAmount);
                }
                break;

            case PendingType.Water:
                GrantWater(_pendingAmount);
                break;
        }
        ClearPending();
    }

    private void ClearPending()
    {
        _pendingType = PendingType.None;
        _pendingTower = null;
        _pendingAmount = 0;
    }

    // =========================================================
    // ===============  FREE TOWER – PUBLIC API  ===============
    // =========================================================

    /// <summary>Cộng thêm 'count' lần đặt miễn phí cho 1 TowerData.</summary>
    // ====== FREE TOWER API ======
    public void GrantFreeTower(TowerData data, int count = 1)
    {
        if (!data) return;
        string id = GetTowerId(data);
        if (!_freeBank.ContainsKey(id)) _freeBank[id] = 0;
        _freeBank[id] += Mathf.Max(1, count);

        // 🔔 Bắn event để TowerSlotButton tự update UI
        OnFreeCountsChanged?.Invoke();

        if (freeTowerSprite)
            ShowPanel(freeTowerSprite, $"{data.displayName} +x{count} (Free Place)");
    }

    /// <summary>Trừ 1 lần free nếu còn → dùng tại TowerPlacer.PlaceTower().</summary>
    public bool TryConsumeFree(TowerData data)
    {
        if (!data) return false;
        string id = GetTowerId(data);
        if (_freeBank.TryGetValue(id, out int n) && n > 0)
        {
            _freeBank[id] = n - 1;
            OnFreeCountsChanged?.Invoke();  // 🔔 update UI ngay
            return true;
        }
        return false;
    }

    /// <summary>Lấy số lần free còn lại cho 1 tower (để hiển thị badge xN).</summary>
    public int GetFreeCount(TowerData data)
    {
        if (!data) return 0;
        string id = GetTowerId(data);
        return _freeBank.TryGetValue(id, out int n) ? n : 0;
    }

    private static string GetTowerId(TowerData d)
    {
        // Ưu tiên dùng towerId nếu có; fallback tên asset
        return d && !string.IsNullOrEmpty(d.towerId) ? d.towerId : (d ? d.name : "<null>");
    }

    // =========================================================
    // =====================  TIỆN ÍCH UI  =====================
    // =========================================================

    /// <summary>Hiển thị panel & bắt đầu đếm ngược (unscaled). KHÔNG tự nhận reward.</summary>
    private void ShowPanel(Sprite icon, string text)
    {
        if (!rewardPanel) return;

        if (_showCo != null) StopCoroutine(_showCo);

        if (rewardIcon) rewardIcon.sprite = icon;
        if (rewardText) rewardText.text = text;

        rewardPanel.SetActive(true);

        // Pause như các panel khác
        PauseManager.PushPause();

        _showCo = StartCoroutine(ShowTimer());
    }

    private System.Collections.IEnumerator ShowTimer()
    {
        float d = Mathf.Max(0.2f, showDuration);
        float t = 0f;
        if (timerSlider)
        {
            timerSlider.minValue = 0f;
            timerSlider.maxValue = d;
            timerSlider.value = d; // đếm lùi d → 0
        }

        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            if (timerSlider) timerSlider.value = d - t;
            yield return null;
        }

        // Hết giờ: coi như không nhận -> đóng panel, xoá pending
        OnClickNo();
    }

    public void ClosePanel()
    {
        if (_showCo != null) { StopCoroutine(_showCo); _showCo = null; }
        if (rewardPanel) rewardPanel.SetActive(false);

        // Resume
        PauseManager.PopPause();
    }

    // =========================================================
    // ==================  HỖ TRỢ UPGRADE  =====================
    // =========================================================

    // Tìm tất cả TowerController đang hoạt động và còn CanUpgrade
    private List<TowerController> GetUpgradableTowersInScene()
    {
        var all = FindObjectsOfType<TowerController>(includeInactive: false);
        var list = new List<TowerController>();

        foreach (var t in all)
        {
            if (!t || !t.gameObject.activeInHierarchy) continue;

            // Loại trừ preview/ghost bằng cách yêu cầu có collider đang bật
            bool hasActiveCollider = false;
            var cols = t.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
                if (cols[i] && cols[i].enabled) { hasActiveCollider = true; break; }

            if (!hasActiveCollider) continue;
            if (!t.CanUpgrade) continue;

            list.Add(t);
        }
        return list;
    }

    private void GrantWater(int amount)
    {
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.AddSeed(amount);
        else if (GameController.Instance != null)
            GameController.Instance.AddSeed(amount);
    }
}
