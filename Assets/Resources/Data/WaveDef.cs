using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.Scripting;   // ✅ thêm dòng này

[Preserve]   // giữ lại class này khi build, không bị code stripping
[CreateAssetMenu(menuName = "TD/WaveDef")]
public class WaveDef : ScriptableObject
{
     public string waveName = "Wave 1";
    public List<SpawnItem> items = new List<SpawnItem>();
     public int aliveCap = 60;          // backpressure limit
    public float interWaveDelay = 5f;  // delay sau wave (GameController dùng)
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