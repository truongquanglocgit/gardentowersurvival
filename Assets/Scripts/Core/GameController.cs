using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour
{
    public WaveManager waveManager;
    public TowerLoadoutManager towerManager;
    public GameObject dieCanvas;

    [SerializeField] private Transform player;
    [SerializeField] private Transform playerSpawnPoint;
    private TowerSelectUI towerSelectUI;

    public TMPro.TextMeshProUGUI seedText;
    public int Seed = 100;

    [Header("Tower Limit")]
    public int maxTowerCount = 20;
    public TMPro.TextMeshProUGUI towerCountText;
    [HideInInspector] public int currentTowerCount;

    public static GameController Instance;

    void Awake() => Instance = this;

    void Start()
    {
        dieCanvas.SetActive(false);

        // ✅ Load wave từ GameSession
        var waveList = GameSession.Instance.selectedWaveList;

        // ✅ Load tower đã equip từ PlayerData thông qua AllTowerDatabase
        towerManager.LoadEquippedFromPlayerData(GameSession.Instance.allTowerDatabase.allTowers);

        // ✅ Gán vào UI
        towerSelectUI = FindObjectOfType<TowerSelectUI>();
        if (towerSelectUI != null)
            towerSelectUI.InitTowerButtons(towerManager.equippedTowers);
        else
            Debug.LogWarning("TowerSelectUI not found");

        // ✅ Load waves
        waveManager.LoadWaves(waveList);
        StartCoroutine(waveManager.PlayWaves());

        // ✅ Set player position
        if (player != null && playerSpawnPoint != null)
        {
            player.position = playerSpawnPoint.position;
            player.rotation = playerSpawnPoint.rotation;
        }

        // ✅ Set camera follow
        Camera.main.GetComponent<CameraOrbit>().target = player.transform;
    }

    void Update()
    {
        seedText.text = $"Seed: {Seed}";
    }

    public bool TrySpendSeed(int amount)
    {
        if (Seed >= amount)
        {
            Seed -= amount;
            return true;
        }
        return false;
    }

    public void AddSeed(int amount) => Seed += amount;

    public void die()
    {
        Time.timeScale = 0;
        Debug.Log(" Player Died");

        if (dieCanvas != null)
        {
            dieCanvas.SetActive(true);
        }
        else
        {
            Debug.LogError(" dieCanvas is NULL. Check inspector reference!");
        }
    }

    public void TryAgain()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void UpdateTowerCount()
    {
        currentTowerCount = 0;
        GameObject[] towers = GameObject.FindGameObjectsWithTag("Tower");

        foreach (var tower in towers)
        {
            if (tower.activeInHierarchy)
                currentTowerCount++;
        }

        if (towerCountText != null)
            towerCountText.text = $"Tower: {currentTowerCount} / {maxTowerCount}";
    }

    public bool CanPlaceTower()
    {
        return currentTowerCount < maxTowerCount;
    }
}
