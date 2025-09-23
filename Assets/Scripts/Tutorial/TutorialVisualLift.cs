using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class TutorialVisualLift
{
    public struct LiftHandle
    {
        public Canvas canvas;
        public bool hadCanvas;
        public bool prevOverride;
        public int prevSortingOrder;
        public int prevSortingLayerID;
        public AdditionalCanvasShaderChannels prevChannels; // <-- lưu kênh cũ
    }

    public static LiftHandle LiftTarget(RectTransform target, int sortingOrderAboveOverlay)
    {
        var handle = new LiftHandle();
        if (target == null) return handle;

        // tìm canvas cha gần nhất để copy kênh
        var parentCanvas = target.GetComponentInParent<Canvas>();

        var cvs = target.GetComponent<Canvas>();
        handle.hadCanvas = cvs != null;
        if (cvs == null) cvs = target.gameObject.AddComponent<Canvas>();

        if (target.GetComponent<GraphicRaycaster>() == null)
            target.gameObject.AddComponent<GraphicRaycaster>();

        handle.canvas = cvs;
        handle.prevOverride = cvs.overrideSorting;
        handle.prevSortingOrder = cvs.sortingOrder;
        handle.prevSortingLayerID = cvs.sortingLayerID;
        handle.prevChannels = cvs.additionalShaderChannels;

        cvs.overrideSorting = true;
        cvs.sortingOrder = sortingOrderAboveOverlay;

        // ⭐ Quan trọng: đảm bảo kênh shader đầy đủ cho TMP
        var need = AdditionalCanvasShaderChannels.TexCoord1
                 | AdditionalCanvasShaderChannels.TexCoord2
                 | AdditionalCanvasShaderChannels.Normal
                 | AdditionalCanvasShaderChannels.Tangent;

        if (parentCanvas != null)
            cvs.additionalShaderChannels = parentCanvas.additionalShaderChannels | need;
        else
            cvs.additionalShaderChannels |= need;

        // Refresh padding cho mọi TMP_Text dưới target (tránh bị cắt/outlines kỳ dị)
        var tmps = target.GetComponentsInChildren<TMP_Text>(true);
        foreach (var t in tmps) t.UpdateMeshPadding();

        return handle;
    }

    public static void Restore(in LiftHandle handle)
    {
        if (handle.canvas == null) return;

        handle.canvas.overrideSorting = handle.prevOverride;
        handle.canvas.sortingOrder = handle.prevSortingOrder;
        handle.canvas.sortingLayerID = handle.prevSortingLayerID;
        handle.canvas.additionalShaderChannels = handle.prevChannels; // khôi phục kênh

        if (!handle.hadCanvas)
        {
            Object.Destroy(handle.canvas.GetComponent<GraphicRaycaster>());
            Object.Destroy(handle.canvas);
        }
    }
}