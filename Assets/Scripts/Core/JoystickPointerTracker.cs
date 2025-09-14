using UnityEngine;
using UnityEngine.EventSystems;

public class JoystickPointerTracker : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public int ActiveFingerId { get; private set; } = -1;
    public void OnPointerDown(PointerEventData e) { ActiveFingerId = e.pointerId; }
    public void OnPointerUp(PointerEventData e) { if (e.pointerId == ActiveFingerId) ActiveFingerId = -1; }
}
