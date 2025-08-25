using UnityEngine;
using System.Collections.Generic;

public class PoolManager : MonoBehaviour
{
    public static PoolManager I;

    private readonly Dictionary<GameObject, Queue<GameObject>> pools = new();

    void Awake() => I = this;

    public GameObject Get(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        if (!pools.TryGetValue(prefab, out var q))
        {
            q = new Queue<GameObject>();
            pools[prefab] = q;
        }
        GameObject go;
        if (q.Count > 0)
        {
            go = q.Dequeue();
            go.transform.SetPositionAndRotation(pos, rot);
            go.SetActive(true);
        }
        else
        {
            go = Instantiate(prefab, pos, rot);
            go.AddComponent<PooledMarker>().source = prefab;

            Transform poolParent = GetOrCreatePoolParent(prefab.name);
            go.transform.SetParent(poolParent);
        }

        return go;
    }
    public void Return(GameObject go)
    {
        var mk = go.GetComponent<PooledMarker>();
        if (mk == null || !pools.ContainsKey(mk.source)) { Destroy(go); return; }
        go.SetActive(false);
        pools[mk.source].Enqueue(go);
    }
    public void WarmUp(GameObject prefab, int count)
    {
        if (!pools.TryGetValue(prefab, out var q))
        {
            q = new Queue<GameObject>();
            pools[prefab] = q;
        }

        for (int i = 0; i < count; i++)
        {
            GameObject obj = Instantiate(prefab);
            obj.SetActive(false);
            obj.AddComponent<PooledMarker>().source = prefab;
            // Gán parent cho dễ quản lý
            Transform poolParent = GetOrCreatePoolParent(prefab.name);
            obj.transform.SetParent(poolParent);
            q.Enqueue(obj);
        }
    }
    private Transform GetOrCreatePoolParent(string name)
    {
        string folderName = $"Pool_{name}";
        var folder = GameObject.Find(folderName);
        if (folder == null)
        {
            folder = new GameObject(folderName);
            folder.transform.SetParent(transform); // Gắn vào PoolManager
        }
        return folder.transform;
    }

    private class PooledMarker : MonoBehaviour { public GameObject source; }
}
