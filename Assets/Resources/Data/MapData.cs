using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "TD/MapData")]
public class MapData : ScriptableObject
{
    [Header("Identity / Ordering")]
    public string mapId;
    public int orderIndex = 0;

    [Header("Meta")]
    public string mapName;
    public Sprite previewImage;
    public string sceneToLoad;

    [Header("Rewards / Waves")]
    public int waterReward;
    public int rainbowWaterReward;
    public float xp;
    public List<string> plantRewardList;
    public List<WaveDef> waveList;

    [Header("Main Menu 3D Preview")]
    public GameObject previewPrefab;
    public Vector3 previewPosition = Vector3.zero;
    public Vector3 previewEuler = new Vector3(0, 30, 0);
    public float previewScale = 1f;

    // 🔥 NEW: icon override (tuỳ map), nếu để trống sẽ dùng default icon trong WaveManager
    [Header("Reward Icons (Override per map)")]
    public Sprite waterRewardIcon;
    public Sprite rainbowWaterRewardIcon;
    public Sprite xpRewardIcon;
}
