using UnityEngine;
using System.Collections.Generic;

public class DamageTextPool : MonoBehaviour
{
    [Header("Pool Config (per enemy)")]
    public DamageTextItem itemPrefab;
    [Min(1)] public int initial = 10;

    [Header("Refs on enemy")]
    public Canvas enemyCanvas;
    public Transform damageAnchor;
    public Camera uiCamera;

    [Header("Stacking")]
    public bool enableStacking = true;
    [Range(0.05f, 1.0f)] public float stackSpacing = 0.25f;   // khoảng cách giữa các dòng (m)
    [Range(1f, 30f)] public float stackLerpSpeed = 12f;       // dùng cho chuyển động mượt sau này
    [Min(1)] public int maxStacked = 5;

    private readonly Queue<DamageTextItem> _pool = new();
    private readonly List<DamageTextItem> _active = new();
    private readonly Dictionary<DamageTextItem, float> _currOffset = new();

    void Reset() { enemyCanvas = GetComponent<Canvas>(); }

    void Awake()
    {
        if (!enemyCanvas) enemyCanvas = GetComponent<Canvas>();
        if (!uiCamera) uiCamera = Camera.main;
        for (int i = 0; i < Mathf.Max(1, initial); i++) _pool.Enqueue(Create());
    }

    // Giữ lại Lerp nếu bạn thích animation dịch chậm; có thể tắt nếu đã force-immediate
    void LateUpdate()
    {
        if (!enableStacking || _active.Count == 0) return;

        float dt = Time.unscaledDeltaTime;
        int limit = Mathf.Min(_active.Count, maxStacked);

        for (int i = 0; i < _active.Count; i++)
        {
            var it = _active[i];
            if (!it || !it.gameObject.activeSelf) continue;

            float target = (i < limit) ? (i * stackSpacing) : ((limit - 1) * stackSpacing);

            // nếu không có khóa, thiết lập nhanh
            if (!_currOffset.TryGetValue(it, out float cur)) cur = target;

            // Lerp mượt dần (có thể thay bằng gán thẳng nếu muốn “snap”)
            cur = Mathf.Lerp(cur, target, dt * stackLerpSpeed);
            _currOffset[it] = cur;
            it.SetExternalYOffset(cur);
        }
    }

    private DamageTextItem Create()
    {
        var it = Instantiate(itemPrefab, enemyCanvas ? enemyCanvas.transform : transform);
        it.gameObject.SetActive(false);
        return it;
    }

    private DamageTextItem Get()
    {
        if (_pool.Count == 0) _pool.Enqueue(Create());
        return _pool.Dequeue();
    }

    private bool OnFinishedAndRecycle(DamageTextItem it)
    {
        _active.Remove(it);
        _currOffset.Remove(it);
        it.SetExternalYOffset(0f);
        it.gameObject.SetActive(false);
        it.transform.SetParent(enemyCanvas ? enemyCanvas.transform : transform, false);
        _pool.Enqueue(it);
        return true;
    }

    /// <summary>Spawn damage text, “đẩy” các dòng cũ lên ngay lập tức để không chồng chữ.</summary>
    public void Spawn(float amount, Color color, float extraYOffset = 0.1f)
    {
        var it = Get();

        Vector3 spawnPos = damageAnchor ? damageAnchor.position : transform.position;
        spawnPos.y += extraYOffset;

        it.transform.SetParent(enemyCanvas ? enemyCanvas.transform : transform, false);

        // ✅ chữ mới nằm DƯỚI, không che chữ cũ
        it.transform.SetAsFirstSibling();

        _active.Insert(0, it);
        _currOffset[it] = 0f;

        ForceStackLayoutImmediate();

        it.Play(
            damageAnchor,
            spawnPos,
            Mathf.RoundToInt(amount).ToString(),
            color,
            lifetime: 0.8f,
            startAlpha: 1f,
            riseWorldPerSec: 0.6f,
            billboardToCamera: true,
            onFinished: OnFinishedAndRecycle,
            cam: uiCamera
        );
    }


    /// <summary>Đặt offset cho toàn bộ _active theo thứ tự, tức thì, tránh chồng chữ frame đầu.</summary>
    private void ForceStackLayoutImmediate()
    {
        if (!enableStacking) return;

        int limit = Mathf.Min(_active.Count, maxStacked);
        for (int i = 0; i < _active.Count; i++)
        {
            var it = _active[i];
            if (!it || !it.gameObject.activeSelf) continue;

            float target = (i < limit) ? (i * stackSpacing) : ((limit - 1) * stackSpacing);
            _currOffset[it] = target;
            it.SetExternalYOffset(target);
        }
    }
}
