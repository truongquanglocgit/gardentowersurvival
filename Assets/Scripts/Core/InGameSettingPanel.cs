using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class InGameSettingPanel : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        ClosePanel();
    }

    
    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }
    public void OpenPanel()
    {
        gameObject.SetActive(true);
    }
    public void OnClickExitToMainMenu()
    {
        

        SceneManager.LoadScene("MainMenu");
        ClosePanel();
    }
}
