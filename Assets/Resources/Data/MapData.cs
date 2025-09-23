using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "TD/MapData")]
public class MapData : ScriptableObject
{
    [Header("Meta")]
    public string mapName;
    public Sprite previewImage;             // fallback nếu không có prefab
    public string sceneToLoad;

    [Header("Rewards / Waves")]
    public int waterReward;
    public List<string> plantRewardList;
    public List<WaveDef> waveList;

    [Header("Main Menu 3D Preview")]
    public GameObject previewPrefab;        // ← prefab map thu nhỏ để xoay
    public Vector3 previewPosition = Vector3.zero;
    public Vector3 previewEuler = new Vector3(0, 30, 0);
    public float previewScale = 1f;
}
