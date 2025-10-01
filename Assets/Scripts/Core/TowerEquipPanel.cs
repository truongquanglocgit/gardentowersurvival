using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using System;

public class TowerEquipPanel : MonoBehaviour
{
    [Serializable]
    public class RarityPanelSet
    {
        public GameObject common;
        public GameObject uncommon;
        public GameObject rare;
        public GameObject epic;
        public GameObject legendary;
        public GameObject mythic;
        public GameObject voidPanel; // 'void' là từ khóa -> đặt field khác tên
    }

    [Header("UI References")]
    public Transform towerListContainer;
    public List<EquipSlot> equipSlots;

    [Header("Info Panel")]
    [SerializeField] private TowerInfoPanel infoPanel;
    [SerializeField] private int infoPreviewLevel = 1;

    [Header("Equip Config")]
    [Range(1, 8)] public int maxEquip = 5;
    public bool allowDuplicateEquip = false;

    [Header("Data Source")]
    public AllTowerDatabase allTowerDatabase;

    [Header("Rarity Panels (DÙNG CHUNG CHO CẢ LIST & EQUIP)")]
    public RarityPanelSet panelSet;

    [Header("Child name để gán icon")]
    public string towerImageChildName = "TowerImage";

    // cache
    Dictionary<string, TowerData> byId;
    int selectedEquipSlotIndex = -1;   // -1 = không chọn slot nào
    List<TowerData> sourceCache;

    void Start()
    {
        LoadData();

        maxEquip = Mathf.Clamp(maxEquip, 1, equipSlots.Count);

        for (int i = 0; i < equipSlots.Count; i++)
        {
            equipSlots[i].towerImageChildName = towerImageChildName;
            equipSlots[i].Setup(i, this);
        }

        RenderEquippedSlots();
        RenderTowerList(sourceCache);
        ClosePanel();
    }

    // ================== Data ==================
    public void LoadData()
    {
        var db = allTowerDatabase != null ? allTowerDatabase
                 : (GameSession.Instance ? GameSession.Instance.allTowerDatabase : null);

        if (db == null || db.allTowers == null || db.allTowers.Count == 0)
        {
            Debug.LogError("❌ TowerEquipPanel: Chưa gán AllTowerDatabase và GameSession cũng không có.");
            gameObject.SetActive(false);
            return;
        }

        sourceCache = db.allTowers;
        byId = sourceCache
            .Where(t => t && !string.IsNullOrEmpty(t.towerId))
            .GroupBy(t => t.towerId)
            .ToDictionary(g => g.Key, g => g.First());
    }

    public void Rerender()
    {
        LoadData();
        RenderTowerList(sourceCache);
        RenderEquippedSlots();
    }

    // ================== Slot select/toggle ==================
    public void SelectEquipSlot(int index)
    {
        if (selectedEquipSlotIndex == index)
        {
            DeselectEquipSlot();
            return;
        }

        selectedEquipSlotIndex = Mathf.Clamp(index, 0, equipSlots.Count - 1);
        UpdateSlotHighlights();
        if (sourceCache != null) RenderTowerList(sourceCache);
    }

    void DeselectEquipSlot()
    {
        selectedEquipSlotIndex = -1;
        UpdateSlotHighlights();
        if (sourceCache != null) RenderTowerList(sourceCache);
    }

    void UpdateSlotHighlights()
    {
        for (int i = 0; i < equipSlots.Count; i++)
            equipSlots[i].SetHighlight(i == selectedEquipSlotIndex);
    }

    // ================== Render ==================
    void RenderEquippedSlots()
    {
        var equippedIds = PlayerDataManager.Instance.playerData.equippedTowerIds;

        for (int i = 0; i < equipSlots.Count; i++)
        {
            TowerData td = null;
            if (i < equippedIds.Count && !string.IsNullOrEmpty(equippedIds[i]) && byId.TryGetValue(equippedIds[i], out var found))
                td = found;

            // Nếu có set prefab rarity → dùng; nếu chưa set → truyền null để slot tự dùng layout gắn cứng
            var prefab = GetPanelPrefabFor(td ? td.rarity : Rarity.Common);
            equipSlots[i].SetPanelPrefab(prefab);   // prefab có thể null, slot vẫn hoạt động

            equipSlots[i].SetIcon(td ? td.icon : null);
            equipSlots[i].SetHighlight(i == selectedEquipSlotIndex);
        }
    }


