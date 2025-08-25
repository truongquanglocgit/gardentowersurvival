using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthUI : MonoBehaviour
{
    public Slider healthSlider;
    private PlayerController player;

    void Start()
    {
        player = GameObject.FindWithTag("GameController").GetComponent<PlayerController>();
        healthSlider.maxValue = player.Hp;
    }

    void Update()
    {
        if (player != null)
        {
            healthSlider.value = player.Hp;
        }
    }
}
