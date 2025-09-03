using UnityEngine;
using System.Collections;

public class EnemyController : MonoBehaviour
{
    [Header("Base Stats")]
    public float hp = 1;
    public float damage = 1;
    public float speed = 1;
    public int seedReward = 1;

    [Header("Combat Stats")]
    public float attackSpeed = 1f;
    public float attackRange = 2f; // Dừng di chuyển khi trong tầm

    public bool IsDead { get; private set; } = false;

    private bool _countedDown;       // đảm bảo AliveCount trừ 1 lần
    private bool _inWaveLifetime;    // ✅ chỉ true khi spawn trong wave

    private bool isSlowed = false;
    private float originalSpeed;

    private GameController gameController;

    void OnEnable()
    {
        _countedDown = false;
        _inWaveLifetime = false;   // mặc định false → chỉ WaveManager set true
        IsDead = false;
    }

    void Start()
    {
        gameController = GameObject.FindWithTag("GameController")
            .GetComponent<GameController>();
    }

    void Update()
    {
        if (IsDead) return;

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null)
        {
            Debug.LogWarning("⚠️ Không tìm thấy Player (tag='Player')");
            return;
        }

        float distance = Vector3.Distance(transform.position, playerObj.transform.position);

        if (distance > attackRange)
        {
            Vector3 dir = (playerObj.transform.position - transform.position).normalized;
            transform.position += dir * speed * Time.deltaTime;
        }
        else
        {
            Vector3 lookDir = (playerObj.transform.position - transform.position).normalized;
            if (lookDir != Vector3.zero)
                transform.forward = Vector3.Lerp(transform.forward, lookDir, Time.deltaTime * 10f);
        }
    }

    // ✅ gọi ngay sau khi SpawnEnemy trong WaveManager
    public void BeginWaveLifetime()
    {
        _inWaveLifetime = true;
        // Debug.Log($"[EnemyController] BeginWaveLifetime for {name}");
    }

    public void TakeDamage(float dmg)
    {
        hp -= dmg;
        if (hp <= 0f) Die();
    }

    public void Die()
    {
        if (IsDead) return;

        IsDead = true;
        gameController.AddSeed(seedReward);
        TryCountDownAlive();
        gameObject.SetActive(false);
    }

    void OnDisable()
    {
        // Nếu bị tắt không qua Die() (despawn cưỡng bức, rơi map)
        if (gameObject.scene.IsValid())
            TryCountDownAlive();
    }

    private void TryCountDownAlive()
    {
        // ⛔ chỉ tính nếu thực sự là enemy trong wave
        if (!_inWaveLifetime) return;
        if (_countedDown) return;

        WaveRuntime.AliveCount = Mathf.Max(0, WaveRuntime.AliveCount - 1);
        _countedDown = true;
        _inWaveLifetime = false;

        Debug.Log($"[EnemyController] Enemy died. AliveCount={WaveRuntime.AliveCount}");
        WaveRuntime.NotifyEnemyDied();
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
