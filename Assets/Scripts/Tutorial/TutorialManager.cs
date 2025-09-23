using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Linq;
using System;

public class TutorialManager : MonoBehaviour
{
    // Thêm vào class TutorialManager
    Coroutine _activeStepRoutine;
    int _stepVersion = 0; // token chống đua

    [Header("Scene Loading Gate")]
    [Tooltip("Nếu bật, Tutorial sẽ KHÔNG chạy step mới khi scene đang loading.")]
    [SerializeField] bool blockDuringSceneLoading = true;

    [Tooltip("Đợi thêm (giây, unscaled) sau khi scene xong load trước khi bind step.")]
    [SerializeField] float postSceneReadyDelay = 0.1f;

    // Trạng thái loading do loader của bạn set:
    bool _sceneLoading = false;
    [Header("Default Arrow Prefab (fallback)")]
    [Tooltip("Prefab mũi tên mặc định nếu step.arrowPrefab để trống. Có thể là UI hoặc 3D tùy step.")]
    public GameObject arrowPrefab;

    [Header("World Target Raycast")]
    public Camera worldCamera;
    public LayerMask towerTapMask = ~0;

    GameObject _arrowInstance;
    Transform _worldTarget;
    float _prevTimeScale = 1f;

    [Header("Data")]
    public TutorialSequenceSO sequence;

    [Header("UI")]
    public Canvas tutorialCanvas;
    public TutorialOverlay overlay;
    public TutorialChatBubble chatBubblePrefab;

    [Header("Options")]
    public bool startOnAwake = true;
    public bool autoDim = true;

    private int _index = -1;
    private TutorialChatBubble _currentBubble;
    private RectTransform _currentTarget;
    private Button _hookedButton;
    private TutorialVisualLift.LiftHandle _liftHandle;
    private bool _lifted;

    void Awake()
    {
        if (tutorialCanvas == null) tutorialCanvas = GetComponentInChildren<Canvas>(true);
        if (overlay == null) overlay = GetComponentInChildren<TutorialOverlay>(true);
    }

    void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    void Start()
    {
        if (startOnAwake && sequence != null && sequence.steps != null && sequence.steps.Length > 0)
            StartTutorial();
    }

