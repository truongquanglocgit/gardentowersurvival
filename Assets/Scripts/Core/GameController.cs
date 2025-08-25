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

    void Start()
    {
        

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



    public void TryAgain()
    {
        Time.timeScale = 1f; // 🔁 Khôi phục tốc độ game nếu đang bị dừng
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex); // 🔄 Load lại scene hiện tại
    }
}

