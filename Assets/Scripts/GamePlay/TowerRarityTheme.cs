using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "TD/UI/Tower Rarity Theme", fileName = "TowerRarityTheme")]
public class TowerRarityTheme : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public Rarity rarity;

        [Header("Background / Frame")]
        [Tooltip("Prefab UI (RectTransform) làm nền/khung – sẽ được gắn vào backgroundRoot.")]
        public GameObject backgroundPrefab;

        [Tooltip("Màu nhấn (tùy dùng để tint hình trong prefab).")]
        public Color accentColor = Color.white;

        [Header("Badge (optional)")]
        public Sprite badgeSprite;
        public Color badgeTint = Color.white;
        public string badgeTextOverride; // ví dụ: EPIC/LEGENDARY,…

        [Header("Stars (optional)")]
        [Range(0, 10)] public int starCount;
        public Sprite starSprite;
        public Color starColor = Color.white;

        [Header("Overlay VFX (optional)")]
        public GameObject overlayVfxPrefab;
    }

    public List<Entry> entries = new();

    private Dictionary<Rarity, Entry> _map;

    public Entry Get(Rarity rarity)
    {
        if (_map == null || _map.Count != entries.Count)
        {
            _map = new Dictionary<Rarity, Entry>();
            foreach (var e in entries) _map[e.rarity] = e;
        }
        return _map != null && _map.TryGetValue(rarity, out var entry) ? entry : null;
    }
}
