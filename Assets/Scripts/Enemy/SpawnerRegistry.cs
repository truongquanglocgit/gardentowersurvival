using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnerRegistry : MonoBehaviour
{
    public static SpawnerRegistry Instance { get; private set; }
    private Dictionary<string, Spawner> _map = new Dictionary<string, Spawner>();

    void Awake()
    {
        Instance = this;
        _map.Clear();
        foreach (var s in FindObjectsOfType<Spawner>())
        {
            
            if (!string.IsNullOrEmpty(s.SpawnerId)) _map[s.SpawnerId] = s;
        }
    }

    public bool TryGet(string id, out Spawner spawner) => _map.TryGetValue(id, out spawner);
}

