using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine.UI;   // <- thêm

/// <summary>
/// Runtime counters & events cho vòng đời quái trong 1 wave
/// </summary>
public static class WaveRuntime
{
    // BẬT/TẮT toàn hệ log
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

    // Debug: track instance nào đang sống
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

    /// <summary>Gọi khi rời scene để dọn sạch state tĩnh.</summary>
    public static void ResetAll()
    {
        _aliveIds.Clear();
        _aliveCount = 0;
        OnEnemyDied = null;   // gỡ tất cả subscriber tĩnh
        if (LogEnabled) Debug.Log("[Wave] ResetAll()");
    }
}

/// <summary>
/// Quản lý vòng đời toàn bộ waves trong Level
/// - Đọc WaveDef, lập lịch spawn theo backpressure
/// - Bật/tắt VFX cảnh báo trên các Spawner được dùng ở wave hiện tại
/// - Phát event để lớp khác (UI/Audio/…) bắt kịp trạng thái
/// </summary>
public class WaveManager : MonoBehaviour
{
    [Header("Shared Death VFX (Global)")]
    [Tooltip("Prefab VFX chết dùng CHUNG cho tất cả enemy trong level.")]
    public ParticleSystem sharedDeathVFXPrefab;

    [Header("Boss Banner Timing")]
    [SerializeField] private float bossBannerEnterTime = 0.35f;
    [SerializeField] private float bossBannerHoldTime = 5.0f;  // <- đặt 5s ở Inspector
    [SerializeField] private float bossBannerExitTime = 0.35f;
    public Vector2 lastThreePitchRange = new Vector2(1.06f, 1.18f);
    [Range(0f, 1f)] public float countdownSfxVolume = 1f;
    [SerializeField] private AudioClip waveStartSFX;
    public int tickStartAtSeconds = 5;
    public bool escalatePitchOnLastThree = true;
    // ===== Boss Incoming Alert =====
    [SerializeField] private BossIncomingBanner bossBannerPrefab;
    [SerializeField] private Sprite bossBannerSprite; // gán PNG “BOSS COMING” bạn tạo
    [SerializeField] private Canvas uiCanvas;

    [Tooltip("Anchor để đặt VFX boss; để trống sẽ dùng Camera.main.")]
    [SerializeField] private Transform bossVFXAnchor;

    [Tooltip("Offset local theo anchor (m).")]
    [SerializeField] private Vector3 bossVFXOffset = new Vector3(0, 0, 3f);

    [Tooltip("Có parent VFX vào anchor không (để bám camera)?")]
    [SerializeField] private bool bossVFXParentToAnchor = true;

    [Tooltip("Tự hủy VFX sau (giây). 0 = theo particle duration.")]
    [SerializeField] private float bossVFXLifetime = 0f;

    [Tooltip("SFX cảnh báo boss (siren/beep).")]
    [SerializeField] private AudioClip bossAlertSFX;
    [SerializeField, Range(0f, 1f)] private float bossAlertSFXVolume = 1f;

    [Tooltip("Số lần beep cảnh báo.")]
    [SerializeField] private int bossAlertRepeat = 3;

    [Tooltip("Khoảng cách giữa các beep (giây).")]
    [SerializeField] private float bossAlertInterval = 0.4f;

    [Header("Boss Screen Flash")]
    [Tooltip("Image full-screen (UI) để flash đỏ. RaycastTarget nên OFF.")]
    [SerializeField] private Image bossFlashImage;

    [Tooltip("Alpha tối đa khi flash.")]
    [SerializeField, Range(0f, 1f)] private float bossFlashMaxAlpha = 0.45f;

    [Tooltip("Thời gian tăng alpha.")]
    [SerializeField] private float bossFlashInTime = 0.08f;

    [Tooltip("Giữ đỉnh alpha (giây).")]
    [SerializeField] private float bossFlashHoldTime = 0.05f;

    [Tooltip("Thời gian giảm alpha.")]
    [SerializeField] private float bossFlashOutTime = 0.16f;

    // ===== Win UI/SFX =====
    [Header("Win UI/SFX")]
    [SerializeField] private AudioSource uiSfxSource;         // 2D AudioSource (spatialBlend=0)
    [SerializeField] private AudioClip countdownTickSFX;       // tiếng tick từng giây
    [SerializeField, Range(0f, 1f)] private float countdownTickVolume = 0.9f;
    [SerializeField] private AudioClip winSFX;                 // tiếng thắng cuộc
    [SerializeField, Range(0f, 1f)] private float winSFXVolume = 1f;

