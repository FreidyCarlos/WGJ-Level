using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageZone : MonoBehaviour
{
    public GameObject gameOverCanvas;
    public AudioSource gameOverSound;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            gameOverCanvas.SetActive(true);
            Time.timeScale = 0f;

            if (gameOverSound != null)
            {
                gameOverSound.Play();
            }
        }
    }
}
