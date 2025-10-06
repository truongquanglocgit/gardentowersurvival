using UnityEngine;
using TMPro;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance;
    public TextMeshProUGUI waterText;
    public TextMeshProUGUI rainbowWaterText;
    public int waterAmount;
    public int rainbowWaterAmount;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // Load dữ liệu player
        waterAmount = PlayerDataManager.Instance.playerData.waterAmount;
        rainbowWaterAmount = PlayerDataManager.Instance.playerData.rainbowWaterAmount;
        Debug.Log("Water Loaded: " + waterAmount);
        Debug.Log("Rainbow Loaded: " + rainbowWaterAmount);

        // Tìm UI theo tag nếu chưa gán
        if (waterText == null)
        {
            var go = GameObject.FindGameObjectWithTag("WaterText");
            if (go) waterText = go.GetComponent<TextMeshProUGUI>();
        }
        if (rainbowWaterText == null)
        {
            // ⚠️ Nên tạo tag riêng cho Rainbow, ví dụ "RainbowWaterText"
            var go = GameObject.FindGameObjectWithTag("RainbowWaterText");
            if (go) rainbowWaterText = go.GetComponent<TextMeshProUGUI>();
        }

        RefreshDisplays();
    }

    void OnEnable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (waterText == null)
        {
            var go = GameObject.FindGameObjectWithTag("WaterText");
            if (go) waterText = go.GetComponent<TextMeshProUGUI>();
        }
        if (rainbowWaterText == null)
        {
            var go = GameObject.FindGameObjectWithTag("RainbowWaterText");
            if (go) rainbowWaterText = go.GetComponent<TextMeshProUGUI>();
        }
        RefreshDisplays();
    }

    private void RefreshDisplays()
    {
        if (waterText) waterText.text = $"{waterAmount}";
        if (rainbowWaterText) rainbowWaterText.text = $"{rainbowWaterAmount}";
    }

    public bool TrySpendWater(int amount)
    {
        if (waterAmount >= amount)
        {
            waterAmount -= amount;
            PlayerDataManager.Instance.playerData.waterAmount = waterAmount;
            PlayerDataManager.Instance.SavePlayerData();
            RefreshDisplays();
            return true;
        }
        return false;
    }
    public bool TrySpendRainbowWater(int amount)
    {
        if (rainbowWaterAmount >= amount)
        {
            rainbowWaterAmount -= amount;
            PlayerDataManager.Instance.playerData.rainbowWaterAmount = rainbowWaterAmount;
            PlayerDataManager.Instance.SavePlayerData();
            RefreshDisplays();
            return true;
        }
        return false;
    }
    public void AddWater(int amount)
    {
        if (amount <= 0) return;
        waterAmount += amount;
        PlayerDataManager.Instance.playerData.waterAmount = waterAmount;
        PlayerDataManager.Instance.SavePlayerData();
        RefreshDisplays();
    }

    public void AddRainbowWater(int amount)
    {
        if (amount <= 0) return;
        rainbowWaterAmount += amount; // ✅ FIX: cộng đúng biến rainbow
        PlayerDataManager.Instance.playerData.rainbowWaterAmount = rainbowWaterAmount; // ✅ FIX: gán đúng
        PlayerDataManager.Instance.SavePlayerData();
        RefreshDisplays();
    }
}
