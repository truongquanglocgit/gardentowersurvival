using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;

public class TowerPlacer : MonoBehaviour
{
    public static TowerPlacer I;

    [Header("Config")]
    public LayerMask groundMask;
    public Material ghostMaterial;
    public Material rangeRingMaterial;   // optional, để trống sẽ dùng Sprites/Default
    public float previewDistance = 2.0f; // khoảng cách trước mặt player
    public float ringWidth = 0.05f;
    public int ringSegments = 64;
    public float ringYOffset = 0.03f;

    [Header("UI")]
    public TextMeshProUGUI notEnoughSeedText;
    public Button cancelPlacement;
    public Button placeButton;
    public TextMeshProUGUI placeButtonText;

    private TowerData currentTower;
    private GameObject previewTower;
    private LineRenderer rangeRing;
    private bool isPlacing = false;
    private Coroutine warningCoroutine;

    Transform player;

    void Awake()
    {
        I = this;

        var p = GameObject.FindWithTag("Player");
        if (p) player = p.transform;

        if (cancelPlacement) { cancelPlacement.gameObject.SetActive(false); cancelPlacement.onClick.AddListener(CancelPlacement); }
        if (placeButton)
        {
            placeButton.gameObject.SetActive(false);
            placeButton.onClick.AddListener(PlaceFromPreview);
        }
        if (notEnoughSeedText) notEnoughSeedText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!isPlacing || previewTower == null) return;

        // Preview KHÔNG theo chuột — bám trước mặt player & dán xuống ground
        FollowPlayerAnchor();

