using UnityEngine;

public class TowerPlacer : MonoBehaviour
{
    public static TowerPlacer I;

    [Header("Config")]
    public LayerMask groundMask;
    public Material ghostMaterial;

    private TowerData currentTower;
    private GameObject previewTower;
    private bool isPlacing = false;

    void Awake()
    {
        I = this;
        Debug.Log("✅ TowerPlacer Awake – Singleton set.");
    }

    void Update()
    {
        if (!isPlacing || previewTower == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundMask))
        {
            previewTower.transform.position = hit.point;
            Debug.Log($"🟨 Moving ghost to: {hit.point}");

            if (Input.GetMouseButtonDown(0))
            {
                Debug.Log("🟩 Click to place");
                PlaceTower(hit.point);
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("❌ Cancel placement");
            CancelPlacement();
        }
    }


    public void SetCurrentTower(TowerData data)
    {
        Debug.Log("🔧 SetCurrentTower called: " + data.name);

        currentTower = data;
        StartPlacing();
    }

    void StartPlacing()
    {
        CancelPlacement();

        if (currentTower == null || currentTower.prefab == null)
        {
            
            return;
        }

        previewTower = Instantiate(currentTower.prefab);
        previewTower.name = "Preview_" + currentTower.name;
        MakeGhost(previewTower);
        isPlacing = true;

        
    }

    void PlaceTower(Vector3 pos)
    {
        // Nâng vị trí đặt tower lên một chút (ví dụ 0.2f)
        pos.y += 0.2f;

        Instantiate(currentTower.prefab, pos, Quaternion.identity);
        CancelPlacement();
        
    }


    void CancelPlacement()
    {
        if (previewTower != null)
        {
            Destroy(previewTower);
            Debug.Log("🗑️ Destroyed preview.");
        }
        isPlacing = false;
    }

    void MakeGhost(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            r.material = ghostMaterial;
        }

        obj.layer = LayerMask.NameToLayer("Ignore Raycast"); // Đảm bảo không chặn ray

        Debug.Log("🎨 Ghost material applied");
    }
}
