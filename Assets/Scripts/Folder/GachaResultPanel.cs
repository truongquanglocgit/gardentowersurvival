using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class GachaResultPanel : MonoBehaviour
{
    public GameObject resultItemPrefab;
    public Transform resultContainer;
    public Button closeButton;

    void Start()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));
    }

    public void ShowResults(List<TowerData> towers)
    {
        // Clear cũ
        foreach (Transform child in resultContainer)
            Destroy(child.gameObject);

        // Spawn mới
        foreach (var tower in towers)
        {
            var go = Instantiate(resultItemPrefab, resultContainer);
            var img = go.GetComponentInChildren<Image>();
            var text = go.GetComponentInChildren<TMP_Text>();

            if (img != null) img.sprite = tower.icon;
            if (text != null) text.text = tower.displayName;
        }

        gameObject.SetActive(true); // Hiện panel
    }
}
