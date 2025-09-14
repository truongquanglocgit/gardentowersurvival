using UnityEngine;
using UnityEngine.SceneManagement;

public class InGameSettingPanel : MonoBehaviour
{
    void Start() => ClosePanel();

    public void ClosePanel()
    {
        PauseManager.PopPause();
        gameObject.SetActive(false);
    }
    public void OpenPanel() 
    {
        PauseManager.PushPause();
        gameObject.SetActive(true);
    }

    public void OnClickExitToMainMenu()
    {
        // 1) Reset trạng thái runtime
        GameResetter.ResetBeforeExitToMenu();

        // 2) Load scene ở chế độ Single (unload toàn bộ scene hiện tại)
        LoaderBridge.LoadWithLoadingScreen("MainMenu", "Loading");
        // (ClosePanel sau Load cũng OK vì object sẽ bị unload cùng scene)
    }
}
