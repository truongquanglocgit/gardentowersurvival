using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Audio;
using Unity.VisualScripting;

public class GameController : MonoBehaviour
{
    // ================== Core Refs ==================
    [Header("Core Refs")]
    public WaveManager waveManager;
    public TowerLoadoutManager towerManager;
    public GameObject dieCanvas;

    [SerializeField] private Transform player;
    [SerializeField] private Transform playerSpawnPoint;
    private TowerSelectUI towerSelectUI;

    [Header("Currency")]
    public TextMeshProUGUI seedText;
    public int Seed = 100;

    [Header("Tower Limit")]
    public int maxTowerCount = 20;
    public TextMeshProUGUI towerCountText;
    [HideInInspector] public int currentTowerCount;

    public static GameController Instance;

    // ================== Audio Mixer Routing ==================
    [Header("Audio Mixer Routing")]
    public AudioMixer masterMixer;                 // Kéo MasterMixer
    public AudioMixerGroup musicGroup;             // Kéo Group "Music"
    public AudioMixerGroup sfxGroup;               // Kéo Group "SFX"

    [Header("UI Volume Sliders")]
    public Slider musicSlider;                     // Slider điều khiển MusicVol
    public Slider sfxSlider;                       // Slider điều khiển SFXVol

    private const string MUSIC_PARAM = "MusicExpose";
    private const string SFX_PARAM = "SFXExpose";

    // ================== Warning / Alarm FX ==================
    [Header("Warning FX")]
    [Tooltip("Bật auto báo động ngay khi Awake")]
    public bool alarmOnAwake = true;

    [Tooltip("Panel UI cảnh báo Enemy are coming")]
    public GameObject warningPanel;
    [Tooltip("Text cảnh báo (TMP)")]
    public TextMeshProUGUI warningText;

    [Tooltip("Âm thanh báo động")]
    public AudioClip alarmSFX;
    [Min(1)] public int alarmRepeat = 3;
    [Min(0.05f)] public float alarmInterval = 0.8f;
    private AudioSource alarmSource; // 2D alarm (route SFX)

    [Tooltip("Overlay nhấp nháy đỏ (UI Image full screen)")]
    public Image redFlashOverlay;
    [Min(0.05f)] public float flashDuration = 0.25f;
    [Min(1)] public int flashCountPerBeep = 1;

    [Header("Red Lines")]
    [Tooltip("Độ dày line")]
    public float lineWidth = 0.05f;
    [Tooltip("Màu line")]
    public Color lineColor = Color.red;
    [Tooltip("Material cho LineRenderer (để trống sẽ tạo Sprites/Default)")]
    public Material lineMaterial;

    [Header("Moving Arrows")]
    [Tooltip("Prefab mũi tên di chuyển về phía player")]
    public GameObject arrowPrefab;
    public float arrowSpeed = 3f;

    [Header("Arrow Orientation")]
    [Tooltip("Bù góc Euler cho arrow nếu prefab không nhìn theo +Z")]
    public Vector3 arrowSpawnEulerOffset = Vector3.zero;
    [Tooltip("Chỉ xoay theo mặt phẳng XZ (top-down)")]
    public bool arrowFlattenY = true;
    [Tooltip("Nếu muốn xoay 1 child thay vì root, nhập tên child ở đây. Để trống sẽ xoay root.")]
    public string arrowRotateChildName = "";

    // Runtime caches cho Warning FX
    private readonly List<Transform> _spawnerList = new();
    private readonly List<LineRenderer> _lines = new();
    private readonly List<GameObject> _arrows = new();

    private Material _runtimeLineMat;   // shared for all lines
    private Coroutine _flashCo;
    private Coroutine _alarmCo;
    private bool _alarmActive;

    // ================== Background Music (BGM) – 2 track ==================
    [Header("Background Music (2 tracks)")]
    [Tooltip("bgmClips[0]=Normal, bgmClips[1]=Boss")]
    public AudioClip[] bgmClips; // Size=2
    [Min(0f)] public float bgmCrossfade = 0.6f;
    public bool playBgmOnStart = false; // để WaveManager quyết định ngay từ wave 1
    public int startBgmIndex = 0;       // 0=Normal (nếu muốn phát khi Start)

