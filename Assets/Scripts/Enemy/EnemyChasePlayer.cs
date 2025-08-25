  using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyChasePlayer : MonoBehaviour
{
    public GameObject player;
    public float speed = 2f;
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        player = GameObject.FindWithTag("Player");
        if (player == null) return;

        Vector3 dir = (player.transform.position - transform.position).normalized;
        Vector3 velocity = dir * speed;

        // Giữ nguyên Y
        velocity.y = rb.velocity.y;

        rb.velocity = velocity;
    }
}
