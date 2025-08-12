using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelIntroSequence : MonoBehaviour
{
    public GameObject introCanvas;
    public CanvasGroup perrito;
    public CanvasGroup marco;
    public AudioSource audioSource;

    public float fadeDuration = 1f; // tiempo de fade in/out
    public float displayTime = 2f;  // tiempo visible entre fades

    void Start()
    {
        if (PlayerPrefs.GetInt("IntroPlayed", 0) == 1)
        {
            introCanvas.SetActive(false);
            Time.timeScale = 1f;
            return;
        }

        StartCoroutine(IntroSequence());
    }

    IEnumerator IntroSequence()
    {
        Time.timeScale = 0f; 
        introCanvas.SetActive(true);

        yield return StartCoroutine(FadeIn(perrito));
        if (audioSource != null) audioSource.Play();
        yield return new WaitForSecondsRealtime(displayTime);
        yield return StartCoroutine(FadeOut(perrito));

        yield return StartCoroutine(FadeIn(marco));
        yield return new WaitForSecondsRealtime(displayTime);
        yield return StartCoroutine(FadeOut(marco));

        introCanvas.SetActive(false);
        Time.timeScale = 1f;
    }

    IEnumerator FadeIn(CanvasGroup cg)
    {
        cg.gameObject.SetActive(true);
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(0f, 1f, t / fadeDuration);
            yield return null;
        }
        cg.alpha = 1f;
    }

    IEnumerator FadeOut(CanvasGroup cg)
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(1f, 0f, t / fadeDuration);
            yield return null;
        }
        cg.alpha = 0f;
        cg.gameObject.SetActive(false);
    }
}
