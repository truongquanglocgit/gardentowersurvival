using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

public class GameController : MonoBehaviour
{
    // ===== KEEP: Your original references =====
    public WaveManager waveManager;
    public TowerLoadoutManager towerManager;
    public GameObject dieCanvas;

    [SerializeField] private Transform player;
    [SerializeField] private Transform playerSpawnPoint;
    private TowerSelectUI towerSelectUI;

    public TextMeshProUGUI seedText;
    public int Seed = 100;

    [Header("Tower Limit")]
    public int maxTowerCount = 20;
    public TextMeshProUGUI towerCountText;
    [HideInInspector] public int currentTowerCount;

    public static GameController Instance;

    // ================= NEW: Alarm / Warning FX =================
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
    private AudioSource alarmSource; // 2D alarm

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
    [Tooltip("Bù góc Euler cho arrow nếu prefab không nhìn theo +Z. Ví dụ (0,90,0) nếu mesh nhìn +X.")]
    public Vector3 arrowSpawnEulerOffset = Vector3.zero;

    [Tooltip("Chỉ xoay theo mặt phẳng XZ (top-down).")]
    public bool arrowFlattenY = true;

    [Tooltip("Nếu muốn xoay 1 child thay vì root, nhập tên child ở đây. Để trống sẽ xoay root.")]
    public string arrowRotateChildName = "";

    // ===== Runtime caches =====
    private readonly List<Transform> _spawnerList = new();
    private readonly List<LineRenderer> _lines = new();
    private readonly List<GameObject> _arrows = new();

    private Material _runtimeLineMat;   // shared for all lines
    private Coroutine _flashCo;
    private Coroutine _alarmCo;
    private bool _alarmActive;

    // ===========================================================

    void Awake()
    {
        currentTowerCount = 0;
        Instance = this;

        // Ensure we have player ref
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }

        // --- Init 2D alarm audio ---
        alarmSource = gameObject.AddComponent<AudioSource>();
        alarmSource.spatialBlend = 0f;
        alarmSource.playOnAwake = false;
        alarmSource.loop = false;

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

    void Start()
    {
        if (dieCanvas) dieCanvas.SetActive(false);

        // Load wave from GameSession
        var waveList = GameSession.Instance.selectedWaveList;

        // Load equipped towers
        towerManager.LoadEquippedFromPlayerData(GameSession.Instance.allTowerDatabase.allTowers);

        // Bind tower UI
        towerSelectUI = FindObjectOfType<TowerSelectUI>();
        if (towerSelectUI != null) towerSelectUI.InitTowerButtons(towerManager.equippedTowers);
        else Debug.LogWarning("TowerSelectUI not found");

        // Load waves and start
        waveManager.LoadWaves(waveList);
        StartCoroutine(waveManager.PlayWaves());
        UpdateTowerCount(0);

        // Spawn player at spawn point
        if (player != null && playerSpawnPoint != null)
        {
            player.position = playerSpawnPoint.position;
            player.rotation = playerSpawnPoint.rotation;
        }

        // Set camera follow
        var cam = Camera.main ? Camera.main.GetComponent<CameraOrbit>() : null;
        if (cam && player) cam.target = player.transform;
    }

    void Update()
    {
        if (seedText) seedText.text = $"Seed: {Seed}";
    }

    // Keep lines attached to player every frame
    void LateUpdate()
    {
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

    // ===================== Alarm helpers ======================

    /// <summary>
    /// Find all Spawners, create red lines & spawn moving arrows pointing at player.
    /// </summary>
    void CacheSpawnersAndMakeGuides()
    {
        CleanupGuides();

        var spawners = GameObject.FindGameObjectsWithTag("Spawner");
        foreach (var go in spawners)
        {
            if (!go) continue;
            var t = go.transform;
            _spawnerList.Add(t);

            // --- Red line from spawner to player ---
            var lr = go.GetComponent<LineRenderer>();
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

            // --- Arrow moving toward player ---
            if (arrowPrefab && player)
            {
                var arrow = Instantiate(arrowPrefab, t.position, Quaternion.identity);
                // Orient IMMEDIATELY with manual euler offset/child option
                OrientArrow(arrow.transform, player.position);

                // Ensure it moves
                var mover = arrow.GetComponent<ArrowMove>();
                if (!mover) mover = arrow.AddComponent<ArrowMove>();
                mover.Init(player, arrowSpeed);

                _arrows.Add(arrow);
            }
        }
    }

    /// <summary>
    /// Orient an arrow (or its child) toward a target position with optional euler offset and Y flatten.
    /// </summary>
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

        // Play alarm beeps
        if (alarmSFX)
        {
            for (int i = 0; i < Mathf.Max(1, alarmRepeat); i++)
            {
                alarmSource.pitch = 1f;
                alarmSource.PlayOneShot(alarmSFX, 1f);
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
        var baseColor = redFlashOverlay.color;

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
                    redFlashOverlay.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
                    yield return null;
                }
                // fade out
                t = 0f;
                while (t < flashDuration)
                {
                    t += Time.deltaTime;
                    float a = Mathf.Lerp(0.5f, 0f, t / flashDuration);
                    redFlashOverlay.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
                    yield return null;
                }
            }
            yield return null;
        }

        // ensure alpha off when leaving
        redFlashOverlay.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
    }

    /// <summary>
    /// Public: stop alarm early (e.g., call when wave1 starts).
    /// </summary>
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

    /// <summary>
    /// Remove line components from spawners and destroy spawned arrows.
    /// </summary>
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

    // ===================== Other original methods =====================

    public void winning()
    {
        CurrencyManager.Instance.AddSeed(GameSession.Instance.currentMapData.waterReward);
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
