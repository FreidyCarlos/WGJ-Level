using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyScript : MonoBehaviour
{
    public Transform jugador;           // Transform del jugador
    public Animator animator;           // Animator del enemigo
    public GameObject gameOverCanvas;   // Canvas de Game Over
    public AudioSource BiteSound;   // Sonido opcional

    public MonoBehaviour playerMovementScript; // Asigna aquí el script que controla al Player

    private bool atacando = false;

    void Update()
    {
        if (jugador != null)
        {
            Vector3 escala = transform.localScale;

            // Se invierte por el diseño
            if (jugador.position.x < transform.position.x)
                escala.x = Mathf.Abs(escala.x);
            else
                escala.x = Mathf.Abs(escala.x) * -1;

            transform.localScale = escala;
        }
    }

    void OnCollisionEnter2D(Collision2D c)
    {
        if (!atacando && c.collider.CompareTag("Player"))
        {
            atacando = true;
            StartCoroutine(MorderYPerder());
        }
    }

    private System.Collections.IEnumerator MorderYPerder()
    {
        animator.SetTrigger("Mordida");

        BiteSound.Play();

        playerMovementScript.enabled = false;

        yield return new WaitForSeconds(2.0f);

        gameOverCanvas.SetActive(true);

    }
}