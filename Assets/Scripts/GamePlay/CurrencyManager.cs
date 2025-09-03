using UnityEngine;
using TMPro;
public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance;
    public TextMeshProUGUI seedText;
    public int seedAmount ; // số lượng seed hiện có

    void Start()
    {
        if (Instance != null) Destroy(gameObject);
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // để giữ giữa các scene nếu cần
        }

        // Có thể load từ PlayerData
        seedAmount = PlayerDataManager.Instance.playerData.seedAmount;
        Debug.Log(seedAmount);
        SeedDisplay();
    }
    public void SeedDisplay()
    {
        seedText.text = $"Seed: {seedAmount}";
    }
    public bool TrySpendSeed(int amount)
    {
        if (seedAmount >= amount)
        {
            seedAmount -= amount;
            PlayerDataManager.Instance.playerData.seedAmount = seedAmount;
            PlayerDataManager.Instance.SavePlayerData();
            SeedDisplay();
            return true;
        }
        return false;
    }

    public void AddSeed(int amount)
    {
        seedAmount += amount;
        PlayerDataManager.Instance.playerData.seedAmount = seedAmount;
        PlayerDataManager.Instance.SavePlayerData();
        SeedDisplay();
    }
}