    [Tooltip("Panel sẽ bay từ trên xuống khi HandleWin (thường là panel con của rewardCanvas)")]
    [SerializeField] private RectTransform winPanelRect;

    [Tooltip("Khoảng rơi theo trục Y (pixel UI). Nếu = 0 sẽ tự ước lượng theo chiều cao panel.")]
    [SerializeField] private float panelDropDistance = 0f;
    [SerializeField, Range(0.1f, 3f)] private float panelDropDuration = 0.7f;
    [SerializeField]
    private AnimationCurve panelDropCurve =
        AnimationCurve.EaseInOut(0, 0, 1, 1); // có thể thay curve khác nếu muốn

    // ===== Public Events (để Audio/UI bắt) =====
    /// <summary>Phát khi chuẩn bị bắt đầu 1 wave. Dùng để đổi nhạc theo wave.isBossWave.</summary>
    public static event Action<int, WaveDef> OnWaveStarted;

    /// <summary>Phát khi bắt đầu inter-wave delay (giữa 2 wave). Dùng để chuyển nhạc Calm/Menu.</summary>
    public static event Action<int, float> OnInterWaveDelayStarted;

    /// <summary>Phát khi clear toàn bộ waves.</summary>
    public static event Action OnAllWavesCompleted;

    // ===== VFX cảnh báo Spawner của wave hiện tại =====
    [Header("Spawner Warning VFX")]
    [Tooltip("Prefab VFX cảnh báo đặt trên spawner được dùng ở wave hiện tại")]
    public ParticleSystem spawnerWarningVFXPrefab;

    [Tooltip("Offset đặt VFX theo world-space so với spawnPoint")]
    public Vector3 spawnerVFXOffset = new Vector3(0f, 1.2f, 0f);

    [Tooltip("Parent VFX vào spawner để bám theo (nếu spawner có chuyển động)")]
    public bool spawnerVFXParentToSpawner = true;

    [Tooltip("Scale áp cho VFX khi spawn")]
    public Vector3 spawnerVFXScale = Vector3.one;

    // runtime: danh sách VFX đang bật cho wave hiện tại
    private readonly List<ParticleSystem> _activeSpawnerVFX = new();

    // ===== Reward UI =====
    [Header("Reward UI")]
    public TextMeshProUGUI rewardText;
    public Canvas rewardCanvas;

    // ===== Data =====
    [Header("Data")]
    public List<WaveDef> waveList = new();
    public List<EnemyData> enemyDataList = new();

    // ===== Wave Delay Control =====
    [Header("Wave Delay Control")]
    public bool isSkippingDelay = false;  // có thể bật từ UI để skip
    public float interWaveTimer = 0f;

    // ===== Wave UI =====
    [Header("Wave UI")]
    public TextMeshProUGUI waveCounterText;  // "Wave X / Tổng"

    [Header("Countdown UI")]
    public TMP_Text countdownText;
    public bool autoHideCountdown = true;

    // ==== DEBUG EXPOSE ====
    public int PendingToSpawnDebug => pendingToSpawn;
    public int KilledThisWaveDebug => killedThisWave;
    public int AliveCountDebug => WaveRuntime.AliveCount;

    // ===== INTERNAL STATE =====
    private Dictionary<string, EnemyData> enemyMap;
    private int currentWaveIndex = 0;
    private bool hasWon = false;
    private int pendingToSpawn = 0;
    private int totalPlannedThisWave = 0;
    private int killedThisWave = 0;
    private bool forceEndCurrentWave = false;
    private readonly List<Coroutine> runningSpawners = new();

    private bool IsLastWave => currentWaveIndex >= (waveList.Count - 1);
    public int CurrentWaveIndex => currentWaveIndex;
    public int TotalWaves => waveList.Count;

    // Guard tránh PlayWaves() chạy trùng
    private bool isWavesLoopRunning = false;
    private Coroutine wavesLoopCo;

    // Single instance guard (tuỳ kiến trúc)
    public static WaveManager Instance { get; private set; }

