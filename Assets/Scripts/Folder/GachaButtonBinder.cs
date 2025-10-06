using UnityEngine;
using UnityEngine.UI;

public class GachaButtonBinder : MonoBehaviour
{
    public GachaController controller;
    public bool isRoll10 = false;
    public bool isSpecial = false;

    void Start()
    {
        GetComponent<Button>().onClick.AddListener(() =>
        {
            if (isSpecial)
            {
                
                if (isRoll10)
                    controller.RollTenSpecial();
                else
                    controller.RollOnceSpecial();
            }
            else
            {
                if (isRoll10)
                    controller.RollTenNormal();
                else
                    controller.RollOnceNormal();
            }
        });
    }
}
