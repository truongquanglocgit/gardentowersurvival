using UnityEngine;

public class Bullets : MonoBehaviour
{

    private Transform target;
    public TowerController towerController;
    public float speed;
    public float damage;
    private Rigidbody rb;
    private Transform firePoint; // điểm bắn ban đầu để reset về

    public BulletType bulletType = BulletType.Normal;

    public void Start()
    {
        GetStat();
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }
    public void GetStat()
    {
        speed = towerController.minBulletSpeed;
        damage = towerController.CurrentDamage;
    }
    public void SetTarget(Transform t, Transform fireOrigin)
    {
        target = t;
        firePoint = fireOrigin;
    }

    void Update()
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            ResetBullet();
            return;
        }

        Vector3 dir = target.position - transform.position;
        float step = speed * Time.deltaTime;

        if (dir.magnitude <= step)
        {
            Hit();
        }
        else
        {
            transform.Translate(dir.normalized * step, Space.World);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            EnemyController enemy = other.GetComponent<EnemyController>();
            if (enemy != null)
            {
                Hit();
                enemy.TakeDamage(damage);
                ResetBullet();
            }
        }
    }

    void Hit()
    {
        if (target == null) return;

        EnemyController enemy = target.GetComponent<EnemyController>();
        if (enemy != null)
        {

            switch (bulletType)
            {
                case BulletType.Normal:
                    enemy.TakeDamage(damage);
                    break;
                case BulletType.Fire:
                    enemy.TakeDamage(damage);
                    if (enemy.isActiveAndEnabled) { enemy.ApplyBurn(5f, 1f); }

                    break;
                case BulletType.Ice:
                    enemy.TakeDamage(damage);
                    if (enemy.isActiveAndEnabled) { enemy.ApplySlow(0.5f, 2f); }

                    break;
                case BulletType.Poison:
                    if (enemy.isActiveAndEnabled) { enemy.ApplyPoison(3f, 1f, 1f); }

                    break;
                case BulletType.Stun:
                    enemy.TakeDamage(damage);
                    if (enemy.isActiveAndEnabled) { enemy.ApplyStun(2f); }

                    break;
            }
        }

        ResetBullet();
    }

    void ResetBullet()
    {
        target = null;

        // Reset lại vị trí về firePoint
        if (firePoint != null)
            transform.position = firePoint.position;

        gameObject.SetActive(false);
    }

    public enum BulletType
    {
        Normal,
        Fire,
        Ice,
        Poison,
        Stun
    }
}