using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class UIVirtualLook : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [System.Serializable] public class Vector2Event : UnityEvent<Vector2> { }
    [System.Serializable] public class FloatEvent : UnityEvent<float> { }

    [Header("Output")]
    public Vector2Event onLookDelta;     // gửi (deltaX, deltaY) cho Player
    public FloatEvent onPinchDelta;    // gửi (+/-) cho zoom (đơn vị pixel khoảng cách 2 ngón)

    [Header("Sensitivity")]
    public float touchSensitivity = 0.1f; // yaw/pitch = delta * sensitivity
    public float dragTimeout = 0.05f;     // auto reset nếu ngừng kéo (ms)
    public float threshold = 0.1f;        // bỏ vi sai quá nhỏ

    // Track tối đa 2 pointer trong panel này
    private readonly Dictionary<int, Vector2> _lastPos = new();
    private readonly List<int> _activeIds = new(2);

    private float _lastDragTime;
    private bool _sendingZero;

    void Update()
    {
        // Nếu không kéo nữa -> trả (0,0) 1 lần để Player dừng xoay mượt
        if (_activeIds.Count == 0 && !_sendingZero && Time.time - _lastDragTime > dragTimeout)
        {
            _sendingZero = true;
            onLookDelta?.Invoke(Vector2.zero);
        }
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (!_lastPos.ContainsKey(e.pointerId))
        {
            _lastPos[e.pointerId] = e.position;
            if (_activeIds.Count < 2) _activeIds.Add(e.pointerId);
        }
        _lastDragTime = Time.time;
        _sendingZero = false;
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (_lastPos.ContainsKey(e.pointerId)) _lastPos.Remove(e.pointerId);
        _activeIds.Remove(e.pointerId);

        // Khi rời tay, chủ động phát (0,0)
        onLookDelta?.Invoke(Vector2.zero);

        // Nếu còn 1 ngón → từ pinch về look; nếu 0 → idle
        _lastDragTime = Time.time;
        _sendingZero = false;
    }

    public void OnDrag(PointerEventData e)
    {
        if (!_lastPos.ContainsKey(e.pointerId)) return;

        _lastDragTime = Time.time;
        _sendingZero = false;

        // Cập nhật delta của pointer này
        Vector2 prev = _lastPos[e.pointerId];
        Vector2 curr = e.position;
        _lastPos[e.pointerId] = curr;

        int count = Mathf.Min(_activeIds.Count, 2);

        if (count >= 2)
        {
            // ----- PINCH (chỉ khi 2 ngón đều trong panel này) -----
            int idA = _activeIds[0];
            int idB = _activeIds[1];
            if (!_lastPos.ContainsKey(idA) || !_lastPos.ContainsKey(idB)) return;

            // Khoảng cách frame trước & hiện tại
            Vector2 prevA = prev; // prev của pointer đang drag
            Vector2 currA = curr;

            // Lấy pos còn lại
            int otherId = (e.pointerId == idA) ? idB : idA;
            // Vì không có prev của other ngay trong event này, dùng _lastPos (curr) và ước lượng prev = curr - delta (ổn vì frame liền kề)
            Vector2 currB = _lastPos[otherId];
            // Không có delta chính xác của other → bỏ qua vi sai nhỏ bằng threshold để chống nhiễu
            float prevDist = (prevA - currB).magnitude;
            float currDist = (currA - currB).magnitude;
            float diff = currDist - prevDist;

            if (Mathf.Abs(diff) > threshold)
                onPinchDelta?.Invoke(diff); // dương = nới xa, âm = thu gần
        }
        else if (count == 1)
        {
            // ----- LOOK (1 ngón trong panel này) -----
            if (e.delta.sqrMagnitude > threshold * threshold)
            {
                float dx = e.delta.x * touchSensitivity;
                float dy = -e.delta.y * touchSensitivity; // đảo trục Y cho cảm giác kéo lên = nhìn lên
                onLookDelta?.Invoke(new Vector2(dx, dy));
            }
        }
    }
}
