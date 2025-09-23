// ExplosionAOE.cs
using System.Collections.Generic;
using UnityEngine;

public static class ExplosionAOE
{
    static readonly Collider[] _buf = new Collider[128];
    static readonly HashSet<EnemyController> _victims = new HashSet<EnemyController>();

    /// Nổ AOE, trả về số enemy trúng.
    public static int Do(Vector3 pos, float radius, float damage, LayerMask mask)
    {
        _victims.Clear();
        if (radius <= 0.01f) return 0;

        int n = Physics.OverlapSphereNonAlloc(
            pos, radius, _buf,
            (mask.value != 0) ? mask : ~0,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < n; i++)
        {
            var c = _buf[i]; if (!c) continue;
            EnemyController ec = c.attachedRigidbody
                ? c.attachedRigidbody.GetComponent<EnemyController>()
                : c.GetComponentInParent<EnemyController>() ?? c.GetComponent<EnemyController>();

            if (ec != null && !ec.IsDead) _victims.Add(ec);
        }

        int hit = 0;
        foreach (var ec in _victims) { ec.TakeDamage(damage); hit++; }
        return hit;
    }
}
