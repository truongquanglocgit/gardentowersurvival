using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class EquipSlot : MonoBehaviour
{
    public GameObject highlight;

    [Header("Panel chèn vào đây (optional)")]
    [Tooltip("Nếu để trống, slot sẽ dùng luôn TowerImage có sẵn dưới chính EquipSlot.")]
    public RectTransform panelRoot;

    [Header("Child name để gán icon")]
    public string towerImageChildName = "TowerImage";

    // runtime
    private int slotIndex;
    private TowerEquipPanel panelRef;
    private Image _iconInPanel;    // Image của child "TowerImage" hiện tại

    // ======== Setup / Highlight ========
    public void Setup(int index, TowerEquipPanel panel)
    {
        slotIndex = index;
        panelRef = panel;

        var btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => panelRef.SelectEquipSlot(index));
        }

        // Resolve sẵn trong trường hợp không dùng panel động
        if (_iconInPanel == null)
            _iconInPanel = ResolveTowerImage();
    }

    public void SetHighlight(bool on)
    {
        if (highlight != null) highlight.SetActive(on);
    }

    // ======== Panel động (optional) ========
    /// <summary>
    /// Nhận prefab panel theo rarity. Nếu prefab == null → KHÔNG đụng hierarchy,
    /// chỉ re-resolve TowerImage hiện có (phục vụ trường hợp slot gắn cứng trong editor).
    /// </summary>
    public void SetPanelPrefab(GameObject prefab)
    {
        // Nếu không cấu hình panelRoot hoặc không truyền prefab → dùng layout gắn cứng
        if (prefab == null || panelRoot == null)
        {
            _iconInPanel = ResolveTowerImage(); // tìm TowerImage ở nơi thích hợp
            return;
        }

        // Dùng panel động: clear panel cũ trong panelRoot, rồi gắn prefab mới
        for (int i = panelRoot.childCount - 1; i >= 0; i--)
            Destroy(panelRoot.GetChild(i).gameObject);

        var inst = Instantiate(prefab, panelRoot);
        var rt = inst.transform as RectTransform;
        if (rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        _iconInPanel = ResolveTowerImage(); // resolve lại trên panel mới
    }

    // ======== Gán icon (luôn gán vào TowerImage) ========
    public void SetIcon(Sprite sprite)
    {
        if (_iconInPanel == null)
            _iconInPanel = ResolveTowerImage();

        if (_iconInPanel != null)
        {
            _iconInPanel.sprite = sprite;
            _iconInPanel.preserveAspect = true;
            _iconInPanel.type = Image.Type.Simple;
            _iconInPanel.enabled = (sprite != null);
        }
        else
        {
            Debug.LogWarning("[EquipSlot] Chưa resolve được TowerImage để gán icon.");
        }
    }

    // ======== Helpers ========
    /// <summary>Trả về Transform gốc để tìm TowerImage:
    /// - Có panelRoot → tìm bên trong panelRoot
    /// - Không có panelRoot → tìm từ chính EquipSlot (transform)
    /// </summary>
    private Transform GetSearchRoot()
    {
        return panelRoot != null ? (Transform)panelRoot : transform;
    }

    private Image ResolveTowerImage()
    {
        var root = GetSearchRoot();
        var tr = FindChildIgnoreCase(root, towerImageChildName);
        return tr ? tr.GetComponent<Image>() : null;
    }

    private static Transform FindChildIgnoreCase(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName)) return null;
        var stack = new Stack<Transform>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var t = stack.Pop();
            if (string.Equals(t.name, targetName, System.StringComparison.OrdinalIgnoreCase))
                return t;
            for (int i = 0; i < t.childCount; i++) stack.Push(t.GetChild(i));
        }
        return null;
    }
}
