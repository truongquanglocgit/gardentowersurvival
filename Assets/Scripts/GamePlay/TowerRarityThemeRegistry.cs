using UnityEngine;

public static class TowerRarityThemeRegistry
{
    private static TowerRarityTheme _active;
    public static TowerRarityTheme Active
    {
        get => _active;
        set => _active = value;
    }
}
