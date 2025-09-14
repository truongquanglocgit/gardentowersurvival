using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(GridLayoutGroup))]
public class SquareCellFitter : MonoBehaviour
{
    private GridLayoutGroup grid;

    void Awake()
    {
        grid = GetComponent<GridLayoutGroup>();
    }

    void Update()
    {
        float parentWidth = ((RectTransform)transform).rect.width;

        // số cột mà bạn set trong GridLayoutGroup
        int constraintCount = grid.constraintCount;

        // trừ khoảng cách padding + spacing
        float totalSpacing = grid.spacing.x * (constraintCount - 1) + grid.padding.left + grid.padding.right;

        float cellSize = (parentWidth - totalSpacing) / constraintCount;

        // gán lại size ô (vuông)
        grid.cellSize = new Vector2(cellSize, cellSize);
    }
}
