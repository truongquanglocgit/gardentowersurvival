using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InGameSettingBUtton : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }
    public void OpenPanel()
    {
        gameObject.SetActive(true);
    }
}
