using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Linq;
using System;
using System.Collections.Generic;

public class TutorialManager : MonoBehaviour
{
    [Header("World click shield")]
    [SerializeField] bool useWorldClickShield = true;          // bật/tắt tính năng
    [SerializeField] float shieldDepthFromNear = 0.05f;         // cách near clip một chút
    [SerializeField] string shieldLayerName = "Default";        // layer của tấm chắn (phải nằm trong mask raycast của bạn)

    GameObject _worldClickShield;
    public bool IsWorldInputBlocked => _worldClickShield && _worldClickShield.activeSelf;
    const string DBG = "[TutorialDBG] ";
    private readonly Dictionary<string, int> _signalConsumed = new();
    private int _defaultSignalConsumed = 0;
    private readonly Dictionary<string, int> _lastEmitFrame = new(); // key -> Time.frameCount
    [SerializeField] float minEmitInterval = 0f; // nếu muốn theo thời gian: set > 0 (giây)
    private readonly Dictionary<string, float> _lastEmitTime = new();
    private readonly System.Collections.Generic.Dictionary<string, int> _signalCounts = new();
    private int _defaultSignalCount = 0;
    bool _defaultSignalReady = false;
    bool _isEnding = false;
    public static TutorialManager Instance { get; private set; }

    private RectTransform _customBubbleAnchor;  // anchor ảo đặt đúng vị trí custom cho bubble
    private RectTransform skipButtonRectTransform;
    public Button skipAllBtn;
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
    [Header("UI Arrow")]
    [Tooltip("Prefab mũi tên UI (RectTransform). Dùng khi step.showPointerArrow = true và step.arrowPrefab trống.")]
    public GameObject uiArrowPrefab;

    // Runtime
    GameObject _uiArrowInstance;          // ⬅️ chỉ dành cho UI arrow (Instantiate/Destroy)
    GameObject _worldArrowAnchorGO;       // ⬅️ anchor của world arrow (SetActive on/off)
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
    [Header("Behavior")]
    [SerializeField] bool autoHideCanvasWhenIdle = true;
    void Awake()
    {
        // Singleton + DDOL
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (PlayerDataManager.Instance && PlayerDataManager.Instance.playerData.tutorialCompleted && autoHideCanvasWhenIdle)
        {
            SetTutorialCanvasActive(false);   // ẩn toàn bộ tutorial UI
        }
        if (tutorialCanvas == null) tutorialCanvas = GetComponentInChildren<Canvas>(true);
        if (overlay == null) overlay = GetComponentInChildren<TutorialOverlay>(true);
        if (skipAllBtn && !skipButtonRectTransform)
            skipButtonRectTransform = skipAllBtn.GetComponent<RectTransform>();
    }
    void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    void Start()
    {
        EnsureWorldShield();
        // Nếu có sequence “route” đặc biệt của bạn thì xử lý trước
        var queued = TutorialRouter.Consume();
        if (queued != null)
        {
            // Nếu đã hoàn thành tutorial chính rồi, và queued là cùng loại tutorial
            // thì có thể bỏ qua. Nếu muốn luôn cho chạy queued, hãy bỏ if này.
            if (PlayerDataManager.Instance && PlayerDataManager.Instance.playerData.tutorialCompleted)
            {
                Debug.Log("[Tutorial] Tutorial completed -> ignore queued default tutorial.");
                return;
            }
            if (PlayerDataManager.Instance && PlayerDataManager.Instance.playerData.tutorialCompleted && autoHideCanvasWhenIdle)
            {
                SetTutorialCanvasActive(false);   // ẩn toàn bộ tutorial UI
            }
            sequence = queued;
            StartTutorial();
            return;
        }

        if (startOnAwake && sequence != null && sequence.steps?.Length > 0)
        {
            if (PlayerDataManager.Instance && PlayerDataManager.Instance.playerData.tutorialCompleted)
            {
                // đã xong ⇒ ẩn canvas
                if (autoHideCanvasWhenIdle) SetTutorialCanvasActive(false);
                // tắt skip nếu nó nằm trong tutorial canvas
                if (skipAllBtn) { skipAllBtn.gameObject.SetActive(false); skipAllBtn.interactable = false; }
                return;
            }
            StartTutorial();
        }
    }

