using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// EnemyBurrow: Cho phép một số enemy (vd: chuột chũi) chui xuống đất theo chu kỳ.
/// - Khi bắt đầu chui: hiện icon "!" trên đầu, tắt EnemyController (không tấn công).
/// - Tuỳ chọn: vô hiệu hoá va chạm hoặc chuyển sang layer "Underground" để tower không khóa mục tiêu.
/// - Hạ model xuống (dịch -Y) rồi trồi lên sau duration.
/// </summary>
[DisallowMultipleComponent]
public class EnemyBurrow : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Logic chiến đấu/di chuyển của enemy sẽ bị vô hiệu hoá khi đi ngầm.")]
    public EnemyController enemyController;
    [Tooltip("Agent để dừng lại khi đi ngầm (không bắt buộc).")]
    public NavMeshAgent agent;
    [Tooltip("Gốc của mesh/model để hạ xuống/nhô lên (nếu null sẽ dùng transform).")]
    public Transform modelRoot;
    [Tooltip("Điểm neo để spawn icon '!' (thường là một empty trên đầu). Nếu null, dùng transform.")]
    public Transform headAnchor;

    [Header("Burrow Settings")]
    [Tooltip("Bật/tắt hành vi đi ngầm.")]
    public bool canGoUnderground = true;
    [Tooltip("Bao lâu thì bắt đầu chui (khoảng cách giữa hai lần chui).")]
    public Vector2 intervalSeconds = new Vector2(5f, 8f);
    [Tooltip("Thời gian ở trạng thái ngầm (không bị tấn công/không tấn công).")]
    public float undergroundDuration = 2.5f;
    [Tooltip("Thời gian tween khi hạ xuống/nhô lên.")]
    public float burrowTweenTime = 0.35f;
    [Tooltip("Độ sâu hạ model khi chui ngầm (m).")]
    public float burrowDepth = 0.6f;

    [Header("Invulnerability While Underground")]
    [Tooltip("Khi ngầm thì tắt toàn bộ Collider để tower không hit được.")]
    public bool disableCollidersWhileUnderground = true;
    [Tooltip("Hoặc đổi sang layer này khi ngầm (để tower không target). Để rỗng nếu không dùng.")]
    public string undergroundLayerName = "Underground";

    [Header("FX / UI")]
    [Tooltip("Prefab icon dấu chấm than hiện khi bắt đầu chui (tuỳ chọn).")]
    public GameObject exclamationPrefab;
    [Tooltip("Khoảng cao hơn đầu để đặt icon '!'.")]
    public float exclamationYOffset = 0.6f;
    [Tooltip("Ẩn icon sau bấy nhiêu giây kể từ khi hiện.")]
    public float exclamationLifetime = 1.2f;

    [Header("Animator (optional)")]
    [Tooltip("Animator ở child để bắn trigger khi chui/nhô (nếu có).")]
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
    }

    void OnEnable()
    {
        if (canGoUnderground && _loop == null)
        {
            _loop = StartCoroutine(BurrowLoop());
        }
    }

    void OnDisable()
    {
        if (_loop != null) { StopCoroutine(_loop); _loop = null; }
        // đảm bảo reset trạng thái nếu bị disable giữa chừng
        if (_isUnderground)
        {
            _isUnderground = false;
            RestoreCollisionAndLayer();
            RestoreController();
            TweenModelTo(_modelStartLocalPos, 0f);
        }
    }

    IEnumerator BurrowLoop()
    {
        // chờ một nhịp nhỏ để tránh vừa spawn đã chui
        yield return new WaitForSeconds(Random.Range(0.5f, 1.25f));

        while (canGoUnderground && enemyController && !enemyController.IsDead)
        {
            // đợi tới lượt
            float wait = Random.Range(intervalSeconds.x, intervalSeconds.y);
            yield return new WaitForSeconds(wait);

            if (!isActiveAndEnabled || enemyController.IsDead) yield break;

            // BẮT ĐẦU CHUI
            ShowExclamation();
            PlayAnimTrigger(burrowDownTrigger);

            // dừng agent
            if (agent) { agent.isStopped = true; agent.ResetPath(); }

            // tắt điều khiển (không tấn công)
            //if (enemyController) enemyController.enabled = false;

            // vô hiệu hoá va chạm / đổi layer
            ApplyUndergroundCollisionAndLayer();

            // hạ model
            yield return TweenYOffset(-burrowDepth, burrowTweenTime);

            // ở trạng thái ngầm
            _isUnderground = true;
            yield return new WaitForSeconds(undergroundDuration);

            // NGÓI LÊN
            PlayAnimTrigger(burrowUpTrigger);

            // nâng model về vị trí cũ
            yield return TweenYOffset(0f, burrowTweenTime);

            // khôi phục va chạm/layer + bật lại controller + agent
            RestoreCollisionAndLayer();
            RestoreController();

            _isUnderground = false;

            if (agent && agent.enabled) agent.isStopped = false;
        }
    }

    void ShowExclamation()
    {
        if (!exclamationPrefab) return;
        var pos = (headAnchor ? headAnchor.position : transform.position) + Vector3.up * exclamationYOffset;
        var go = Instantiate(exclamationPrefab, pos, Quaternion.identity, headAnchor);
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
        // layer
        if (!string.IsNullOrEmpty(undergroundLayerName))
        {
            int layer = LayerMask.NameToLayer(undergroundLayerName);
            if (layer >= 0) SetLayerRecursively(transform, layer);
        }

        // colliders
        if (disableCollidersWhileUnderground && _cols != null)
        {
            for (int i = 0; i < _cols.Length; i++) _cols[i].enabled = false;
        }
    }

    void RestoreCollisionAndLayer()
    {
        // layer
        if (!string.IsNullOrEmpty(undergroundLayerName) && _origLayer >= 0)
        {
            SetLayerRecursively(transform, _origLayer);
        }

        // colliders
        if (disableCollidersWhileUnderground && _cols != null)
        {
            for (int i = 0; i < _cols.Length; i++) _cols[i].enabled = true;
        }
    }

    void RestoreController()
    {
        if (enemyController && !enemyController.IsDead)
        {
            enemyController.enabled = true;
        }
    }

    static void SetLayerRecursively(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i), layer);
    }
}
