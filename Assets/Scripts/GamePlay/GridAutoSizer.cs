using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(GridLayoutGroup))]
public class GridAutoSizer : MonoBehaviour
{
    public RectTransform viewport;  // kéo Viewport vào
    public int columns = 5;

    void OnEnable() { Resize(); }
    void OnRectTransformDimensionsChange() { Resize(); }

    void Resize()
    {
        if (!viewport) return;
        var grid = GetComponent<GridLayoutGroup>();

        float totalSpacing = (columns - 1) * grid.spacing.x + grid.padding.left + grid.padding.right;
        float cellWidth = Mathf.Floor((viewport.rect.width - totalSpacing) / columns);

        grid.cellSize = new Vector2(cellWidth, grid.cellSize.y > 0 ? cellWidth : cellWidth); // vuông
    }
}