        // Cập nhật trạng thái nút Place (đủ tiền/chưa max)
        UpdatePlaceButtonState();
    }

    public void SetCurrentTower(TowerData data)
    {
        currentTower = data;
        StartPlacing();
    }

    void StartPlacing()
    {
        CancelPlacement(); // clear phiên trước
        if (currentTower == null || currentTower.prefab == null) return;

        if (cancelPlacement) cancelPlacement.gameObject.SetActive(true);
        if (placeButton) placeButton.gameObject.SetActive(true);

        // tạo preview
        previewTower = Instantiate(currentTower.prefab);
        previewTower.name = "Preview_" + currentTower.name;
        MakeGhost(previewTower);
        CreateOrUpdateRangeRing();

        isPlacing = true;
        UpdatePlaceButtonState();
    }

    void PlaceFromPreview()
    {
        if (!isPlacing || previewTower == null) return;
        Vector3 pos = GetGroundPoint(previewTower.transform.position);
        PlaceTower(pos);
    }

    void PlaceTower(Vector3 pos)
    {
        if (currentTower == null || currentTower.prefab == null) return;

        // lấy cost từ loại controller nào có
        int cost = GetSeedCost(currentTower.prefab);
        GameController gc = FindObjectOfType<GameController>();

        if (gc.Seed < cost)
        {
            ShowSeedWarning($"Not enough Seed! Need {cost}");
            return;
        }
        if (!gc.CanPlaceTower())
        {
            ShowSeedWarning("🚫 Max towers placed!");
            return;
        }

        // ✅ Place
        gc.Seed -= cost;
        pos.y += 0.2f;
        Instantiate(currentTower.prefab, pos, Quaternion.identity);
        gc.UpdateTowerCount();

        CancelPlacement();
    }

    public void CancelPlacement()
    {
        if (previewTower) Destroy(previewTower);
        if (rangeRing) Destroy(rangeRing.gameObject);
        if (cancelPlacement) cancelPlacement.gameObject.SetActive(false);
        if (placeButton) placeButton.gameObject.SetActive(false);
        isPlacing = false;
    }

    void MakeGhost(GameObject obj)
    {
        FollowPlayerAnchor(obj.transform);

        // đổi material ghost
        foreach (var r in obj.GetComponentsInChildren<Renderer>())
            r.material = ghostMaterial;

        // tránh cản ray/tap
        obj.layer = LayerMask.NameToLayer("Ignore Raycast");

        // tắt logic chiến đấu của tower ghost (cả range lẫn melee)
        var ranged = obj.GetComponent<TowerController>();
        if (ranged) ranged.enabled = false;
        var melee = obj.GetComponent<MeleeTowerController>();
        if (melee) melee.enabled = false;

        foreach (var col in obj.GetComponentsInChildren<Collider>())
            col.enabled = false;

        obj.tag = "Untagged";
    }

    void FollowPlayerAnchor(Transform t = null)
    {
        if (!player) return;
        if (t == null) t = previewTower.transform;

        Vector3 target = player.position + player.forward * previewDistance;
        t.position = GetGroundPoint(target);
        t.rotation = Quaternion.LookRotation(player.forward, Vector3.up);
    }

    Vector3 GetGroundPoint(Vector3 around)
    {
        Vector3 origin = around + Vector3.up * 5f;
        if (Physics.Raycast(origin, Vector3.down, out var hit, 20f, groundMask))
            return hit.point + Vector3.up * 0.01f;
        return around;
    }

    void CreateOrUpdateRangeRing()
    {
        if (!previewTower) return;

        float radius = GetPreviewRange(previewTower); // đọc range từ controller phù hợp

        if (rangeRing) Destroy(rangeRing.gameObject);

        var go = new GameObject("RangeRing");
        go.transform.SetParent(previewTower.transform, false);
        go.transform.localPosition = new Vector3(0f, ringYOffset, 0f);

        rangeRing = go.AddComponent<LineRenderer>();
        rangeRing.loop = true;
        rangeRing.useWorldSpace = false;
        rangeRing.alignment = LineAlignment.View;
        rangeRing.startWidth = ringWidth;
        rangeRing.endWidth = ringWidth;
        rangeRing.numCapVertices = 4;
        rangeRing.numCornerVertices = 4;
        rangeRing.material = rangeRingMaterial != null ? rangeRingMaterial : new Material(Shader.Find("Sprites/Default"));

        int n = Mathf.Max(16, ringSegments);
        rangeRing.positionCount = n;
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            rangeRing.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
        }
    }

    void UpdatePlaceButtonState()
    {
        if (!placeButton) return;

        bool interact = false;
        string label = "Place";

        var gc = FindObjectOfType<GameController>();
        if (currentTower && currentTower.prefab && gc)
        {
            int cost = GetSeedCost(currentTower.prefab);
            bool enoughSeed = gc.Seed >= cost;
            bool canPlace = gc.CanPlaceTower();

            interact = enoughSeed && canPlace;
            if (placeButtonText)
                label = enoughSeed ? $"Place (-{cost})" : $"Need {cost}";
        }

        placeButton.interactable = interact;
        if (placeButtonText) placeButtonText.text = label;
    }

    // ===== Helpers: hỗ trợ cả melee và ranged =====
    int GetSeedCost(GameObject prefab)
    {
        var r = prefab.GetComponent<TowerController>();
        if (r) return Mathf.RoundToInt(r.seedCost);
        var m = prefab.GetComponent<MeleeTowerController>();
        if (m) return Mathf.RoundToInt(m.seedCost);
        return 0;
    }

    float GetPreviewRange(GameObject obj)
    {
        // ưu tiên GetRangeAtLevel(1) nếu có, fallback CurrentRange
        var r = obj.GetComponent<TowerController>();
        if (r)
        {
            try { return r.GetRangeAtLevel(1); } catch { return Mathf.Max(1f, r.CurrentRange); }
        }
        var m = obj.GetComponent<MeleeTowerController>();
        if (m)
        {
            try { return m.GetRangeAtLevel(1); } catch { return Mathf.Max(1f, m.CurrentRange); }
        }
        return 2f;
    }

    // ===== UI warning =====
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

        for (int i = 0; i < 8; i++)
        {
            float offset = (i % 2 == 0 ? 1 : -1) * 8f;
            notEnoughSeedText.rectTransform.localPosition = originalPos + new Vector3(offset, 0, 0);
            yield return new WaitForSeconds(0.03f);
        }

        notEnoughSeedText.rectTransform.localPosition = originalPos;

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