    private AudioSource bgmSourceA; // 2 nguồn để crossfade mượt (route Music)
    private AudioSource bgmSourceB;
    private AudioSource _activeBgm;
    private Coroutine _bgmFadeCo;
    private int _currentBgmIndex = -1; // đang phát track nào (-1=chưa)
    [Range(0f, 1f)] public float defaultSfxVol = 0.8f;


    [Header("Default Volumes")]
    [Range(0f, 1f)] public float defaultMusicVol = 0.6f;
    

    void Awake()
    {
        currentTowerCount = 0;
        Instance = this;

        // Ensure player ref
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }

        // --- Init BGM audio sources (route vào Music) ---
        bgmSourceA = gameObject.AddComponent<AudioSource>();
        bgmSourceB = gameObject.AddComponent<AudioSource>();
        foreach (var src in new[] { bgmSourceA, bgmSourceB })
        {
            src.spatialBlend = 0f;
            src.loop = true;
            src.playOnAwake = false;
            src.volume = 1f; // để 1.0; volume tổng điều qua Mixer
            if (musicGroup) src.outputAudioMixerGroup = musicGroup;   // route vào Music
        }
        _activeBgm = bgmSourceA;

        // --- Init 2D alarm audio (route vào SFX) ---
        alarmSource = gameObject.AddComponent<AudioSource>();
        alarmSource.spatialBlend = 0f;
        alarmSource.playOnAwake = false;
        alarmSource.loop = false;
        alarmSource.volume = 1f; // để 1.0; tổng thể bị khống chế bởi Mixer SFX
        if (sfxGroup) alarmSource.outputAudioMixerGroup = sfxGroup;   // route vào SFX

        // --- Shared material for all LineRenderers (avoid many instances) ---
        if (!lineMaterial)
        {
            var shader = Shader.Find("Sprites/Default");
            _runtimeLineMat = new Material(shader) { color = lineColor };
        }
        else
        {
            _runtimeLineMat = lineMaterial;
        }

        // --- Find spawners and setup guides (lines + arrows) ---
        CacheSpawnersAndMakeGuides();

