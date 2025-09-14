using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class PlayerDataManager : MonoBehaviour
{
    public static PlayerDataManager Instance;

    public PlayerData playerData = new();
    

    private string SavePath => Application.persistentDataPath + "/player_data.json";

    void Awake()
    {
        
        Instance = this;
        

        Debug.Log("📂 Save path: " + Application.persistentDataPath);
        LoadPlayerData();
    }

    public void SavePlayerData()
    {
        string json = JsonUtility.ToJson(playerData, true);
        File.WriteAllText(SavePath, json);
        Debug.Log("💾 PlayerData saved to: " + SavePath);
    }

    public void LoadPlayerData()
    {
        if (File.Exists(SavePath))
        {
            string json = File.ReadAllText(SavePath);
            playerData = JsonUtility.FromJson<PlayerData>(json);
            Debug.Log("📤 PlayerData loaded from: " + SavePath);
        }
        else
        {
            Debug.Log("🆕 No existing player data. Creating default test data.");

            playerData = new PlayerData
            {
                unlockedTowerIds = new List<string> { "t1", "t2", "t3", "t4", "t5", "t6", "t7", "t8", "t9" },
                equippedTowerIds = new List<string> { "t10", "t7", "t8", "t9", "t2" }
            };

            SavePlayerData();
        }
    }
}
