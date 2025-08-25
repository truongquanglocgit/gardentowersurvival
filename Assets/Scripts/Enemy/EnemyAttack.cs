using UnityEngine;

public class EnemyAttack : MonoBehaviour
{
    private EnemyController controller;
    
    private Transform player;
    private float lastAttackTime = -Mathf.Infinity;

    void Start()
    {
        controller = GetComponent<EnemyController>();
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    void Update()
    {
        if (player == null || controller == null) return;

        float dist = Vector3.Distance(transform.position, player.position);

        if (dist <= controller.attackRange)
        {
            

            if (Time.time - lastAttackTime >= controller.attackSpeed)
            {
                var playerControllerComponent = player.GetComponent<PlayerController>();
                if (playerControllerComponent != null)
                {
                    playerControllerComponent.TakeDamage(Mathf.RoundToInt(controller.damage));
                    lastAttackTime = Time.time;
                    
                }
            }
        }
        else
        {
           
        }
    }
}
