using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[DisallowMultipleComponent]
public class EnemyBurrow : MonoBehaviour
{
    [Header("References")]
    public EnemyController enemyController;
    public NavMeshAgent agent;
    public Transform modelRoot;
    public Transform headAnchor;

    [Tooltip("GameObject tượng trưng cục đất hiện khi enemy chui xuống.")]
    public GameObject Dirt;

    [Header("Burrow Settings")]
    public bool canGoUnderground = true;
    public Vector2 intervalSeconds = new Vector2(5f, 8f);
    public float undergroundDuration = 2.5f;
    public float burrowTweenTime = 0.35f;
    public float burrowDepth = 0.6f;

    [Header("Invulnerability While Underground")]
    public bool disableCollidersWhileUnderground = true;
    public string undergroundLayerName = "Underground";

    [Header("FX / UI")]
    public GameObject exclamationPrefab;
    public float exclamationYOffset = 0.6f;
    public float exclamationLifetime = 1.2f;

    [Min(0.01f)] public float exclamationScale = 1f;
    public bool exclamationFixedWorldScale = true;

    // 👉 NEW: cấu hình Dirt
    [Header("Dirt (Ground FX)")]
    [Tooltip("Đặt Dirt theo vị trí headAnchor + offset Y (nếu tắt thì giữ nguyên vị trí/parent hiện tại).")]
    public bool dirtUseHeadAnchorPosition = true;
    [Tooltip("Độ lệch theo trục Y tính từ headAnchor khi hiện Dirt.")]
    public float dirtYOffset = 0.05f;

    [Header("Animator (optional)")]
    public Animator childAnimator;
    public string burrowDownTrigger = "BurrowDown";
    public string burrowUpTrigger = "BurrowUp";

    // runtime state
    int _origLayer = -1;
    Collider[] _cols;
    Vector3 _modelStartLocalPos;
    bool _isUnderground;
    Coroutine _loop;

    void Reset()
    {
        enemyController = GetComponent<EnemyController>();
        agent = GetComponent<NavMeshAgent>();
        childAnimator = GetComponentInChildren<Animator>(true);
    }

    void Awake()
    {
        if (!modelRoot) modelRoot = transform;
        _modelStartLocalPos = modelRoot.localPosition;
        _cols = GetComponentsInChildren<Collider>(true);
        if (string.IsNullOrEmpty(undergroundLayerName)) _origLayer = -1;
        else _origLayer = gameObject.layer;
        if (!headAnchor) headAnchor = transform;

        // 👉 đảm bảo Dirt ẩn lúc start
        if (Dirt) Dirt.SetActive(false);
    }

    void OnEnable()
    {
        if (canGoUnderground && _loop == null)
            _loop = StartCoroutine(BurrowLoop());
    }

    void OnDisable()
    {
        if (_loop != null) { StopCoroutine(_loop); _loop = null; }
        if (_isUnderground)
        {
            _isUnderground = false;
            RestoreCollisionAndLayer();
            RestoreController();
            TweenModelTo(_modelStartLocalPos, 0f);
        }
        // 👉 ẩn Dirt khi bị tắt giữa chừng
        HideDirt();
    }

    IEnumerator BurrowLoop()
    {
        yield return new WaitForSeconds(Random.Range(0.5f, 1.25f));

        while (canGoUnderground && enemyController && !enemyController.IsDead)
        {
            float wait = Random.Range(intervalSeconds.x, intervalSeconds.y);
            yield return new WaitForSeconds(wait);
            if (!isActiveAndEnabled || enemyController.IsDead) yield break;

            // BẮT ĐẦU CHUI
            ShowExclamation();
            PlayAnimTrigger(burrowDownTrigger);

            if (agent) { agent.isStopped = true; agent.ResetPath(); }

            // 👉 hiện Dirt ngay khi bắt đầu chìm
            ShowDirt();

            ApplyUndergroundCollisionAndLayer();
            yield return TweenYOffset(-burrowDepth, burrowTweenTime);

            _isUnderground = true;
            yield return new WaitForSeconds(undergroundDuration);

            // NGÓI LÊN
            PlayAnimTrigger(burrowUpTrigger);
            yield return TweenYOffset(0f, burrowTweenTime);

            RestoreCollisionAndLayer();
            RestoreController();
            _isUnderground = false;

            if (agent && agent.enabled) agent.isStopped = false;

            // 👉 ẩn Dirt khi đã trồi lên
            HideDirt();
        }
    }

