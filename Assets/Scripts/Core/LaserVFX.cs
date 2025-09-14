using UnityEngine;
using System.Collections;

public class LaserVFX : MonoBehaviour
{
    public void Play(Transform from, Transform to, float duration)
    {
        StartCoroutine(Beam(from, to, duration));
    }

    IEnumerator Beam(Transform from, Transform to, float duration)
    {
        float t = 0f;
        var lr = GetComponent<LineRenderer>();
        while (t < duration && from && to)
        {
            if (lr)
            {
                lr.SetPosition(0, from.position);
                lr.SetPosition(1, to.position);
            }
            else
            {
                // nếu không có LineRenderer, kéo dài prefab dọc theo Z
                Vector3 a = from.position, b = to.position;
                Vector3 dir = b - a;
                transform.position = (a + b) * 0.5f;
                transform.rotation = Quaternion.LookRotation(dir);
                var ls = transform.localScale;
                ls.z = dir.magnitude;
                transform.localScale = ls;
            }
            t += Time.deltaTime;
            yield return null;
        }
        Destroy(gameObject);
    }
}
