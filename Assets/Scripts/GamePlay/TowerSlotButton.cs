using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

public class TowerSlotButton : MonoBehaviour
{
    [Header("UI")]
    public Image icon;                 // Panel sẽ gán sprite vào child "TowerImage"
    public TMP_Text nameText;
    public TMP_Text costText;

    [Header("Behavior")]
    [Tooltip("Nếu false, Setup sẽ KHÔNG gán icon (để Panel/Slot gán trực tiếp vào child TowerImage).")]
    public bool bindIconInSetup = true;

    [Tooltip("Tên child Image để gắn sprite Tower (không phân biệt hoa/thường).")]
    public string towerImageChildName = "TowerImage";

    [Header("Tint")]
    [SerializeField] Color grayTint = new Color(0.18f, 0.18f, 0.18f, 1f);
    [Tooltip("Gốc để tint toàn bộ nút. Nếu để trống sẽ dùng chính GameObject của script.")]
    public Transform tintRoot;

    [Header("Data refs")]
    [Tooltip("Tham chiếu TowerController lấy từ prefab trong TowerData (không Instantiate).")]
    public TowerController controllerFromPrefab;

    private TowerData data;
    private Action<TowerData> onClick;

    // cache tint
    List<Graphic> _graphics;
    readonly Dictionary<Graphic, Color> _originalColors = new Dictionary<Graphic, Color>();
    bool _graphicsCached = false;

    void Awake()
    {
        // Ưu tiên đúng child TowerImage
        if (icon == null || !IsTowerImage(icon.transform))
        {
            var tr = FindChildIgnoreCase(transform, towerImageChildName);
            icon = tr ? tr.GetComponent<Image>() : null;
            if (icon == null) icon = GetComponentInChildren<Image>(); // fallback
        }

        if (nameText == null)
            nameText = GetComponentInChildren<TMP_Text>();

        // Nếu object đang active, có thể cache sẵn; nếu không, sẽ cache lazy khi dùng
        TryCacheGraphics();
    }

    public void Setup(TowerData towerData, Action<TowerData> onClicked)
    {
        data = towerData;
        onClick = onClicked;

        // LẤY TOWERCONTROLLER TỪ PREFAB (không Instantiate)
        controllerFromPrefab = null;
        if (data != null && data.prefab != null)
        {
            controllerFromPrefab = data.prefab.GetComponent<TowerController>()
                                   ?? data.prefab.GetComponentInChildren<TowerController>(true);
        }

        // KHÔNG gán icon ở đây nếu để Panel/Slot gán
        if (bindIconInSetup && icon != null)
        {
            icon.sprite = data ? data.icon : null;
            icon.preserveAspect = true;
            icon.type = Image.Type.Simple;
            icon.enabled = (data && data.icon);
        }

        if (nameText != null) nameText.text = data ? data.displayName : string.Empty;
        if (costText != null) costText.text = data ? data.cost.ToString() : string.Empty;

        var btn = GetButtonOnTintRoot();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => onClick?.Invoke(data));
        }

        // Đảm bảo đã có cache trước khi ai đó gọi ApplyListItemState ngay sau Setup
        TryCacheGraphics();
    }

    /// <summary>
    /// Cập nhật trạng thái hiển thị của list item.
    /// - isUnlocked == false  => xám toàn nút + không cho bấm
    /// - isEquippedElsewhere  => xám toàn nút + vẫn cho bấm để xem info
    /// - ngược lại            => khôi phục màu gốc + cho bấm
    /// </summary>
    public void ApplyListItemState(bool isUnlocked, bool isEquippedElsewhere)
    {
        TryCacheGraphics();

        var btn = GetButtonOnTintRoot();

        if (!isUnlocked)
        {
            if (btn) btn.interactable = false;
            TintAll(grayTint);
            return;
        }

        if (isEquippedElsewhere)
        {
            if (btn) btn.interactable = true; // vẫn cho click để xem info
            TintAll(grayTint);
            return;
        }

        if (btn) btn.interactable = true;
        RestoreColors();
    }

    // ===== Helpers =====
    private void TintAll(Color c)
    {
        TryCacheGraphics();
        foreach (var g in _graphics)
            if (g) g.color = c;
    }

    private void RestoreColors()
    {
        TryCacheGraphics();
        foreach (var kv in _originalColors)
            if (kv.Key) kv.Key.color = kv.Value;
    }

    private void TryCacheGraphics()
    {
        if (_graphicsCached) return;

        var root = tintRoot ? tintRoot : transform;
        // Thu thập mọi Graphic dưới gốc (kể cả inactive)
        var gs = root.GetComponentsInChildren<Graphic>(true);
        _graphics = new List<Graphic>(gs.Length);

        _originalColors.Clear();
        foreach (var g in gs)
        {
            if (g == null) continue;
            _graphics.Add(g);
            if (!_originalColors.ContainsKey(g))
                _originalColors[g] = g.color;
        }

        _graphicsCached = true;
    }

    private Button GetButtonOnTintRoot()
    {
        var root = tintRoot ? tintRoot : transform;
        var btn = root.GetComponent<Button>();
        if (btn == null) btn = root.GetComponentInChildren<Button>(true);
        return btn;
    }

    private bool IsTowerImage(Transform t)
        => t != null && string.Equals(t.name, towerImageChildName, StringComparison.OrdinalIgnoreCase);

    private static Transform FindChildIgnoreCase(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName)) return null;
        var stack = new Stack<Transform>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var cur = stack.Pop();
            if (string.Equals(cur.name, targetName, StringComparison.OrdinalIgnoreCase))
                return cur;
            for (int i = 0; i < cur.childCount; i++)
                stack.Push(cur.GetChild(i));
        }
        return null;
    }
}
