using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public GameController gameController;

    [Header("Stats")]
    public float MaxHp = 150f;
    public float Hp;
    public float Speed;

    void Awake()
    {
        Hp = MaxHp; // Đảm bảo khi scene vừa khởi tạo là reset lại HP đúng
    }

    public void ResetHp()
    {
        Hp = MaxHp;
        
    }

    public void TakeDamage(float damage)
    {
        Hp -= damage;
        

        if (Hp <= 0)
        {
            gameController.die();
        }
    }
}
