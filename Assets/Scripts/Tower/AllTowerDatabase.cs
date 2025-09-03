using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "TD/AllTowerDatabase")]
public class AllTowerDatabase : ScriptableObject
{
    public List<TowerData> allTowers;

    public List<TowerData> GetTowersByRarity(Rarity rarity)
    {
        return allTowers.FindAll(t => t.rarity == rarity);
    }
}
