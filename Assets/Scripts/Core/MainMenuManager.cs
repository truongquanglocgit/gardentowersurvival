using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    public GameObject RewardPanel;
    public GameObject OwnedPanel;
    public TowerData freePlant;
    public Image freePlantImg;
    public TextMeshProUGUI rewardTxt;
    public TextMeshProUGUI TotalAdsCount;
    public TextMeshProUGUI levelText;
    public Slider XPSlider;
    [Tooltip("FOV camera preview (để fit đẹp hơn)")]
    public float camFov = 35f;
    [Tooltip("Đệm cho khung hình (1.0 = sát, 1.2 = thoáng)")]
    public float fitPadding = 1.2f;
    [Tooltip("Đẩy tâm nhìn lên 1 chút cho map ít phẳng")]
    public Vector3 focusOffset = new Vector3(0f, 0.25f, 0f);
    [Header("Map Selection")]
    public List<MapData> maps;
    public RawImage mapPreviewRawImage;     // ← RawImage để show RenderTexture
    public TextMeshProUGUI mapNameText;
    public Button upMapButton;
    public Button downMapButton;
    private int currentMapIndex = 0;

    [Header("Panels")]
    public GameObject characterPanel;
    public GameObject growPlanPanel;
    public GameObject gamemodePanel;
    public GameObject shopPanel;

    [Header("Map Load")]
    public Button startGameButton;

    [Header("3D Preview Settings")]
    [Tooltip("Kích thước RT vuông cho preview.")]
    public int previewResolution = 1024;
    [Tooltip("Layer riêng cho preview (tự tạo một layer, vd: Preview3D).")]
    public int previewLayer = 30; // đặt đúng Layer ID bạn tạo
    public float autoRotateSpeed = 15f;
    public Color stageBgColor = new Color(0, 0, 0, 0); // trong suốt
                                                       // NEW: Lock UI
    [Header("Lock Overlay")]
    public GameObject lockOverlayRoot;           // Panel phủ
    public Image lockImage;                      // Ảnh ổ khóa (tuỳ bạn)
    public TextMeshProUGUI lockText;
    // runtime preview stage
    RenderTexture _rt;
    Camera _cam;
    Transform _anchor;
    Light _light;
    GameObject _currentPreviewRoot;
    bool _mapsSorted;
    bool _firstRenderDone;
    [Header("Rarity Colors (chỉnh trong Inspector)")]
    [SerializeField] Color commonColor = new Color32(180, 180, 180, 255);
    [SerializeField] Color uncommonColor = new Color32(80, 200, 120, 255);
    [SerializeField] Color rareColor = new Color32(80, 120, 255, 255);
    [SerializeField] Color epicColor = new Color32(170, 90, 255, 255);
    [SerializeField] Color legendaryColor = new Color32(255, 170, 40, 255);
    [SerializeField] Color mythicColor = new Color32(255, 60, 60, 255);
    [SerializeField] Color voidColor = new Color32(80, 220, 220, 255);
    void Start()
    {

        // KHÔNG gọi BuildPreviewStage ở đây nữa
        if (upMapButton) upMapButton.onClick.AddListener(() => ChangeMap(1));
        if (downMapButton) downMapButton.onClick.AddListener(() => ChangeMap(-1));
        if (startGameButton) startGameButton.onClick.AddListener(LoadSelectedMap);
        CloseAllPanels();
        LoadLevel();

    }

    void Awake()
    {
        RewardPanel.SetActive(false);
        addFreePlant();
        SortMapsIfNeeded();
        BuildPreviewStage();   // ⬅️ chuyển từ Start() sang đây
    }
    void LoadLevel()
    {
        if (levelText) { levelText.text = PlayerDataManager.Instance.PlayerCurrentLevel().ToString(); }

        getAdsCount();
        LoadXPSlider();
    }
    void LoadXPSlider()
    {

        var (current, total) = PlayerDataManager.Instance.GetLevelXpProgress();
        Debug.Log(current + "  " + total);
        XPSlider.maxValue = total;
        XPSlider.minValue = 0;
        XPSlider.value = current;
    }
    void OnEnable()
    {
        // Khi quay về MainMenu từ scene khác → đảm bảo refresh sau khi PlayerData có mặt
        StartCoroutine(EnsurePlayerDataAndRefresh());
    }
    System.Collections.IEnumerator EnsurePlayerDataAndRefresh()
    {
        int safety = 0;
        while ((PlayerDataManager.Instance == null ||
                PlayerDataManager.Instance.playerData == null ||
                _cam == null) && safety < 300)
        {
            safety++;
            yield return null;
        }

        // Sắp xếp lại map nếu cần
        SortMapsIfNeeded();

        // ✅ Chọn map cao nhất đã unlock làm mặc định
        currentMapIndex = GetHighestUnlockedIndexSafe();

        // Cập nhật UI + lock state theo map mới chọn
        UpdateMapUI();
        ApplyLockUI();
    }

    public void addAdsCount()
    {
#if ON_ADS
        MAXUtil.Instance.ShowRewardAd(FirebaseEventMKTEnum.popup_watchAdsDirect.ToString(), (placement, adUnit, adInfo, countryCodes) =>
        {
            if (adUnit == null && adInfo == null && countryCodes == null)
            {
                //GameManager.Instance.NoticeNoAds.gameObject.SetActive(true);
            }
            else
            {
                FirebaseUtil.Instance.LogEventWatchAds(FirebaseEventMKTEnum.watchads);
                PlayerDataManager.Instance.AddAdsCount();
                getAdsCount();
            }
        });
#else
                PlayerDataManager.Instance.AddAdsCount();
                getAdsCount();
#endif

    }
    public void getAdsCount()
    {
        int totalAdsCount = PlayerDataManager.Instance.GetAdsCount();
        TotalAdsCount.text = totalAdsCount.ToString();
        checkOwned();
        if (totalAdsCount >= 20)
        {
            PlayerDataManager.Instance.ResetAdsCount(20);
            TotalAdsCount.text = "0";
            grandReward();


        }

    }
    public void grandReward()
    {

        var pd = PlayerDataManager.Instance.playerData;

        if (!pd.unlockedTowerIds.Contains(freePlant.towerId))
        {
            // ✅ Thêm tower mới vào danh sách
            pd.unlockedTowerIds.Add(freePlant.towerId);
            PlayerDataManager.Instance.SavePlayerData();

            // ✅ Tìm child tên "RewardImage" trong RewardPanel
            Transform rewardImageTf = RewardPanel.transform.Find("RewardImage");
            if (rewardImageTf != null)
            {
                var rewardImage = rewardImageTf.GetComponent<Image>();
                if (rewardImage != null)
                {
                    rewardImage.sprite = freePlant.icon;
                    rewardTxt.text = freePlant.name;
                    var rarityColor = GetRarityColor(freePlant.rarity);
                    rewardTxt.color = rarityColor;

                }
                else
                {
                    Debug.LogError("❌ RewardImage không có component Image!");
                }
            }
            else
            {
                Debug.LogError("❌ Không tìm thấy child 'RewardImage' trong RewardPanel!");
            }

            // ✅ Bật RewardPanel lên
            RewardPanel.SetActive(true);

        }
    }
    public void checkOwned()
    {
        var pd = PlayerDataManager.Instance.playerData;

        if (pd.unlockedTowerIds.Contains(freePlant.towerId))
        {
            OwnedPanel.SetActive(true);
        }
    }
    public void addFreePlant()
    {
        freePlantImg.sprite = freePlant.icon;
    }
    bool IsUnlockedByIndex(int index)
    {
        if (maps == null || maps.Count == 0) return false;
        index = Mathf.Clamp(index, 0, maps.Count - 1);

        if (index == 0) return true; // map đầu luôn mở

        var pd = PlayerDataManager.Instance?.playerData;
        var completed = (pd != null && pd.completedMapIds != null)
                        ? new HashSet<string>(pd.completedMapIds)
                        : null;
        if (completed == null) return false;

        var cur = maps[index];
        var prev = maps[index - 1];

        if (cur && !string.IsNullOrEmpty(cur.mapId) && completed.Contains(cur.mapId)) return true;
        if (prev && !string.IsNullOrEmpty(prev.mapId) && completed.Contains(prev.mapId)) return true;

        return false;
    }



    void SortMapsIfNeeded()
    {
        if (_mapsSorted || maps == null || maps.Count == 0) return;

        // Cảnh báo cấu hình để tránh unlock sai
        var dup = maps.Where(m => m && !string.IsNullOrWhiteSpace(m.mapId))
                      .GroupBy(m => m.mapId).Where(g => g.Count() > 1)
                      .Select(g => g.Key).ToList();
        if (dup.Count > 0) Debug.LogWarning("[MainMenu] Trùng mapId: " + string.Join(", ", dup));

        maps = maps.OrderBy(m => m.orderIndex).ToList();
        currentMapIndex = Mathf.Clamp(currentMapIndex, 0, maps.Count - 1);
        _mapsSorted = true;
    }

    int GetHighestUnlockedIndexSafe()
    {
        if (maps == null || maps.Count == 0) return 0;
        int hi = 0;
        for (int i = 0; i < maps.Count; i++)
            if (IsUnlockedByIndex(i)) hi = i;
        return hi;
    }
    void OnDestroy()
    {
        CleanupPreviewInstance();
        if (_rt) Destroy(_rt);
        if (_cam) Destroy(_cam.gameObject);
    }

    void ChangeMap(int delta)
    {
        SortMapsIfNeeded();
        currentMapIndex = Mathf.Clamp(currentMapIndex + delta, 0, maps.Count - 1);
        UpdateMapUI();
        ApplyLockUI();
    }

    void UpdateMapUI()
    {
        if (maps == null || maps.Count == 0) return;
        var currentMap = maps[currentMapIndex];

        if (mapNameText) mapNameText.text = currentMap.mapName;

        if (currentMap.previewPrefab != null)
        {
            if (mapPreviewRawImage) mapPreviewRawImage.texture = _rt;
            SpawnPreview(currentMap);
        }
        else
        {
            CleanupPreviewInstance();
            if (mapPreviewRawImage) mapPreviewRawImage.texture = currentMap.previewImage ? currentMap.previewImage.texture : null;
        }
    }

    public void LoadSelectedMap()
    {
        if (!IsUnlockedByIndex(currentMapIndex))
        {
            Debug.Log("⛔ Map locked. Complete previous map first.");
            return;
        }

        var selectedMap = maps[currentMapIndex];
        if (GameSession.Instance)
        {
            GameSession.Instance.currentMapData = selectedMap;
            GameSession.Instance.selectedWaveList = selectedMap.waveList;
        }
        LoaderBridge.LoadWithLoadingScreen(selectedMap.sceneToLoad, "Loading");
    }

    // ---------------- Panels ----------------
    public void OpenCharacterPanel() => TogglePanel(characterPanel);
    public void OpenGrowPlanPanel() => TogglePanel(growPlanPanel);
    public void OpenGamemodePanel() => TogglePanel(gamemodePanel);
    public void OpenShopPanel() => TogglePanel(shopPanel);

    void TogglePanel(GameObject panel)
    {
        CloseAllPanels();
        if (panel) panel.SetActive(true);
    }

    void CloseAllPanels()
    {
        // nếu muốn đóng hết panel khi vào menu:
        // if (characterPanel) characterPanel.SetActive(false);
        // if (growPlanPanel)  growPlanPanel.SetActive(false);
        // if (gamemodePanel)  gamemodePanel.SetActive(false);
        // if (shopPanel)      shopPanel.SetActive(false);
    }

    // ======================================================
    // =============== 3D PREVIEW IMPLEMENTATION ============
    // ======================================================
    void BuildPreviewStage()
    {
        _rt = new RenderTexture(previewResolution, previewResolution, 16, RenderTextureFormat.ARGB32);
        _rt.antiAliasing = 4;

        var camGO = new GameObject("MapPreviewCamera");
        _cam = camGO.AddComponent<Camera>();
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = stageBgColor;
        _cam.cullingMask = 1 << previewLayer;        // chỉ render layer preview
        _cam.targetTexture = _rt;
        _cam.nearClipPlane = 0.05f;
        _cam.farClipPlane = 500f;
        _cam.fieldOfView = camFov;

        var lightGO = new GameObject("MapPreviewKeyLight");
        _light = lightGO.AddComponent<Light>();
        _light.type = LightType.Directional;
        _light.intensity = 1.1f;
        _light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        _light.cullingMask = 1 << previewLayer;      // đảm bảo chiếu đúng layer

        // Optional: Fill light mềm cho bớt tối
        var fill = new GameObject("MapPreviewFillLight").AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = 0.35f;
        fill.transform.rotation = Quaternion.Euler(30f, 140f, 0f);
        fill.cullingMask = 1 << previewLayer;

        _anchor = new GameObject("MapPreviewAnchor").transform;

    }

    void SpawnPreview(MapData data)
    {
        CleanupPreviewInstance();

        _currentPreviewRoot = new GameObject("MapPreviewRoot");
        _currentPreviewRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        var inst = Instantiate(data.previewPrefab, _currentPreviewRoot.transform);
        inst.transform.localPosition = data.previewPosition;
        inst.transform.localRotation = Quaternion.Euler(data.previewEuler);
        inst.transform.localScale = Vector3.one * Mathf.Max(0.0001f, data.previewScale);

        SetLayerRecursively(_currentPreviewRoot, previewLayer);

        // --- AUTO-FIT ---
        Bounds b = GetRenderersBounds(_currentPreviewRoot.transform);
        Vector3 center = b.center + focusOffset;

        // Bán kính xấp xỉ để fit theo FOV dọc
        float radius = Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z));
        float fovRad = _cam.fieldOfView * Mathf.Deg2Rad;
        float dist = (radius * fitPadding) / Mathf.Tan(fovRad * 0.5f);
        dist = Mathf.Max(dist, 0.5f);

        // Hướng nhìn hơi từ trên xuống
        Quaternion viewRot = Quaternion.Euler(20f, 30f, 0f);
        Vector3 camDir = viewRot * Vector3.forward;

        _cam.transform.position = center - camDir * dist;
        _cam.transform.rotation = Quaternion.LookRotation(center - _cam.transform.position, Vector3.up);
    }

    static Bounds GetRenderersBounds(Transform root)
    {
        var rends = root.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) return new Bounds(root.position, Vector3.one);
        Bounds b = new Bounds(rends[0].bounds.center, Vector3.zero);
        foreach (var r in rends) b.Encapsulate(r.bounds);
        return b;
    }



    void CleanupPreviewInstance()
    {
        if (_currentPreviewRoot != null)
        {
            Destroy(_currentPreviewRoot);
            _currentPreviewRoot = null;
        }
    }

    void Update()
    {
        // auto rotate mượt (không phụ thuộc timeScale)
        if (_currentPreviewRoot != null)
        {
            _currentPreviewRoot.transform.Rotate(Vector3.up, autoRotateSpeed * Time.unscaledDeltaTime, Space.World);
        }
    }

    static void SetLayerRecursively(GameObject go, int layer)
    {
        if (!go) return;
        go.layer = layer;
        foreach (Transform t in go.transform)
            SetLayerRecursively(t.gameObject, layer);
    }
    // ========= NEW: Helpers cho unlock =========
    bool IsCurrentMapUnlocked() => IsUnlockedByIndex(currentMapIndex);


    void ApplyLockUI()
    {
        bool unlocked = IsUnlockedByIndex(currentMapIndex);

        if (lockOverlayRoot) lockOverlayRoot.SetActive(!unlocked);
        if (!unlocked && lockText) lockText.text = "You Need To Complete Previeous Map";

        if (startGameButton) startGameButton.interactable = unlocked;

        if (mapPreviewRawImage)
            mapPreviewRawImage.color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.5f);

        if (mapNameText)
            mapNameText.color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.6f);
    }
    // -------- Helpers --------
    private Color GetRarityColor(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Common: return commonColor;
            case Rarity.Uncommon: return uncommonColor;
            case Rarity.Rare: return rareColor;
            case Rarity.Epic: return epicColor;
            case Rarity.Legendary: return legendaryColor;
            case Rarity.Mythic: return mythicColor;
            case Rarity.Void: return voidColor;
            default: return commonColor;
        }
    }
}
