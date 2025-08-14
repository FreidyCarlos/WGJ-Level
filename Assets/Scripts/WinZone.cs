using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WinZone : MonoBehaviour
{
    public GameObject winCanvas;
    public AudioSource winSound; 

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            winCanvas.SetActive(true);
            Time.timeScale = 0f;

            if (winSound != null)
            {
                winSound.Play();
            }
        }
    }
}