    public void RenderTowerList(List<TowerData> source)
    {
        // Clear list cũ
        foreach (Transform child in towerListContainer) Destroy(child.gameObject);
        if (source == null) return;

        var unlockedIds = PlayerDataManager.Instance.playerData.unlockedTowerIds;
        var equippedIds = PlayerDataManager.Instance.playerData.equippedTowerIds;

        foreach (var tower in source)
        {
            if (!tower) continue;

            // 1) Chọn prefab list-item theo rarity (dùng 1 set prefab chung)
            var panelPrefab = GetPanelPrefabFor(tower.rarity);
            if (panelPrefab == null)
            {
                Debug.LogError($"[TowerEquipPanel] Chưa gán panel cho rarity {tower.rarity}");
                continue;
            }

            var go = Instantiate(panelPrefab, towerListContainer);
            // 🔤 đổi tên instance theo yêu cầu
            go.name = BuildPanelName(tower, tower.rarity, panelPrefab);
            // 2) LUÔN tìm child 'TowerImage' để gán sprite
            var imgTr = FindChildIgnoreCase(go.transform, towerImageChildName);
            var img = imgTr ? imgTr.GetComponent<Image>() : null;
            if (img != null)
            {
                img.sprite = tower.icon;
                img.preserveAspect = true;
                img.type = Image.Type.Simple;
                img.enabled = (tower.icon != null);
            }
            else
            {
                Debug.LogError($"[TowerEquipPanel] Prefab '{panelPrefab.name}' không có child Image '{towerImageChildName}'.");
            }

            // 3) Trạng thái mở khoá / đã equip ở slot khác (tại thời điểm render)
            bool isUnlocked = unlockedIds.Contains(tower.towerId);
            int equippedIndex = equippedIds.FindIndex(id => id == tower.towerId);
            bool isEquippedSomewhere = equippedIndex >= 0;
            bool equippedAtOtherSlot = isEquippedSomewhere && equippedIndex != selectedEquipSlotIndex;

            // 4) Bind button hành vi + truyền TowerController cho Info Panel
            var slotBtn = go.GetComponent<TowerSlotButton>();
            if (slotBtn == null) slotBtn = go.GetComponentInChildren<TowerSlotButton>(true);

            if (slotBtn != null)
            {
                // Không cho Setup tự gán icon (panel đã gán vào TowerImage)
                slotBtn.bindIconInSetup = false;
                slotBtn.icon = img; // để có thể tint toàn nút bên trong script của button

                var localBtn = slotBtn; // capture đúng instance trong lambda

                // Setup: đặt tên/cost + lấy sẵn controllerFromPrefab
                slotBtn.Setup(tower, (towerData) =>
                {
                    // RE-CHECK trạng thái khi click (tránh lệ thuộc biến đóng băng lúc render)
                    var unlockedNow = PlayerDataManager.Instance.playerData.unlockedTowerIds.Contains(towerData.towerId);
                    var equippedIdsNow = PlayerDataManager.Instance.playerData.equippedTowerIds;
                    int equippedIdxNow = equippedIdsNow.FindIndex(id => id == towerData.towerId);
                    bool equippedElsewhereNow = equippedIdxNow >= 0 && equippedIdxNow != selectedEquipSlotIndex;

                    // CHƯA chọn slot → mở Info kèm TowerController từ prefab (nằm ở child)
                    if (selectedEquipSlotIndex < 0)
                    {
                        if (infoPanel)
                        {
                            var ctrl = localBtn.controllerFromPrefab
                                       ?? towerData?.prefab?.GetComponentInChildren<TowerController>(true);
                            infoPanel.OpenFor(towerData, ctrl);
                        }
                        else Debug.Log("⚠️ Chưa gán TowerInfoPanel.");
                        return;
                    }

                    // Chưa mở khoá
                    if (!unlockedNow)
                    {
                        Debug.Log("🔒 Tower chưa mở khóa.");
                        return;
                    }

                    // Chống trùng
                    int dupIdx = equippedIdsNow.FindIndex(id => id == towerData.towerId);
                    if (!allowDuplicateEquip && dupIdx >= 0 && dupIdx != selectedEquipSlotIndex)
                    {
                        if (infoPanel)
                        {
                            var ctrl = localBtn.controllerFromPrefab
                                       ?? towerData?.prefab?.GetComponentInChildren<TowerController>(true);
                            infoPanel.OpenFor(towerData, ctrl);
                        }
                        for (int i = 0; i < equipSlots.Count; i++)
                            equipSlots[i].SetHighlight(i == dupIdx);
                        return;
                    }

                    // Equip bình thường
                    EnsureSize(equippedIdsNow, maxEquip);
                    equippedIdsNow[selectedEquipSlotIndex] = towerData.towerId;

                    PlayerDataManager.Instance.SavePlayerData();

                    // Re-check rarity & refresh UI
                    RenderEquippedSlots();
                    RenderTowerList(source);

                    // Hủy trạng thái chờ equip
                    DeselectEquipSlot();
                });

                // ✅ ÁP TRẠNG THÁI NGAY LÚC RENDER (XÁM CẢ NÚT NẾU ĐÃ EQUIP Ở SLOT KHÁC)
                slotBtn.ApplyListItemState(isUnlocked, equippedAtOtherSlot);
            }
            else
            {
                // Fallback nếu prefab không có TowerSlotButton: bind trực tiếp vào Button
                var btn = go.GetComponent<Button>();
                if (btn == null) btn = go.GetComponentInChildren<Button>(true);
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() =>
                    {
                        var ctrl = tower?.prefab?.GetComponentInChildren<TowerController>(true);

                        if (selectedEquipSlotIndex < 0)
                        {
                            if (infoPanel) infoPanel.OpenFor(tower, ctrl);
                            return;
                        }

                        // Re-check khi click
                        var unlockedNow = PlayerDataManager.Instance.playerData.unlockedTowerIds.Contains(tower.towerId);
                        var equippedIdsNow = PlayerDataManager.Instance.playerData.equippedTowerIds;

                        if (!unlockedNow)
                        {
                            Debug.Log("🔒 Tower chưa mở khóa.");
                            return;
                        }

                        int dupIdx = equippedIdsNow.FindIndex(id => id == tower.towerId);
                        if (!allowDuplicateEquip && dupIdx >= 0 && dupIdx != selectedEquipSlotIndex)
                        {
                            if (infoPanel) infoPanel.OpenFor(tower, ctrl);
                            for (int i = 0; i < equipSlots.Count; i++)
                                equipSlots[i].SetHighlight(i == dupIdx);
                            return;
                        }

                        EnsureSize(equippedIdsNow, maxEquip);
                        equippedIdsNow[selectedEquipSlotIndex] = tower.towerId;

                        PlayerDataManager.Instance.SavePlayerData();
                        RenderEquippedSlots();
                        RenderTowerList(source);
                        DeselectEquipSlot();
                    });
                }

                // ✅ Fallback: xám/khôi phục ngay lúc render
                var allG = go.GetComponentsInChildren<Graphic>(true);
                var gray = new Color(0.6f, 0.6f, 0.6f, 1f);
                foreach (var g in allG)
                    g.color = (!isUnlocked || equippedAtOtherSlot) ? gray : Color.white;

                if (btn != null) btn.interactable = isUnlocked; // chưa mở khoá thì không cho bấm
            }
        }
    }



    // ================== Click handler dùng chung ==================
    private void OnClickListItem(TowerData towerData, bool isUnlocked, List<string> equippedIds, List<TowerData> source)
    {
        if (selectedEquipSlotIndex < 0)
        {
            if (infoPanel) infoPanel.OpenFor(towerData);
            else Debug.Log("⚠️ Chưa gán TowerInfoPanel.");
            return;
        }

        if (!isUnlocked)
        {
            Debug.Log("🔒 Tower chưa mở khóa.");
            return;
        }

        int dupIdx = equippedIds.FindIndex(id => id == towerData.towerId);
        if (!allowDuplicateEquip && dupIdx >= 0 && dupIdx != selectedEquipSlotIndex)
        {
            if (infoPanel) infoPanel.OpenFor(towerData);
            for (int i = 0; i < equipSlots.Count; i++)
                equipSlots[i].SetHighlight(i == dupIdx);
            return;
        }

        EnsureSize(equippedIds, maxEquip);
        equippedIds[selectedEquipSlotIndex] = towerData.towerId;

        PlayerDataManager.Instance.SavePlayerData();

        // Re-check rarity & đổi panel ngay
        RenderEquippedSlots();
        RenderTowerList(source);

        DeselectEquipSlot();
    }

    // ================== Open/Close ==================
    public void ClosePanel() => gameObject.SetActive(false);

    public void OpenPanel()
    {
        gameObject.SetActive(true);
        DeselectEquipSlot();

        RenderEquippedSlots();
        if (sourceCache != null) RenderTowerList(sourceCache);
    }

    // ================== Helpers ==================
    private GameObject GetPanelPrefabFor(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Common: return panelSet.common;
            case Rarity.Uncommon: return panelSet.uncommon;
            case Rarity.Rare: return panelSet.rare;
            case Rarity.Epic: return panelSet.epic;
            case Rarity.Legendary: return panelSet.legendary;
            case Rarity.Mythic: return panelSet.mythic;
            case Rarity.Void: return panelSet.voidPanel;
            default: return panelSet.common;
        }
    }

    private static void EnsureSize(List<string> list, int size)
    {
        while (list.Count < size) list.Add("");
        if (list.Count > size) list.RemoveRange(size, list.Count - size);
    }

    private static Transform FindChildIgnoreCase(Transform root, string targetName)
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
    // === thêm helper ở cuối class (trước dấu ngoặc cuối) ===
    private static string BuildPanelName(TowerData tower, Rarity rarity, GameObject prefab)
    {
        // Tên hiển thị: ưu tiên displayName, fallback towerId, cuối cùng là "Tower"
        string baseName = !string.IsNullOrWhiteSpace(tower.displayName) ? tower.displayName
                         : (!string.IsNullOrWhiteSpace(tower.towerId) ? tower.towerId : "Tower");

        // Tên rarity panel: theo yêu cầu "panelrarity"
        // Ví dụ: "Cactus_Epic_panelrarity"
        return $"{baseName}_{rarity}_panelrarity";
    }

}
