using UnityEngine;
using System.Collections;

public class EnemyController : MonoBehaviour
{
    public float hp = 1;
    public float damage = 1;
    public float speed = 1;
    public int seedReward = 1;

    [Header("Combat Stats")]
    public float attackSpeed = 1f;
    public float attackRange = 2f; // Dừng di chuyển khi ở trong tầm này

    public bool IsDead { get; private set; } = false;

    private bool isSlowed = false;
    private float originalSpeed;
    public GameController gameController;
    private void Start()
    {
        gameController = GameObject.FindWithTag("GameController").GetComponent<GameController>();

    }
    void Update()
    {
        if (IsDead)
        {
            //Debug.Log($"{gameObject.name} is dead — skipping movement.");
            return;
        }

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null)
        {
            Debug.LogWarning("⚠️ Không tìm thấy Player (tag='Player')");
            return;
        }

        float distance = Vector3.Distance(transform.position, playerObj.transform.position);
        //Debug.Log($"📏 Distance to Player: {distance} | attackRange = {attackRange}");

        if (distance > attackRange)
        {
            Vector3 dir = (playerObj.transform.position - transform.position).normalized;
            transform.position += dir * speed * Time.deltaTime;
            //Debug.Log("➡️ Moving towards player");
        }
        else
        {
            Vector3 lookDir = (playerObj.transform.position - transform.position).normalized;
            if (lookDir != Vector3.zero)
            {
                transform.forward = Vector3.Lerp(transform.forward, lookDir, Time.deltaTime * 10f);
            }
            //Debug.Log("🛑 Player in attack range, stopping movement");
        }
    }


    public void TakeDamage(float dmg)
    {
        hp -= dmg;
        //Debug.Log($"🟥 Enemy HP: {hp}");
        if (hp <= 0f) Die();
    }

    public void Die()
    {
        IsDead = true;
        gameController.addSeed(seedReward);
        gameObject.SetActive(false);
    }

    public void Init(float powerMultiplier)
    {
        hp *= powerMultiplier;
        damage *= powerMultiplier;
        seedReward = Mathf.RoundToInt(seedReward * powerMultiplier);
    }

    #region Status Effects
    public void ApplySlow(float slowFactor, float duration)
    {
        
        if (!isSlowed)
        {
            
            originalSpeed = speed;
            speed *= slowFactor;
            isSlowed = true;
            StartCoroutine(RemoveSlow(duration));
        }
    }

    IEnumerator RemoveSlow(float duration)
    {
        yield return new WaitForSeconds(duration);
        speed = originalSpeed;
        isSlowed = false;
    }

    public void ApplyStun(float duration)
    {
        if (!isSlowed)
        {
            originalSpeed = speed;
            speed = 0;
            isSlowed = true;
            StartCoroutine(RemoveStun(duration));
        }
    }

    IEnumerator RemoveStun(float duration)
    {
        yield return new WaitForSeconds(duration);
        speed = originalSpeed;
        isSlowed = false;
    }

    public void ApplyBurn(float duration, float interval)
    {
        StartCoroutine(BurnCoroutine(duration, interval));
    }

    IEnumerator BurnCoroutine(float duration, float interval)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            TakeDamage(5f);
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }
    }

    public void ApplyPoison(float duration, float interval, float damage)
    {
        StartCoroutine(PoisonCoroutine(duration, interval, damage));
    }

    IEnumerator PoisonCoroutine(float duration, float interval, float damage)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            TakeDamage(damage);
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }
    }
    #endregion
}