    public void StartTutorial()
    {
        // Chắn thêm 1 lớp cho các chỗ gọi tay
        if (PlayerDataManager.Instance && PlayerDataManager.Instance.playerData.tutorialCompleted ) // nếu bạn có flag debug
        {
            if (autoHideCanvasWhenIdle) SetTutorialCanvasActive(false);
            return;
        }
        if (autoHideCanvasWhenIdle) SetTutorialCanvasActive(true);   // ✅ bật lên khi bắt đầu
        _index = -1;
        NextStep();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureWorldShield();
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
        if (!step.pointArrowOnWorldTarget)
        {
            _currentTarget = null;
            yield return TutorialTargetResolver.ResolveTargetRectCoroutine(tutorialCanvas, step, rt => _currentTarget = rt);

            if (overlay != null)
            {
                overlay.ClearWhitelist();
                overlay.SetDim(step.showFocus || step.mode == StepMode.ForceTarget);
                if (_currentTarget != null && step.mode != StepMode.TapAnywhere)
                    overlay.AddWhitelist(_currentTarget);
            }

            // Re-attach UI arrow nếu cần
            DestroyUIArrow();
            if (step.showPointerArrow && _currentTarget != null)
                SpawnUIArrowToTarget(_currentTarget, step);

            ShowChat(step, _currentTarget);
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
    void StopActiveStepRoutine()
    {
        if (_activeStepRoutine != null)
        {
            StopCoroutine(_activeStepRoutine);
            _activeStepRoutine = null;
        }
    }

    public void NextStep()
    {
        if (_isEnding) return;
        if (sequence == null || sequence.steps == null || sequence.steps.Length == 0) { EndTutorial(); return; }

        int nextIndex = _index + 1;

        // ✅ HẾT SEQUENCE → đánh dấu tutorial xong rồi mới End
        if (nextIndex >= sequence.steps.Length)
        {
            // mark completion (an toàn khi PlayerDataManager chưa có)
            try { PlayerDataManager.Instance?.MarkTutorialCompleted(); }
            catch (System.Exception e) { Debug.LogWarning("[Tutorial] MarkTutorialCompleted failed: " + e.Message); }

            _isEnding = true;
            ++_stepVersion;
            StopActiveStepRoutine();
            CleanupStep();
            EndTutorial();
            _isEnding = false;
            return;
        }

        // ... phần còn lại giữ nguyên
        StopActiveStepRoutine();
        CleanupStep();
        _index = nextIndex;
        Debug.Log($"[Tutorial] NextStep -> index={_index}/{sequence.steps.Length - 1}");
        int version = ++_stepVersion;
        _activeStepRoutine = StartCoroutine(RunStepWhenReady(sequence.steps[_index], version));
    }


    IEnumerator RunStepWhenReady(TutorialStepSO step, int versionToken)
    {
        yield return WaitWhileSceneLoading();

        // Nếu trong lúc chờ đã End hoặc nhảy bước khác -> thoát
        if (versionToken != _stepVersion || _index < 0 || step != sequence.steps[_index]) yield break;

        yield return ApplyStep(step);
    }
    public void EndTutorial()
    {
        ++_stepVersion;
        StopActiveStepRoutine();

        CleanupStep();
        if (overlay) { overlay.ClearWhitelist(); overlay.SetDim(false); overlay.SetBlockerOnly(false); }
        if (_currentBubble) Destroy(_currentBubble.gameObject);
        _index = -1;
        Dbg("EndTutorial(): cleanup done, timescale=" + Time.timeScale);

        // Tắt Skip cho chắc
        if (skipAllBtn)
        {
            skipAllBtn.interactable = false;
            skipAllBtn.gameObject.SetActive(false);
        }

        if (Mathf.Approximately(Time.timeScale, 0f))
            Time.timeScale = 1f;
        PlayerDataManager.Instance?.MarkTutorialCompleted(); 
        
        Debug.Log("[Tutorial] EndTutorial()");
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

        // 🔻 dọn anchor ảo nếu có
        if (_customBubbleAnchor) { Destroy(_customBubbleAnchor.gameObject); _customBubbleAnchor = null; }
        SetWorldShieldActive(false);
        _currentTarget = null;

        if (_lifted)
        {
            TutorialVisualLift.Restore(_liftHandle);
            _lifted = false;
        }

        DestroyUIArrow();         // (nếu bạn đã tách UI arrow như trước)
        DeactivateWorldArrow();   // (world arrow bật/tắt anchor)
        Time.timeScale = _prevTimeScale;
    }


    IEnumerator ApplyStep(TutorialStepSO step, bool rebindOnly = false)
    {
        // 0) Nếu cần chờ tín hiệu → chờ, tắt overlay/skip trong lúc chờ
        if (!rebindOnly && step.waitForExternalSignal)
        {
            Dbg($"ApplyStep('{step.stepId}') WAIT SIGNAL... index={_index}");
            if (overlay) { overlay.SetDim(false); overlay.ClearWhitelist(); overlay.SetBlockerOnly(false); }
            SetSkipActive(false);
            DbgSkip("ApplyStep/enter-waitSignal");

            if (!string.IsNullOrEmpty(step.armSignalInSceneName))
            {
                Dbg($"Wait scene='{step.armSignalInSceneName}' to arm");
                while (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != step.armSignalInSceneName)
                    yield return null;
            }
            else
            {
                Dbg("Wait LoaderBridge.IsLoading=false");
                while (LoaderBridge.IsLoading) yield return null;
            }

            Dbg("WaitForExternalSignal -> start");
            yield return WaitForExternalSignal(step);
            Dbg("WaitForExternalSignal -> GOT SIGNAL");
            SetSkipActive(true);
            WhitelistSkipIfNeeded();
            if (overlay) { overlay.SetBlockerOnly(false); overlay.ClearWhitelist(); overlay.SetDim(false); }
            // ✅ Quan trọng: bật lại Skip + overlay sạch
            ResetUIAfterSignal(step);
        }

        // 0.1) BẬT lại Skip + baseline overlay CHO MỌI STEP
        SetSkipActive(true);
        if (overlay) { overlay.SetBlockerOnly(false); overlay.ClearWhitelist(); overlay.SetDim(false); }
        WhitelistSkipIfNeeded();

        // 1) Freeze nếu cần (đặt sau chờ signal)
        if (!rebindOnly && step.freezeTime)
        {
            if (!Mathf.Approximately(Time.timeScale, 0f)) _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        // 2) Countdown (nếu có)
        if (!rebindOnly && step.timeCountdown > 0f)
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

            SetWorldShieldActive(false);

            // Chờ người chơi bấm đúng object (raycast 3D)
            yield return WaitForTapOnWorldTarget(_worldTarget);

            // Cleanup + Next
            DeactivateWorldArrow();
            NextStep();
            yield break;
        }

        // ==== UI MODE ====
        if (autoDim && overlay != null)
            overlay.SetDim(step.showFocus || step.mode == StepMode.ForceTarget);

        if (step.mode == StepMode.TapAnywhere)
        {
            // ——— (A) TapAnywhere nhưng vẫn highlight target ———
            if (step.tapAnywhereHighlightTarget)
            {
                // 1) Resolve UI target
                _currentTarget = null;
                yield return TutorialTargetResolver.ResolveTargetRectCoroutine(
                    tutorialCanvas, step, rt => _currentTarget = rt);

                // 🔧 Fallback cho panel / non-Button UI:
                if (_currentTarget == null)
                    _currentTarget = FallbackResolveUITarget(step);

                // Nếu vẫn không có → tap-anywhere thuần
                if (_currentTarget == null)
                {
                    ShowChat(step, null);
                    if (_currentBubble && _currentBubble.root) RaiseUIRectAboveOverlay(_currentBubble.root);

                    if (overlay)
                    {
                        overlay.SetBlockerOnly(false);               // ⬅️ để tap-anywhere bằng input thô hoạt động
                        overlay.ClearWhitelist();
                        overlay.SetDim(autoDim && step.showFocus);
                        if (_currentBubble && _currentBubble.root) overlay.AddWhitelist(_currentBubble.root);
                        WhitelistSkipIfNeeded();
                    }
                    SetWorldShieldActive(true);
                    yield return WaitForTapAnywhere();
                    NextStep();
                    yield break;

                }

                // 2) Nâng target lên trên overlay (nếu dùng lift)
                if (tutorialCanvas != null)
                {
                    int above = tutorialCanvas.sortingOrder + 1;
                    _liftHandle = TutorialVisualLift.LiftTarget(_currentTarget, above);
                    _lifted = true;
                }

                // 3) Arrow UI — ép spawn trong highlight (kể cả step.showUIArrow chưa tick)
                DestroyUIArrow();
                if (uiArrowPrefab || step.arrowPrefab || step.showUIArrow || step.showPointerArrow)
                {
                    SpawnUIArrowInSameCanvas(_currentTarget, step);
                    if (_uiArrowInstance)
                        RaiseUIRectAboveOverlay(_uiArrowInstance.GetComponent<RectTransform>());
                }

                // 4) Bubble neo theo target
                ShowChat(step, _currentTarget);
                if (_currentBubble && _currentBubble.root) RaiseUIRectAboveOverlay(_currentBubble.root);

                // 5) Overlay: DIM CHẮC CHẮN + whitelist target/arrow/bubble/skip
                if (overlay)
                {
                    overlay.ClearWhitelist();
                    overlay.SetDim(true);                               // 👈 cưỡng bách dim để “nổi” target
                    overlay.SetBlockerOnly(!step.tapAnywherePassThroughUI);

                    overlay.AddWhitelist(_currentTarget);
                    if (_uiArrowInstance) overlay.AddWhitelist(_uiArrowInstance.GetComponent<RectTransform>());
                    if (_currentBubble && _currentBubble.root) overlay.AddWhitelist(_currentBubble.root);
                    if (skipButtonRectTransform) overlay.AddWhitelist(skipButtonRectTransform);
                }

                // 6) Tap anywhere để Next
                yield return WaitForTapAnywhere();
                NextStep();
                yield break;
            }

            // ——— (B) TapAnywhere thuần (không highlight target) ———
            _currentTarget = null;
            ShowChat(step, null);
            if (_currentBubble && _currentBubble.root) RaiseUIRectAboveOverlay(_currentBubble.root);

            if (overlay)
            {
                overlay.ClearWhitelist();
                overlay.SetDim(autoDim && step.showFocus);
                overlay.SetBlockerOnly(!step.tapAnywherePassThroughUI);
                WhitelistIfAny(_currentBubble ? _currentBubble.root : null);
                if (skipButtonRectTransform) overlay.AddWhitelist(skipButtonRectTransform);
            }

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
            if (_uiArrowInstance && overlay)
                overlay.AddWhitelist(_uiArrowInstance.GetComponent<RectTransform>());
        }
        DestroyUIArrow(); // clear trước
        if (step.showUIArrow)
            SpawnUIArrowInSameCanvas(_currentTarget, step);

        // chat bubble
        ShowChat(step, _currentTarget);
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
                if (step.showPointerArrow)
                    SpawnUIArrowToTarget(_currentTarget, step);

                // Chat (neo vào target)
                ShowChat(step, _currentTarget);
                
                // Nếu không requireButtonClick → tap vùng target để Next
                if (!step.requireButtonClick)
                {
                    yield return WaitForTapOnTarget(_currentTarget);
                    NextStep();
                }
                yield break;
            }
            // 🔒 Freeze gameplay trong UI step (nếu tick), vẫn bấm UI được vì EventSystem chạy unscaled
            
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

    private void ShowChat(TutorialStepSO step, RectTransform anchor)
    {
        if (step == null || chatBubblePrefab == null || tutorialCanvas == null) return;

        // Text rỗng → dọn bubble và anchor ảo rồi thoát
        if (string.IsNullOrWhiteSpace(step.chatText))
        {
            if (_currentBubble) { Destroy(_currentBubble.gameObject); _currentBubble = null; }
            if (_customBubbleAnchor) { Destroy(_customBubbleAnchor.gameObject); _customBubbleAnchor = null; }
            return;
        }

        // Dọn bubble & anchor ảo cũ
        if (_currentBubble) { Destroy(_currentBubble.gameObject); _currentBubble = null; }
        if (_customBubbleAnchor) { Destroy(_customBubbleAnchor.gameObject); _customBubbleAnchor = null; }

        // Xác định follow (anchor) cho bubble
        RectTransform follow;
        if (step.chatBubbleUseCustomPos)
        {
            var go = new GameObject("BubbleAnchor_Custom", typeof(RectTransform));
            _customBubbleAnchor = go.GetComponent<RectTransform>();
            _customBubbleAnchor.SetParent(tutorialCanvas.transform, false);
            _customBubbleAnchor.anchorMin = _customBubbleAnchor.anchorMax = new Vector2(0.5f, 0.5f);
            _customBubbleAnchor.pivot = new Vector2(0.5f, 0.5f);
            _customBubbleAnchor.localRotation = Quaternion.identity;
            _customBubbleAnchor.localScale = Vector3.one;

            if (step.chatBubblePosAsScreenSpace)
            {
                var cam = tutorialCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : tutorialCanvas.worldCamera;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        tutorialCanvas.transform as RectTransform,
                        step.chatBubbleCustomPos,
                        cam,
                        out Vector2 localPos))
                    _customBubbleAnchor.anchoredPosition = localPos;
                else
                    _customBubbleAnchor.anchoredPosition = Vector2.zero;
            }
            else
            {
                _customBubbleAnchor.anchoredPosition = step.chatBubbleCustomPos;
            }

            follow = _customBubbleAnchor;
        }
        else
        {
            follow = anchor != null ? anchor : (tutorialCanvas.transform as RectTransform);
        }

        // 1) Instantiate
        _currentBubble = Instantiate(chatBubblePrefab, tutorialCanvas.transform);

        // 2) Setup (gán canvas/follow/text/avatar/offset)
        _currentBubble.Setup(
            tutorialCanvas,
            follow,
            step.chatText,
            step.speakerAvatar,
            step.chatBubbleOffset
        );

        // 3) Typewriter hoặc hiện ngay
        if (step.useTypewriter)
        {
            _currentBubble.StartTypewriter(
                step.typewriterCharsPerSecond,
                step.typewriterUseUnscaledTime,   // chạy cả khi freeze
                step.typewriterAllowSkip
            );
        }
        else
        {
            _currentBubble.ShowFullNow();
        }
    }

