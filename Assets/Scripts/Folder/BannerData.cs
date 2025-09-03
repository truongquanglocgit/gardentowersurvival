using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "TD/GachaBanner")]
public class GachaBannerData : ScriptableObject
{
    public string bannerName;
    public Sprite bannerImage;

    [System.Serializable]
    public class RarityGroup
    {
        public Rarity rarity;
        [Range(0, 100)] public float weight;
        public TowerRarityGroup towerGroup; // nhóm các tower theo độ hiếm
    }

    public List<RarityGroup> rarityGroups;

    // ------------------- Gacha Roll -------------------

    public TowerData GetRandomTower()
    {
        var group = GetRandomRarityGroup();
        if (group == null || group.towerGroup == null || group.towerGroup.towerList.Count == 0)
        {
            Debug.LogWarning("❌ No valid towers to roll in this banner.");
            return null;
        }

        var list = group.towerGroup.towerList;
        return list[Random.Range(0, list.Count)];
    }

    public List<TowerData> RollMulti(int count)
    {
        List<TowerData> result = new();
        for (int i = 0; i < count; i++)
        {
            result.Add(GetRandomTower());
        }
        return result;
    }

    // ------------------- Internal -------------------

    private RarityGroup GetRandomRarityGroup()
    {
        float totalWeight = 0;
        foreach (var group in rarityGroups) totalWeight += group.weight;

        float roll = Random.Range(0, totalWeight);
        float sum = 0;
        foreach (var group in rarityGroups)
        {
            sum += group.weight;
            if (roll <= sum)
                return group;
        }

        return null;
    }
}
