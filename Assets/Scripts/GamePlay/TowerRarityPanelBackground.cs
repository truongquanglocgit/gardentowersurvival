using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TowerRarityPanelBackground : MonoBehaviour
{
    [Header("Theme Source")]
    [Tooltip("Nếu để trống sẽ fallback sang TowerRarityThemeRegistry.Active")]
    public TowerRarityTheme theme;

    [Header("Roots")]
    public RectTransform backgroundRoot;   // nơi khung/nền sẽ được instantiate
    public RectTransform overlayRoot;      // nơi đặt VFX (optional)

    [Header("Badge (optional)")]
    public Image badgeImage;               // có thể null
    public TMP_Text badgeText;             // có thể null

    [Header("Stars (optional)")]
    public RectTransform starContainer;    // có thể null
    public GameObject starPrefab;          // một item sao (Image)

    // runtime cache
    private GameObject _bgInst;
    private GameObject _vfxInst;

    public void Apply(Rarity rarity)
    {
        var src = theme != null ? theme : TowerRarityThemeRegistry.Active;
        if (src == null) { Debug.LogError("[TowerRarityPanelBackground] No theme set."); return; }

        var entry = src.Get(rarity);
        if (entry == null)
        {
            Debug.LogWarning($"[TowerRarityPanelBackground] No entry for rarity {rarity}");
            Clear(backgroundRoot);
            Clear(overlayRoot);
            SetBadge(null, Color.white, null);
            SetStars(0, null, Color.white);
            return;
        }

        // Background
        if (backgroundRoot)
        {
            Clear(backgroundRoot);
            if (entry.backgroundPrefab)
            {
                _bgInst = Instantiate(entry.backgroundPrefab, backgroundRoot);
                Stretch(_bgInst.transform as RectTransform);

                // Tint nhanh (tuỳ chọn)
                var img = _bgInst.GetComponentInChildren<Image>();
                if (img) img.color = entry.accentColor;
            }
        }

        // Overlay VFX
        if (overlayRoot)
        {
            Clear(overlayRoot);
            if (entry.overlayVfxPrefab)
            {
                _vfxInst = Instantiate(entry.overlayVfxPrefab, overlayRoot);
                Stretch(_vfxInst.transform as RectTransform);
            }
        }

        // Badge
        var label = string.IsNullOrEmpty(entry.badgeTextOverride) ? rarity.ToString() : entry.badgeTextOverride;
        SetBadge(entry.badgeSprite, entry.badgeTint, label);

        // Stars
        SetStars(entry.starCount, entry.starSprite, entry.starColor);
    }

    private void SetBadge(Sprite sprite, Color tint, string text)
    {
        if (badgeImage)
        {
            badgeImage.enabled = sprite != null;
            badgeImage.sprite = sprite;
            badgeImage.color = tint;
        }
        if (badgeText)
        {
            badgeText.text = text ?? string.Empty;
        }
    }

    private void SetStars(int count, Sprite sprite, Color col)
    {
        if (!starContainer) return;
        Clear(starContainer);
        if (count <= 0 || !starPrefab) return;

        for (int i = 0; i < count; i++)
        {
            var star = Instantiate(starPrefab, starContainer);
            var img = star.GetComponentInChildren<Image>();
            if (img)
            {
                if (sprite) img.sprite = sprite;
                img.color = col;
            }
            var rt = star.transform as RectTransform;
            if (rt) rt.localScale = Vector3.one;
        }
    }

    private static void Clear(RectTransform root)
    {
        if (!root) return;
        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);
    }

    private static void Stretch(RectTransform rt)
    {
        if (!rt) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;
    }
}
