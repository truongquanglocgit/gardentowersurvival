using UnityEngine;
using UnityEngine.UI;

public class GachaButtonBinder : MonoBehaviour
{
    public GachaController controller;
    public bool isRoll10 = false;

    void Start()
    {
        GetComponent<Button>().onClick.AddListener(() =>
        {
            if (isRoll10)
                controller.RollTen();
            else
                controller.RollOnce();
        });
    }
}
