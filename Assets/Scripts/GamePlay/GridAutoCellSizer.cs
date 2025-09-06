using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(GridLayoutGroup))]
public class GridAutoCellSizer : MonoBehaviour
{
    [Header("Layout")]
    public int columns = 5;                 // số cột cố định
    public float aspect = 1f;               // tỉ lệ width/height; =1 là ô vuông
    public Vector2 minMaxCell = new Vector2(64, 9999); // chặn dưới/trên nếu cần

    GridLayoutGroup grid;
    RectTransform rt;

    void Awake()
    {
        grid = GetComponent<GridLayoutGroup>();
        rt = GetComponent<RectTransform>();

        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
    }

    void OnEnable()
    {
        // Đợi 1 frame để RectTransform có kích thước chính xác rồi mới tính
        StartCoroutine(DelayedRecalc());
    }

    IEnumerator DelayedRecalc()
    {
        yield return null;
        Recalc();
    }

    void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled) Recalc();
    }

    public void Recalc()
    {
        float totalWidth = rt.rect.width;

        // Nếu width chưa sẵn sàng (có thể =0 ở Start), bỏ qua
        if (totalWidth <= 0f) return;

        float paddingLR = grid.padding.left + grid.padding.right;
        float spacingTotal = grid.spacing.x * (columns - 1);
        float available = Mathf.Max(0, totalWidth - paddingLR - spacingTotal);

        float cellW = Mathf.Floor(available / columns);
        cellW = Mathf.Clamp(cellW, minMaxCell.x, minMaxCell.y);

        float cellH = (aspect <= 0f) ? cellW : cellW / aspect; // ô vuông khi aspect=1
        grid.cellSize = new Vector2(cellW, cellH);
    }
}
