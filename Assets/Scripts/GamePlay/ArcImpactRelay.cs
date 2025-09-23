// ArcImpactRelay.cs
using UnityEngine;

public class ArcImpactRelay : MonoBehaviour
{
    public TowerController owner;
    public float radius;
    public float damage;
    public LayerMask mask;

    [Header("Arming")]
    public float armTime = 0.08f;      // tối thiểu 1–2 frame
    public float armDistance = 0.25f;  // hoặc bay tối thiểu 25cm
    Vector3 _spawnPos;
    float _spawnTime;
    bool _done;

    void OnEnable()
    {
        _spawnPos = transform.position;
        _spawnTime = Time.time;
        _done = false;
    }

    bool Armed()
    {
        if (Time.time - _spawnTime < armTime) return false;
        if ((transform.position - _spawnPos).sqrMagnitude < armDistance * armDistance) return false;
        return true;
    }

    void OnCollisionEnter(Collision c) { TryExplode(c.collider); }
    void OnTriggerEnter(Collider c) { TryExplode(c); }

    void TryExplode(Collider hit)
    {
        if (_done || owner == null) return;
        // Bỏ qua va chạm với chính tower
        if (owner && hit && (hit.transform.IsChildOf(owner.transform))) return;
        if (!Armed()) return;

        _done = true;
        //owner.ExplodeAt(transform.position, radius, damage, mask);
        gameObject.SetActive(false);
    }
}
