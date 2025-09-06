using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;

public class TowerEquipPanel : MonoBehaviour
{
    
    [Header("UI References")]
    public Transform towerListContainer;
    public GameObject towerButtonPrefab;
    public List<EquipSlot> equipSlots;
    [Header("Info Panel")]
    [SerializeField] private TowerInfoPanel infoPanel;   // <-- kéo panel vào Inspector
    [SerializeField] private int infoPreviewLevel = 1;   // level dùng để xem trước
    [Header("Equip Config")]
    [Range(1, 8)]
    public int maxEquip = 5;
    [Tooltip("Nếu bật sẽ cho phép trang bị 2 slot cùng 1 tower")]
    public bool allowDuplicateEquip = false;

    [Header("Data Source")]
    [Tooltip("Kéo đúng AllTowerDatabase.asset (cùng file với GameSession) vào đây. Nếu để trống sẽ tự lấy từ GameSession.")]
    public AllTowerDatabase allTowerDatabase;

    // cache
    Dictionary<string, TowerData> byId;
    int selectedEquipSlotIndex = -1;
    List<TowerData> sourceCache; // lưu list hiện dùng để re-render

    void Start()
    {
        LoadData();
        // giới hạn theo số slot thật
        maxEquip = Mathf.Clamp(maxEquip, 1, equipSlots.Count);

        for (int i = 0; i < equipSlots.Count; i++)
            equipSlots[i].Setup(i, this);

        RenderEquippedSlots();
        RenderTowerList(sourceCache);
        ClosePanel();
    }
    public void LoadData()
    {
        // ƯU TIÊN: dùng asset gắn tay → nếu null thì lấy từ GameSession
        var db = allTowerDatabase != null ? allTowerDatabase
                 : (GameSession.Instance ? GameSession.Instance.allTowerDatabase : null);

        if (db == null || db.allTowers == null || db.allTowers.Count == 0)
        {
            Debug.LogError("❌ TowerEquipPanel: Chưa gán AllTowerDatabase (asset) và GameSession cũng không có. Gán asset giúp nhé.");
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
    }
    public void SelectEquipSlot(int index)
    {
        selectedEquipSlotIndex = Mathf.Clamp(index, 0, equipSlots.Count - 1);

        for (int i = 0; i < equipSlots.Count; i++)
            equipSlots[i].SetHighlight(i == selectedEquipSlotIndex);

        // cập nhật trạng thái nút theo slot đang chọn (để disable những tower đã được equip ở slot khác)
        if (sourceCache != null) RenderTowerList(sourceCache);
    }

    void RenderEquippedSlots()
    {
        var equippedIds = PlayerDataManager.Instance.playerData.equippedTowerIds;

        for (int i = 0; i < equipSlots.Count; i++)
        {
            Sprite icon = null;
            if (i < equippedIds.Count && !string.IsNullOrEmpty(equippedIds[i]) && byId.TryGetValue(equippedIds[i], out var td))
                icon = td.icon;

            equipSlots[i].SetIcon(icon);
            equipSlots[i].SetHighlight(i == selectedEquipSlotIndex);
        }
    }

    public void RenderTowerList(List<TowerData> source)
    {
        foreach (Transform child in towerListContainer) Destroy(child.gameObject);

        var unlockedIds = PlayerDataManager.Instance.playerData.unlockedTowerIds;
        var equippedIds = PlayerDataManager.Instance.playerData.equippedTowerIds;

        foreach (var tower in source)
        {
            if (!tower) continue;

            var btnObj = Instantiate(towerButtonPrefab, towerListContainer);
            var btn = btnObj.GetComponent<TowerSlotButton>();

            bool isUnlocked = unlockedIds.Contains(tower.towerId);

            // tower đang được trang bị ở slot nào khác?
            int equippedIndex = equippedIds.FindIndex(id => id == tower.towerId);
            bool isEquippedSomewhere = equippedIndex >= 0;
            bool equippedAtOtherSlot = isEquippedSomewhere && equippedIndex != selectedEquipSlotIndex;

            btn.Setup(tower, (towerData) =>
            {
                // ❗ Nếu chưa chọn slot equip → mở panel info thay vì trang bị
                if (selectedEquipSlotIndex < 0)
                {
                    if (infoPanel) infoPanel.OpenFor(towerData);
                    else Debug.Log("⚠️ Chưa gán TowerInfoPanel vào EquipPanel.");
                    return;
                }

                if (!isUnlocked)
                {
                    Debug.Log("🔒 Tower chưa mở khóa.");
                    return;
                }

                // === CHỐNG TRÙNG ===
                int dupIdx = equippedIds.FindIndex(id => id == towerData.towerId);
                if (!allowDuplicateEquip && dupIdx >= 0 && dupIdx != selectedEquipSlotIndex)
                {
                    Debug.Log($"❌ Tower '{towerData.towerId}' đã trang bị ở slot {dupIdx + 1}.");
                    for (int i = 0; i < equipSlots.Count; i++)
                        equipSlots[i].SetHighlight(i == dupIdx);
                    return;
                }

                EnsureSize(equippedIds, maxEquip);
                equippedIds[selectedEquipSlotIndex] = towerData.towerId;

                PlayerDataManager.Instance.SavePlayerData();
                RenderEquippedSlots();
                RenderTowerList(source);
            });

            // trạng thái click theo mở khóa & chống trùng
            var uiBtn = btn.GetComponent<Button>();
            if (uiBtn)
            {
                // Nếu đã equip ở slot khác → vẫn cho interactable = true
                // Nhưng khi click thì chỉ mở Info, không cho equip
                uiBtn.interactable = isUnlocked;
            }

            // màu icon
            if (btn.icon != null)
            {
                if (!isUnlocked) btn.icon.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                else if (equippedAtOtherSlot) btn.icon.color = Color.green;   // đã equip ở slot khác
                else btn.icon.color = Color.white;
            }
        }
    }

    static void EnsureSize(List<string> list, int size)
    {
        while (list.Count < size) list.Add("");
        if (list.Count > size) list.RemoveRange(size, list.Count - size);
    }

    public void ClosePanel() => gameObject.SetActive(false);
    public void OpenPanel()
    {
        
        gameObject.SetActive(true);
    }
}
