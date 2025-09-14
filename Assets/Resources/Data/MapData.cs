using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "TD/MapData")]
public class MapData : ScriptableObject
{
    public string mapName;
    public Sprite previewImage;
    public string sceneToLoad;
    public int waterReward;
    public List<string> plantRewardList;
    public List<WaveDef> waveList; // giữ lại
    // ❌ BỎ: public List<TowerData> startingTowers;
}
