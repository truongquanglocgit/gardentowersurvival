using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton nhẹ, giữ TutorialRoot xuyên scene.
/// </summary>
public class TutorialRoot : MonoBehaviour
{
    private static TutorialRoot _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
