
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class ArrowUIAimAtTarget : MonoBehaviour
{
    [Header("Target")]
    public RectTransform target;              // nút cần ấn

    [Header("Placement")]
    public float radialOffset = 64f;          // px từ target ra ngoài theo hướng từ tâm -> target
    public float orientationOffset = 0f;      // độ (nếu prefab không “chỉ lên” theo +Y)

    [Header("Bobbing")]
    public float bobAmplitude = 6f;           // px
    public float bobFrequency = 2f;           // Hz

    RectTransform _self, _parentRT;
    Canvas _canvas;
    CanvasGroup _cg;

    void Awake()
    {
        _self = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
        _parentRT = _self.parent as RectTransform;

        _cg = gameObject.GetComponent<CanvasGroup>();
        if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
        _cg.blocksRaycasts = false;   // không chặn click UI bên dưới
        _cg.interactable = false;
    }

    void LateUpdate()
    {
        if (target == null || _canvas == null || _parentRT == null) return;

        // lấy tâm màn hình (screen space)
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        // tọa độ screen của target
        var cam = (_canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : _canvas.worldCamera;
        Vector2 targetScreen = RectTransformUtility.WorldToScreenPoint(cam, target.position);

        // vector từ tâm -> target
        Vector2 v = targetScreen - screenCenter;
        if (v.sqrMagnitude < 1f) v = Vector2.up; // tránh zero

        // nhấp-nhổm dọc theo hướng mũi tên
        float bob = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * bobFrequency) * bobAmplitude;

        // vị trí mũi tên: lùi ra khỏi target một đoạn (radialOffset + bob)
        Vector2 arrowScreen = targetScreen - v.normalized * (radialOffset + bob);

        // chuyển về local trong parent (canvas)
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRT, arrowScreen, cam, out var localPos))
            _self.anchoredPosition = localPos;

        // xoay: đầu mũi tên hướng vào target (tức cùng hướng với v), đuôi về tâm
        float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg - 90f + orientationOffset;
        _self.localRotation = Quaternion.Euler(0, 0, angle);

        _self.localScale = Vector3.one;
    }
}