    void ShowExclamation()
    {
        if (!exclamationPrefab) return;

        var pos = (headAnchor ? headAnchor.position : transform.position) + Vector3.up * exclamationYOffset;

        var go = Instantiate(exclamationPrefab, pos, Quaternion.identity);
        var baseScale = go.transform.localScale;

        if (exclamationFixedWorldScale)
        {
            go.transform.localScale = baseScale * exclamationScale;
            go.transform.SetParent(headAnchor, true);
        }
        else
        {
            go.transform.SetParent(headAnchor, false);
            go.transform.localScale = baseScale * exclamationScale;
        }

        if (exclamationLifetime > 0f) Destroy(go, exclamationLifetime);
    }

    void PlayAnimTrigger(string triggerName)
    {
        if (childAnimator && !string.IsNullOrEmpty(triggerName))
        {
            childAnimator.ResetTrigger(triggerName);
            childAnimator.SetTrigger(triggerName);
        }
    }

    IEnumerator TweenYOffset(float targetDeltaY, float time)
    {
        if (time <= 0f)
        {
            var p = _modelStartLocalPos; p.y += targetDeltaY;
            modelRoot.localPosition = p;
            yield break;
        }

        Vector3 from = modelRoot.localPosition;
        Vector3 to = _modelStartLocalPos; to.y += targetDeltaY;

        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / time));
            modelRoot.localPosition = Vector3.LerpUnclamped(from, to, k);
            yield return null;
        }
        modelRoot.localPosition = to;
    }

    void TweenModelTo(Vector3 localPos, float time)
    {
        if (!isActiveAndEnabled) { modelRoot.localPosition = localPos; return; }
        StartCoroutine(_TweenModelTo(localPos, time));
    }

    IEnumerator _TweenModelTo(Vector3 localPos, float time)
    {
        Vector3 from = modelRoot.localPosition;
        if (time <= 0f) { modelRoot.localPosition = localPos; yield break; }

        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / time));
            modelRoot.localPosition = Vector3.LerpUnclamped(from, localPos, k);
            yield return null;
        }
        modelRoot.localPosition = localPos;
    }

    void ApplyUndergroundCollisionAndLayer()
    {
        if (!string.IsNullOrEmpty(undergroundLayerName))
        {
            int layer = LayerMask.NameToLayer(undergroundLayerName);
            if (layer >= 0) SetLayerRecursively(transform, layer);
        }

        if (disableCollidersWhileUnderground && _cols != null)
        {
            for (int i = 0; i < _cols.Length; i++) _cols[i].enabled = false;
        }
    }

    void RestoreCollisionAndLayer()
    {
        if (!string.IsNullOrEmpty(undergroundLayerName) && _origLayer >= 0)
        {
            SetLayerRecursively(transform, _origLayer);
        }

        if (disableCollidersWhileUnderground && _cols != null)
        {
            for (int i = 0; i < _cols.Length; i++) _cols[i].enabled = true;
        }
    }

    void RestoreController()
    {
        if (enemyController && !enemyController.IsDead)
            enemyController.enabled = true;
    }

    static void SetLayerRecursively(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i), layer);
    }

    // ===== Dirt helpers =====
    void ShowDirt()
    {
        if (!Dirt) return;

        if (dirtUseHeadAnchorPosition && headAnchor)
        {
            // đặt đúng world pos theo headAnchor và offset
            Vector3 pos = headAnchor.position + Vector3.up * dirtYOffset;
            Dirt.transform.position = pos;
        }

        Dirt.SetActive(true);
    }

    void HideDirt()
    {
        if (Dirt) Dirt.SetActive(false);
    }
}
