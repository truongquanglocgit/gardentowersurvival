using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlayerData
{
    public int seedAmount;

    // Towers
    public List<string> unlockedTowerIds = new();
    public List<string> equippedTowerIds = new();

    // Map progression
    public List<string> completedMapIds = new();

    // Tutorial flag
    public bool tutorialCompleted = false;

    // NEW: Player progression
    public int playerLevel = 1;     // Bắt đầu từ Level 1
    public int currentXp = 0;       // XP hiện tại
    public int totalAds;
}
