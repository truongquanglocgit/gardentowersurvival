using UnityEngine;
using System.IO;
using System.Collections.Generic;
using TMPro;

public class PlayerDataManager : MonoBehaviour
{
    public static PlayerDataManager Instance;
    public PlayerData playerData = new();
    public TextMeshProUGUI levelText;
    private string SavePath => Application.persistentDataPath + "/player_data.json";

    void Awake()
    {
        
        
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);            // ✅ bền qua scene

        Debug.Log("📂 Save path: " + Application.persistentDataPath);
        LoadPlayerData();

        // An toàn: khởi tạo list nếu null (save cũ)
        if (playerData.unlockedTowerIds == null) playerData.unlockedTowerIds = new List<string>();
        if (playerData.equippedTowerIds == null) playerData.equippedTowerIds = new List<string>();
        if (playerData.completedMapIds == null) playerData.completedMapIds = new List<string>();
    }

    public void SavePlayerData()
    {
        string json = JsonUtility.ToJson(playerData, true);
        File.WriteAllText(SavePath, json);
        //Debug.Log("💾 PlayerData saved to: " + SavePath);
    }

    public void LoadPlayerData()
    {
        if (File.Exists(SavePath))
        {
            string json = File.ReadAllText(SavePath);
            playerData = JsonUtility.FromJson<PlayerData>(json);

            // ✅ Khởi tạo an toàn nếu save cũ chưa có field mới
            if (playerData.unlockedTowerIds == null) playerData.unlockedTowerIds = new List<string>();
            if (playerData.equippedTowerIds == null) playerData.equippedTowerIds = new List<string>();
            if (playerData.completedMapIds == null) playerData.completedMapIds = new List<string>();
        }
        else
        {
            Debug.Log("🆕 No existing player data. Creating default test data.");

            playerData = new PlayerData
            {
                seedAmount = 0, // hoặc tuỳ số bạn muốn
                unlockedTowerIds = new List<string> { "t2" },
                equippedTowerIds = new List<string> { "t2" },
                completedMapIds = new List<string>(), // chưa hoàn thành map nào
                tutorialCompleted = false
            };

            SavePlayerData();
        }
    }

    // OPTIONAL: tiện ích đánh dấu hoàn thành map
    public void MarkMapCompleted(string mapId)
    {
        if (string.IsNullOrEmpty(mapId)) return;
        if (!playerData.completedMapIds.Contains(mapId))
        {
            playerData.completedMapIds.Add(mapId);
            SavePlayerData();
        }
    }

    // OPTIONAL: đánh dấu hoàn thành tutorial (gọi ở nơi bạn xong tutorial)
    public void MarkTutorialCompleted()
    {
        if (!playerData.tutorialCompleted)
        {
            playerData.tutorialCompleted = true;
            Debug.Log("Skiped and save");
            SavePlayerData();
        }
    }
    /// <summary>
    /// Kiểm tra xem map đã từng hoàn thành chưa
    /// </summary>
    public bool IfMapPlayed(string mapId)
    {
        if (string.IsNullOrEmpty(mapId)) return false;
        return playerData.completedMapIds.Contains(mapId);
    }
    /// <summary>
    /// Lấy số XP cần để lên cấp hiện tại
    /// </summary>
    public int GetXpRequiredForLevel(int level)
    {
        if (level <= 0) return 0;
        return 100 * (int)Mathf.Pow(2, level - 1);
    }

    /// <summary>
    /// Cộng XP cho player
    /// </summary>
    public void AddXp(int amount)
    {
        if (amount <= 0) return;

        playerData.currentXp += amount;
        Debug.Log($"✨ Gained {amount} XP. Total XP = {playerData.currentXp}");

        CheckLevelUp();
        SavePlayerData();
    }
    public int PlayerCurrentLevel()
    {
        Debug.Log(playerData.playerLevel);
        return playerData.playerLevel;
    }
    /// <summary>
    /// Kiểm tra và xử lý khi đủ XP lên level
    /// </summary>
    private void CheckLevelUp()
    {
        while (playerData.currentXp >= GetXpRequiredForLevel(playerData.playerLevel))
        {
            int required = GetXpRequiredForLevel(playerData.playerLevel);
            playerData.currentXp -= required;
            playerData.playerLevel++;

            Debug.Log($"🎉 Level Up! → {playerData.playerLevel} (remaining XP: {playerData.currentXp})");
        }
    }
    /// <summary>
    /// Trả về XP hiện tại và XP cần để lên cấp hiện tại
    /// Dùng cho Slider (value = current, max = total)
    /// </summary>
    public (int current, int total) GetLevelXpProgress()
    {
        int total = GetXpRequiredForLevel(playerData.playerLevel);
        int current = Mathf.Clamp(playerData.currentXp, 0, total);
        return (current, total);
    }
    public void AddAdsCount()
    {
        playerData.totalAds += 1;
        SavePlayerData();
    }
    public int GetAdsCount()
    {
       return playerData.totalAds;
    }
    public void ResetAdsCount(int amount)
    {
        playerData.totalAds -= amount;
    }
}
