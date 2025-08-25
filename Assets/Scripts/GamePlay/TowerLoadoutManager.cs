using UnityEngine;
using System.Collections.Generic;

public class TowerLoadoutManager : MonoBehaviour
{
    public static TowerLoadoutManager I;

    public List<TowerData> equippedTowers = new();
    public int maxTowers = 5;

    void Awake()
    {
        if (I == null) I = this;
        else Destroy(gameObject);
    }

    public bool TryAddTower(TowerData data)
    {
        if (equippedTowers.Contains(data)) return false;
        if (equippedTowers.Count >= maxTowers) return false;

        equippedTowers.Add(data);
        return true;
    }

    public void RemoveTower(TowerData data)
    {
        if (equippedTowers.Contains(data))
            equippedTowers.Remove(data);
    }

    public bool IsEquipped(TowerData data)
    {
        return equippedTowers.Contains(data);
    }
}
