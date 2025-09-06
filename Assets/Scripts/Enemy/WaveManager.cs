using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using System;

public static class WaveRuntime
{
    private static int _aliveCount = 0;
    public static int AliveCount
    {
        get => _aliveCount;
        set => _aliveCount = Math.Max(0, value);
    }

    // EnemyController gọi khi 1 quái chết
    public static event Action OnEnemyDied;
    public static void NotifyEnemyDied() => OnEnemyDied?.Invoke();
}

public class WaveManager : MonoBehaviour
{
    // Win toàn bộ waves
    public static event Action OnAllWavesCompleted;

    [Header("Reward UI")]
    public TextMeshProUGUI rewardText;
    public Canvas rewardCanvas;

    [Header("Data")]
    public List<WaveDef> waveList = new();
    public List<EnemyData> enemyDataList = new();

    [Header("Wave Delay Control")]
    public bool isSkippingDelay = false;  // vẫn giữ cho countdown bình thường
    public float interWaveTimer = 0f;

    [Header("Wave UI")]
    public TextMeshProUGUI waveCounterText;  // "Wave X / Tổng"

    [Header("Countdown UI")]
    public TMP_Text countdownText;
    public bool autoHideCountdown = true;

    // ======== SKIP FEATURE DISABLED ========
    // [Header("Mid-wave Skip")]
    // public GameObject skipMidWaveButton;     // kéo thả nút
    // public bool clearEnemiesOnSkip = true;   // dọn sạch quái khi skip?

    // // Kill thực tế & carryover cho logic skip (tắt)
    // private int killedThisWaveActual = 0;
    // private int carryoverAppliedThisWave = 0;
    // private int carryoverKills = 0;
    // =======================================

    // ===== INTERNAL STATE =====
    private Dictionary<string, EnemyData> enemyMap;
    private int currentWaveIndex = 0;
    private bool hasWon = false;
    private int pendingToSpawn = 0;
    private int totalPlannedThisWave = 0;
    private int killedThisWave = 0;           // dùng cho log/thống kê nhẹ
    private bool forceEndCurrentWave = false;
    private readonly List<Coroutine> runningSpawners = new();

    private bool IsLastWave => currentWaveIndex >= (waveList.Count - 1);
    public int CurrentWaveIndex => currentWaveIndex;
    public int TotalWaves => waveList.Count;

    // Guard tránh PlayWaves() chạy trùng
    private bool isWavesLoopRunning = false;
    private Coroutine wavesLoopCo;

    // Tránh 2 WaveManager đồng thời
    public static WaveManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[WaveManager] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (rewardCanvas) rewardCanvas.gameObject.SetActive(false);

        enemyMap = enemyDataList.ToDictionary(e => e.enemyId, e => e);

        if (countdownText) countdownText.gameObject.SetActive(false);

        // ======== SKIP FEATURE DISABLED ========
        // if (skipMidWaveButton) skipMidWaveButton.SetActive(false);
        // =======================================