        // --- Auto alarm sequence on Awake if enabled ---
        if (alarmOnAwake) _alarmCo = StartCoroutine(AlarmSequence());
    }

    void OnEnable()
    {
        // Subscribe wave audio events
        WaveManager.OnWaveStarted += HandleWaveStarted_BGM;
        WaveManager.OnInterWaveDelayStarted += HandleInterDelay_BGM;
        WaveManager.OnAllWavesCompleted += HandleAllWaves_BGM;
    }

    void OnDisable()
    {
        // Unsubscribe wave audio events
        WaveManager.OnWaveStarted -= HandleWaveStarted_BGM;
        WaveManager.OnInterWaveDelayStarted -= HandleInterDelay_BGM;
        WaveManager.OnAllWavesCompleted -= HandleAllWaves_BGM;
    }

    void Start()
    {
        if (dieCanvas) dieCanvas.SetActive(false);

        // 0) Set mixer trước, KHÔNG chờ slider
        if (masterMixer)
        {
            masterMixer.SetFloat(MUSIC_PARAM, Linear01ToDb(defaultMusicVol));
            masterMixer.SetFloat(SFX_PARAM, Linear01ToDb(defaultSfxVol));
        }

        // 1) Dọn toàn bộ listener cũ để tránh “static = 0” còn sót
        if (musicSlider) musicSlider.onValueChanged.RemoveAllListeners();
        if (sfxSlider) sfxSlider.onValueChanged.RemoveAllListeners();

        // 2) Cấu hình range
        if (musicSlider) { musicSlider.minValue = 0f; musicSlider.maxValue = 1f; }
        if (sfxSlider) { sfxSlider.minValue = 0f; sfxSlider.maxValue = 1f; }

        // 3) Đặt giá trị mặc định **không bắn event**
        if (musicSlider) musicSlider.SetValueWithoutNotify(defaultMusicVol);
        if (sfxSlider) sfxSlider.SetValueWithoutNotify(defaultSfxVol);

        // 4) Áp lại vào mixer một lần (phòng có script khác vừa reset)
        if (masterMixer)
        {
            masterMixer.SetFloat(MUSIC_PARAM, Linear01ToDb(defaultMusicVol));
            masterMixer.SetFloat(SFX_PARAM, Linear01ToDb(defaultSfxVol));
        }

        // 5) GIỜ mới add listener (Dynamic)
        if (musicSlider) musicSlider.onValueChanged.AddListener(SetMusicVolume);
        if (sfxSlider) sfxSlider.onValueChanged.AddListener(SetSfxVolume);


        // Load wave từ GameSession
        var waveList = GameSession.Instance.selectedWaveList;

        // Load equipped towers
        towerManager.LoadEquippedFromPlayerData(GameSession.Instance.allTowerDatabase.allTowers);

        // Bind tower UI
        towerSelectUI = FindObjectOfType<TowerSelectUI>();
        if (towerSelectUI != null) towerSelectUI.InitTowerButtons(towerManager.equippedTowers);
        else Debug.LogWarning("TowerSelectUI not found");

        // Load waves & start
        waveManager.LoadWaves(waveList);
        StartCoroutine(waveManager.PlayWaves());
        UpdateTowerCount(0);

        // Spawn player
        if (player && playerSpawnPoint)
        {
            player.position = playerSpawnPoint.position;
            player.rotation = playerSpawnPoint.rotation;
        }

        // Camera follow
        var cam = Camera.main ? Camera.main.GetComponent<CameraOrbit>() : null;
        if (cam && player) cam.target = player.transform;

        // (Tuỳ chọn) Auto BGM khi Start (không khuyến khích – đã để false mặc định)
        if (playBgmOnStart) PlayBGM(startBgmIndex); // 0 = Normal
    }

    void Update()
    {
        if (seedText) seedText.text = $"{Seed}";
    }

    void LateUpdate()
    {
        // Keep guides only when alarm is active
        if (!_alarmActive || player == null) return;

        // Update red lines
        int count = Mathf.Min(_spawnerList.Count, _lines.Count);
        for (int i = 0; i < count; i++)
        {
            var sp = _spawnerList[i];
            var lr = _lines[i];
            if (!sp || !lr) continue;

            lr.SetPosition(0, sp.position);
            lr.SetPosition(1, player.position);
        }

        // Keep arrows oriented toward player
        for (int i = 0; i < _arrows.Count; i++)
        {
            var a = _arrows[i];
            if (!a) continue;
            OrientArrow(a.transform, player.position);
        }
    }

    // ================== Volume helpers (Mixer) ==================
    private static float Linear01ToDb(float v)
    {
        if (v <= 0.0001f) return -80f;   // sàn (gần mute)
        return Mathf.Log10(v) * 20f;     // dB
    }
    public void SetMusicVolume(float v)
    {
        if (masterMixer) masterMixer.SetFloat(MUSIC_PARAM, Linear01ToDb(v));
    }
    public void SetSfxVolume(float v)
    {
        float db = Linear01ToDb(v);
        Debug.Log($"[Slider→Mixer] SFX v={v:F2} → {db:F1} dB | sliderRef={(sfxSlider ? sfxSlider.value.ToString("F2") : "<null>")}");
        if (masterMixer) masterMixer.SetFloat(SFX_PARAM, db);
    }

    // ================== Alarm helpers ==================
    void CacheSpawnersAndMakeGuides()
    {
        CleanupGuides();

        var spawners = GameObject.FindGameObjectsWithTag("Spawner");
        foreach (var go in spawners)
        {
            
            if (!go) continue;
            var sc = go.GetComponent<Spawner>();   // <-- sửa ở đây
            sc.hideArrow();
            var t = go.gameObject.GetComponentInChildren<LineRenderer>().transform;
            _spawnerList.Add(t);

            // Red line
            var lr = go.GetComponentInChildren<LineRenderer>();
            if (!lr) lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.useWorldSpace = true;
            lr.material = _runtimeLineMat;
            lr.startColor = lineColor;
            lr.endColor = lineColor;

            Vector3 p0 = t.position;
            Vector3 p1 = player ? player.position : p0;
            lr.SetPosition(0, p0);
            lr.SetPosition(1, p1);
            _lines.Add(lr);

            // Arrow
            if (arrowPrefab && player)
            {
                var arrow = Instantiate(arrowPrefab, t.position, Quaternion.identity);
                OrientArrow(arrow.transform, player.position);

                var mover = arrow.GetComponent<ArrowMove>();
                if (!mover) mover = arrow.AddComponent<ArrowMove>();
                mover.Init(player, arrowSpeed);

                _arrows.Add(arrow);
            }
        }
    }

    void OrientArrow(Transform arrowRoot, Vector3 targetPos)
    {
        if (!arrowRoot) return;

        Transform t = arrowRoot;
        if (!string.IsNullOrEmpty(arrowRotateChildName))
        {
            var child = arrowRoot.Find(arrowRotateChildName);
            if (child) t = child;
        }

        Vector3 dir = targetPos - t.position;
        if (arrowFlattenY) dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion look = Quaternion.LookRotation(dir.normalized, Vector3.up);
        t.rotation = look * Quaternion.Euler(arrowSpawnEulerOffset);
    }

    IEnumerator AlarmSequence()
    {
        _alarmActive = true;

        // UI on
        if (warningPanel) warningPanel.SetActive(true);
        if (warningText) warningText.text = "Enemy are coming!";

        // Flash overlay in parallel
        if (redFlashOverlay) _flashCo = StartCoroutine(FlashOverlayLoop());

        // Play alarm beeps (SFX bus)
        if (alarmSFX)
        {
            for (int i = 0; i < Mathf.Max(1, alarmRepeat); i++)
            {
                alarmSource.pitch = 1f;
                alarmSource.PlayOneShot(alarmSFX, 1f); // cường độ chủ quan; tổng thể bị SFXVol khống chế
                yield return new WaitForSeconds(Mathf.Max(0.05f, alarmInterval));
            }
        }
        else
        {
            // No SFX? Still flash a couple times
            yield return new WaitForSeconds(flashDuration * 2f);
        }

        // End alarm → turn everything off/cleanup
        StopAlarmNow();
    }

    IEnumerator FlashOverlayLoop()
    {
        var baseColor = redFlashOverlay ? redFlashOverlay.color : Color.red;

        while (_alarmActive)
        {
            for (int j = 0; j < Mathf.Max(1, flashCountPerBeep); j++)
            {
                // fade in
                float t = 0f;
                while (t < flashDuration)
                {
                    t += Time.deltaTime;
                    float a = Mathf.Lerp(0f, 0.5f, t / flashDuration);
                    if (redFlashOverlay)
                        redFlashOverlay.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
                    yield return null;
                }
                // fade out
                t = 0f;
                while (t < flashDuration)
                {
                    t += Time.deltaTime;
                    float a = Mathf.Lerp(0.5f, 0f, t / flashDuration);
                    if (redFlashOverlay)
                        redFlashOverlay.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
                    yield return null;
                }
            }
            yield return null;
        }

        // ensure alpha off when leaving
        if (redFlashOverlay)
        {
            var c = redFlashOverlay.color;
            redFlashOverlay.color = new Color(c.r, c.g, c.b, 0f);
        }
    }

    public void StopAlarmNow()
    {
        if (!_alarmActive) return;
        _alarmActive = false;

        // stop flash
        if (_flashCo != null) StopCoroutine(_flashCo);
        _flashCo = null;
        if (redFlashOverlay)
        {
            var c = redFlashOverlay.color;
            redFlashOverlay.color = new Color(c.r, c.g, c.b, 0f);
        }

        // hide panel
        if (warningPanel) warningPanel.SetActive(false);

        // cleanup guide visuals (lines + arrows)
        CleanupGuides();

        // stop main sequence if still running
        if (_alarmCo != null) StopCoroutine(_alarmCo);
        _alarmCo = null;
    }

    void CleanupGuides()
    {
        // remove line renderers (component only)
        foreach (var lr in _lines)
        {
            if (!lr) continue;
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(lr);
            else Destroy(lr);
#else
            Destroy(lr);
#endif
        }
        _lines.Clear();
        _spawnerList.Clear();

        // destroy arrows
        foreach (var a in _arrows)
        {
            if (!a) continue;
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(a);
            else Destroy(a);
#else
            Destroy(a);
#endif
        }
        _arrows.Clear();
    }

    // ================== BGM helpers (2 tracks) ==================
    public void PlayBGM(int index)
    {
        if (bgmClips == null || bgmClips.Length < 2) return; // cần 2 bài: 0=Normal, 1=Boss
        if (index < 0 || index >= bgmClips.Length) { Debug.LogWarning("BGM index out of range"); return; }

        // Nếu đang phát đúng bài thì thôi
        if (_currentBgmIndex == index && _activeBgm && _activeBgm.isPlaying) return;

        var nextClip = bgmClips[index];
        if (!nextClip) { Debug.LogWarning("BGM clip is null"); return; }

        var inactive = (_activeBgm == bgmSourceA) ? bgmSourceB : bgmSourceA;
        inactive.clip = nextClip;
        inactive.time = 0f;
        inactive.volume = 1f; // tổng thể điều qua Mixer
        inactive.Play();

        if (_bgmFadeCo != null) StopCoroutine(_bgmFadeCo);
        _bgmFadeCo = StartCoroutine(CrossfadeBGM(_activeBgm, inactive, bgmCrossfade));

        _activeBgm = inactive;
        _currentBgmIndex = index;
    }

    IEnumerator CrossfadeBGM(AudioSource from, AudioSource to, float duration)
    {
        float t = 0f;
        float fromStart = from ? from.volume : 0f;
        if (to) to.volume = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime; // không phụ thuộc Time.timeScale (pause menu mượt)
            float k = (duration > 0f) ? (t / duration) : 1f;
            if (from) from.volume = Mathf.Lerp(fromStart, 0f, k);
            if (to) to.volume = Mathf.Lerp(0f, 1f, k);
            yield return null;
        }

        if (from)
        {
            from.Stop();
            from.volume = 1f;
        }
        if (to) to.volume = 1f;
    }

    public void StopBGM(float fadeOut = 0.3f)
    {
        if (_bgmFadeCo != null) StopCoroutine(_bgmFadeCo);
        _bgmFadeCo = StartCoroutine(FadeOutStop(_activeBgm, fadeOut));
        _currentBgmIndex = -1;
    }

    IEnumerator FadeOutStop(AudioSource src, float duration)
    {
        if (!src || !src.isPlaying) yield break;
        float start = src.volume;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            src.volume = Mathf.Lerp(start, 0f, t / duration);
            yield return null;
        }
        src.Stop();
        src.volume = 1f;
    }

    // ================== Wave Audio Handlers ==================
    // Mapping 2 bài: [0]=Normal, [1]=Boss
    private void HandleWaveStarted_BGM(int waveIndex, WaveDef wave)
    {
        if (wave != null && wave.isBossWave) PlayBGM(1);   // Boss
        else PlayBGM(0);   // Normal
    }

    private void HandleInterDelay_BGM(int waveIndex, float delaySeconds)
    {
        // no-op: giữ nguyên bài đang phát giữa các wave (theo yêu cầu)
    }

    private void HandleAllWaves_BGM()
    {
        // Clear all → về Normal (nếu muốn yên lặng, gọi StopBGM)
        PlayBGM(0);
    }

    // ================== Game Flow ==================
    public void winning()
    {
        CurrencyManager.Instance?.AddSeed(GameSession.Instance.currentMapData.waterReward);
    }

    public bool TrySpendSeed(int amount)
    {
        if (Seed >= amount) { Seed -= amount; return true; }
        return false;
    }

    public void AddSeed(int amount) => Seed += amount;

    public void die()
    {
        Time.timeScale = 0;
        Debug.Log(" Player Died");
        StopBGM(0.4f); // fade out nhạc
        if (dieCanvas != null) dieCanvas.SetActive(true);
        else Debug.LogError(" dieCanvas is NULL. Check inspector reference!");
    }

    public void TryAgain()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void UpdateTowerCount(int number)
    {
        currentTowerCount += number;
        if (towerCountText != null)
            towerCountText.text = $"Tower: {currentTowerCount} / {maxTowerCount}";
    }

    public bool CanPlaceTower() => currentTowerCount < maxTowerCount;
}
