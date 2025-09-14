using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Animator))]
public class EnemyAnimDriver : MonoBehaviour
{
    public string isMovingParam = "IsMoving";
    public string attackTrigger = "Attack";
    

    Animator _anim;
    NavMeshAgent _agent; // nếu không dùng Agent, thay bằng tính tốc độ thủ công
    

    void Awake()
    {
        _anim = GetComponent<Animator>();
        _agent = GetComponent<NavMeshAgent>(); // null nếu không dùng
    }

    void Update()
    {
        

        // ---- Moving bool
        float speed = _agent ? _agent.velocity.magnitude : 0f;
        bool moving = speed > 0.1f;
        _anim.SetBool(isMovingParam, moving);
    }

    // Gọi từ gameplay code khi bắn/tấn công
    public void PlayAttack()
    {
        
        _anim.ResetTrigger(attackTrigger);
        _anim.SetTrigger(attackTrigger);
    }

    
}
