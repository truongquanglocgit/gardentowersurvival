using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;   // nếu bạn đã có countdownText
using System;

public static class WaveRuntime
{
    private static int _aliveCount = 0;
    public static int AliveCount
    {
        get => _aliveCount;
        set => _aliveCount = Math.Max(0, value);
    }

    // ✅ Sự kiện thông báo 1 quái đã chết (để WaveManager cập nhật killedThisWave)
    public static event Action OnEnemyDied;

    public static void NotifyEnemyDied()
    {
        OnEnemyDied?.Invoke();
    }
}

public class WaveManager : MonoBehaviour
{
    [Header("Data")]
    public List<WaveDef> waveList = new();
    public List<EnemyData> enemyDataList = new();

    private Dictionary<string, EnemyData> enemyMap;
    private int currentWaveIndex = 0;

    [Header("Wave Delay Control")]
    public bool isSkippingDelay = false;
    public float interWaveTimer = 0f;

    [Header("UI")]
    public TMP_Text countdownText;              // (đã có từ lần trước)
    public bool autoHideCountdown = true;

    [Header("Mid-wave Skip")]
    public GameObject skipMidWaveButton;        // ✅ Kéo thả nút vào đây (SetActive false lúc đầu)
    public bool clearEnemiesOnSkip = true;      // ✅ Có dọn sạch quái đang sống khi skip không?

    // ✅ Theo dõi trạng thái wave hiện tại
    private int pendingToSpawn = 0;             // còn bao nhiêu con CHƯA spawn
    private int totalPlannedThisWave = 0;       // tổng dự kiến sẽ spawn trong wave
    private int killedThisWave = 0;             // số đã chết trong wave
    private bool forceEndCurrentWave = false;   // cờ kết thúc ngay wave hiện tại
    private readonly List<Coroutine> runningSpawners = new(); // để dừng các nhóm spawn

    void Awake()
    {
        enemyMap = enemyDataList.ToDictionary(e => e.enemyId, e => e);
        if (countdownText) countdownText.gameObject.SetActive(false);
        if (skipMidWaveButton) skipMidWaveButton.SetActive(false);
    }

    void OnEnable()
    {
        WaveRuntime.OnEnemyDied += HandleEnemyDiedInWave;
    }

    void OnDisable()
    {
        WaveRuntime.OnEnemyDied -= HandleEnemyDiedInWave;
    }

    IEnumerator Start()
    {
        yield return new WaitUntil(() => PoolManager.I != null);
        PreloadEnemiesAllWaves();
        // yield return StartCoroutine(PlayWaves());
    }

    public IEnumerator PlayWaves()
    {
        for (currentWaveIndex = 0; currentWaveIndex < waveList.Count; currentWaveIndex++)
        {
            var wave = waveList[currentWaveIndex];

            // Chơi 1 wave
            yield return StartCoroutine(PlaySingleWave(wave));

            // Ẩn nút skip (nếu còn hiện)
            if (skipMidWaveButton) skipMidWaveButton.SetActive(false);

            // Đếm ngược chuyển wave (phần này giữ nguyên từ trước)
            isSkippingDelay = false;
            interWaveTimer = wave.interWaveDelay;

            if (countdownText) countdownText.gameObject.SetActive(true);

            while (interWaveTimer > 0f && !isSkippingDelay)
            {
                interWaveTimer -= Time.deltaTime;
                if (countdownText)
                {
                    int sec = Mathf.Max(0, Mathf.CeilToInt(interWaveTimer));
                    countdownText.text = $"Wave {currentWaveIndex + 1} ✓  |  Next wave in {sec}s\n<alpha=#AA>(Tap Skip to start now)</alpha>";
                }
                yield return null;
            }

            if (countdownText && autoHideCountdown)
                countdownText.gameObject.SetActive(false);
        }

        if (countdownText)
        {
            countdownText.text = "All waves completed!";
            countdownText.gameObject.SetActive(true);
        }
    }

    private IEnumerator PlaySingleWave(WaveDef wave)
    {
        // Reset trạng thái của wave
        forceEndCurrentWave = false;
        killedThisWave = 0;
        WaveRuntime.AliveCount = 0;
        runningSpawners.Clear();

        var sortedItems = wave.items.OrderBy(i => i.startTime).ToList();
        float waveStart = Time.time;

        totalPlannedThisWave = sortedItems.Sum(i => i.count);
        pendingToSpawn = totalPlannedThisWave;

        if (skipMidWaveButton) skipMidWaveButton.SetActive(false);

        // Lên lịch spawn các nhóm
        foreach (var item in sortedItems)
        {
            float targetTime = waveStart + item.startTime;
            yield return new WaitUntil(() => Time.time >= targetTime || forceEndCurrentWave);

            if (forceEndCurrentWave) break;

            // chạy SpawnGroup có kiểm tra forceEndCurrentWave
            var co = StartCoroutine(SpawnGroup(item, wave));
            runningSpawners.Add(co);
        }

        // Chờ tới khi đã spawn hết & không còn quái sống, HOẶC bị forceEnd
        yield return new WaitUntil(() =>
            forceEndCurrentWave || (pendingToSpawn <= 0 && WaveRuntime.AliveCount <= 0)
        );

        // Nếu forceEnd: đảm bảo dừng hết spawners còn lại
        if (forceEndCurrentWave)
        {
            foreach (var co in runningSpawners)
                if (co != null) StopCoroutine(co);

            runningSpawners.Clear();
        }

        // Dọn UI nút skip giữa wave (nếu còn)
        if (skipMidWaveButton) skipMidWaveButton.SetActive(false);
    }