    public void SkipAll()
    {
        // Disable ngay để chống double-click
        if (skipAllBtn)
        {
            skipAllBtn.interactable = false;
            skipAllBtn.gameObject.SetActive(false); // hoặc giữ lại và chỉ disable: interactable=false
        }

        // Nếu overlay đang whitelist nút skip, không sao; EndTutorial sẽ ClearWhitelist()
        EndTutorial();

        // (tuỳ chọn) đánh dấu hoàn thành
        // PlayerPrefs.SetInt("TutorialDone", 1);
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
            if (Input.GetMouseButtonDown(0))
            {
                Dbg("WaitForTapAnywhere: MouseDown(0)");
                tapped = true;
            }
            else if (Input.touchCount > 0)
            {
                var t = Input.touches[0];
                if (t.phase == TouchPhase.Began || t.phase == TouchPhase.Ended)
                {
                    Dbg($"WaitForTapAnywhere: Touch {t.phase}");
                    tapped = true;
                }
            }
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

        var rtPrefab = prefab.GetComponent<RectTransform>();
        if (rtPrefab == null)
        {
            Debug.LogWarning("[Tutorial] arrowPrefab không phải UI (RectTransform).");
            return;
        }

        _arrowInstance = Instantiate(prefab, target);
        var arrowRT = _arrowInstance.GetComponent<RectTransform>();
        arrowRT.anchorMin = arrowRT.anchorMax = new Vector2(0.5f, 0.5f);
        arrowRT.anchoredPosition = step.arrowUIOffset;
        arrowRT.localRotation = Quaternion.identity;
        arrowRT.localScale = Vector3.one;

        // 🔼 đưa lên trên overlay + whitelist
        RaiseUIRectAboveOverlay(arrowRT);
        if (overlay)
        {
            overlay.AddWhitelist(target);
            overlay.AddWhitelist(arrowRT);
        }

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
    // BẬT mũi tên có sẵn bên dưới anchor
    void ActivateWorldArrow(Transform tower, TutorialStepSO step)
    {
        if (!tower) return;

        // tìm ArrowAnchor
        var anchor = tower.Find(step.arrowAnchorPath);
        if (!anchor)
            anchor = tower.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name.Equals("ArrowAnchor", StringComparison.OrdinalIgnoreCase));

        if (anchor)
        {
            _worldArrowAnchorGO = anchor.gameObject;
            _worldArrowAnchorGO.SetActive(true); // ✅ chỉ bật
        }
    }
    void SpawnUIArrowToTarget(RectTransform target, TutorialStepSO step)
    {
        if (target == null) return;

        // Chọn prefab: ưu tiên step.arrowPrefab; fallback uiArrowPrefab của Manager
        var prefab = step.arrowPrefab != null ? step.arrowPrefab : uiArrowPrefab;
        if (!prefab)
        {
            Debug.LogWarning("[Tutorial] UI step bật showPointerArrow nhưng không có prefab (step.uiArrowPrefab/manager.uiArrowPrefab).");
            return;
        }

        var rtPrefab = prefab.GetComponent<RectTransform>();
        if (rtPrefab == null)
        {
            Debug.LogWarning("[Tutorial] ui arrow prefab không phải UI (RectTransform).");
            return;
        }

        // Destroy instance cũ (nếu có) để tránh duplicate khi rebind
        DestroyUIArrow();

        _uiArrowInstance = Instantiate(prefab, target);  // child của chính target
        var arrowRT = _uiArrowInstance.GetComponent<RectTransform>();

        arrowRT.anchorMin = arrowRT.anchorMax = new Vector2(0.5f, 0.5f);
        arrowRT.anchoredPosition = step.arrowUIOffset;
        arrowRT.localRotation = Quaternion.identity;
        arrowRT.localScale = Vector3.one;

        // Nếu arrow có hiệu ứng (bobbing…), nó sẽ tự chạy. Không đụng đến overlay ở đây.
    }

