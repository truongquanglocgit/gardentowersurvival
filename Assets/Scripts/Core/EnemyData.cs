using UnityEngine;
using System;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "TD/EnemyData")]
public class EnemyData : ScriptableObject
{
    public string enemyId;
    public GameObject prefab;
    
}

[Serializable]
public class SpawnItem
{
    public float startTime = 0f;       // giây kể từ đầu wave
    public string spawnerId;           // phải khớp Spawner.SpawnerId
    public string enemyId;             // khớp EnemyData.enemyId
    public int count = 5;
    public float interval = 0.5f;      // giữa 2 con
    public float powerMultiplier = 1f;
}

[CreateAssetMenu(menuName = "TD/WaveDef")]
public class WaveDef : ScriptableObject
{
    public string waveName = "Wave 1";
    public List<SpawnItem> items = new List<SpawnItem>();
    public int aliveCap = 60;          // backpressure limit
    public float interWaveDelay = 5f;  // delay sau wave (GameController dùng)
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
[CreateAssetMenu(menuName = "TD/TowerData")]
public class TowerData : ScriptableObject
{
    [Header("Info")]
    public string towerId;
    public string displayName;
    public Sprite icon;
    public Rarity rarity;
    [Header("Stats")]
    public int cost;
    public float range = 5f;
    public float fireRate = 1f;
    public float damage = 10f;

    [Header("Gameplay")]
    public GameObject prefab;
}
[CreateAssetMenu(menuName = "TD/Tower Rarity Group")]
public class TowerRarityGroup : ScriptableObject
{
    public string rarityName; // Common, Rare,...
    public List<TowerData> towerList;
}