        if (waveCounterText) waveCounterText.gameObject.SetActive(true);
    }

    void OnEnable()
    {
        WaveRuntime.OnEnemyDied += HandleEnemyDiedInWave;
    }

    void OnDisable()
    {
        WaveRuntime.OnEnemyDied -= HandleEnemyDiedInWave;

        if (wavesLoopCo != null)
        {
            StopCoroutine(wavesLoopCo);
            wavesLoopCo = null;
            isWavesLoopRunning = false;
        }
    }

    IEnumerator Start()
    {
        // Đợi Pool sẵn sàng
        yield return new WaitUntil(() => PoolManager.I != null);

        PreloadEnemiesAllWaves();

        // Hiển thị wave ngay từ đầu
        currentWaveIndex = 0;
        UpdateWaveCounter();

        // Bắt đầu vòng waves (có guard)
        if (!isWavesLoopRunning)
            wavesLoopCo = StartCoroutine(PlayWaves());
        else
            Debug.LogWarning("[WaveManager] PlayWaves() already running, skip start.");
    }

    // ===== Vòng lặp toàn level =====
    public IEnumerator PlayWaves()
    {
        if (isWavesLoopRunning)
        {
            Debug.LogError("[WaveManager] DUPLICATE PlayWaves() detected!");
            yield break;
        }
        isWavesLoopRunning = true;

        for (currentWaveIndex = 0; currentWaveIndex < waveList.Count; currentWaveIndex++)
        {
            UpdateWaveCounter();

            var wave = waveList[currentWaveIndex];

            // Chơi 1 wave
            yield return StartCoroutine(PlaySingleWave(wave));

            // ======== SKIP FEATURE DISABLED ========
            // if (skipMidWaveButton) skipMidWaveButton.SetActive(false);
            // =======================================

            // Đếm ngược chuyển wave (trừ wave cuối)
            if (!IsLastWave)
            {
                isSkippingDelay = false;
                interWaveTimer = wave.interWaveDelay;

                if (countdownText) countdownText.gameObject.SetActive(true);

                while (interWaveTimer > 0f && !isSkippingDelay)
                {
                    interWaveTimer -= Time.deltaTime;
                    if (countdownText)
                    {
                        int sec = Mathf.Max(0, Mathf.CeilToInt(interWaveTimer));
                        countdownText.text = $"Wave {currentWaveIndex + 1}/{TotalWaves}\nNext wave in {sec}";
                    }
                    yield return null;
                }

                if (countdownText && autoHideCountdown)
                    countdownText.gameObject.SetActive(false);
            }
        }

        // Hoàn tất tất cả waves -> Win
        if (countdownText && autoHideCountdown) countdownText.gameObject.SetActive(false);
        HandleWin();

        isWavesLoopRunning = false;
    }

    // ===== Chơi 1 wave =====
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

        // ======== SKIP FEATURE DISABLED ========
        // carryoverAppliedThisWave = 0;
        // killedThisWaveActual = 0;
        // if (skipMidWaveButton) skipMidWaveButton.SetActive(false);
        // =======================================

        // Cập nhật UI wave
        UpdateWaveCounter();

        // Lên lịch spawn các nhóm
        foreach (var item in sortedItems)
        {
            float targetTime = waveStart + item.startTime;
            yield return new WaitUntil(() => Time.time >= targetTime || forceEndCurrentWave);
            if (forceEndCurrentWave) break;

            var co = StartCoroutine(SpawnGroup(item, wave));
            runningSpawners.Add(co);
        }

        // Kết thúc wave khi đã spawn hết & không còn quái sống, HOẶC bị forceEnd
        yield return new WaitUntil(() =>
            forceEndCurrentWave || (pendingToSpawn <= 0 && WaveRuntime.AliveCount <= 0)
        );

        // Nếu forceEnd: dừng spawners còn lại
        if (forceEndCurrentWave)
        {
            foreach (var co in runningSpawners)
                if (co != null) StopCoroutine(co);
            runningSpawners.Clear();
        }

        // ======== SKIP FEATURE DISABLED ========
        // if (skipMidWaveButton) skipMidWaveButton.SetActive(false);
        // =======================================
    }

    // Khi 1 quái chết
    private void HandleEnemyDiedInWave()
    {
        killedThisWave++;
        // ======== SKIP FEATURE DISABLED ========
        // killedThisWaveActual++;
        // TryUpdateSkipButtonByProgress();
        // =======================================
    }

    // ======== SKIP FEATURE DISABLED ========
    // private void TryUpdateSkipButtonByProgress() { /* disabled */ }
    //
    // public void SkipMidWaveNow() { /* disabled completely */ }
    // =======================================

    // ===== Spawn nhóm trong 1 wave =====
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

            if (item.interval > 0f) yield return new WaitForSeconds(item.interval);
            else yield return null;
        }
    }

    void SpawnEnemy(EnemyData data, Spawner spawner, float powerMultiplier)
    {
        GameObject enemy = PoolManager.I.Get(data.prefab, spawner.spawnPoint.position, spawner.spawnPoint.rotation);
        var controller = enemy.GetComponent<EnemyController>();
        if (controller != null) controller.Init(powerMultiplier);
        controller.BeginWaveLifetime();
        WaveRuntime.AliveCount++;
    }

    // ===== Utilities =====
    private void UpdateWaveCounter()
    {
        if (waveCounterText == null) return;
        int total = Mathf.Max(0, TotalWaves);
        int currentDisplay = (total > 0) ? Mathf.Clamp(currentWaveIndex + 1, 1, total) : 0;
        waveCounterText.text = (total > 0) ? $"Wave {currentDisplay} / {total}" : "Wave 0 / 0";
        if (!waveCounterText.gameObject.activeSelf) waveCounterText.gameObject.SetActive(true);
    }

    private void ClearAllEnemies()
    {
        var allEnemies = FindObjectsOfType<EnemyController>();
        foreach (var e in allEnemies) e.Die();
        WaveRuntime.AliveCount = 0;
    }

    private void HandleWin()
    {
        if (hasWon) return;
        hasWon = true;

        foreach (var co in runningSpawners)
            if (co != null) StopCoroutine(co);
        runningSpawners.Clear();

        ClearAllEnemies();

        // ======== SKIP FEATURE DISABLED ========
        // if (skipMidWaveButton) skipMidWaveButton.SetActive(false);
        // =======================================

        if (countdownText) countdownText.gameObject.SetActive(false);

        OnAllWavesCompleted?.Invoke();

        var gc = GameObject.FindWithTag("GameController")?.GetComponent<GameController>();
        if (gc != null)
        {
            // gc.Win(); // nếu bạn có
        }

        if (rewardText && GameSession.Instance != null && GameSession.Instance.currentMapData != null)
            rewardText.text = $"Water x {GameSession.Instance.currentMapData.waterReward}";
        if (rewardCanvas) rewardCanvas.gameObject.SetActive(true);

        if (waveCounterText) waveCounterText.text = "All waves cleared!";
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
}
