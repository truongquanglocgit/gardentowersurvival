using UnityEngine;

public class ArrowBobbing : MonoBehaviour
{
    [Header("Targets")]
    public Transform bobTarget;   // nhún theo local Y
    public Transform spinTarget;  // xoay quanh Y

    [Header("Spin (around Y)")]
    public float spinDegreesPerSecond = 90f;

    [Header("Bob (local Y)")]
    public float bobAmplitude = 0.25f;
    public float bobFrequency = 1.5f;

    Vector3 _baseLocalPos;
    Quaternion _baseSpinRot;
    float _spinAngle;

    void OnEnable()
    {
        if (!bobTarget) bobTarget = transform;
        if (!spinTarget) spinTarget = transform;

        _baseLocalPos = bobTarget.localPosition;
        _baseSpinRot = spinTarget.localRotation; // lưu rotation gốc
        _spinAngle = 0f;
    }

    void LateUpdate()
    {
        // Bob theo local Y
        float t = Time.unscaledTime;
        float bob = Mathf.Sin(t * Mathf.PI * 2f * bobFrequency) * bobAmplitude;
        bobTarget.localPosition = new Vector3(_baseLocalPos.x, _baseLocalPos.y + bob, _baseLocalPos.z);

        // Spin tuyệt đối quanh trục Y
        _spinAngle += spinDegreesPerSecond * Time.unscaledDeltaTime;
        spinTarget.localRotation = _baseSpinRot * Quaternion.Euler(0f, _spinAngle, 0f);
        //            ^ giữ nguyên X/Z, chỉ thay đổi Y
    }
}
