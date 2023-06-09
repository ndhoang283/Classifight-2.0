﻿using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerNetwork : NetworkBehaviour
{
    public float currentHealth;
    private Animator animator;
    public float moveSpeed = 4f;
    private Rigidbody2D rb;
    private SpriteRenderer sprite;
    private MovementState state;
    public float jumpForce = 0f;
    private enum MovementState { idle, running, jumping, attacking, back, idleback, attackingback, jumpingback }
    private bool checkJump = false;
    public Transform attackPoint;
    public LayerMask enemyLayers;
    public float attackRange = 0.5f;
    public float attackDamage = 20f;
    public float attackRate = 2f;
    float nextAttackTime = 0f;
    public LayerMask jumableGround;
    public GameObject hitBox;
    private bool isFlipped = false; // Biến để theo dõi trạng thái quay của nhân vật
    private Vector3 hitBoxOriginalPosition;
    private BoxCollider2D boxColl;

    [SerializeField] private AudioSource jumpSoundEffect;
    [SerializeField] private AudioSource deathSoundEffect;
    [SerializeField] private AudioSource moveSoundEffect;
    [SerializeField] private AudioSource attackSoundEffect;
    [SerializeField] private AudioSource hurtEffect;
    private void Start()
    {
        if (IsServer)
            transform.position = new Vector3(-7, 0, 0);
        else
        {
            transform.position = new Vector3(7, 0, 0);
            ServerFlipServerRpc(true);
        }

        currentHealth = 100;
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        boxColl = GetComponent<BoxCollider2D>();
        if (hitBox != null)
        {
            hitBoxOriginalPosition = hitBox.transform.localPosition;
        }
    }
    private void Update()
    {
        if(!IsOwner) return;
        float dirX = Input.GetAxisRaw("Horizontal");
        rb.velocity = new Vector2(dirX * moveSpeed, rb.velocity.y);

        checkJump = Input.GetKeyDown(KeyCode.W);
        if (checkJump && IsGrounded())
        {
            jumpSoundEffect.Play();
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            state = MovementState.jumping;
        }
        else
        {
            if (dirX > 0f)
            {
                moveSoundEffect.Play();
                state = MovementState.running;
                ServerFlipServerRpc(false);
            }
            else if (dirX < 0f)
            {
                moveSoundEffect.Play();
                state = MovementState.running;
                ServerFlipServerRpc(true);
            }
            else
            {
                state = MovementState.idle;
            }

            if (Input.GetKey(KeyCode.J))
            {
                state = MovementState.attacking;
            }
        }

        animator.SetInteger("state", (int)state);

        if (Time.time >= nextAttackTime)
        {
            if (Input.GetKeyDown(KeyCode.J))
            {
                attackSoundEffect.Play();
                AttackServerRpc();
                nextAttackTime = Time.time + 1f / attackRate;
            }
        }
    }
    private bool IsGrounded()
    {
        return Physics2D.BoxCast(boxColl.bounds.center, boxColl.bounds.size, 0f, Vector2.down, .1f, jumableGround);
    }
    public void OnCollisionEnter2D(Collision2D collision)
    {
        if(!IsOwner) return;
        if (collision.gameObject.CompareTag("fallout"))
        {
            TakeDamageServerRpc(200);
        }
    }
    void Attack()
    {
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayers);

        foreach (Collider2D hitCollider in hitColliders)
        {
            if (hitCollider.TryGetComponent<PlayerNetwork>(out PlayerNetwork playerNetwork))
            {
                playerNetwork.TakeDamageClientRpc(attackDamage);
            }
        }
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        animator.SetTrigger("Hurt");
        hurtEffect.Play();
        if (currentHealth <= 0)
        {
            Destroy(NetworkManager);
            if (IsOwner)
            {
                DieClientRpc();
            }
            else
            {
                SceneManager.LoadScene("Win");
                this.enabled = false;
            }
            
        }
    }

    async void Die()
    {
        deathSoundEffect.Play();
        Debug.Log("Player Died");
        animator.SetTrigger("Hurt");
        animator.SetBool("isDead", true);
        await Task.Delay(500);
        GetComponent<Collider2D>().enabled = false;
        await Task.Delay(500);
        DieClientRpc();
    }

    [ClientRpc]
    private void DieClientRpc()
    {
        SceneManager.LoadScene("Lose");
        this.enabled = false;
    }
    [ServerRpc]
    private void AttackServerRpc()
    {
        Attack();
    }

    [ServerRpc]
    private void TakeDamageServerRpc(float damage)
    {
        TakeDamageClientRpc(damage);
    }

    [ClientRpc]
    private void TakeDamageClientRpc(float damage)
    {
        TakeDamage(damage);
    }


    [ServerRpc]
    private void ServerFlipServerRpc(bool flipX)
    {
        RpcFlipClientRpc(flipX);
    }

    [ClientRpc]
    private void RpcFlipClientRpc(bool flipX)
    {
        if (flipX)
        {
            if (!isFlipped) // Kiểm tra nếu trạng thái quay mới là quay sang trái
            {
                isFlipped = true;
                sprite.flipX = true;
                if (hitBox != null)
                {
                    // Đảo ngược vị trí của hit box
                    hitBox.transform.localPosition = new Vector3(-hitBox.transform.localPosition.x, hitBox.transform.localPosition.y, hitBox.transform.localPosition.z);
                }
            }
        }
        else
        {
            if (isFlipped) // Kiểm tra nếu trạng thái quay mới là quay sang phải (hướng ban đầu)
            {
                isFlipped = false;
                sprite.flipX = false;
                if (hitBox != null)
                {
                    // Đặt lại vị trí ban đầu của hit box
                    hitBox.transform.localPosition = hitBoxOriginalPosition;
                }
            }
        }
    }
    void OnDrawGizmosSelected()
    {
        if (attackPoint == null)
        { return; }
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }
}
