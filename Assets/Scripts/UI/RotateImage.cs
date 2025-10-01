using UnityEngine;
using UnityEngine.UI;

public class RotateImage : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private float rotationSpeed = 180f; // độ/giây
    [SerializeField] private bool clockwise = true;

    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        // Reset góc xoay nếu muốn về 0 mỗi lần bật
        // rectTransform.localRotation = Quaternion.identity;
    }

    private void Update()
    {
        float direction = clockwise ? -1f : 1f;
        rectTransform.Rotate(0f, 0f, rotationSpeed * direction * Time.deltaTime);
    }
}
