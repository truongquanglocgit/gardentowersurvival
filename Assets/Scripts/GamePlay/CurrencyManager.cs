using UnityEngine;
using TMPro;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance;
    public TextMeshProUGUI seedText;
    public int seedAmount; // số lượng seed hiện có

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        SeedDisplay();
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // Load dữ liệu player
        seedAmount = PlayerDataManager.Instance.playerData.seedAmount;
        Debug.Log("Seed Loaded: " + seedAmount);

        // Tìm TMP có tag "WaterText" nếu chưa gán sẵn
        if (seedText == null)
        {
            GameObject taggedObj = GameObject.FindGameObjectWithTag("WaterText");
            if (taggedObj != null)
            {
                seedText = taggedObj.GetComponent<TextMeshProUGUI>();
            }
        }

        SeedDisplay();
    }

    void OnEnable()
    {
        // Đăng ký sự kiện khi đổi scene để tự tìm lại UI
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // Khi load scene mới, tìm lại WaterText
        GameObject taggedObj = GameObject.FindGameObjectWithTag("WaterText");
        if (taggedObj != null)
        {
            seedText = taggedObj.GetComponent<TextMeshProUGUI>();
            SeedDisplay();
        }
    }

    public void SeedDisplay()
    {
        if (seedText != null)
            seedText.text = $"{seedAmount}";
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
