using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.Scripting;   // ✅ thêm dòng này

[Preserve]   // giữ lại class này khi build, không bị code stripping
[CreateAssetMenu(menuName = "TD/EnemyData")]
public class EnemyData : ScriptableObject
{
    public string enemyId;
    public GameObject prefab;
    
}




