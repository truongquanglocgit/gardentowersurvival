using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    

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

    // runtime preview stage
    RenderTexture _rt;
    Camera _cam;
    Transform _anchor;
    Light _light;
    GameObject _currentPreviewRoot;

    void Start()
    {
        BuildPreviewStage();
        UpdateMapUI();

        if (upMapButton) upMapButton.onClick.AddListener(() => ChangeMap(1));
        if (downMapButton) downMapButton.onClick.AddListener(() => ChangeMap(-1));
        if (startGameButton) startGameButton.onClick.AddListener(LoadSelectedMap);

        CloseAllPanels(); // nếu muốn ẩn hết từ đầu
    }

    void OnDestroy()
    {
        CleanupPreviewInstance();
        if (_rt) Destroy(_rt);
        if (_cam) Destroy(_cam.gameObject);
    }

    // ---------------- Map select ----------------
    void ChangeMap(int delta)
    {
        currentMapIndex = Mathf.Clamp(currentMapIndex + delta, 0, maps.Count - 1);
        UpdateMapUI();
    }

    void UpdateMapUI()
    {
        if (maps == null || maps.Count == 0) return;
        var currentMap = maps[currentMapIndex];

        if (mapNameText) mapNameText.text = currentMap.mapName;

        // Nếu có prefab preview => show 3D; ngược lại fallback ảnh sprite
        if (currentMap.previewPrefab != null)
        {
            if (mapPreviewRawImage) mapPreviewRawImage.texture = _rt;
            SpawnPreview(currentMap);
        }
        else
        {
            CleanupPreviewInstance();
            if (mapPreviewRawImage) mapPreviewRawImage.texture = currentMap.previewImage
                ? currentMap.previewImage.texture
                : null;
        }
    }

    public void LoadSelectedMap()
    {
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
}
