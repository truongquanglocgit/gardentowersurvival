using UnityEngine;
using UnityEngine.EventSystems;

public class TowerClickable : MonoBehaviour
{
    [SerializeField] private TowerController tower;

    private void Reset()
    {
        if (!tower) tower = GetComponent<TowerController>();
    }

    void Update()
    {
        // Bỏ qua khi đang chạm UI
        if (EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return;

        // Touch (mobile)
        if (Input.touchCount > 0 && Input.touches[0].phase == TouchPhase.Ended)
        {
            var t = Input.touches[0];
            TryClickAt(t.position);
        }

        // Chuột (editor)
        if (Input.GetMouseButtonUp(0))
        {
            TryClickAt(Input.mousePosition);
        }
    }

    void TryClickAt(Vector2 screenPos)
    {
        var cam = Camera.main;
        if (!cam) return;

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
        {
            if (hit.collider && hit.collider.gameObject == gameObject)
            {
                TowerUpgradeUI.Instance.Show(tower);
            }
        }
    }
}
