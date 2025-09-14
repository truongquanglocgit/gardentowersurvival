using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("Map Selection")]
    public List<MapData> maps;
    public Image mapPreviewImage;
    public TextMeshProUGUI mapNameText;
    public Button upMapButton;
    public Button downMapButton;
    private int currentMapIndex = 0;

    [Header("Panel References")]
    public GameObject characterPanel;
    public GameObject growPlanPanel;
    public GameObject gamemodePanel;
    public GameObject shopPanel;

    [Header("Map Load")]
    public Button startGameButton;

    void Start()
    {
        UpdateMapUI();

        upMapButton.onClick.AddListener(() => ChangeMap(1));
        downMapButton.onClick.AddListener(() => ChangeMap(-1));

        CloseAllPanels();

        if (startGameButton != null)
            startGameButton.onClick.AddListener(LoadSelectedMap);
    }

    void ChangeMap(int delta)
    {
        currentMapIndex = Mathf.Clamp(currentMapIndex + delta, 0, maps.Count - 1);
        UpdateMapUI();
    }

    void UpdateMapUI()
    {
        if (maps == null || maps.Count == 0) return;
        var currentMap = maps[currentMapIndex];
        if (mapPreviewImage) mapPreviewImage.sprite = currentMap.previewImage;
        if (mapNameText) mapNameText.text = currentMap.mapName;
    }

    public void LoadSelectedMap()
    {
        var selectedMap = maps[currentMapIndex];

        // Lưu lựa chọn vào GameSession (nếu bạn cần ở scene sau)
        if (GameSession.Instance)
        {
            GameSession.Instance.currentMapData = selectedMap;
            GameSession.Instance.selectedWaveList = selectedMap.waveList;
        }

        // *** NHẢY VÀO LOADING TRƯỚC ***
        LoaderBridge.LoadWithLoadingScreen(selectedMap.sceneToLoad, "Loading");
    }

    // Nếu bạn dùng OnClickPlay riêng:
    void OnClickPlay()
    {
        LoadSelectedMap(); // gom về một chỗ
    }

    // UI Button Events
    public void OpenCharacterPanel() => TogglePanel(characterPanel);
    public void OpenGrowPlanPanel() => TogglePanel(growPlanPanel);
    public void OpenGamemodePanel() => TogglePanel(gamemodePanel);
    public void OpenShopPanel() => TogglePanel(shopPanel);

    void TogglePanel(GameObject panel)
    {
        CloseAllPanels();
        if (panel) panel.SetActive(true);
    }

    void CloseAllPanels()
    {
        // nếu muốn đóng hết panel khi vào menu:
        // if (characterPanel) characterPanel.SetActive(false);
        // if (growPlanPanel)  growPlanPanel.SetActive(false);
        // if (gamemodePanel)  gamemodePanel.SetActive(false);
        // if (shopPanel)      shopPanel.SetActive(false);
    }
}
