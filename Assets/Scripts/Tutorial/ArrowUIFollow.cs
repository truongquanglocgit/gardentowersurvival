// ArrowUIFollow.cs
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class ArrowUIFollow : MonoBehaviour
{
    public RectTransform target;        // UI cần ấn
    public Vector2 offset = new Vector2(0f, 80f);

    RectTransform _self;
    Canvas _canvas;
    RectTransform _parentRT;

    void Awake()
    {
        _self = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
        _parentRT = _self.parent as RectTransform;

        // Không chặn click
        var cg = gameObject.GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;
    }

    void LateUpdate()
    {
        if (target == null || _canvas == null || _parentRT == null) return;

        // Lấy screen pos của target
        var cam = (_canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : _canvas.worldCamera;
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, target.position);

        // Đổi sang local pos trong parent của arrow
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRT, screenPos, cam, out var localPos))
        {
            _self.anchoredPosition = localPos + offset;
            _self.localRotation = Quaternion.identity;
            _self.localScale = Vector3.one;
        }
    }
}