    void DestroyUIArrow()
    {
        if (_uiArrowInstance) { Destroy(_uiArrowInstance); _uiArrowInstance = null; }
    }
    void SpawnUIArrowInSameCanvas(RectTransform target, TutorialStepSO step)
    {
        if (!target) return;
        var prefab = step.arrowPrefab != null ? step.arrowPrefab : uiArrowPrefab;
        if (!prefab) { Debug.LogWarning("[Tutorial] UI arrow prefab chưa gán."); return; }

        var targetCanvas = target.GetComponentInParent<Canvas>();
        if (!targetCanvas) { Debug.LogWarning("[Tutorial] Target UI không nằm trong Canvas."); return; }

        DestroyUIArrow(); // tránh trùng khi rebind
        _uiArrowInstance = Instantiate(prefab, targetCanvas.transform);

        // Đưa mũi tên lên trên overlay
        var arrowRT = _uiArrowInstance.GetComponent<RectTransform>();
        RaiseUIRectAboveOverlay(arrowRT);
        MakeUIArrowNonPassThrough(_uiArrowInstance);
        // cấu hình follow/aim
        var aim = _uiArrowInstance.GetComponent<ArrowUIAimAtTarget>();
        if (!aim) aim = _uiArrowInstance.AddComponent<ArrowUIAimAtTarget>();
        aim.target = target;
        aim.radialOffset = step.uiArrowRadialOffset > 0 ? step.uiArrowRadialOffset : 64f;
        aim.bobAmplitude = step.uiArrowBobAmplitude;
        aim.bobFrequency = step.uiArrowBobFrequency;
        aim.orientationOffset = step.uiArrowOrientationOffset;

        // ✅ whitelist target & arrow để không bị dim/chặn
        if (overlay)
        {
            overlay.AddWhitelist(target);
            overlay.AddWhitelist(arrowRT);
        }

        // nếu cần Skip luôn click được khi dim:
        if (overlay && skipButtonRectTransform)
            overlay.AddWhitelist(skipButtonRectTransform);
    }

