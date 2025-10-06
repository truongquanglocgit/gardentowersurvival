using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

public class GachaResultPanel : MonoBehaviour
{
    [Header("Item Prefabs")]
    [Tooltip("Dùng khi chưa gán panel theo rarity hoặc làm mặc định.")]
    public GameObject resultItemPrefab;

    [Header("Rarity Panels")]
    public GameObject panelCommon;
    public GameObject panelUncommon;
    public GameObject panelRare;
    public GameObject panelEpic;
    public GameObject panelLegendary;
    public GameObject panelMythic;
    public GameObject panelVoid;

    [Header("Container & Controls")]
    public Transform resultContainer;
    public Button closeButton;

    [Header("Target child names")]
    [Tooltip("Tên child để gán icon Plant (không phân biệt hoa/thường).")]
    public string towerImageChildName = "TowerImage";   // <- bạn có thể đổi nếu muốn

    void Start()
    {
        
            
    }
    public void Close()
    {
#if ON_ADS
        gameObject.SetActive(false);
        TutorialManager.EmitSignalGlobal("DoneRoll");
        MAXUtil.Instance.ShowInterAd(FirebaseEventMKTEnum.popup_close_gacha.ToString());
#else
        gameObject.SetActive(false);
#endif
        

    }
    public void ShowResults(List<TowerData> towers)
    {
        if (resultContainer == null)
        {
            Debug.LogError("[GachaResultPanel] resultContainer is null.");
            return;
        }

        // Clear cũ
        for (int i = resultContainer.childCount - 1; i >= 0; i--)
            Destroy(resultContainer.GetChild(i).gameObject);

        // Spawn mới
        foreach (var tower in towers)
        {
            if (tower == null) continue;

            // 1) Chọn panel prefab theo rarity (hoặc fallback)
            var panelPrefab = GetPanelPrefabFor(tower.rarity);
            if (panelPrefab == null)
            {
                Debug.LogWarning($"[GachaResultPanel] Chưa gán panel cho rarity {tower.rarity}, dùng resultItemPrefab.");
                panelPrefab = resultItemPrefab;
            }
            if (panelPrefab == null)
            {
                Debug.LogError("[GachaResultPanel] Không có panelPrefab hợp lệ.");
                continue;
            }

            // 2) Tạo item
            var go = Instantiate(panelPrefab, resultContainer);

            // 3) Tìm đúng child "TowerImage" (ignore case) để gắn icon
            Image img = null;
            if (!string.IsNullOrEmpty(towerImageChildName))
            {
                var targetTr = FindChildIgnoreCase(go.transform, towerImageChildName);
                if (targetTr != null)
                {
                    img = targetTr.GetComponent<Image>();
                    if (img == null)
                        Debug.LogWarning($"[GachaResultPanel] Child '{towerImageChildName}' không có Image component.");
                }
                else
                {
                    Debug.LogWarning($"[GachaResultPanel] Không tìm thấy child '{towerImageChildName}' trong prefab '{panelPrefab.name}'. Dùng fallback Image đầu tiên.");
                }
            }

            // 4) Fallback: nếu không có child TowerImage, lấy Image đầu tiên trong cây con
            if (img == null)
                img = go.GetComponentInChildren<Image>(true);

            // 5) Gán icon + preserve aspect
            if (img != null)
            {
                img.sprite = tower.icon;
                img.preserveAspect = true;
                img.type = Image.Type.Simple;
            }
            else
            {
                Debug.LogWarning($"[GachaResultPanel] Không tìm thấy Image để gán icon cho item từ tower '{tower.displayName}'.");
            }

            // 6) Gán tên (lấy TMP_Text đầu tiên)
            var text = go.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
                text.text = tower.displayName ?? string.Empty;
        }

        gameObject.SetActive(true); // Hiện panel
    }

    private GameObject GetPanelPrefabFor(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Common: return panelCommon;
            case Rarity.Uncommon: return panelUncommon;
            case Rarity.Rare: return panelRare;
            case Rarity.Epic: return panelEpic;
            case Rarity.Legendary: return panelLegendary;
            case Rarity.Mythic: return panelMythic;
            case Rarity.Void: return panelVoid;
            default: return panelCommon;
        }
    }

    /// <summary>
    /// Tìm child theo tên, không phân biệt hoa/thường, duyệt đệ quy toàn bộ cây con.
    /// </summary>
    private Transform FindChildIgnoreCase(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName)) return null;

        var stack = new Stack<Transform>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var t = stack.Pop();
            if (string.Equals(t.name, targetName, StringComparison.OrdinalIgnoreCase))
                return t;

            for (int i = 0; i < t.childCount; i++)
                stack.Push(t.GetChild(i));
        }
        return null;
    }
}