    public void StartTutorial()
    {
        _index = -1;
        NextStep();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_index >= 0 && _index < sequence.steps.Length)
        {
            StartCoroutine(RebindVisualsWhenReady(sequence.steps[_index])); // ❗ không gọi ApplyStep
        }
    }
    IEnumerator RebindVisualsWhenReady(TutorialStepSO step)
    {
        yield return WaitWhileSceneLoading();

        // Nếu step world → chỉ tìm lại target và bật/tắt arrow theo state hiện tại
        if (step.pointArrowOnWorldTarget)
        {
            _worldTarget = null;
            if (!string.IsNullOrEmpty(step.targetTag))
            {
                var goByTag = GameObject.FindGameObjectWithTag(step.targetTag);
                if (goByTag) _worldTarget = goByTag.transform;
            }
            if (_worldTarget == null && !string.IsNullOrEmpty(step.targetName))
            {
                var goByName = GameObject.Find(step.targetName);
                if (goByName) _worldTarget = goByName.transform;
            }
            // Không spawn/không chờ — chỉ đảm bảo arrow có sẵn vẫn bật nếu đang ở step này
            // (nếu bạn muốn tự bật lại: ActivateWorldArrow(_worldTarget, step);)
            yield break;
        }

        // Step UI → resolve lại RectTransform + vẽ lại overlay/bubble
        _currentTarget = null;
        yield return TutorialTargetResolver.ResolveTargetRectCoroutine(tutorialCanvas, step, rt => _currentTarget = rt);

        if (overlay != null)
        {
            overlay.ClearWhitelist();
            overlay.SetDim(step.showFocus || step.mode == StepMode.ForceTarget); // ❗ đảm bảo overlay lại bật đúng
            if (_currentTarget != null && step.mode != StepMode.TapAnywhere)
                overlay.AddWhitelist(_currentTarget);
        }

        // Vẽ lại chat neo đúng target (không thay đổi index/flow)
        ShowChat(step, _currentTarget);
    }

    IEnumerator RebindCurrentStepWhenReady()
    {
        yield return WaitWhileSceneLoading();
        yield return ApplyStep(sequence.steps[_index], rebindOnly: true);
    }

    public void NextStep()
    {
        CleanupStep();
        _index++;
        if (sequence == null || _index >= sequence.steps.Length) { EndTutorial(); return; }

        // Hủy step cũ nếu còn
        if (_activeStepRoutine != null) StopCoroutine(_activeStepRoutine);
        _activeStepRoutine = StartCoroutine(RunStepWhenReady(sequence.steps[_index], ++_stepVersion));
    }

    IEnumerator RunStepWhenReady(TutorialStepSO step, int versionToken)
    {
        yield return WaitWhileSceneLoading();
        // Step cũ bị vượt? thoát
        if (versionToken != _stepVersion) yield break;
        yield return ApplyStep(step);
    }

    public void EndTutorial()
    {
        if (_activeStepRoutine != null) { StopCoroutine(_activeStepRoutine); _activeStepRoutine = null; }
        CleanupStep();
        if (overlay) { overlay.ClearWhitelist(); overlay.SetDim(false); overlay.SetBlockerOnly(false); }
        if (_currentBubble) Destroy(_currentBubble.gameObject);
        _index = -1;
    }

    void CleanupStep()
    {
        if (_hookedButton != null)
        {
            _hookedButton.onClick.RemoveListener(OnRequiredButtonClicked);
            _hookedButton = null;
        }
        if (overlay) overlay.ClearWhitelist();
        if (_currentBubble) { Destroy(_currentBubble.gameObject); _currentBubble = null; }
        _currentTarget = null;

        if (_lifted)
        {
            TutorialVisualLift.Restore(_liftHandle);
            _lifted = false;
        }

        DetachArrow();
        Time.timeScale = _prevTimeScale;
    }

    IEnumerator ApplyStep(TutorialStepSO step, bool rebindOnly = false)
    {
        // 🔓 luôn bỏ blocker cũ (nếu còn) trước khi vẽ step mới
        if (overlay != null) { overlay.SetBlockerOnly(false); overlay.ClearWhitelist(); }

        if (step.timeCountdown > 0f)
            yield return DoCountdown(step.timeCountdown);
        // ==== WORLD TARGET MODE ====
        if (step.pointArrowOnWorldTarget)
        {
            if (overlay != null) { overlay.ClearWhitelist(); overlay.SetDim(false); overlay.SetBlockerOnly(false); }

            _worldTarget = null;
            if (!string.IsNullOrEmpty(step.targetTag))
            {
                var goByTag = GameObject.FindGameObjectWithTag(step.targetTag);
                if (goByTag) _worldTarget = goByTag.transform;
            }
            if (_worldTarget == null && !string.IsNullOrEmpty(step.targetName))
            {
                var goByName = GameObject.Find(step.targetName);
                if (goByName) _worldTarget = goByName.transform;
            }

            if (_worldTarget == null)
            {
                Debug.LogWarning($"[Tutorial] Không tìm thấy world target cho step '{step.stepId}'.");
                yield return WaitForTapAnywhere();
                NextStep();
                yield break;
            }

            // GẮN MŨI TÊN 3D → ArrowAnchor (child rỗng)
            ActivateWorldArrow(_worldTarget, step);

            // Freeze nếu cần
            _prevTimeScale = Time.timeScale;
            if (step.freezeTime) Time.timeScale = 0f;

            // Chờ người chơi bấm đúng object (raycast 3D)
            yield return WaitForTapOnWorldTarget(_worldTarget);

            // Cleanup + Next
            DeactivateWorldArrow();
            Time.timeScale = _prevTimeScale;
            NextStep();
            yield break;
        }

        // ==== UI MODE ====
        if (autoDim && overlay != null)
            overlay.SetDim(step.showFocus || step.mode == StepMode.ForceTarget);

        if (step.mode == StepMode.TapAnywhere)
        {
            _currentTarget = null;
            if (step.showFocus && overlay != null) overlay.ClearWhitelist();
            ShowChat(step, null);
            yield return WaitForTapAnywhere();
            NextStep();
            yield break;
        }

        // ForceTarget: resolve UI target
        _currentTarget = null;
        yield return TutorialTargetResolver.ResolveTargetRectCoroutine(tutorialCanvas, step, rt => _currentTarget = rt);

        if (_currentTarget == null)
        {
            Debug.LogWarning($"[Tutorial] Step '{step.stepId}' không tìm thấy target UI. Cho phép tap-anywhere để tránh kẹt.");
            ShowChat(step, null);
            yield return WaitForTapAnywhere();
            NextStep();
            yield break;
        }

        // Whitelist vùng target
        if (overlay != null)
        {
            overlay.ClearWhitelist();
            overlay.AddWhitelist(_currentTarget);
        }

        // Nếu có yêu cầu click Button con
        Button btn = null;
        if (step.requireButtonClick)
        {
            btn = string.IsNullOrEmpty(step.requiredButtonChildName)
                ? _currentTarget.GetComponent<Button>()
                : TutorialTargetResolver.FindChildButtonByName(_currentTarget, step.requiredButtonChildName);

            if (btn == null)
            {
                ShowChat(step, _currentTarget);
                // UI arrow vẫn có thể hiển nếu step.showPointerArrow bật
                if (step.showPointerArrow) AttachUIArrowToTarget(_currentTarget, step);
                yield return WaitForTapOnTarget(_currentTarget);
                NextStep();
                yield break;
            }

            _hookedButton = btn;
            // Gỡ trước để tránh add trùng nếu step từng rebind
            _hookedButton.onClick.RemoveListener(OnRequiredButtonClicked);
            _hookedButton.onClick.AddListener(OnRequiredButtonClicked);
            
        }

        // NÂNG target lên trên overlay (nếu có)
        if (tutorialCanvas != null && _currentTarget != null)
        {
            int above = tutorialCanvas.sortingOrder + 1;
            _liftHandle = TutorialVisualLift.LiftTarget(_currentTarget, above);
            _lifted = true;
        }

        // GẮN MŨI TÊN UI nếu bật
        if (step.showPointerArrow)
            AttachUIArrowToTarget(_currentTarget, step);

        // Chat (neo theo target)
        ShowChat(step, _currentTarget);

        // Nếu không bắt buộc onClick → tap vào vùng target là đủ
        if (!step.requireButtonClick)
        {
            yield return WaitForTapOnTarget(_currentTarget);
            NextStep();
        }
        // Nếu có requireButtonClick → chờ OnRequiredButtonClicked
    }

    void ShowChat(TutorialStepSO step, RectTransform anchor)
    {
        if (chatBubblePrefab == null || tutorialCanvas == null) return;

        if (string.IsNullOrWhiteSpace(step.chatText))
        {
            if (_currentBubble) Destroy(_currentBubble.gameObject);
            _currentBubble = null;
            return;
        }

        if (_currentBubble) Destroy(_currentBubble.gameObject);
        _currentBubble = Instantiate(chatBubblePrefab, tutorialCanvas.transform);

        var follow = anchor != null ? anchor : (tutorialCanvas.transform as RectTransform);
        _currentBubble.Setup(
            tutorialCanvas,
            follow,
            step.chatText,
            step.speakerAvatar,
            step.chatBubbleOffset
        );
    }

    void OnRequiredButtonClicked()
    {
        if (_hookedButton != null)
            _hookedButton.onClick.RemoveListener(OnRequiredButtonClicked);
        _hookedButton = null;
        NextStep();
    }

    IEnumerator WaitForTapAnywhere()
    {
        bool tapped = false;
        while (!tapped)
        {
            if (Input.GetMouseButtonDown(0) || Input.touchCount > 0)
                tapped = true;
            yield return null;
        }
    }

    IEnumerator WaitForTapOnTarget(RectTransform target)
    {
        if (target == null) yield break;
        bool tapped = false;
        var cam = tutorialCanvas.worldCamera;

        while (!tapped)
        {
            if (Input.GetMouseButtonDown(0) || Input.touchCount > 0)
            {
                Vector2 sp = Input.touchCount > 0 ? (Vector2)Input.touches[0].position : (Vector2)Input.mousePosition;
                if (RectTransformUtility.RectangleContainsScreenPoint(target, sp, cam))
                    tapped = true;
            }
            yield return null;
        }
    }

    IEnumerator WaitForTapOnWorldTarget(Transform target)
    {
        var cam = worldCamera ? worldCamera : Camera.main;
        if (!cam || !target) yield break;

        bool ok = false;
        while (!ok)
        {
            if (Input.GetMouseButtonDown(0) || Input.touchCount > 0)
            {
                Vector2 sp = Input.touchCount > 0 ? (Vector2)Input.touches[0].position : (Vector2)Input.mousePosition;
                var ray = cam.ScreenPointToRay(sp);
                if (Physics.Raycast(ray, out var hit, 1000f, towerTapMask))
                {
                    if (hit.transform == target || hit.transform.IsChildOf(target))
                        ok = true;
                }
            }
            yield return null; // unscaled loop vẫn chạy khi timeScale=0
        }
    }

    // =========================
    //       ARROW HELPERS
    // =========================

    // Tạo hoặc lấy ArrowAnchor (child rỗng) cho World target
    Transform CreateOrGetWorldArrowAnchor(Transform tower, TutorialStepSO step)
    {
        if (tower == null) return null;

        // 1) Nếu step chỉ rõ path → thử tìm
        if (!string.IsNullOrEmpty(step.arrowAnchorPath))
        {
            var exist = tower.Find(step.arrowAnchorPath);
            if (exist) return exist;
        }

        // 2) Tìm child tên "ArrowAnchor"
        var existByName = tower.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name.Equals("ArrowAnchor", StringComparison.OrdinalIgnoreCase));
        if (existByName) return existByName;

        // 3) Không có → tạo mới child rỗng
        var anchor = new GameObject("ArrowAnchor").transform;
        anchor.SetParent(tower, worldPositionStays: false);
        anchor.localPosition = Vector3.zero;      // cơ sở đặt ở gốc tower
        anchor.localRotation = Quaternion.identity;
        anchor.localScale = Vector3.one;

        return anchor;
    }

    // Gắn mũi tên 3D vào ArrowAnchor (World)
    void AttachWorldArrow(Transform tower, TutorialStepSO step)
    {
        var prefab = step.arrowPrefab != null ? step.arrowPrefab : arrowPrefab;
        if (!prefab || !tower) return;

        var anchor = CreateOrGetWorldArrowAnchor(tower, step);
        if (!anchor) { Debug.LogWarning("[Tutorial] Không có ArrowAnchor."); return; }

        // Parent: bob theo Y + scale
        var carrier = new GameObject("ArrowCarrier").transform;
        carrier.SetParent(anchor, false);
        carrier.localPosition = step.arrowLocalOffset;
        carrier.localRotation = Quaternion.identity;
        carrier.localScale = new Vector3(60f, 60f, 1f); // ✅ scale X/Y = 60

        // Child: chỉ quay quanh Y
        var visual = Instantiate(prefab, carrier, false).transform;
        visual.localPosition = Vector3.zero;
        // Carrier giữ identity
        carrier.localRotation = Quaternion.identity;

        // Visual xoay đứng dọc và chỉ xuống đất
        visual.localRotation = Quaternion.Euler(0f, 0f, 90f);
        visual.localScale = Vector3.one;

        var bb = visual.GetComponent<ArrowBillboard>();
        if (bb) bb.faceCamera = worldCamera ? worldCamera : Camera.main;

        // ✅ Bob theo Y (carrier) + Spin quanh Y (visual)
        var bob = carrier.gameObject.GetComponent<ArrowBobbing>() ?? carrier.gameObject.AddComponent<ArrowBobbing>();
        bob.bobTarget = carrier;  // nhún local Y của carrier
        bob.spinTarget = visual;   // quay Y trên visual
        bob.spinDegreesPerSecond = 90f;      // tùy chỉnh nếu cần
        bob.bobAmplitude = 0.25f;    // tùy chỉnh nếu cần
        bob.bobFrequency = 1.5f;     // tùy chỉnh nếu cần

        _arrowInstance = carrier.gameObject;   // để CleanupStep() destroy
    }


    // Gắn mũi tên UI vào target RectTransform (UI)
    void AttachUIArrowToTarget(RectTransform target, TutorialStepSO step)
    {
        if (target == null) return;

        var prefab = step.arrowPrefab != null ? step.arrowPrefab : arrowPrefab;
        if (!prefab)
        {
            Debug.LogWarning("[Tutorial] UI step bật showPointerArrow nhưng không có arrowPrefab.");
            return;
        }

        // Prefab phải là UI (có RectTransform)
        var rtPrefab = prefab.GetComponent<RectTransform>();
        if (rtPrefab == null)
        {
            Debug.LogWarning("[Tutorial] arrowPrefab không phải UI (RectTransform). Bỏ qua mũi tên UI.");
            return;
        }

        // Gắn dưới chính target để đi theo layout/animation của target
        _arrowInstance = Instantiate(prefab, target);
        var arrowRT = _arrowInstance.GetComponent<RectTransform>();

        // Căn giữa target + offset
        arrowRT.anchorMin = arrowRT.anchorMax = new Vector2(0.5f, 0.5f);
        arrowRT.anchoredPosition = step.arrowUIOffset;
        arrowRT.localRotation = Quaternion.identity;
        arrowRT.localScale = Vector3.one;

        // Bảo đảm có bobbing
        if (_arrowInstance.GetComponent<ArrowBobbing>() == null)
            _arrowInstance.AddComponent<ArrowBobbing>();
    }

    void DetachArrow()
    {
        if (_arrowInstance) Destroy(_arrowInstance);
        _arrowInstance = null;
    }
    // TÌM anchor “có sẵn”, KHÔNG tạo mới
    Transform FindWorldArrowAnchorExisting(Transform tower, TutorialStepSO step)
    {
        if (!tower) return null;

        // Ưu tiên path chỉ định (vd: "ArrowAnchor")
        if (!string.IsNullOrEmpty(step.arrowAnchorPath))
        {
            var p = tower.Find(step.arrowAnchorPath);
            if (p) return p;
        }

        // Tìm child tên "ArrowAnchor" (không phân biệt hoa/thường)
        var byName = tower.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name.Equals("ArrowAnchor", StringComparison.OrdinalIgnoreCase));
        if (byName) return byName;

        // Không tự tạo nữa → trả null để bỏ qua
        return null;
    }

    // BẬT mũi tên có sẵn bên dưới anchor
    void ActivateWorldArrow(Transform tower, TutorialStepSO step)
    {
        if (!tower) return;

        // tìm ArrowAnchor
        var anchor = tower.Find(step.arrowAnchorPath);
        if (!anchor)
        {
            anchor = tower.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name.Equals("ArrowAnchor", StringComparison.OrdinalIgnoreCase));
        }

        if (anchor)
        {
            anchor.gameObject.SetActive(true);   // ✅ bật anchor (mũi tên con sẽ hiện theo)
            _arrowInstance = anchor.gameObject;  // lưu lại để tắt khi cleanup
        }
    }


    // TẮT mũi tên khi kết thúc step
    void DeactivateWorldArrow()
    {
        if (_arrowInstance)
        {
            _arrowInstance.SetActive(false); // ✅ chỉ tắt, không xóa
            _arrowInstance = null;
        }
    }
    private IEnumerator DoCountdown(float seconds)
    {
        // Bật overlay dạng blocker-only: không dim, không whitelist, chặn toàn bộ raycast
        if (overlay != null)
        {
            overlay.ClearWhitelist();
            overlay.SetDim(false);
            overlay.SetBlockerOnly(true); // overlay là ảnh trong suốt nhưng chặn click
        }

        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;   // chạy cả khi timeScale=0
            yield return null;
        }

        // Thả blocker; trạng thái overlay cho step kế tiếp sẽ được set lại bởi ApplyStep
        if (overlay != null)
        {
            overlay.SetBlockerOnly(false);
            overlay.ClearWhitelist();
        }
    }
    public void SetSceneLoading(bool isLoading)
    {
        _sceneLoading = isLoading;
    }

    IEnumerator WaitWhileSceneLoading()
    {
        while (blockDuringSceneLoading && LoaderBridge.IsLoading)
            yield return null;

        float t = 0f;
        while (t < postSceneReadyDelay) { t += Time.unscaledDeltaTime; yield return null; }
        // KHÔNG set overlay ở đây. Overlay sẽ do ApplyStep/Rebind quyết định.
    }
}
