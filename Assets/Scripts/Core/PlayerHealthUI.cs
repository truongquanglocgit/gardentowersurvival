using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthUI : MonoBehaviour
{
    public Slider healthSlider;
    private PlayerController player;

    void Start()
    {
        player = GameObject.FindWithTag("Player").GetComponent<PlayerController>();
        if (player != null)
        {
            
            healthSlider.maxValue = player.MaxHp;
        }
        
    }

    void Update()
    {
        if (player != null)
        {
            healthSlider.value = Mathf.Lerp(healthSlider.value, player.Hp, Time.deltaTime * 10f);

        }
    }
}
