using UnityEngine;

/// <summary>
/// Quản lý Shop Panel: Bật / Tắt / Toggle
/// Gắn script này trực tiếp vào GameObject Panel trong Canvas.
/// </summary>
public class ShopPanel : MonoBehaviour
{
    void Start()
    {
        // Ẩn Panel Gacha khi mới bắt đầu
        gameObject.SetActive(false);
    }
    /// <summary>
    /// Bật panel (nếu chưa bật).
    /// </summary>
    public void ShowPanel()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
    }

    /// <summary>
    /// Tắt panel (nếu đang bật).
    /// </summary>
    public void HidePanel()
    {
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    /// <summary>
    /// Đảo trạng thái bật/tắt panel.
    /// </summary>
    public void TogglePanel()
    {
        gameObject.SetActive(!gameObject.activeSelf);
    }
}
