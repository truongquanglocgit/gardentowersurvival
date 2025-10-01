using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;

public class DemoSDK : MonoBehaviour, MMEventListener<FirebaseInitEvent>
{
    private void Start()
    {
        MMEventManager.AddListener(this);
        InvokeRepeating("Loop", 1, 1);
    }

    //private float _currTimeBetweenShowInter = 0;
    private void Loop()
    {
    }

    public void OnMMEvent(FirebaseInitEvent eventType)
    {
    }
}
