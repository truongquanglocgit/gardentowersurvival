using UnityEngine;

public class InGameSettingPanel : MonoBehaviour
{
    // Chỉ để tránh Push nhiều lần nếu OnEnable chạy lặp
    bool _pauseTaken;

    void Awake()
    {
        // Prefab để sẵn inactive là tốt nhất.
        // Nếu bạn muốn chắc chắn: tắt panel mà KHÔNG đụng PauseManager
        // (OnDisable chỉ chạy khi đã từng bật; lúc Awake panel đang tắt thì không gọi Pop).
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    // ✅ Vào panel => pause
    void OnEnable()
    {
        if (_pauseTaken) return;
        PauseManager.PushPause();
        _pauseTaken = true;
    }

    // ✅ Rời panel => unpause (đếm đúng cặp)
    void OnDisable()
    {
        if (!_pauseTaken) return; // chưa lấy pause thì khỏi trả
        PauseManager.PopPause();
        _pauseTaken = false;
    }

    // UI hook
    public void OpenPanel()
    {
        // KHÔNG gọi PushPause ở đây nữa
        gameObject.SetActive(true);
    }

    public void ClosePanel()
    {
        // KHÔNG gọi PopPause ở đây nữa
        gameObject.SetActive(false);
    }

    public void OnClickExitToMainMenu()
    {
        // Trước khi rời scene: chắc chắn trả pause
        if (gameObject.activeSelf) gameObject.SetActive(false);
        Time.timeScale = 1f;
#if ON_ADS


        // phòng hờ
        GameResetter.ResetBeforeExitToMenu(); // như cũ
        LoaderBridge.LoadWithLoadingScreen("MainMenu", "Loading");
        MAXUtil.Instance.ShowInterAd(FirebaseEventMKTEnum.popup_back_mainmenu.ToString());
#else
        gameObject.SetActive(false);
#endif
        
    }
}
