using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("Map Selection")]
    public List<MapData> maps;
    public Image mapPreviewImage; // hoặc RawImage nếu bạn dùng RawImage
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
    public Button startGameButton; // nút "Play" hoặc "Start"

    void Start()
    {
        UpdateMapUI();

        // Gắn sự kiện chuyển map
        upMapButton.onClick.AddListener(() => ChangeMap(1));
        downMapButton.onClick.AddListener(() => ChangeMap(-1));

        // Đóng toàn bộ panel phụ khi vào menu
        CloseAllPanels();

        // Nếu có nút chơi → gắn event load scene
        if (startGameButton != null)
            startGameButton.onClick.AddListener(LoadSelectedMap);
    }

    void ChangeMap(int delta)
    {
        currentMapIndex += delta;
        currentMapIndex = Mathf.Clamp(currentMapIndex, 0, maps.Count - 1);
        UpdateMapUI();
    }

    void UpdateMapUI()
    {
        if (maps == null || maps.Count == 0) return;

        MapData currentMap = maps[currentMapIndex];
        mapPreviewImage.sprite = currentMap.previewImage;
        mapNameText.text = currentMap.mapName;
    }

    public void LoadSelectedMap()
    {
        string sceneName = maps[currentMapIndex].sceneToLoad;

        SceneManager.LoadScene(sceneName);
    }

    void OnClickPlay()
    {
        var selectedMap = maps[currentMapIndex];
        GameSession.Instance.currentMapData = selectedMap;
        GameSession.Instance.selectedWaveList = selectedMap.waveList;

        SceneManager.LoadScene(selectedMap.sceneToLoad);
    }


    // UI Button Events
    public void OpenCharacterPanel() => TogglePanel(characterPanel);
    public void OpenGrowPlanPanel() => TogglePanel(growPlanPanel);
    public void OpenGamemodePanel() => TogglePanel(gamemodePanel);
    public void OpenShopPanel() => TogglePanel(shopPanel);

    void TogglePanel(GameObject panel)
    {
        CloseAllPanels();
        panel.SetActive(true);
    }

    void CloseAllPanels()
    {
        //characterPanel.SetActive(false);
        //growPlanPanel.SetActive(false);
        //gamemodePanel.SetActive(false);
        //shopPanel.SetActive(false);
    }
}
