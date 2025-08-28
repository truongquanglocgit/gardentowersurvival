using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour
{
    public WaveManager waveManager;
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

    public bool TrySpendSeed(int amount)
    {
        if (Seed >= amount)
        {
            Seed -= amount;
            // TODO: cập nhật HUD seed
            return true;
        }
        return false;
    }

    public void AddSeed(int amount)
    {
        Seed += amount;
        // TODO: cập nhật HUD seed
    }
    void Start()
    {

        UpdateTowerCount();
        StartCoroutine(waveManager.PlayWave());
        dieCanvas.SetActive(false);
        if (player != null && playerSpawnPoint != null)
        {
            player.position = playerSpawnPoint.position;
            player.rotation = playerSpawnPoint.rotation; // nếu cần quay đúng hướng
        }

        // Gán player vào camera follow
        Camera.main.GetComponent<CameraOrbit>().target = player.transform;
    }
    

    void Update()
    {
        seedText.text = $"Seed: {Seed}";
    }

    internal void die()
    {
        Time.timeScale = 0;
        Debug.Log("die");

        if (dieCanvas != null)
        {
            dieCanvas.SetActive(true);
            Debug.Log("Canvas enabled");
        }
        else
        {
            Debug.LogError("❌ dieCanvas is NULL. Check inspector reference!");
        }
    }


    public void addSeed( int seed)
    {
        Seed += seed;
    }
    public void TryAgain()
    {
        Time.timeScale = 1f; // 🔁 Khôi phục tốc độ game nếu đang bị dừng
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex); // 🔄 Load lại scene hiện tại
    }
    public void UpdateTowerCount()
    {
        GameObject[] towers = GameObject.FindGameObjectsWithTag("Tower");
        currentTowerCount = 0;

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

