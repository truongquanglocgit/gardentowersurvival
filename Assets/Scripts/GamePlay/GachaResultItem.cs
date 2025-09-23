using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GachaResultItem : MonoBehaviour
{
    [Header("UI Refs")]
    public Image iconImage;
    public TMP_Text nameText;

    [Header("Reusable Background")]
    public TowerRarityPanelBackground rarityBg; // drag component này

    public void Bind(Sprite icon, string displayName, Rarity rarity)
    {
        if (iconImage)
        {
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            iconImage.type = Image.Type.Simple;
        }
        if (nameText) nameText.text = displayName ?? string.Empty;

        if (rarityBg) rarityBg.Apply(rarity);
    }
}
