using UnityEngine;
using TMPro;
using System.Collections;

public class TowerPlacer : MonoBehaviour
{
    public static TowerPlacer I;

    [Header("Config")]
    public LayerMask groundMask;
    public Material ghostMaterial;
    public TextMeshProUGUI notEnoughSeedText;

    private TowerData currentTower;
    private GameObject previewTower;
    private bool isPlacing = false;
    //private bool warnedInsufficientSeed = false;
    private Coroutine warningCoroutine;

    void Awake()
    {
        I = this;
        Debug.Log("✅ TowerPlacer Awake – Singleton set.");
        if (notEnoughSeedText != null)
            notEnoughSeedText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!isPlacing || previewTower == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundMask))
        {
            previewTower.transform.position = hit.point;

            if (Input.GetMouseButtonDown(0))
            {
                PlaceTower(hit.point);
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelPlacement();
        }
    }

    public void SetCurrentTower(TowerData data)
    {
        //Debug.Log("🔧 SetCurrentTower called: " + data.name);

        currentTower = data;
        StartPlacing();
    }

    void StartPlacing()
    {
        CancelPlacement();

        if (currentTower == null || currentTower.prefab == null) return;

        previewTower = Instantiate(currentTower.prefab);
        previewTower.name = "Preview_" + currentTower.name;

        MakeGhost(previewTower);
        isPlacing = true;
    }

    void PlaceTower(Vector3 pos)
    {
        if (currentTower == null || currentTower.prefab == null) return;

        TowerController towerCtrl = currentTower.prefab.GetComponent<TowerController>();
        if (towerCtrl == null) return;

        int cost = Mathf.RoundToInt(towerCtrl.seedCost);
        GameController gameController = FindObjectOfType<GameController>();

        if (gameController.Seed < cost)
        {
            ShowSeedWarning($"Not enough Seed! Need {cost}");
            return;
        }

        if (!gameController.CanPlaceTower())
        {
            ShowSeedWarning("🚫 Max towers placed!");
            return;
        }

        // ✅ Place
        gameController.Seed -= cost;
        pos.y += 0.2f;
        Instantiate(currentTower.prefab, pos, Quaternion.identity);
        gameController.UpdateTowerCount();

        CancelPlacement();
    }

    void CancelPlacement()
    {
        if (previewTower != null)
            Destroy(previewTower);

        isPlacing = false;
    }

    void MakeGhost(GameObject obj)
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            Vector3 offset = player.transform.forward * 2f;
            obj.transform.position = player.transform.position + offset;
            obj.transform.rotation = Quaternion.identity;
        }

        foreach (var r in obj.GetComponentsInChildren<Renderer>())
            r.material = ghostMaterial;

        obj.layer = LayerMask.NameToLayer("Ignore Raycast");

        TowerController ctrl = obj.GetComponent<TowerController>();
        if (ctrl != null) ctrl.enabled = false;

        foreach (var col in obj.GetComponentsInChildren<Collider>())
            col.enabled = false;

        obj.tag = "Untagged";
    }

    void ShowSeedWarning(string message)
    {
        if (notEnoughSeedText == null) return;

        notEnoughSeedText.text = message;
        if (warningCoroutine != null) StopCoroutine(warningCoroutine);
        warningCoroutine = StartCoroutine(SeedWarningAnimation());
    }

    IEnumerator SeedWarningAnimation()
    {
        notEnoughSeedText.gameObject.SetActive(true);
        notEnoughSeedText.alpha = 1f;

        Vector3 originalPos = notEnoughSeedText.rectTransform.localPosition;

        // Shake
        for (int i = 0; i < 8; i++)
        {
            float offset = (i % 2 == 0 ? 1 : -1) * 8f;
            notEnoughSeedText.rectTransform.localPosition = originalPos + new Vector3(offset, 0, 0);
            yield return new WaitForSeconds(0.03f);
        }

        notEnoughSeedText.rectTransform.localPosition = originalPos;

        // Fade out
        float duration = 0.8f;
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            notEnoughSeedText.alpha = Mathf.Lerp(1f, 0f, timer / duration);
            yield return null;
        }

        notEnoughSeedText.gameObject.SetActive(false);
    }
}
