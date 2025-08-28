using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class WorldSpaceHealthbarChild : MonoBehaviour
{
    [Header("Binding")]
    public Transform enemyRoot;          // nếu để trống, tự lấy transform.parent
    public Image fill;                   // ảnh fill (0..1)
    public float maxHP = 100f;

    [Header("Placement")]
    public Vector3 extraWorldOffset = new Vector3(0f, 0.2f, 0f);
    public float pushTowardCamera = 0.03f;     // đẩy 3cm về phía camera
    public bool keepConstantWorldSize = true;  // giữ kích thước thanh máu không méo theo scale enemy
    public Vector2 desiredWorldSize = new Vector2(1.2f, 0.15f); // chiều rộng × cao mong muốn (m đơn vị)

    Camera cam;
    Renderer[] rends;
    Collider[] cols;
    float _maxHP = 100f;

    void Reset()
    {
        enemyRoot = transform.parent;
    }

    void Awake()
    {
        cam = Camera.main;
        _maxHP = Mathf.Max(1f, maxHP);
    }

    void OnEnable()
    {
        CacheBoundsSources();
    }

    void CacheBoundsSources()
    {
        if (!enemyRoot) enemyRoot = transform.parent;
        if (!enemyRoot) return;
        rends = enemyRoot.GetComponentsInChildren<Renderer>();
        cols = enemyRoot.GetComponentsInChildren<Collider>();
    }

    public void SetMaxHP(float hp)
    {
        _maxHP = Mathf.Max(1f, hp);
    }

    public void SetHP(float hp)
    {
        if (!fill) return;
        float pct = Mathf.Clamp01(hp / Mathf.Max(1f, _maxHP));
        fill.fillAmount = pct;
        gameObject.SetActive(pct < 0.999f); // ẩn khi full (tuỳ chọn)
    }

    void LateUpdate()
    {
        if (!enemyRoot) enemyRoot = transform.parent;
        if (!enemyRoot) return;

        if (!cam) cam = Camera.main;
        if (!cam) return;

        // 1) Tính bounds tổng của enemy
        bool hasBounds = false;
        Bounds b = new Bounds(enemyRoot.position, Vector3.zero);

        if (rends != null && rends.Length > 0)
        {
            for (int i = 0; i < rends.Length; i++)
            {
                if (!rends[i] || !rends[i].enabled) continue;
                if (!hasBounds) { b = rends[i].bounds; hasBounds = true; }
                else b.Encapsulate(rends[i].bounds);
            }
        }
        if (!hasBounds && cols != null && cols.Length > 0)
        {
            for (int i = 0; i < cols.Length; i++)
            {
                if (!cols[i]) continue;
                if (!hasBounds) { b = cols[i].bounds; hasBounds = true; }
                else b.Encapsulate(cols[i].bounds);
            }
        }
        if (!hasBounds) { b = new Bounds(enemyRoot.position, Vector3.one * 1f); }

        // 2) Lấy điểm trên cùng của bounds + offset
        Vector3 top = new Vector3(b.center.x, b.max.y, b.center.z) + extraWorldOffset;

        // 3) Đặt vị trí & quay mặt về camera
        transform.position = top;
        transform.forward = cam.transform.forward;

        // 4) Đẩy nhẹ về phía camera (tránh z-fighting/che khuất viền)
        transform.position += cam.transform.forward * pushTowardCamera;

        // 5) Giữ kích thước ổn định dù enemy scale to/nhỏ
        if (keepConstantWorldSize)
        {
            Vector3 lossy = (enemyRoot != null) ? enemyRoot.lossyScale : Vector3.one;
            // scale ngược lại theo 3 trục để bù scale enemy
            Vector3 inv = new Vector3(
                Mathf.Approximately(lossy.x, 0f) ? 1f : 1f / lossy.x,
                Mathf.Approximately(lossy.y, 0f) ? 1f : 1f / lossy.y,
                Mathf.Approximately(lossy.z, 0f) ? 1f : 1f / lossy.z
            );
            // áp dụng kích thước mong muốn theo trục X-Y (UI world)
            // giả định HealthBarCanvas có RectTransform gốc 1×1 (hoặc bạn tự quy đổi)
            transform.localScale = new Vector3(inv.x * desiredWorldSize.x, inv.y * desiredWorldSize.y, inv.z);
        }
    }
}
