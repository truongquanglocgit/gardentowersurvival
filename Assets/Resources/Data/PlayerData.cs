using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlayerData
{
    public int seedAmount ;
    public List<string> unlockedTowerIds = new() ;   // Id của TowerData đã mở
    public List<string> equippedTowerIds = new();   // Id của TowerData đã trang bị
}
