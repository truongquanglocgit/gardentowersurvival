using System;
using UnityEngine;
public static class TutorialSignals
{
    public static event Action<MonoBehaviour> TowerPlaced; // truyền TowerController hoặc component gốc

    public static void RaiseTowerPlaced(MonoBehaviour towerController)
    {
        TowerPlaced?.Invoke(towerController);
    }
}