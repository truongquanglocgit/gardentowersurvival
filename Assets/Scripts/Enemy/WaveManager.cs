using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using System;

public static class WaveRuntime
{
    // BẬT/TẮT log toàn hệ
    public static bool LogEnabled = false;

    private static int _aliveCount = 0;
    public static int AliveCount
    {
        get => _aliveCount;
        private set
        {
            _aliveCount = Math.Max(0, value);
            if (LogEnabled) Debug.Log($"[Wave] AliveCount = {_aliveCount}");
        }
    }

    // Debug: theo dõi cụ thể instance nào đang "sống"
    private static readonly HashSet<int> _aliveIds = new HashSet<int>();

    public static event Action OnEnemyDied;
    public static void NotifyEnemyDied() => OnEnemyDied?.Invoke();

    // Gọi khi spawn 1 enemy
    public static void RegisterSpawn(GameObject go)
    {
        int id = go ? go.GetInstanceID() : -1;
        _aliveIds.Add(id);
        AliveCount = _aliveIds.Count;
        if (LogEnabled) Debug.Log($"[Wave] SPAWN {go?.name} id={id}  -> alive={AliveCount}");
    }

    // Gọi khi enemy chết/biến mất khỏi wave
    public static void RegisterDeath(GameObject go)
    {
        int id = go ? go.GetInstanceID() : -1;
        bool removed = _aliveIds.Remove(id);
        if (!removed && LogEnabled)
            Debug.LogWarning($"[Wave] RegisterDeath: id={id} ({go?.name}) KHÔNG có trong aliveIds (double count?)");
        AliveCount = _aliveIds.Count;
        if (LogEnabled) Debug.Log($"[Wave] DIE   {go?.name} id={id}  -> alive={AliveCount}");
        NotifyEnemyDied();
    }

    // ⭐ GỌI HÀM NÀY KHI RỜI SCENE (trước khi Load MainMenu / map mới)
    public static void ResetAll()
    {
        _aliveIds.Clear();
        _aliveCount = 0;
        OnEnemyDied = null;   // gỡ toàn bộ subscriber tĩnh
        if (LogEnabled) Debug.Log("[Wave] ResetAll()");
    }
}



public class WaveManager : MonoBehaviour
{
    // Win toàn bộ waves
    public static event Action OnAllWavesCompleted;
    [Header("Spawner Warning VFX")]
    [Tooltip("Prefab VFX cảnh báo hiển thị trên spawner khi spawner đó được dùng ở wave hiện tại")]
    public ParticleSystem spawnerWarningVFXPrefab;

    [Tooltip("Offset world-space đặt VFX so với vị trí spawnPoint của Spawner (m)")]
    public Vector3 spawnerVFXOffset = new Vector3(0f, 1.2f, 0f);

    [Tooltip("Parent VFX vào spawner để nó bám theo spawner (nếu spawner có chuyển động)")]
    public bool spawnerVFXParentToSpawner = true;

    [Tooltip("Scale áp cho VFX khi spawn")]
    public Vector3 spawnerVFXScale = Vector3.one;

    // runtime: VFX đang bật cho wave hiện tại
    private readonly List<ParticleSystem> _activeSpawnerVFX = new();

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
    // ==== DEBUG EXPOSE ====
    public int PendingToSpawnDebug => pendingToSpawn;
    public int KilledThisWaveDebug => killedThisWave;
    public int AliveCountDebug => WaveRuntime.AliveCount;
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
        if (WaveRuntime.LogEnabled)
            Debug.Log($"[Wave] Awake. waves={waveList.Count}, enemies={enemyDataList.Count}");
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
    /// <summary>
    /// Bật VFX cảnh báo trên các spawner được sử dụng trong wave hiện tại.
    /// </summary>
    private void ShowSpawnerWarningVFXForWave(WaveDef wave)
    {
        // Không có prefab => bỏ qua
        if (!spawnerWarningVFXPrefab) return;

        // Tập hợp spawnerId được sử dụng trong wave
        var usedSpawnerIds = new HashSet<string>();
        foreach (var it in wave.items)
            if (!string.IsNullOrEmpty(it.spawnerId))
                usedSpawnerIds.Add(it.spawnerId);

        // Với mỗi spawnerId -> lấy Spawner thực tế -> spawn VFX ở spawnPoint
        foreach (var sid in usedSpawnerIds)
        {
            if (!SpawnerRegistry.Instance.TryGet(sid, out var spawner) || spawner == null || spawner.spawnPoint == null)
            {
                Debug.LogWarning($"[Wave] Spawner VFX: cannot find spawnerId={sid}");
                continue;
            }

            var anchor = spawner.spawnPoint; // dùng spawnPoint để đúng tọa độ ra quái
            Vector3 pos = anchor.position + spawnerVFXOffset;
            Quaternion rot = anchor.rotation;

            Transform parent = spawnerVFXParentToSpawner ? anchor : null;

            // Tạo VFX
            var vfx = Instantiate(spawnerWarningVFXPrefab, pos, rot, parent);
            vfx.transform.localScale = spawnerVFXScale;
            vfx.Play(true);

            _activeSpawnerVFX.Add(vfx);
        }
    }

