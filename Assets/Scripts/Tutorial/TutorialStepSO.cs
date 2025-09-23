using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Tutorial/Step", fileName = "Step_")]
public class TutorialStepSO : ScriptableObject
{
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

    [Header("Force Target Behavior")]
    public bool requireButtonClick = true;
    public string requiredButtonChildName = "";

    [Header("Focus/Highlight")]
    public bool showFocus = true;
    public Vector2 focusPadding = new Vector2(16, 16);

    [Tooltip("BẬT để hiện mũi tên cho bước UI (ví dụ target là Button trong Canvas).")]
    public bool showPointerArrow = false;
}
