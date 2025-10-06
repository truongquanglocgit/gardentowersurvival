using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum RewardType
{
    Water,
    Rainbow,
    XP,
    Plant,
    Custom
}

public class RewardItemUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;

    [Header("Colors")]
    [SerializeField] private Color waterColor = Color.white;
    [SerializeField] private Color xpColor = new(0.4f, 0.7f, 1f);        // xanh dương
    [SerializeField] private Color plantColor = new(0.3f, 1f, 0.3f);     // xanh lá
    [SerializeField]
    private VertexGradient rainbowGradient = new(
        new Color(1f, 0.4f, 0.4f),   // top-left
        new Color(1f, 0.8f, 0.4f),   // top-right
        new Color(0.4f, 0.8f, 1f),   // bottom-left
        new Color(0.8f, 0.4f, 1f));  // bottom-right

    public void Set(Sprite sp, string text, RewardType type = RewardType.Custom)
    {
        if (icon) icon.sprite = sp;
        if (label)
        {
            label.text = text;
            ApplyColor(type);
        }
    }

    private void ApplyColor(RewardType type)
    {
        if (label == null) return;

        switch (type)
        {
            case RewardType.Water:
                label.colorGradientPreset = null; // remove TMP preset gradient
                label.color = waterColor;
                label.enableVertexGradient = false;
                break;

            case RewardType.Rainbow:
                label.enableVertexGradient = true;
                label.colorGradient = rainbowGradient;
                break;

            case RewardType.XP:
                label.colorGradientPreset = null;
                label.enableVertexGradient = false;
                label.color = xpColor;
                break;

            case RewardType.Plant:
                label.colorGradientPreset = null;
                label.enableVertexGradient = false;
                label.color = plantColor;
                break;

            default:
                label.colorGradientPreset = null;
                label.enableVertexGradient = false;
                label.color = Color.white;
                break;
        }
    }
}
