using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Linq;
using System.Collections;
using TMPro;

public class GachaController : MonoBehaviour
{
    public GachaBannerData normalBanner;
    public GachaBannerData specialBanner;
    public int costPerRoll = 1;
    public TowerEquipPanel equipPanel;
    public Transform resultParent;
    public GameObject resultUIPrefab;
    public GachaResultPanel gachaResultPanel;
    // ====================== FREE ROLL CONFIG ======================
    [Header("Free Roll")]
    [Tooltip("Số giờ cooldown cho Free roll kế tiếp.")]
    [Min(1)] public int freeCooldownHours = 1;

    [Tooltip("Free roll dùng banner nào?")]
    public FreeBannerType freeBannerType = FreeBannerType.Normal;

    [Tooltip("Nút Free (gọi OnClickFreeRoll)")]
    public Button freeRollButton;

    [Tooltip("Text hiển thị countdown còn lại (vd: 00:42:10)")]
    public TextMeshProUGUI freeCountdownText;

    [Tooltip("Hiển thị text khi Free đã sẵn sàng (vd: FREE!)")]
    public string freeReadyText = "FREE!";

    private Coroutine _freeTimerCo;

    public enum FreeBannerType { Normal, Special }

    // ============ Not-enough Toast (failsafe) ============


[Header("Not-enough Toast")]
[SerializeField] private TMP_Text notEnoughText;              // kéo TMP_Text vào
[SerializeField, Min(10f)] private float toastMoveDistance = 80f;
[SerializeField, Min(0.2f)] private float toastDuration = 1.2f;
[SerializeField] private AnimationCurve toastMoveCurve = null; // null => auto Ease
[SerializeField] private AnimationCurve toastFadeCurve = null; // null => Linear
[SerializeField] private bool toastUseUnscaledTime = true;

private Coroutine _toastCo;
private bool _toastActive;
private CanvasGroup _toastCg;
private Vector2 _toastTargetAnchored;  // vị trí dừng
private Vector2 _toastStartAnchored;   // vị trí bắt đầu

    void OnEnable()
    {
        EnsureToastSetup();
        // Bắt đầu/khởi tạo UI đếm ngược mỗi khi panel mở
        StartFreeTimerLoop();
        UpdateFreeButtonStateImmediate();
    }
    private void EnsureToastSetup()
    {
        if (!notEnoughText) return;

        if (_toastCg == null)
        {
            _toastCg = notEnoughText.GetComponent<CanvasGroup>();
            if (_toastCg == null) _toastCg = notEnoughText.gameObject.AddComponent<CanvasGroup>();
        }
        // Lưu vị trí đích (khi bạn đặt text trong editor)
        _toastTargetAnchored = notEnoughText.rectTransform.anchoredPosition;

        if (toastMoveCurve == null) toastMoveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        if (toastFadeCurve == null) toastFadeCurve = AnimationCurve.Linear(0, 0, 1, 1);

        // reset đảm bảo ngay từ đầu
        ToastForceHide();
    }
    /// <summary>Ẩn toast ngay lập tức & reset state (gọi khi panel đóng/mở).</summary>
    private void ToastForceHide()
    {
        if (!notEnoughText) return;

        if (_toastCo != null) StopCoroutine(_toastCo);
        _toastCo = null;
        _toastActive = false;

        // reset visual
        var rt = notEnoughText.rectTransform;
        rt.anchoredPosition = _toastTargetAnchored;

        if (_toastCg == null)
        {
            _toastCg = notEnoughText.GetComponent<CanvasGroup>();
            if (_toastCg == null) _toastCg = notEnoughText.gameObject.AddComponent<CanvasGroup>();
        }
        _toastCg.alpha = 0f;
        notEnoughText.gameObject.SetActive(false);
    }
    void OnDisable()
    {
        ToastForceHide();
        if (_freeTimerCo != null) StopCoroutine(_freeTimerCo);
        _freeTimerCo = null;
    }
    public void RollOnceNormal()
    {
        Debug.Log("👉 RollOnce button clicked");

        if (!CurrencyManager.Instance.TrySpendWater(costPerRoll))
        {
            ShowNotEnoughToast("Not enough Water");
            Debug.Log("❌ Not enough seed for 1 roll");
            return;
        }




        var tower = normalBanner?.GetRandomTower();
        if (tower == null)
        {
            Debug.LogError("[Gacha] GetRandomTower() trả về null. Kiểm tra config banner!");
            return;
        }

        Debug.Log($"🎯 Rolled one: {tower.displayName} ({tower.rarity})");
        UnlockTower(tower);
        gachaResultPanel.ShowResults(new List<TowerData> { tower });
    }
    /// <summary>
    /// Hiện thông báo thiếu currency. Nếu đang hiện, bỏ qua để không spam.
    /// </summary>
    private void ShowNotEnoughToast(string message)
    {
        if (notEnoughText == null) { Debug.LogWarning("NotEnough TMP_Text is not assigned."); return; }
        if (_toastCo != null) return; // đang chạy → không hiện thêm

        notEnoughText.gameObject.SetActive(true);
        notEnoughText.text = message;

        // đảm bảo có CanvasGroup để fade
        var cg = notEnoughText.GetComponent<CanvasGroup>();
        if (cg == null) cg = notEnoughText.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 1f;

        // đặt vị trí bắt đầu ở phía trên target, trôi xuống target
        var rt = notEnoughText.rectTransform;
        Vector2 target = rt.anchoredPosition;
        Vector2 start = target + new Vector2(0f, toastMoveDistance);
        rt.anchoredPosition = start;

        _toastCo = StartCoroutine(Co_Toast(rt, cg, start, target));
    }

