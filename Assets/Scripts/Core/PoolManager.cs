using UnityEngine;
using System.Collections.Generic;

public class PoolManager : MonoBehaviour
{
    public static PoolManager I;

    // Khóa theo prefab gốc
    private readonly Dictionary<GameObject, Queue<GameObject>> pools = new();

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; } // chống trùng
        I = this;
        // Nếu muốn PoolManager sống qua scene, bật dòng dưới rồi nhớ gọi ClearAll khi rời trận:
        // DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (I == this) I = null;
    }

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
            go.transform.SetParent(poolParent, true);
        }
        return go;
    }

    public void Return(GameObject go)
    {
        if (!go) return;
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

        Transform poolParent = GetOrCreatePoolParent(prefab.name);
        for (int i = 0; i < count; i++)
        {
            var obj = Instantiate(prefab);
            obj.SetActive(false);
            obj.AddComponent<PooledMarker>().source = prefab;
            obj.transform.SetParent(poolParent, false);
            q.Enqueue(obj);
        }
    }

    /// <summary>Hủy sạch mọi thứ trong pool (active + inactive) và xóa thư mục Pool_*.</summary>
    public void ClearAll()
    {
        // 1) Hủy tất cả object đang xếp hàng
        foreach (var kvp in pools)
        {
            var q = kvp.Value;
            while (q.Count > 0)
            {
                var go = q.Dequeue();
                if (go) Destroy(go);
            }
        }
        pools.Clear();

        // 2) Hủy mọi con dưới PoolManager (bao gồm cả object đang active vì chúng vẫn là con của Pool_*)
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            Destroy(child.gameObject);
        }
    }

    /// <summary>Hủy sạch 1 pool theo prefab (khi rời map chỉ dọn một số loại).</summary>
    public void ClearPool(GameObject prefab)
    {
        if (!prefab) return;

        // Hủy queue
        if (pools.TryGetValue(prefab, out var q))
        {
            while (q.Count > 0)
            {
                var go = q.Dequeue();
                if (go) Destroy(go);
            }
            pools.Remove(prefab);
        }

        // Hủy folder Pool_prefabName (gồm cả active/inactive)
        string folderName = $"Pool_{prefab.name}";
        var folder = transform.Find(folderName);
        if (folder) Destroy(folder.gameObject);
    }

    private Transform GetOrCreatePoolParent(string name)
    {
        string folderName = $"Pool_{name}";
        var folder = transform.Find(folderName);
        if (folder == null)
        {
            var go = new GameObject(folderName);
            go.transform.SetParent(transform);
            folder = go.transform;
        }
        return folder;
    }

    private class PooledMarker : MonoBehaviour { public GameObject source; }
}
