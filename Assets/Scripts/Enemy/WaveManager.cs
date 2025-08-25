using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public static class WaveRuntime
{
    public static int AliveCount = 0;
}

public class WaveManager : MonoBehaviour
{
    [Header("Data")]
    public WaveDef waveDef;
    public List<EnemyData> enemyDataList = new();

    private Dictionary<string, EnemyData> enemyMap;

    void Awake()
    {
        // Tạo map từ enemyId → EnemyData
        enemyMap = enemyDataList.ToDictionary(e => e.enemyId, e => e);
    }

    IEnumerator Start()
    {
        yield return new WaitUntil(() => PoolManager.I != null);
        PreloadEnemies();
    }

    public IEnumerator PlayWave()
    {
        WaveRuntime.AliveCount = 0;

        var sortedItems = waveDef.items.OrderBy(i => i.startTime).ToList();
        float waveStart = Time.time;

        foreach (var item in sortedItems)
        {
            float targetTime = waveStart + item.startTime;
            yield return new WaitUntil(() => Time.time >= targetTime);
            StartCoroutine(SpawnGroup(item));
        }

        yield return new WaitUntil(() => WaveRuntime.AliveCount <= 0);
        Debug.Log($"✅ Wave {waveDef.waveName} đã hoàn thành.");
    }

    IEnumerator SpawnGroup(SpawnItem item)
    {
        if (!SpawnerRegistry.Instance.TryGet(item.spawnerId, out var spawner))
        {
            Debug.LogError($"❌ Không tìm thấy SpawnerId = {item.spawnerId}");
            yield break;
        }

        if (!enemyMap.TryGetValue(item.enemyId, out var enemyData))
        {
            Debug.LogError($"❌ Không tìm thấy enemyId = {item.enemyId}");
            yield break;
        }

        for (int i = 0; i < item.count; i++)
        {
            while (WaveRuntime.AliveCount >= waveDef.aliveCap)
                yield return null;

            SpawnEnemy(enemyData, spawner, item.powerMultiplier);
            yield return new WaitForSeconds(item.interval);
        }
    }

    void SpawnEnemy(EnemyData data, Spawner spawner, float powerMultiplier)
    {
        GameObject enemy = PoolManager.I.Get(data.prefab, spawner.spawnPoint.position, spawner.spawnPoint.rotation);

        var controller = enemy.GetComponent<EnemyController>();
        if (controller != null)
        {
            controller.Init(powerMultiplier);
        }

        WaveRuntime.AliveCount++;
    }

    public void PreloadEnemies()
    {
        // Tính tổng số enemy spawn cùng lúc theo thời điểm → enemyId → time → totalCount
        Dictionary<string, Dictionary<float, int>> spawnTimeline = new();

        foreach (var item in waveDef.items)
        {
            if (!spawnTimeline.ContainsKey(item.enemyId))
                spawnTimeline[item.enemyId] = new Dictionary<float, int>();

            float keyTime = item.startTime;

            if (!spawnTimeline[item.enemyId].ContainsKey(keyTime))
                spawnTimeline[item.enemyId][keyTime] = item.count;
            else
                spawnTimeline[item.enemyId][keyTime] += item.count;
        }

        // Tìm peak spawn count mỗi enemyId
        Dictionary<string, int> maxCountPerEnemy = new();

        foreach (var kvp in spawnTimeline)
        {
            string enemyId = kvp.Key;
            int maxAtSameTime = 0;

            foreach (var timeCount in kvp.Value)
            {
                maxAtSameTime = Mathf.Max(maxAtSameTime, timeCount.Value);
            }

            maxCountPerEnemy[enemyId] = maxAtSameTime;
        }

        // WarmUp pool theo số lượng cần thiết
        foreach (var kvp in maxCountPerEnemy)
        {
            string enemyId = kvp.Key;
            int count = kvp.Value;

            if (!enemyMap.TryGetValue(enemyId, out var enemyData))
            {
                Debug.LogError($"❌ Không tìm thấy EnemyData cho enemyId = {enemyId}");
                continue;
            }

            PoolManager.I.WarmUp(enemyData.prefab, count);
            Debug.Log($"📦 Pool sẵn {count} con enemy [{enemyId}]");
        }
    }
}
