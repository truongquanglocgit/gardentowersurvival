using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthUI : MonoBehaviour
{
    public Slider healthSlider;
    public EnemyController controller;
    

    void Start()
    {
        
        if (controller != null && healthSlider != null)
        {
            
            healthSlider.maxValue = controller.hp;
            healthSlider.value = controller.hp;
        }
    }

    void Update()
    {
        if (controller != null && healthSlider != null)
        {
            healthSlider.value = controller.hp;
        }
    }
}