    void DeactivateWorldArrow()
    {
        if (_worldArrowAnchorGO)
            _worldArrowAnchorGO.SetActive(false); // ✅ chỉ tắt (không Destroy)
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
        SetWorldShieldActive(true);
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
        SetWorldShieldActive(false);
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
    void SetSkipVisible(bool visible)
    {
        if (!skipAllBtn) return;
        skipAllBtn.interactable = visible;
        skipAllBtn.gameObject.SetActive(visible);
    }
    public void EmitSignal(string key = "")
    {
        string k = string.IsNullOrEmpty(key) ? "<default>" : key;

        // --- de-bounce theo frame ---
        int frame = Time.frameCount;
        if (_lastEmitFrame.TryGetValue(k, out var lastFrame) && lastFrame == frame)
            return; // bỏ các emit lặp trong cùng frame
        _lastEmitFrame[k] = frame;

        // --- (tuỳ chọn) de-bounce theo thời gian ---
        if (minEmitInterval > 0f)
        {
            float now = Time.unscaledTime;
            if (_lastEmitTime.TryGetValue(k, out var t) && (now - t) < minEmitInterval) return;
            _lastEmitTime[k] = now;
        }

        // tăng counter (edge-trigger)
        if (k == "<default>")
        {
            unchecked { _defaultSignalCount++; }
            Debug.Log($"[Tutorial] Signal '<default>' -> count={_defaultSignalCount}");
        }
        else
        {
            _signalCounts.TryGetValue(k, out var c);
            unchecked { c++; }
            _signalCounts[k] = c;
            Debug.Log($"[Tutorial] Signal '{k}' -> count={c}");
        }
#if UNITY_EDITOR
        var st = new System.Diagnostics.StackTrace(1, true);
        //Debug.Log($"[Tutorial] Signal '{(string.IsNullOrEmpty(key) ? "<default>" : key)}' "
        //        + $"-> count={(string.IsNullOrEmpty(key) ? _defaultSignalCount : _signalCounts[key])}\n{st}");
#endif
    }


    // (tuỳ chọn) tiện gọi toàn cục nếu bạn không giữ reference tới manager
    public static void EmitSignalGlobal(string key = "")
    {
        if (Instance != null) { Instance.EmitSignal(key); return; }
        var found = FindObjectOfType<TutorialManager>();
        if (found) { Instance = found; Instance.EmitSignal(key); }
        else Debug.LogWarning("[Tutorial] EmitSignalGlobal called but no TutorialManager exists.");
    }
    IEnumerator WaitForExternalSignal(TutorialStepSO step)
    {
        // key rỗng = kênh mặc định
        if (string.IsNullOrEmpty(step.externalSignalKey))
        {
            // Nếu đã có emit CHƯA tiêu thụ → đi tiếp ngay
            if (_defaultSignalCount > _defaultSignalConsumed)
            {
                _defaultSignalConsumed = _defaultSignalCount;
                Dbg($"Wait(default): consume immediately -> count={_defaultSignalCount}");
                yield break;
            }

            // Chờ tới khi có emit mới
            int consumed = _defaultSignalConsumed;
            Dbg($"Wait(default): start, consumed={consumed}, total={_defaultSignalCount}");
            while (_defaultSignalCount == consumed)
                yield return null;

            _defaultSignalConsumed = _defaultSignalCount;
            Dbg($"Wait(default): got new -> consumed={_defaultSignalConsumed}");
            yield break;
        }
        else
        {
            string k = step.externalSignalKey;

            _signalCounts.TryGetValue(k, out var emitted);
            _signalConsumed.TryGetValue(k, out var used);

            // Đã có emit CHƯA tiêu thụ → đi tiếp ngay
            if (emitted > used)
            {
                _signalConsumed[k] = emitted;
                Dbg($"Wait('{k}'): consume immediately -> used={used} -> {emitted}");
                yield break;
            }

            // Chờ emit mới
            Dbg($"Wait('{k}'): start, used={used}, emitted={emitted}");
            while (true)
            {
                _signalCounts.TryGetValue(k, out emitted);
                if (emitted > used)
                {
                    _signalConsumed[k] = emitted;
                    Dbg($"Wait('{k}'): got new -> used={used} -> {emitted}");
                    break;
                }
                yield return null;
            }
            yield break;
        }
    }


    void SetSkipActive(bool active)
    {
        if (!skipAllBtn)
        {
            Dbg("SetSkipActive: skipAllBtn=NULL");
            return;
        }
        skipAllBtn.gameObject.SetActive(active);
        skipAllBtn.interactable = active;
        Dbg($"SetSkipActive({active})");
    }
    void WhitelistSkipIfNeeded()
    {
        if (overlay != null && skipAllBtn != null && skipButtonRectTransform != null)
            overlay.AddWhitelist(skipButtonRectTransform);
    }

    // Thay thế toàn bộ hàm cũ bằng hàm này
    void RaiseUIRectAboveOverlay(RectTransform rt)
    {
        if (!rt) return;

        var parentCanvas = rt.GetComponentInParent<Canvas>(true);

        // Nếu nằm cùng tutorialCanvas → chỉ cần đưa lên cuối cùng là đủ
        if (tutorialCanvas && parentCanvas == tutorialCanvas)
        {
            rt.SetAsLastSibling();
            return;
        }

        // Khác canvas → dùng Canvas riêng để overrideSorting > overlay
        var cv = rt.GetComponent<Canvas>();
        if (!cv) cv = rt.gameObject.AddComponent<Canvas>();
        cv.overrideSorting = true;

        int baseOrder = tutorialCanvas ? tutorialCanvas.sortingOrder : 0;
        cv.sortingLayerID = tutorialCanvas ? tutorialCanvas.sortingLayerID : cv.sortingLayerID;
        cv.sortingOrder = baseOrder + 2;

        if (!rt.GetComponent<UnityEngine.UI.GraphicRaycaster>())
            rt.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
    }

    RectTransform FallbackResolveUITarget(TutorialStepSO step)
    {
        // 1) Ưu tiên ref trực tiếp nếu có
        if (step.uiTargetRef) return step.uiTargetRef;

        // 2) ByTag → lấy RectTransform của object đó hoặc con của nó
        if (!string.IsNullOrEmpty(step.targetTag))
        {
            var go = GameObject.FindGameObjectWithTag(step.targetTag);
            if (go)
            {
                var rt = go.GetComponent<RectTransform>();
                if (rt) return rt;
                rt = go.GetComponentInChildren<RectTransform>(true);
                if (rt) return rt;
            }
        }

        // 3) ByName → tìm trong tất cả Canvas (kể cả inactive)
        if (!string.IsNullOrEmpty(step.targetName))
        {
            var canvases = FindObjectsOfType<Canvas>(true);
            foreach (var c in canvases)
            {
                var rts = c.GetComponentsInChildren<RectTransform>(true);
                var hit = rts.FirstOrDefault(x => x.name.Equals(step.targetName, StringComparison.Ordinal));
                if (hit) return hit;
            }
        }

        // 4) Cuối cùng: quét toàn scene (kể cả ngoài canvas)
        {
            var all = Resources.FindObjectsOfTypeAll<RectTransform>();
            var hit = all.FirstOrDefault(x => x.name.Equals(step.targetName, StringComparison.Ordinal));
            if (hit) return hit;
        }

        return null;
    }


    void WhitelistIfAny(RectTransform rt)
    {
        if (overlay != null && rt != null) overlay.AddWhitelist(rt);
    }
    void ResetUIAfterSignal(TutorialStepSO step)
    {
        // bật lại Skip
        SetSkipActive(true);
        DbgSkip("ResetUIAfterSignal/after-SetSkipActive");

        // overlay sạch – KHÔNG chặn
        if (overlay)
        {
            overlay.SetBlockerOnly(false);
            overlay.ClearWhitelist();
            overlay.SetDim(false);
            // cho Skip bấm xuyên nếu bạn đang dim lại sau này
            WhitelistSkipIfNeeded();
        }
        DbgOverlay("ResetUIAfterSignal", blockerOnly: false, dim: false);
    }
    void Dbg(string msg) => Debug.Log(DBG + msg);

    // In ra trạng thái Skip
    void DbgSkip(string where)
    {
        Dbg($"{where}: skipAllBtn={(skipAllBtn ? "OK" : "NULL")}, active={(skipAllBtn && skipAllBtn.gameObject.activeInHierarchy)}, interact={(skipAllBtn && skipAllBtn.interactable)}");
    }

    // In ra trạng thái overlay (những gì ta set)
    void DbgOverlay(string where, bool blockerOnly, bool dim, int extraWhitelists = -1)
    {
        Dbg($"{where}: overlay: blockerOnly={blockerOnly}, dim={dim}, extraWhitelistCount={extraWhitelists}");
    }
    void ResetUIAfterSignal()
    {
        // bật lại Skip
        SetSkipActive(true);
        WhitelistSkipIfNeeded();

        // overlay sạch để bước kế tiếp tự cấu hình
        if (overlay)
        {
            overlay.SetBlockerOnly(false);
            overlay.ClearWhitelist();
            overlay.SetDim(false);
        }
    }
    void SetTutorialCanvasActive(bool active)
    {
        if (!tutorialCanvas) return;
        if (tutorialCanvas.gameObject.activeSelf == active) return;
        tutorialCanvas.gameObject.SetActive(active);
    }
    void EnsureWorldShield()
    {
        if (!useWorldClickShield || !worldCamera) return;
        if (_worldClickShield) { UpdateWorldShieldGeometry(); return; }

        _worldClickShield = new GameObject("TutorialWorldClickShield");
        _worldClickShield.transform.SetParent(worldCamera.transform, false);

        var bc = _worldClickShield.AddComponent<BoxCollider>();
        bc.isTrigger = false;                      // cần là collider “rắn” để Physics.Raycast trúng

        int layer = LayerMask.NameToLayer(shieldLayerName);
        if (layer >= 0) _worldClickShield.layer = layer;

        UpdateWorldShieldGeometry();
        _worldClickShield.SetActive(false);
    }

    void UpdateWorldShieldGeometry()
    {
        if (!_worldClickShield || !worldCamera) return;

        var cam = worldCamera;
        var t = _worldClickShield.transform;

        float z = Mathf.Max(cam.nearClipPlane + shieldDepthFromNear, cam.nearClipPlane + 0.02f);
        t.localPosition = new Vector3(0f, 0f, z);
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;

        var bc = _worldClickShield.GetComponent<BoxCollider>();

        if (cam.orthographic)
        {
            float h = cam.orthographicSize * 2f;
            float w = h * cam.aspect;
            bc.size = new Vector3(w, h, 0.02f);
            bc.center = Vector3.zero;
        }
        else
        {
            float h = 2f * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f) * z;
            float w = h * cam.aspect;
            bc.size = new Vector3(w, h, 0.02f);
            bc.center = Vector3.zero;
        }
    }

    public void SetWorldShieldActive(bool on)
    {
        if (!useWorldClickShield) return;
        EnsureWorldShield();
        if (_worldClickShield)
        {
            UpdateWorldShieldGeometry();           // update theo FOV/aspect hiện tại
            _worldClickShield.SetActive(on);
        }
    }
    void MakeUIArrowNonPassThrough(GameObject go)
    {
        // luôn bắt raycast UI
        var cg = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = true;
        cg.interactable = false; // arrow không “bấm” được, chỉ chặn

        foreach (var g in go.GetComponentsInChildren<Graphic>(true))
            g.raycastTarget = true;

        if (!go.GetComponent<UIPassThroughBlocker>())
            go.AddComponent<UIPassThroughBlocker>();
    }
}
