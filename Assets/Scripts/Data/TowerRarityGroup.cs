using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "TD/Tower Rarity Group")]
public class TowerRarityGroup : ScriptableObject
{
    public string rarityName; // Common, Rare,...
    public List<TowerData> towerList;
}
public enum Rarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary,
    Mythic,
    Void
}