    private IEnumerator Co_Toast(RectTransform rt, CanvasGroup cg, Vector2 start, Vector2 target)
    {
        float t = 0f;
        float d = Mathf.Max(0.1f, toastDuration);

        while (t < d)
        {
            t += toastUseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float k = Mathf.Clamp01(t / d);

            // move (từ trên xuống)
            float km = toastMoveCurve != null ? toastMoveCurve.Evaluate(k) : k;
            rt.anchoredPosition = Vector2.LerpUnclamped(start, target, km);

            // fade out
            float kf = toastFadeCurve != null ? toastFadeCurve.Evaluate(k) : k;
            cg.alpha = 1f - kf;

            yield return null;
        }

        cg.alpha = 0f;
        rt.anchoredPosition = target;
        _toastCo = null;
    }
    public void RollTenNormal()
    {
        Debug.Log("👉 RollTen button clicked");

        int totalCost = costPerRoll * 10;
        if (!CurrencyManager.Instance.TrySpendWater(totalCost))
        {
            ShowNotEnoughToast("Not enough Water");
            Debug.Log(" Not enough seed for 10 rolls");
            return;
        }




        List<TowerData> results = normalBanner?.RollMulti(10);
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
                    var fb = normalBanner.GetRandomTower();
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
    public void RollOnceSpecial()
    {
        Debug.Log("👉 RollOnce button clicked");

        if (!CurrencyManager.Instance.TrySpendRainbowWater(costPerRoll))
        {
            ShowNotEnoughToast("Not enough Rainbow Water");
            Debug.Log("❌ Not enough seed for 1 roll");
            return;
        }




        var tower = specialBanner?.GetRandomTower();
        if (tower == null)
        {
            Debug.LogError("[Gacha] GetRandomTower() trả về null. Kiểm tra config banner!");
            return;
        }

        Debug.Log($"🎯 Rolled one: {tower.displayName} ({tower.rarity})");
        UnlockTower(tower);
        gachaResultPanel.ShowResults(new List<TowerData> { tower });
    }

    public void RollTenSpecial()
    {
        Debug.Log("👉 RollTen button clicked");

        int totalCost = costPerRoll * 10;
        if (!CurrencyManager.Instance.TrySpendRainbowWater(totalCost))
        {
            ShowNotEnoughToast("Not enough Rainbow Water");
            Debug.Log("❌ Not enough seed for 10 rolls");
            return;
        }




        List<TowerData> results = specialBanner?.RollMulti(10);
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
                    var fb = specialBanner.GetRandomTower();
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
    // ====================== 🔥 FREE ROLL ======================
    /// <summary>OnClick cho nút Free Roll.</summary>
    public void OnClickFreeRoll()
    {
        if (!IsFreeReady())
        {
            Debug.Log("⏳ Free roll chưa sẵn sàng.");
            return;
        }

        // Chọn banner theo config
        GachaBannerData banner = (freeBannerType == FreeBannerType.Special) ? specialBanner : normalBanner;
        if (banner == null)
        {
            Debug.LogWarning("Free banner chưa được gán.");
            return;
        }

        List<TowerData> results = normalBanner?.RollMulti(10);
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
                    var fb = normalBanner.GetRandomTower();
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

        // Set mốc giờ kế tiếp
        SetNextFreeTimeFromNow();

        // Cập nhật UI ngay
        UpdateFreeButtonStateImmediate();
    }

    // ====================== Helpers: PlayerData time & UI ======================
    private static long NowUnix()
    {
        // UTC epoch seconds
        return System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private long GetNextFreeUnix()
    {
        return PlayerDataManager.Instance != null ? PlayerDataManager.Instance.playerData.nextFreeGachaUnix : 0;
    }

    private void SetNextFreeTimeFromNow()
    {
        long next = NowUnix() + (long)freeCooldownHours * 3600L;
        if (PlayerDataManager.Instance != null)
        {
            PlayerDataManager.Instance.playerData.nextFreeGachaUnix = next;
            PlayerDataManager.Instance.SavePlayerData();
        }
        else
        {
            Debug.LogWarning("PlayerDataManager.Instance is null – không thể lưu nextFreeGachaUnix.");
        }
    }

    private bool IsFreeReady()
    {
        long now = NowUnix();
        long next = GetNextFreeUnix();
        return now >= next;
    }

    private void StartFreeTimerLoop()
    {
        if (_freeTimerCo != null) StopCoroutine(_freeTimerCo);
        _freeTimerCo = StartCoroutine(Co_FreeTimerLoop());
    }

    private IEnumerator Co_FreeTimerLoop()
    {
        // Cập nhật mỗi 0.25 giây cho mượt; có thể tăng lên 1.0 giây nếu muốn nhẹ hơn
        const float interval = 0.25f;

        while (true)
        {
            UpdateFreeButtonStateImmediate();
            yield return new WaitForSecondsRealtime(interval);
        }
    }

    private void UpdateFreeButtonStateImmediate()
    {
        bool ready = IsFreeReady();

        if (freeRollButton)
        {
            // 🔹 Nếu chưa tới giờ → ẩn nút; khi sẵn sàng → hiện lên
            freeRollButton.gameObject.SetActive(ready);
            freeRollButton.interactable = ready;
        }

        if (freeCountdownText)
        {
            if (ready)
            {
                // Có thể tuỳ chọn: hiển thị "FREE!" hoặc ẩn hẳn text
                freeCountdownText.text = freeReadyText;
            }
            else
            {
                long now = NowUnix();
                long next = GetNextFreeUnix();
                long remain = Mathf.Max(0, (int)(next - now));
                freeCountdownText.text = FormatHMS(remain);
            }
        }
    }


    private static string FormatHMS(long seconds)
    {
        if (seconds <= 0) return "00:00:00";
        long h = seconds / 3600;
        long m = (seconds % 3600) / 60;
        long s = seconds % 60;
        if (h > 0) return $"{h:00}:{m:00}:{s:00}";
        return $"{m:00}:{s:00}";
    }
}
