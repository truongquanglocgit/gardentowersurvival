using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public class Spawner : MonoBehaviour
{
    public string SpawnerId;
    [Tooltip("Hướng quay khi spawn")]
    public Transform spawnPoint;

    private void Reset()
    {
        spawnPoint = transform;
    }
}

