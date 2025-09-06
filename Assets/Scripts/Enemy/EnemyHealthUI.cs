using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthUI : MonoBehaviour
{
    public Slider healthSlider;
    public EnemyController controller;
    public Vector3 extraOffset = new Vector3(0, 0.3f, 0); // thêm một chút khoảng cách
    public TextMeshProUGUI healthText;
    private float maxHealth;    
    private Camera cam;
    private Renderer[] rends;
    private Collider[] cols;

    void Start()
    {
        cam = Camera.main;
        if (controller != null && healthSlider != null)
        {
            healthSlider.maxValue = controller.hp;
            healthSlider.value = controller.hp;
        }
        maxHealth = controller.hp;
        // lấy bounds của enemy
        rends = controller.GetComponentsInChildren<Renderer>();
        cols = controller.GetComponentsInChildren<Collider>();
    }

    void Update()
    {
        if (controller == null || healthSlider == null) return;
        healthText.text = $"{healthSlider.value.ToString()} / {maxHealth}" ;
        healthSlider.value = controller.hp;

        // Tính bounds tổng của enemy
        Bounds b = new Bounds(controller.transform.position, Vector3.zero);
        bool hasBounds = false;

        foreach (var r in rends)
        {
            if (!r || !r.enabled) continue;
            if (!hasBounds) { b = r.bounds; hasBounds = true; }
            else b.Encapsulate(r.bounds);
        }

        if (!hasBounds)
        {
            foreach (var c in cols)
            {
                if (!c) continue;
                if (!hasBounds) { b = c.bounds; hasBounds = true; }
                else b.Encapsulate(c.bounds);
            }
        }

        if (hasBounds)
        {
            // đặt health bar lên đỉnh enemy
            Vector3 worldTop = new Vector3(b.center.x, b.max.y, b.center.z) + extraOffset;
            transform.position = worldTop;
        }
    }
}
