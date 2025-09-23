using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// PoolManager tối giản, an toàn:
/// - Mỗi prefab có 1 folder "Pool_<PrefabName>" dưới PoolManager
/// - Instance luôn là con của folder đó, kể cả khi Active
/// - Không có overload Get(..., parent)
/// - Return: Stop/Clear PS, SetActive(false), enqueue lại (ở yên trong folder)
/// </summary>
/// 
public sealed class PoolManager : MonoBehaviour
{
    public static PoolManager I;

    // Queue theo prefab gốc
    private readonly Dictionary<GameObject, Queue<GameObject>> _pools = new();

    // === Lifecycle ===
    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;

        // Nếu muốn sống qua scene:
        // DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (I == this) I = null;
    }

    // === Public API ===

    /// <summary>
    /// Khởi tạo sẵn N instance cho 1 prefab (nằm dưới Pool_<PrefabName>, inactive).
    /// Nếu đã có một phần, chỉ bù cho đủ.
    /// </summary>
    public void WarmUp(GameObject prefab, int count)
    {
        if (!_pools.TryGetValue(prefab, out var q))
            _pools[prefab] = q = new Queue<GameObject>();

        var folder = GetOrCreatePoolParent(prefab.name);

        // Đếm tổng tồn tại (inactive trong queue + active dưới folder)
        int inactive = q.Count, active = 0;
        for (int i = 0; i < folder.childCount; i++)
            if (folder.GetChild(i).gameObject.activeSelf) active++;

        int totalHave = inactive + active;
        int need = Mathf.Max(0, count - totalHave);
        for (int i = 0; i < need; i++)
        {
            var obj = Instantiate(prefab);
            var mk = obj.GetComponent<PooledMarker>() ?? obj.AddComponent<PooledMarker>();
            mk.source = prefab;

            obj.transform.SetParent(folder, false);
            obj.SetActive(false);
            q.Enqueue(obj);
        }
    }


    /// <summary>
    /// Overload tiện cho ParticleSystem.
    /// </summary>
    public void WarmUp(ParticleSystem prefab, int count)
    {
        if (prefab) WarmUp(prefab.gameObject, count);
    }

    /// <summary>
    /// Lấy 1 instance từ pool, đặt world pos/rot, Active = true.
    /// Parent luôn giữ là folder Pool_<PrefabName>.
    /// </summary>
    public GameObject Get(GameObject prefab, Vector3 worldPos, Quaternion worldRot)
    {
        if (!prefab)
        {
            Debug.LogError("[Pool] Get() with null prefab");
            return null;
        }

        if (!_pools.TryGetValue(prefab, out var q))
        {
            q = new Queue<GameObject>();
            _pools[prefab] = q;
        }

        GameObject go;
        if (q.Count > 0)
        {
            go = q.Dequeue();
            if (!go) // bị Destroy bên ngoài?
            {
                // tạo mới thay thế
                go = Instantiate(prefab);
                var mkNew = go.GetComponent<PooledMarker>();
                if (!mkNew) mkNew = go.AddComponent<PooledMarker>();
                mkNew.source = prefab;

                // parent về Pool
                go.transform.SetParent(GetOrCreatePoolParent(prefab.name), worldPositionStays: false);
            }
        }
        else
        {
            go = Instantiate(prefab);
            var mk = go.GetComponent<PooledMarker>();
            if (!mk) mk = go.AddComponent<PooledMarker>();
            mk.source = prefab;

            // parent về Pool
            go.transform.SetParent(GetOrCreatePoolParent(prefab.name), worldPositionStays: false);
        }
        //Debug.Log($"[Pool] GET {prefab.name} -> {go.name} parent={go.transform.parent?.name}");

        // Đặt world transform (parent không đổi)
        go.transform.SetPositionAndRotation(worldPos, worldRot);
        go.SetActive(true);
        return go;
    }

    /// <summary>
    /// Overload tiện cho Component (ví dụ ParticleSystem).
    /// </summary>
    public T Get<T>(T prefab, Vector3 worldPos, Quaternion worldRot) where T : Component
    {
        if (!prefab) return null;
        var go = Get(prefab.gameObject, worldPos, worldRot);
        return go ? go.GetComponent<T>() : null;
    }

    /// <summary>
    /// Trả về pool: Stop/Clear (nếu có PS), SetActive(false), reparent về Pool (phòng khi bị đổi),
    /// Enqueue lại vào đúng queue theo source prefab.
    /// </summary>
    public void Return(GameObject go)
    {
        if (!go) return;

        var mk = go.GetComponent<PooledMarker>();
        // Nếu marker thiếu → tự gắn lại để không bị Destroy
        if (mk == null) mk = go.AddComponent<PooledMarker>();

        // Nếu thiếu source (do tạo sai đường), lấy theo tên folder cha; nếu vẫn null, cố gắng suy luận từ prefab name
        if (mk.source == null)
        {
            // THAY thế bằng prefab gốc nếu bạn có registry; nếu không có thì ít nhất vẫn cho vào “thùng rác an toàn”
            // Ở đây: không có registry => cho về parent Pool hiện tại, vẫn tái sử dụng được.
        }
        Debug.Log($"[Pool] RETURN {go.name} parent={go.transform.parent?.name}");

        // Stop/Clear nếu là ParticleSystem
        var ps = go.GetComponent<ParticleSystem>();
        if (ps) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // Đưa về đúng folder Pool_<sourceName> (kể cả khi ai đó đổi parent)
        var folder = GetOrCreatePoolParent(mk.source != null ? mk.source.name : go.name);
        if (go.transform.parent != folder)
            go.transform.SetParent(folder, true);

        go.SetActive(false);

        // Bảo đảm có queue cho source này
        if (mk.source != null)
        {
            if (!_pools.TryGetValue(mk.source, out var q))
            {
                q = new Queue<GameObject>();
                _pools[mk.source] = q;
            }
            q.Enqueue(go);
        }
        else
        {
            // “Orphan” – vẫn giữ trong folder để tái dùng (không Destroy)
            // Có thể tạo một hàng đợi dự phòng theo tên:
            if (!_orphanPools.TryGetValue(folder.name, out var qo))
            {
                qo = new Queue<GameObject>();
                _orphanPools[folder.name] = qo;
            }
            qo.Enqueue(go);
            Debug.LogWarning($"[Pool] {go.name} trả về pool nhưng thiếu source. Giữ lại trong {folder.name} để tái dùng.");
        }
    }

    // Thêm ở class:
    private readonly Dictionary<string, Queue<GameObject>> _orphanPools = new();


    /// <summary>
    /// Trả về pool sau 'seconds'. Hữu ích khi bạn muốn chờ khoảng thời gian cố định.
    /// (Với VFX nên ưu tiên tính lifetime và gọi Return khi xong)
    /// </summary>
    public void ReturnAfter(GameObject go, float seconds)
    {
        StartCoroutine(_ReturnAfter(go, seconds));
    }

    private IEnumerator _ReturnAfter(GameObject go, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (go) Return(go);
    }

    /// <summary>Hủy sạch tất cả pool (active + inactive) và xóa các folder Pool_*.</summary>
    public void ClearAll()
    {
        // Hủy queued inactives
        foreach (var kv in _pools)
        {
            var q = kv.Value;
            while (q.Count > 0)
            {
                var obj = q.Dequeue();
                if (obj) Destroy(obj);
            }
        }
        _pools.Clear();

        // Hủy mọi folder Pool_* cùng toàn bộ con
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            Destroy(child.gameObject);
        }
    }

    /// <summary>Hủy sạch một pool theo prefab (cả active trong folder và inactive trong queue).</summary>
    public void ClearPool(GameObject prefab)
    {
        if (!prefab) return;

        // Queue
        if (_pools.TryGetValue(prefab, out var q))
        {
            while (q.Count > 0)
            {
                var obj = q.Dequeue();
                if (obj) Destroy(obj);
            }
            _pools.Remove(prefab);
        }

        // Folder
        var folder = transform.Find($"Pool_{prefab.name}");
        if (folder) Destroy(folder.gameObject);
    }

    // === Helpers ===

    private Transform GetOrCreatePoolParent(string prefabName)
    {
        string folderName = $"Pool_{prefabName}";
        var folder = transform.Find(folderName);
        if (folder == null)
        {
            var go = new GameObject(folderName);
            go.transform.SetParent(transform, false);
            folder = go.transform;
        }
        return folder;
    }

    /// <summary>Marker để biết instance thuộc pool nào.</summary>
    private sealed class PooledMarker : MonoBehaviour
    {
        public GameObject source;
    }
}
