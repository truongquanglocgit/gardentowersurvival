using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using TMPro;
using UnityEngine.UI;
public class TutorialChatBubble : MonoBehaviour
{
    [Header("Refs")]
    public RectTransform root;     // chính nó
    public Image avatar;
    public TMP_Text text;

    [Header("Style")]
    public Vector2 defaultOffset = new Vector2(0, 120);

    private RectTransform _follow;
    private Canvas _canvas;
    private Camera _uiCam;

    public void Setup(Canvas canvas, RectTransform follow, string content, Sprite avatarSprite, Vector2 offset)
    {
        _canvas = canvas;
        _uiCam = canvas.worldCamera;  // với ScreenSpace-Overlay sẽ là null
        _follow = follow;
        text.SetText(content ?? "");
        avatar.sprite = avatarSprite;
        defaultOffset = offset;
        gameObject.SetActive(true);
    }

    private void LateUpdate()
    {
        if (_follow == null || _canvas == null) return;

        Vector3 worldPos = _follow.TransformPoint(_follow.rect.center);
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(_uiCam, worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.transform as RectTransform,
            screenPos,
            _uiCam,
            out Vector2 local);

        root.anchoredPosition = local + defaultOffset;
    }
}
