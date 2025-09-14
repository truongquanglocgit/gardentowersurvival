using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GraphicsYawFollower : MonoBehaviour
{
    public Transform target; // drag Player (parent)
    void LateUpdate()
    {
        if (!target) return;
        Vector3 e = target.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, e.y, 0f); // chỉ ép yaw
    }
}