    // ===================== Lifecycle =====================

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
        if (waveCounterText) waveCounterText.gameObject.SetActive(true);
        EnsureUISfxSource(); // 🔊 chuẩn bị nguồn SFX UI
        if (WaveRuntime.LogEnabled)
            Debug.Log($"[Wave] Awake. waves={waveList.Count}, enemies={enemyDataList.Count}");

    }

    void OnEnable()
    {
        WaveRuntime.OnEnemyDied += HandleEnemyDiedInWave;
    }
    void EnsureUISfxSource()
    {
        if (uiSfxSource) return;
        var go = new GameObject("UI_SFX_Source");
        go.transform.SetParent(transform, false);
        uiSfxSource = go.AddComponent<AudioSource>();
        uiSfxSource.playOnAwake = false;
        uiSfxSource.loop = false;
        uiSfxSource.spatialBlend = 0f; // 2D
        uiSfxSource.dopplerLevel = 0f;
        uiSfxSource.rolloffMode = AudioRolloffMode.Linear;
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

    void OnDestroy()
    {
        if (Instance == this) Instance = null;

        // Gỡ mọi subscriber static
        OnAllWavesCompleted = null;
        OnWaveStarted = null;
        OnInterWaveDelayStarted = null;

        WaveRuntime.OnEnemyDied -= HandleEnemyDiedInWave;
    }

    IEnumerator Start()
    {
        // Đợi Pool sẵn sàng (nếu bạn dùng PoolManager)
        yield return new WaitUntil(() => PoolManager.I != null);

        // Preload theo max count từng enemy trong toàn bộ waves
        PreloadEnemiesAllWaves();

        // Hiển thị wave từ đầu
        currentWaveIndex = 0;
        UpdateWaveCounter();
         // ✅ Chỉ còn prewarm VFX CHUNG theo wave đông nhất
        PrewarmSharedDeathVFXByMaxWave();

        // Khởi động vòng waves như cũ
        currentWaveIndex = 0;
        UpdateWaveCounter();
        if (!isWavesLoopRunning)
            wavesLoopCo = StartCoroutine(PlayWaves());
        // Bắt đầu vòng waves (guard)
        if (!isWavesLoopRunning)
        {
            if (WaveRuntime.LogEnabled) Debug.Log("[Wave] Start → PlayWaves()");
            wavesLoopCo = StartCoroutine(PlayWaves());
        }
    }

    // ===================== Public API =====================

    public void LoadWaves(List<WaveDef> waves) => waveList = waves;

    public void PreloadEnemiesAllWaves()
    {
        var maxCountPerEnemy = new Dictionary<string, int>();

        foreach (var wave in waveList)
        {
            foreach (var item in wave.items)
            {
                if (!maxCountPerEnemy.ContainsKey(item.enemyId))
                    maxCountPerEnemy[item.enemyId] = item.count;
                else
                    maxCountPerEnemy[item.enemyId] = Mathf.Max(maxCountPerEnemy[item.enemyId], item.count);
            }
        }

        foreach (var kvp in maxCountPerEnemy)
        {
            if (!enemyMap.TryGetValue(kvp.Key, out var enemyData)) continue;
            PoolManager.I.WarmUp(enemyData.prefab, kvp.Value);
        }
    }
    /// <summary>Tính N = tổng quái lớn nhất trong 1 wave, warm-up đúng N instance cho prefab VFX chung.</summary>
    private void PrewarmSharedDeathVFXByMaxWave()
    {
        if (!sharedDeathVFXPrefab || waveList == null || waveList.Count == 0) return;

        int maxInAnyWave = 0;
        foreach (var w in waveList)
        {
            if (w == null || w.items == null) continue;
            int total = 0;
            foreach (var it in w.items) total += Mathf.Max(0, it.count);
            if (total > maxInAnyWave) maxInAnyWave = total;
        }

        maxInAnyWave = Mathf.Max(1, maxInAnyWave);
        PoolManager.I.WarmUp(sharedDeathVFXPrefab, maxInAnyWave); // ✅ Tạo đúng N, nằm dưới Pool_VFXName
        // Debug.Log($"[Pool] WarmUp sharedDeathVFX x{maxInAnyWave}");
    }

    /// <summary>Cho EnemyController lấy prefab VFX chung một cách an toàn.</summary>
    public static ParticleSystem GetSharedDeathVFX()
    {
        return Instance ? Instance.sharedDeathVFXPrefab : null;
    }
    /// <summary>Cho UI gọi để skip inter-wave delay hiện tại.</summary>
    public void SkipCurrentWaveDelay() => isSkippingDelay = true;

    /// <summary>Cho nơi khác chủ động reset các event static (nếu cần).</summary>
    public static void ResetEvents()
    {
        OnAllWavesCompleted = null;
        OnWaveStarted = null;
        OnInterWaveDelayStarted = null;
    }

    // ===================== Core Loop =====================

    /// <summary>Vòng lặp toàn bộ waves của Level.</summary>
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

            // >>> PHÁT EVENT: Bắt đầu 1 wave (để Audio đổi nhạc theo wave.isBossWave)
            OnWaveStarted?.Invoke(currentWaveIndex, wave);
            if (wave.isBossWave)
                yield return StartCoroutine(BossIncomingSequence());


            if (WaveRuntime.LogEnabled)
                Debug.Log($"[Wave] >>> BEGIN Wave #{currentWaveIndex + 1}/{waveList.Count} | items={wave.items.Count}");

            UpdateWaveCounter();

            // Chơi 1 wave
            yield return StartCoroutine(PlaySingleWave(wave));

            // Inter-wave delay (nếu chưa phải last wave)
            if (!IsLastWave)
            {
                if (WaveRuntime.LogEnabled)
                    Debug.Log($"[Wave] Wave #{currentWaveIndex + 1} done. InterDelay={wave.interWaveDelay:F2}s");

                isSkippingDelay = false;
                interWaveTimer = wave.interWaveDelay;

                // >>> PHÁT EVENT: bắt đầu inter-wave delay
                OnInterWaveDelayStarted?.Invoke(currentWaveIndex, interWaveTimer);

                if (countdownText) countdownText.gameObject.SetActive(true);

                int lastAnnouncedSec = -1;

                while (interWaveTimer > 0f && !isSkippingDelay)
                {
                    interWaveTimer -= Time.deltaTime;

                    int sec = Mathf.Max(0, Mathf.CeilToInt(interWaveTimer));
                    if (countdownText) countdownText.text = $"Next wave in {sec}";

                    if (sec != lastAnnouncedSec)
                    {
                        if (sec > 0 && sec <= Mathf.Max(1, tickStartAtSeconds))
                            PlayCountdownTick(sec);        // ⬅️ dùng CHUNG clip cũ
                        lastAnnouncedSec = sec;
                    }
                    yield return null;
                }
                if (!isSkippingDelay) { PlayCountdownFinal(); PlayWaveStartSFX(); }
                if (countdownText && autoHideCountdown)
                    countdownText.gameObject.SetActive(false);
            }
        }

        // Hoàn tất toàn bộ waves → Win
        if (WaveRuntime.LogEnabled) Debug.Log("[Wave] ALL WAVES COMPLETED → HandleWin()");
        if (countdownText && autoHideCountdown) countdownText.gameObject.SetActive(false);

        yield return StartCoroutine(WinSequence());
        isWavesLoopRunning = false;
    }
    private IEnumerator BossIncomingSequence()
    {
        if (bossBannerPrefab && uiCanvas)
        {
            var banner = Instantiate(bossBannerPrefab, uiCanvas.transform);
            banner.gameObject.SetActive(true);

            if (bossBannerSprite) banner.SetSprite(bossBannerSprite);

            // lướt từ trái vào -> ĐỨNG yên bossBannerHoldTime giây -> lướt ra phải
            yield return banner.PlayHorizontalSwipe(
                fromLeftSide: true,
                enterTime: bossBannerEnterTime,
                holdTime: bossBannerHoldTime,
                exitTime: bossBannerExitTime,
                offscreenMargin: 150f
            );
        }
    }



    IEnumerator FlashRedOnce()
    {
        if (!bossFlashImage) yield break;

        // đảm bảo màu ban đầu alpha=0 (không chặn UI)
        Color baseColor = bossFlashImage.color;
        float maxA = Mathf.Clamp01(bossFlashMaxAlpha);

        // In
        float t = 0f;
        float tin = Mathf.Max(0.01f, bossFlashInTime);
        while (t < tin)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / tin);
            Color c = baseColor; c.a = Mathf.Lerp(0f, maxA, k);
            bossFlashImage.color = c;
            yield return null;
        }
        // Hold
        if (bossFlashHoldTime > 0f)
            yield return new WaitForSecondsRealtime(bossFlashHoldTime);

        // Out
        t = 0f;
        float tout = Mathf.Max(0.01f, bossFlashOutTime);
        while (t < tout)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / tout);
            Color c = baseColor; c.a = Mathf.Lerp(maxA, 0f, k);
            bossFlashImage.color = c;
            yield return null;
        }

        // restore alpha 0
        Color done = baseColor; done.a = 0f;
        bossFlashImage.color = done;
    }
    void PlayCountdownTick(int sec)
    {
        if (!countdownTickSFX || !uiSfxSource) return;
        uiSfxSource.pitch = (escalatePitchOnLastThree && sec <= 3)
            ? UnityEngine.Random.Range(lastThreePitchRange.x, lastThreePitchRange.y)
            : 1f;
        uiSfxSource.PlayOneShot(countdownTickSFX, countdownSfxVolume);
    }

    void PlayCountdownFinal()
    {
        if (countdownTickSFX && uiSfxSource)
        {
            uiSfxSource.pitch = 1f;
            uiSfxSource.PlayOneShot(countdownTickSFX, countdownSfxVolume);
        }
    }

    void PlayWaveStartSFX()
    {
        if (waveStartSFX && uiSfxSource)
        {
            uiSfxSource.pitch = 1f;
            uiSfxSource.PlayOneShot(waveStartSFX, countdownSfxVolume);
        }
    }

    IEnumerator WinSequence()
    {
        // Hiện chữ "Allwave Clear"
        if (waveCounterText)
        {
            waveCounterText.text = "Allwave Clear";
            if (!waveCounterText.gameObject.activeSelf) waveCounterText.gameObject.SetActive(true);
        }

        // Hiện số đếm (tuỳ chọn, dùng countdownText sẵn có)
        if (countdownText) countdownText.gameObject.SetActive(true);

        // Đếm ngược 5..1 với SFX tick mỗi giây
        for (int sec = 5; sec >= 1; sec--)
        {
            if (countdownText) countdownText.text = sec.ToString();

            if (countdownTickSFX)
            {
                EnsureUISfxSource();
                uiSfxSource.PlayOneShot(countdownTickSFX, countdownTickVolume);
            }
            yield return new WaitForSeconds(1f);
        }

        if (countdownText && autoHideCountdown) countdownText.gameObject.SetActive(false);

        // Hết đếm ngược → tiến hành HandleWin (sẽ phát win SFX + panel bay từ trên xuống)
        HandleWin();
    }

    /// <summary>Chơi 1 wave: bật VFX spawner, lập lịch spawn theo aliveCap, chờ khi wave kết thúc.</summary>
    private IEnumerator PlaySingleWave(WaveDef wave)
    {
        
        forceEndCurrentWave = false;
        killedThisWave = 0;
        WaveRuntime.ResetAll();    // đảm bảo sạch đếm alive từ wave trước
        runningSpawners.Clear();

        // 🔔 Bật VFX cảnh báo trên các spawner dùng ở wave này
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

            // Đợi đến thời điểm spawn (hoặc wave bị force end)
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

        // 🔕 Tắt & dọn VFX cảnh báo khi wave kết thúc tự nhiên
        ClearSpawnerWarningVFX();

        if (WaveRuntime.LogEnabled)
            Debug.Log($"[Wave] <<< END Wave #{currentWaveIndex + 1}: spawned={totalPlannedThisWave - pendingToSpawn}/{totalPlannedThisWave}, killed={killedThisWave}, alive={WaveRuntime.AliveCount}");
    }

    // ===================== Spawning =====================

    private void HandleEnemyDiedInWave()
    {
        killedThisWave++;
        if (WaveRuntime.LogEnabled)
            Debug.Log($"[Wave] EnemyDied event. killedThisWave={killedThisWave} alive={WaveRuntime.AliveCount} pending={pendingToSpawn}");
    }

    private IEnumerator SpawnGroup(SpawnItem item, WaveDef wave)
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

            // Backpressure: chờ nếu alive >= aliveCap
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

    private void SpawnEnemy(EnemyData data, Spawner spawner, float powerMultiplier)
    {
        GameObject enemy = PoolManager.I.Get(data.prefab, spawner.spawnPoint.position, spawner.spawnPoint.rotation);
        var controller = enemy.GetComponent<EnemyController>();
        if (controller != null) controller.Init(powerMultiplier);

        // Nếu EnemyController có lifecycle riêng cho wave
        controller.BeginWaveLifetime();

        // Đăng ký vào WaveRuntime để đếm alive
        WaveRuntime.RegisterSpawn(enemy);
    }

    // ===================== Spawner Warning VFX =====================

    /// <summary>Bật VFX cảnh báo trên các spawner được sử dụng trong wave hiện tại.</summary>
    private void ShowSpawnerWarningVFXForWave(WaveDef wave)
    {
        if (!spawnerWarningVFXPrefab) return;

        // Tập hợp spawnerId dùng trong wave
        var usedSpawnerIds = new HashSet<string>();
        foreach (var it in wave.items)
            if (!string.IsNullOrEmpty(it.spawnerId))
                usedSpawnerIds.Add(it.spawnerId);

        foreach (var sid in usedSpawnerIds)
        {
            if (!SpawnerRegistry.Instance.TryGet(sid, out var spawner) || spawner == null || spawner.spawnPoint == null)
            {
                Debug.LogWarning($"[Wave] Spawner VFX: cannot find spawnerId={sid}");
                continue;
            }

            var anchor = spawner.spawnPoint; // dùng spawnPoint để đúng vị trí ra quái
            Vector3 pos = anchor.position + spawnerVFXOffset;
            Quaternion rot = anchor.rotation;
            Transform parent = spawnerVFXParentToSpawner ? anchor : null;

            var vfx = Instantiate(spawnerWarningVFXPrefab, pos, rot, parent);
            vfx.transform.localScale = spawnerVFXScale;
            vfx.Play(true);
            _activeSpawnerVFX.Add(vfx);
        }
    }

    /// <summary>Tắt & huỷ mọi VFX cảnh báo đã bật cho wave trước.</summary>
    private void ClearSpawnerWarningVFX()
    {
        if (_activeSpawnerVFX.Count == 0) return;

        for (int i = 0; i < _activeSpawnerVFX.Count; i++)
        {
            var v = _activeSpawnerVFX[i];
            if (!v) continue;

            v.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(v.gameObject);
        }
        _activeSpawnerVFX.Clear();
    }

    // ===================== Win & Utilities =====================

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

        // KHÔNG set WaveRuntime.AliveCount trực tiếp (đã có RegisterDeath trong EnemyController/Pool trả về)
    }

    private void HandleWin()
    {
        if (hasWon) return;
        hasWon = true;

        foreach (var co in runningSpawners)
            if (co != null) StopCoroutine(co);
        runningSpawners.Clear();

        ClearAllEnemies();

        if (countdownText) countdownText.gameObject.SetActive(false);

        // Phát event hoàn tất
        OnAllWavesCompleted?.Invoke();

        // Gợi ý reward UI
        var gc = GameObject.FindWithTag("GameController")?.GetComponent<GameController>();
        if (gc != null)
        {
            // gc.Win(); // tuỳ bạn
        }

        if (rewardText && GameSession.Instance != null && GameSession.Instance.currentMapData != null)
            rewardText.text = $"Water x {GameSession.Instance.currentMapData.waterReward}";

        // Bật canvas (nếu có)
        if (rewardCanvas) rewardCanvas.gameObject.SetActive(true);

        // Phát win SFX (UI 2D)
        if (winSFX)
        {
            EnsureUISfxSource();
            uiSfxSource.PlayOneShot(winSFX, winSFXVolume);
        }

        // Panel bay từ trên xuống
        RectTransform rt = winPanelRect;
        if (!rt && rewardCanvas) rt = rewardCanvas.GetComponentInChildren<RectTransform>(true);
        if (rt) StartCoroutine(PlayPanelDrop(rt));

        // Cập nhật text cuối
        if (waveCounterText) waveCounterText.text = "Allwave Clear!";
    }
    IEnumerator PlayPanelDrop(RectTransform panel)
    {
        // lấy vị trí đích hiện tại
        Vector2 target = panel.anchoredPosition;

        // nếu chưa set khoảng rơi → dùng chiều cao panel * 1.2f
        float dist = panelDropDistance > 0f ? panelDropDistance : (panel.rect.height * 1.2f);

        // bắt đầu từ trên (bay xuống)
        Vector2 start = target + new Vector2(0f, dist);
        panel.anchoredPosition = start;

        float t = 0f;
        float d = Mathf.Max(0.1f, panelDropDuration);

        // dùng unscaled để không bị ảnh hưởng slow/pause
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / d);
            float e = panelDropCurve.Evaluate(k);
            panel.anchoredPosition = Vector2.LerpUnclamped(start, target, e);
            yield return null;
        }

        panel.anchoredPosition = target;
    }

}
