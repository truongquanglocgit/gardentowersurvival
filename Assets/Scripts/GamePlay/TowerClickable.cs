using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class TowerClickable : MonoBehaviour
{
    [SerializeField] private TowerController tower;

    [Header("Range Ring (LineRenderer)")]
    [Tooltip("Màu vòng tầm đánh (alpha < 1 để mờ).")]
    public Color ringColor = new Color(0f, 1f, 0.3f, 0.35f);
    [Tooltip("Độ dày nét vòng.")]
    public float ringWidth = 0.06f;
    [Tooltip("Số đoạn vẽ vòng (càng nhiều càng mịn).")]
    [Range(16, 256)] public int ringSegments = 80;
    [Tooltip("Đặt vòng nhô lên khỏi mặt đất 1 chút để tránh z-fighting.")]
    public float ringYOffset = 0.03f;
    

    // 🔹 THÊM: chỉ raycast vào layer Tower
    [Header("Raycast")]
    [SerializeField] private LayerMask towerMask; // set trong Inspector: chỉ tick layer "Tower"
    const float MaxRayDist = 1000f;
    // Runtime
    static TowerClickable _currentSelected;        // đảm bảo chỉ 1 tower được “chọn”
    Camera _cam;
    GameObject _ringGO;
    LineRenderer _lr;
    float _cachedRadius = -1f;
    bool _hovered;
    bool _selected;
    [Header("Pulse (optional)")]
    public bool enablePulse = true;
    [Tooltip("Tốc độ nháy (rad/s)")]
    public float pulseSpeed = 4f;
    [Range(0f, 1f)] public float pulseAlphaMin = 0.35f;
    [Range(0f, 1f)] public float pulseAlphaMax = 0.75f; // có thể = ringColor.a
    void Reset()
    {
        if (!tower) tower = GetComponentInChildren<TowerController>();
        if (tower == null) Debug.LogWarning("[TowerClickable] cannot find TowerController in children.");
    }

    void Awake()
    {
        _cam = Camera.main;
        EnsureRing();
        HideRing();

        // Fallback: nếu quên set trong Inspector, tự lấy layer "Tower"
        if (towerMask.value == 0)
        {
            int towerLayerIndex = LayerMask.NameToLayer("Tower");
            if (towerLayerIndex >= 0) towerMask = 1 << towerLayerIndex;
            else Debug.LogWarning("[TowerClickable] Layer 'Tower' chưa tồn tại. Hãy tạo layer và gán cho các collider của tower.");
        }
    }

    void Update()
    {
        // Bỏ qua khi trỏ lên UI
        if (EventSystem.current && EventSystem.current.IsPointerOverGameObject())
        {
            _hovered = false;
            if (!_selected) HideRing();
            return;
        }

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
        HandleHoverPC();
        HandleClickPC();
#else
        HandleTouchMobile();
#endif

        // Cập nhật bán kính nếu vòng đang bật
        if ((_hovered || _selected) && tower != null)
        {
            float r = tower.CurrentRange; // dùng đúng range theo Level của TowerController
            if (!Mathf.Approximately(r, _cachedRadius))
            {
                BuildRing(r);
            }
        }
        // Pulse alpha để ring nổi bật hơn trên nền cỏ (không cần LateUpdate)
        if (_ringGO && _ringGO.activeSelf && _lr && enablePulse)
        {
            float t = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed);   // 0..1
            float a = Mathf.Lerp(pulseAlphaMin, pulseAlphaMax, t);

            // Giữ màu gradient, chỉ cập nhật alpha keys
            var grad = _lr.colorGradient;             // trả về bản copy
            var cks = grad.colorKeys;                // giữ nguyên
            var aks = grad.alphaKeys;
            if (aks == null || aks.Length < 2)
                aks = new[] { new GradientAlphaKey(a, 0f), new GradientAlphaKey(a, 1f) };
            else
            {
                aks[0].alpha = a;
                aks[1].alpha = a;
            }
            grad.SetKeys(cks, aks);
            _lr.colorGradient = grad;                 // gán lại để áp dụng
        }

    }

    // ================= PC (chuột) =================
    void HandleHoverPC()
    {
        _hovered = false;
        if (_cam == null) _cam = Camera.main;
        if (!_cam) return;

        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);

        // 🔹 CHỈ bắn vào towerMask, bỏ Player hoàn toàn
        if (Physics.Raycast(ray, out RaycastHit hit, MaxRayDist, towerMask, QueryTriggerInteraction.Ignore))
        {
            // 🔹 Nhận diện tower theo "cha" của collider (không yêu cầu trúng đúng gameObject hiện tại)
            var clickable = hit.collider.GetComponentInParent<TowerClickable>();
            if (clickable == this)
            {
                _hovered = true;
                ShowRing();
            }
        }
        if (!_hovered && !_selected) HideRing();
    }

    void HandleClickPC()
    {
        if (Input.GetMouseButtonUp(0))
            TryClickAt(Input.mousePosition);
    }

    // ================= Mobile (touch) =================
    void HandleTouchMobile()
    {
        if (Input.touchCount > 0 && Input.touches[0].phase == TouchPhase.Ended)
            TryClickAt(Input.touches[0].position);
    }
    // ================= Common =================
    void TryClickAt(Vector2 screenPos)
    {
        if (_cam == null) _cam = Camera.main;
        if (!_cam) return;

        Ray ray = _cam.ScreenPointToRay(screenPos);

        // 🔹 CHỈ bắn vào towerMask
        if (Physics.Raycast(ray, out RaycastHit hit, MaxRayDist, towerMask, QueryTriggerInteraction.Ignore))
        {
            var clickable = hit.collider.GetComponentInParent<TowerClickable>();
            if (clickable == this)
            {
                SelectThis();
                if (tower) TowerUpgradeUI.Instance.Show(tower);
                return;
            }
        }

        // Click ra ngoài → bỏ chọn cái đang chọn (nếu có)
        if (_currentSelected != null)
            _currentSelected.Deselect();
    }


    // Cho phép nơi khác (UI) tắt ring & bỏ chọn tower đang chọn
    public static void HideAnyRingAndDeselect()
    {
        if (_currentSelected != null)
            _currentSelected.Deselect();
    }

    // Cập nhật vòng theo bán kính hiện tại của tower rồi TẮT NGAY (dùng cho Upgrade)
    public void UpdateRingToCurrentRangeThenHide()
    {
        if (tower)
        {
            // cập nhật vòng để phản ánh range mới
            BuildRing(tower.CurrentRange);
        }
        // tắt vòng + bỏ selected để đồng bộ với UI
        Deselect();
    }

    // (tuỳ chọn) Nếu bạn muốn chỉ tắt vòng (không bỏ selected), dùng hàm này
    public void HideRingOnly()
    {
        HideRing();
    }
    void SelectThis()
    {
        if (_currentSelected != null && _currentSelected != this)
            _currentSelected.Deselect();

        _currentSelected = this;
        _selected = true;
        ShowRing();
        // build ngay bán kính hiện tại
        if (tower) BuildRing(tower.CurrentRange);
    }

    void Deselect()
    {
        _selected = false;
        if (!_hovered) HideRing();
        if (_currentSelected == this) _currentSelected = null;
    }

    // ================= Range Ring =================
    void EnsureRing()
    {
        if (_ringGO != null) return;

        _ringGO = new GameObject("RangeRing");
        _ringGO.transform.SetParent(transform, false);
        _ringGO.transform.localPosition = Vector3.up * ringYOffset;

        _lr = _ringGO.AddComponent<LineRenderer>();
        _lr.useWorldSpace = false;
        _lr.loop = true;
        _lr.widthMultiplier = ringWidth;

        // Shader ưu tiên Additive (nổi trên nền cỏ) -> fallback nếu không có
        Shader sh = Shader.Find("Particles/Additive");
        if (!sh) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (!sh) sh = Shader.Find("Sprites/Default");
        _lr.material = new Material(sh);

        // Gradient: trắng -> vàng, alpha lấy từ ringColor.a
        var grad = new Gradient();
        grad.SetKeys(
            new[] {
            new GradientColorKey(Color.white, 0f),
            new GradientColorKey(new Color(1f, 0.85f, 0f), 1f)
            },
            new[] {
            new GradientAlphaKey(ringColor.a, 0f),
            new GradientAlphaKey(ringColor.a, 1f)
            }
        );
        _lr.colorGradient = grad;

        _lr.startColor = _lr.endColor = ringColor;
        _lr.positionCount = Mathf.Max(3, ringSegments);
        _ringGO.SetActive(false);
    }


    void BuildRing(float radius)
    {
        EnsureRing();
        radius = Mathf.Max(0.01f, radius);
        _cachedRadius = radius;

        int segs = Mathf.Max(3, ringSegments);
        _lr.positionCount = segs;

        // Vẽ vòng trong local space của _ringGO
        float step = Mathf.PI * 2f / segs;
        for (int i = 0; i < segs; i++)
        {
            float ang = i * step;
            float x = Mathf.Cos(ang) * radius;
            float z = Mathf.Sin(ang) * radius;
            _lr.SetPosition(i, new Vector3(x, 0f, z));
        }

        // luôn giữ vòng nằm ngang (theo yaw của root), không lật theo model
        _ringGO.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
    }

    void ShowRing()
    {
        EnsureRing();
        if (!_ringGO.activeSelf) _ringGO.SetActive(true);
    }

    void HideRing()
    {
        if (_ringGO && _ringGO.activeSelf) _ringGO.SetActive(false);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        ringSegments = Mathf.Clamp(ringSegments, 16, 256);
        ringWidth = Mathf.Max(0.001f, ringWidth);
        ringYOffset = Mathf.Max(0f, ringYOffset);

        if (_lr != null)
        {
            _lr.startColor = _lr.endColor = ringColor;
            _lr.widthMultiplier = ringWidth;
            if (_ringGO) _ringGO.transform.localPosition = Vector3.up * ringYOffset;
            // rebuild nếu đã cache radius
            if (_cachedRadius > 0f) BuildRing(_cachedRadius);
        }
    }
#endif
}
