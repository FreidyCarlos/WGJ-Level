using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PerroScriptA : MonoBehaviour
{
    public float Speed;
    public float JumpForce;

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Animator Anim;
    private float horizontal;
    private bool bIsJumping = false;
    private int Health = 5;


    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        Anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        horizontal = Input.GetAxisRaw("Horizontal");

        if (horizontal != 0)
            sr.flipX = horizontal < 0;

        Anim.SetBool("running", horizontal != 0.0f);

        if (Input.GetKeyDown(KeyCode.Space))
        {
            Jump();
        }
    }

    private void Jump()
    {
        if (bIsJumping)
        {
            return;
        }

        bIsJumping = true;

        rb.AddForce(Vector2.up * JumpForce);
    }

    private void EndJump()
    {
        bIsJumping = false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            EndJump();
        }
    }

    private void FixedUpdate()
    {
        rb.velocity = new Vector2(horizontal * Speed, rb.velocity.y);
    }

    public void Hit()
    {
        Health = Health - 1;
        if (Health == 0)
        {
            Destroy(gameObject);
        }
    }
}