    /// <summary>
    /// Tắt & hủy mọi VFX cảnh báo đã bật cho wave vừa rồi.
    /// </summary>
    private void ClearSpawnerWarningVFX()
    {
        if (_activeSpawnerVFX.Count == 0) return;

        for (int i = 0; i < _activeSpawnerVFX.Count; i++)
        {
            var v = _activeSpawnerVFX[i];
            if (!v) continue;

            // dừng và hủy
            v.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(v.gameObject);
        }
        _activeSpawnerVFX.Clear();
    }

    // (tuỳ chọn) cho gọi chủ động từ chỗ khác
    public static void ResetEvents()
    {
        OnAllWavesCompleted = null;
    }
    void OnDestroy()
    {
        if (Instance == this) Instance = null;

        // gỡ mọi subscriber tĩnh
        OnAllWavesCompleted = null;
        WaveRuntime.OnEnemyDied -= HandleEnemyDiedInWave;
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
        else { }
        if (WaveRuntime.LogEnabled) Debug.Log("[Wave] Start() → PreloadEnemiesAllWaves()");
        PreloadEnemiesAllWaves();

        currentWaveIndex = 0;
        UpdateWaveCounter();

        if (!isWavesLoopRunning)
        {
            if (WaveRuntime.LogEnabled) Debug.Log("[Wave] Start PlayWaves()");
            wavesLoopCo = StartCoroutine(PlayWaves());
        }
        //Debug.LogWarning("[WaveManager] PlayWaves() already running, skip start."); 
    }


