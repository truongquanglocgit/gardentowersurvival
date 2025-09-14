using UnityEngine;
using System.Collections;

public class VisualBullet : MonoBehaviour
{
    Transform _target;
    float _speed = 20f;
    float _life = 2f;

    public void Init(Transform target, float speed, float life)
    {
        _target = target; _speed = speed; _life = life;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        float t = 0f;
        while (t < _life && _target)
        {
            Vector3 to = _target.position - transform.position;
            float step = _speed * Time.deltaTime;
            if (to.sqrMagnitude <= step * step) break;
            transform.position += to.normalized * step;
            transform.rotation = Quaternion.LookRotation(to);
            t += Time.deltaTime;
            yield return null;
        }
        Destroy(gameObject);
    }
}
