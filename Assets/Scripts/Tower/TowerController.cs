using UnityEngine;

public class TowerController : MonoBehaviour
{
    public float range = 5f;
    public float fireRate = 1f;

    public GameObject bulletObject; // Gắn sẵn trong prefab
    public Transform firePoint;
    public float minBulletSpeed = 15f; // tốc độ tối thiểu
    public float minTowerDamage = 3f; 
    public float maxTowerDamage = 3f; 
    public float maxBulletSpeed = 15f; 
    public float seedCost = 100;
    private Bullet bulletScript;
    private float fireCooldown = 0f;
    private Transform target;

    void Start()
    {
        if (bulletObject != null)
        {
            bulletScript = bulletObject.GetComponent<Bullet>();
            bulletObject.SetActive(false);
        }
    }

    void Update()
    {
        fireCooldown -= Time.deltaTime;

        if (target == null || target.GetComponent<EnemyController>().IsDead)
        {
            FindTarget();
            return;
        }

        if (target != null && fireCooldown <= 0f && !bulletObject.activeInHierarchy)
        {
            Shoot();
            fireCooldown = 1f / fireRate;
        }
    }

    void FindTarget()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        float shortestDist = range;
        GameObject nearest = null;

        foreach (var enemy in enemies)
        {
            float dist = Vector3.Distance(transform.position, enemy.transform.position);
            if (dist < shortestDist)
            {
                shortestDist = dist;
                nearest = enemy;
            }
        }

        target = nearest != null ? nearest.transform : null;
    }

    void Shoot()
    {
        if (bulletScript != null && target != null)
        {
            bulletObject.transform.position = firePoint.position;
            bulletObject.transform.rotation = firePoint.rotation;

            float distance = Vector3.Distance(firePoint.position, target.position);
            float timeBuffer = 0.9f / fireRate;
            float recommendedSpeed = distance / timeBuffer;

            bulletScript.speed = Mathf.Max(recommendedSpeed, minBulletSpeed);

            bulletObject.SetActive(true);
            bulletScript.SetTarget(target, firePoint);
        }
    }

}