    // ✅ Khi có 1 quái chết, kiểm tra ngưỡng 50%
    private void HandleEnemyDiedInWave()
    {
        killedThisWave++;
        Debug.Log($"[WaveManager] Enemy died. killedThisWave={killedThisWave}/{totalPlannedThisWave}");

        if (!forceEndCurrentWave &&
            totalPlannedThisWave > 0 &&
            killedThisWave >= (totalPlannedThisWave + 1) / 2)
        {
            Debug.Log("[WaveManager] >=50% enemy killed. Show Skip button!");
            if (skipMidWaveButton && !skipMidWaveButton.activeSelf)
                skipMidWaveButton.SetActive(true);
        }
    }


    // ✅ Hàm UI gọi để skip ngay sang wave kế tiếp
    public void SkipMidWaveNow()
    {
        if (forceEndCurrentWave) return;

        Debug.Log("[WaveManager] SkipMidWaveNow clicked!");

        forceEndCurrentWave = true;
        pendingToSpawn = 0;

        if (clearEnemiesOnSkip)
        {
            var allEnemies = FindObjectsOfType<EnemyController>();
            foreach (var e in allEnemies)
                e.Die();
            WaveRuntime.AliveCount = 0;
        }

        if (skipMidWaveButton) skipMidWaveButton.SetActive(false);

        // ✅ Bắt đầu luôn wave tiếp theo
        if (currentWaveIndex + 1 < waveList.Count)
        {
            Debug.Log("Skipping.");
            StopAllCoroutines(); // dừng coroutine hiện tại
            StartCoroutine(PlaySingleWave(waveList[currentWaveIndex + 1]));
            currentWaveIndex++; // cập nhật index
        }
        else
        {
            Debug.Log("[WaveManager] No more waves to skip to.");
        }
    }



    IEnumerator SpawnGroup(SpawnItem item, WaveDef wave)
    {
        if (!SpawnerRegistry.Instance.TryGet(item.spawnerId, out var spawner)) yield break;
        if (!enemyMap.TryGetValue(item.enemyId, out var enemyData)) yield break;

        for (int i = 0; i < item.count; i++)
        {
            if (forceEndCurrentWave) yield break;

            while (!forceEndCurrentWave && WaveRuntime.AliveCount >= wave.aliveCap)
                yield return null;

            if (forceEndCurrentWave) yield break;

            SpawnEnemy(enemyData, spawner, item.powerMultiplier);

            // giảm số còn phải spawn
            pendingToSpawn = Mathf.Max(0, pendingToSpawn - 1);

            if (item.interval > 0f)
                yield return new WaitForSeconds(item.interval);
            else
                yield return null;
        }
    }

    void SpawnEnemy(EnemyData data, Spawner spawner, float powerMultiplier)
    {
        GameObject enemy = PoolManager.I.Get(data.prefab, spawner.spawnPoint.position, spawner.spawnPoint.rotation);
        var controller = enemy.GetComponent<EnemyController>();
        if (controller != null)
            controller.Init(powerMultiplier);
        controller.BeginWaveLifetime();

        WaveRuntime.AliveCount++;
    }

    public void LoadWaves(List<WaveDef> waves) => waveList = waves;

    public void PreloadEnemiesAllWaves()
    {
        var maxCountPerEnemy = new Dictionary<string, int>();

        foreach (var wave in waveList)
            foreach (var item in wave.items)
            {
                if (!maxCountPerEnemy.ContainsKey(item.enemyId))
                    maxCountPerEnemy[item.enemyId] = item.count;
                else
                    maxCountPerEnemy[item.enemyId] = Mathf.Max(maxCountPerEnemy[item.enemyId], item.count);
            }

        foreach (var kvp in maxCountPerEnemy)
        {
            if (!enemyMap.TryGetValue(kvp.Key, out var enemyData)) continue;
            PoolManager.I.WarmUp(enemyData.prefab, kvp.Value);
        }
    }

    public void SkipCurrentWaveDelay() => isSkippingDelay = true;

    public int CurrentWaveIndex => currentWaveIndex;
    public int TotalWaves => waveList.Count;
}
