using UnityEngine;
using UnityEngine.EventSystems;

public sealed class UIPassThroughBlocker :
    MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IScrollHandler
{
    public void OnPointerDown(PointerEventData e) { /* swallow */ }
    public void OnPointerUp(PointerEventData e) { /* swallow */ }
    public void OnPointerClick(PointerEventData e) { /* swallow */ }
    public void OnBeginDrag(PointerEventData e) { /* swallow */ }
    public void OnDrag(PointerEventData e) { /* swallow */ }
    public void OnEndDrag(PointerEventData e) { /* swallow */ }
    public void OnScroll(PointerEventData e) { /* swallow */ }
}
