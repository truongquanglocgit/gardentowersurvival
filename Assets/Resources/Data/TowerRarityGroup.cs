using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "TD/Tower Rarity Group")]
public class TowerRarityGroup : ScriptableObject
{
    [SerializeField] public string rarityName; // Common, Rare,...
    [SerializeField] public List<TowerData> towerList;
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