    // ===== Vòng lặp toàn level =====
    public IEnumerator PlayWaves()
    {
        if (isWavesLoopRunning)
        {
            Debug.LogError("[Wave] DUPLICATE PlayWaves() detected!");
            yield break;
        }
        isWavesLoopRunning = true;

        for (currentWaveIndex = 0; currentWaveIndex < waveList.Count; currentWaveIndex++)
        {
            var wave = waveList[currentWaveIndex];
            if (WaveRuntime.LogEnabled)
                Debug.Log($"[Wave] >>> BEGIN Wave #{currentWaveIndex + 1}/{waveList.Count} | items={wave.items.Count}");

            UpdateWaveCounter();
            yield return StartCoroutine(PlaySingleWave(wave));

            if (!IsLastWave)
            {
                if (WaveRuntime.LogEnabled)
                    Debug.Log($"[Wave] Wave #{currentWaveIndex + 1} done. InterDelay={wave.interWaveDelay:F2}s");
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
        if (WaveRuntime.LogEnabled) Debug.Log("[Wave] ALL WAVES COMPLETED → HandleWin()");
        if (countdownText && autoHideCountdown) countdownText.gameObject.SetActive(false);
        HandleWin();
        isWavesLoopRunning = false;
    }

    // ===== Chơi 1 wave =====
    private IEnumerator PlaySingleWave(WaveDef wave)
    {
        forceEndCurrentWave = false;
        killedThisWave = 0;
        WaveRuntime.ResetAll();              // ⭐ đảm bảo sạch đếm sống từ wave trước
        runningSpawners.Clear();
        // 🔔 Bật VFX cảnh báo trên các spawner sẽ được dùng ở wave này
        ShowSpawnerWarningVFXForWave(wave);

        var sortedItems = wave.items.OrderBy(i => i.startTime).ToList();
        float waveStart = Time.time;

        totalPlannedThisWave = sortedItems.Sum(i => i.count);
        pendingToSpawn = totalPlannedThisWave;

        UpdateWaveCounter();

        if (WaveRuntime.LogEnabled)
            Debug.Log($"[Wave] Setup Wave: planned={totalPlannedThisWave}, aliveCap={wave.aliveCap}");

        // Lên lịch spawn
        foreach (var item in sortedItems)
        {
            float targetTime = waveStart + item.startTime;
            if (WaveRuntime.LogEnabled)
                Debug.Log($"[Wave] Schedule group: enemyId={item.enemyId} count={item.count} startAt={item.startTime:F2}s interval={item.interval:F2}s");

            yield return new WaitUntil(() => Time.time >= targetTime || forceEndCurrentWave);
            if (forceEndCurrentWave) break;

            var co = StartCoroutine(SpawnGroup(item, wave));
            runningSpawners.Add(co);
        }

        // Watchdog: log mỗi 2s trong thời gian chờ kết thúc wave
        float lastLog = Time.time;
        yield return new WaitUntil(() =>
        {
            bool done = forceEndCurrentWave || (pendingToSpawn <= 0 && WaveRuntime.AliveCount <= 0);
            if (!done && WaveRuntime.LogEnabled && Time.time - lastLog >= 2f)
            {
                lastLog = Time.time;
                Debug.Log($"[Wave] Waiting end... pending={pendingToSpawn} alive={WaveRuntime.AliveCount} killedThisWave={killedThisWave}");
            }
            return done;
        });

        if (forceEndCurrentWave)
        {
            if (WaveRuntime.LogEnabled) Debug.LogWarning("[Wave] FORCE END current wave. Stopping spawners...");
            foreach (var co in runningSpawners)
                if (co != null) StopCoroutine(co);
            runningSpawners.Clear();

            // 🔕 Tắt & dọn VFX nếu wave bị kết thúc sớm
            ClearSpawnerWarningVFX();
        }
        // 🔕 Tắt & dọn VFX cảnh báo cho wave này
        ClearSpawnerWarningVFX();
        if (WaveRuntime.LogEnabled)
            Debug.Log($"[Wave] <<< END Wave #{currentWaveIndex + 1}: spawned={totalPlannedThisWave - pendingToSpawn}/{totalPlannedThisWave}, killed={killedThisWave}, alive={WaveRuntime.AliveCount}");
    }

    private void HandleEnemyDiedInWave()
    {
        killedThisWave++;
        if (WaveRuntime.LogEnabled)
            Debug.Log($"[Wave] EnemyDied event. killedThisWave={killedThisWave} alive={WaveRuntime.AliveCount} pending={pendingToSpawn}");
    }

    IEnumerator SpawnGroup(SpawnItem item, WaveDef wave)
    {
        if (!SpawnerRegistry.Instance.TryGet(item.spawnerId, out var spawner))
        {
            Debug.LogError($"[Wave] Missing spawnerId={item.spawnerId}");
            yield break;
        }
        if (!enemyMap.TryGetValue(item.enemyId, out var enemyData))
        {
            Debug.LogError($"[Wave] Missing enemyId={item.enemyId}");
            yield break;
        }

        for (int i = 0; i < item.count; i++)
        {
            if (forceEndCurrentWave) yield break;

            while (!forceEndCurrentWave && WaveRuntime.AliveCount >= wave.aliveCap)
            {
                if (WaveRuntime.LogEnabled)
                    Debug.Log($"[Wave] Cap alive reached ({WaveRuntime.AliveCount}/{wave.aliveCap}), waiting spawn...");
                yield return null;
            }
            if (forceEndCurrentWave) yield break;

            SpawnEnemy(enemyData, spawner, item.powerMultiplier);
            pendingToSpawn = Mathf.Max(0, pendingToSpawn - 1);
            if (WaveRuntime.LogEnabled)
                Debug.Log($"[Wave] Spawned {item.enemyId}  leftToSpawn={pendingToSpawn}  alive={WaveRuntime.AliveCount}");

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

        // 🔴 ĐIỂM MẤU CHỐT: dùng RegisterSpawn để đếm & log
        WaveRuntime.RegisterSpawn(enemy);
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
        var all = FindObjectsOfType<EnemyController>();
        foreach (var e in all) e.Die();

        // ❌ bỏ dòng WaveRuntime.AliveCount = 0;
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
