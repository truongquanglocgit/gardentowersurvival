using UnityEngine;
using System;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "TD/TowerData")]
public class TowerData : ScriptableObject
{
    [Header("Info")]
    public string towerId;
    public string displayName;
    public Sprite icon;
    public Rarity rarity;
    [Header("Stats")]
    public int cost;
    public float range = 5f;
    public float fireRate = 1f;
    public float damage = 10f;

    [Header("Gameplay")]
    public GameObject prefab;
}