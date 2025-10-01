using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Tutorial/Step", fileName = "Step_")]
public class TutorialStepSO : ScriptableObject
{
    [Header("External Start Gate")]
    public bool waitForExternalSignal = false;
    public string externalSignalKey = "";
    [Tooltip("Chỉ bắt đầu chờ signal khi scene này đang active. Để trống = bất kỳ scene.")]
    public string armSignalInSceneName = "MainMenu";
    [Header("External Start Gate")]
    

    [Tooltip("Sau khi nhận tín hiệu, có xóa tín hiệu luôn không (một lần dùng)?")]
    public bool consumeSignal = true;
    [Header("Chat Typewriter")]
    [Tooltip("Hiển thị chat theo kiểu gõ từng ký tự.")]
    public bool useTypewriter = true;

    [Tooltip("Số ký tự/giây.")]
    public float typewriterCharsPerSecond = 28f;

    [Tooltip("Dùng unscaled time để hiệu ứng vẫn chạy khi freezeTime.")]
    public bool typewriterUseUnscaledTime = true;

    [Tooltip("Cho phép click để hiện hết ngay lập tức.")]
    public bool typewriterAllowSkip = true;
    [Header("Chat Bubble Custom Placement")]
    [Tooltip("Nếu bật: chat bubble sẽ xuất hiện tại vị trí chỉ định, thay vì neo theo target.")]
    public bool chatBubbleUseCustomPos = false;

    [Tooltip("Toạ độ cho bubble. Nếu 'asScreenSpace' = false → hiểu là anchoredPosition trong TutorialCanvas. Nếu = true → hiểu là pixel screen-space.")]
    public Vector2 chatBubbleCustomPos = Vector2.zero;

    [Tooltip("Nếu true: 'chatBubbleCustomPos' là toạ độ màn hình (pixel). Nếu false: là anchoredPosition trong canvas.")]
    public bool chatBubblePosAsScreenSpace = false;
    [Header("UI Arrow")]
    [Tooltip("Nếu true và target là UI: spawn prefab mũi tên UI vào cùng Canvas với target.")]
    public bool showUIArrow = false;

    [Tooltip("Khoảng cách tính từ mép nút ra phía tâm màn hình (px).")]
    public float uiArrowRadialOffset = 64f;

    [Tooltip("Biên độ nhấp-nhổm (px) dọc theo hướng mũi tên.")]
    public float uiArrowBobAmplitude = 6f;

    [Tooltip("Tần số nhấp-nhổm (chu kỳ/giây).")]
    public float uiArrowBobFrequency = 2f;

    // (tuỳ chọn) nếu prefab mũi tên không “chỉ lên” theo +Y, bù góc ở đây (độ)
    [Tooltip("Bù góc nếu hướng mặc định của prefab không phải 'lên trên'.")]
    public float uiArrowOrientationOffset = 0f;
    [Header("Pre-Step Countdown")]
    [Tooltip("Nếu > 0: chặn toàn bộ tương tác UI trong thời gian đếm ngược (unscaled), không hiện thêm gì mới.")]
    public float timeCountdown = 0f;
    [Header("Arrow on Tower (World)")]
    [Tooltip("Nếu bật: step này là target World (tower/đối tượng 3D). Sẽ gắn mũi tên 3D vào ArrowAnchor (child rỗng) của target.")]
    public bool pointArrowOnWorldTarget = false;

    [Tooltip("Prefab mũi tên (UI cho bước UI; 3D cho bước World). Nếu để trống sẽ lấy ở TutorialManager.arrowPrefab.")]
    public GameObject arrowPrefab;

    [Tooltip("TÊN hoặc PATH con để gắn làm anchor. Với World: ưu tiên Transform rỗng tên 'ArrowAnchor' (nếu không có sẽ tự tạo).")]
    public string arrowAnchorPath = "ArrowAnchor";

    [Tooltip("Offset khi anchor là RectTransform (UI).")]
    public Vector2 arrowUIOffset = new Vector2(0f, 80f);

    [Tooltip("Offset thêm khi anchor là Transform 3D.")]
    public Vector3 arrowLocalOffset = new Vector3(0f, 2f, 0f);

    [Tooltip("Nếu tick: khi gắn arrow sẽ dừng Time.timeScale cho tới khi người chơi bấm đúng target (World).")]
    public bool freezeTime = true;

    [Header("Step")]
    public string stepId;
    public StepMode mode = StepMode.TapAnywhere;

    [TextArea(2, 4)]
    public string chatText;
    public Sprite speakerAvatar;
    public Vector2 chatBubbleOffset = new Vector2(0, 120);

    [Header("Target Resolve")]
    public ResolveMode resolveMode = ResolveMode.None;
    public RectTransform uiTargetRef;     // ByRef
    public string targetName;             // ByName / RuntimeFindByName
    public string targetTag;              // ByTag  / RuntimeFindByTag
    public float findTimeout = 8f;        // thời gian chờ target xuất hiện
    [Header("Tap Anywhere + Highlight Target")]
    [Tooltip("Ở chế độ TapAnywhere nhưng vẫn resolve/nhấn mạnh target (nâng UI, arrow, bubble).")]
    public bool tapAnywhereHighlightTarget = true;

    [Tooltip("TapAnywhere: có cho phép UI nhận raycast xuyên overlay không.")]
    public bool tapAnywherePassThroughUI = false;
    [Header("Force Target Behavior")]
    public bool requireButtonClick = true;
    public string requiredButtonChildName = "";

    [Header("Focus/Highlight")]
    public bool showFocus = true;
    public Vector2 focusPadding = new Vector2(16, 16);

    [Tooltip("BẬT để hiện mũi tên cho bước UI (ví dụ target là Button trong Canvas).")]
    public bool showPointerArrow = false;
}
