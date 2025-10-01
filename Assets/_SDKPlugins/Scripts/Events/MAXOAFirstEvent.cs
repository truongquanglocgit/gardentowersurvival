using MoreMountains.Tools;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct MAXOAFirstEvent
{
    public static MAXOAFirstEvent e;
    public static void Trigger()
    {
        MMEventManager.TriggerEvent(e);
    }
}
