using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.Universal; // URP 2D (Experimental)

[DisallowMultipleComponent]
public class Light2D_FlickerSync : MonoBehaviour
{
    [Header("Refs")]
    public LightConeTilemapCells cone;  // en el mismo GO
    public Light2D light2D;             // hijo con Light 2D (Freeform/Parametric/Point/Global)

    [Header("Comportamiento")]
    [Tooltip("Copia range/angle del cono. En Freeform no tendrá efecto (solo en tipos que usan radio/ángulo).")]
    public bool syncRangeAndAngle = true;
    [Tooltip("La luz queda encendida visualmente durante lingerSeconds, igual que los tiles.")]
    public bool visualLinger = false;

    [Header("Intensidad")]
    public float onIntensity = 1.5f;
    public float offIntensity = 0f;
    public float intensityLerpSpeed = 12f;

    float _phase;
    float _jitterSign;
    float _lingerUntil = -999f;

    void Reset()
    {
        if (!cone) cone = GetComponent<LightConeTilemapCells>();
        if (!light2D) light2D = GetComponentInChildren<Light2D>();
    }

    void Awake()
    {
        if (!cone) cone = GetComponent<LightConeTilemapCells>();
        if (!light2D) light2D = GetComponentInChildren<Light2D>();

        _phase = Random.value * 10f;
        _jitterSign = Random.value < 0.5f ? -1f : 1f;
    }

    void LateUpdate()
    {
        if (!cone || !light2D) return;

        // Copiar geometría (si el tipo de luz la usa: Parametric/Point sí, Freeform no)
        if (syncRangeAndAngle)
        {
            light2D.pointLightOuterRadius = cone.range;
            light2D.pointLightOuterAngle = cone.angle;
        }

        // Encendido según flicker del cono
        bool onNow = IsConeOnLike(cone);

        // Linger visual opcional
        if (visualLinger && cone.lingerSeconds > 0f && onNow)
            _lingerUntil = Time.time + cone.lingerSeconds;

        bool shouldShow = onNow || (visualLinger && Time.time <= _lingerUntil);

        // Suavizar intensidad
        float target = shouldShow ? onIntensity : offIntensity;
        light2D.intensity = Mathf.Lerp(light2D.intensity, target, intensityLerpSpeed * Time.deltaTime);
    }

    bool IsConeOnLike(LightConeTilemapCells c)
    {
        if (c.flickerHz <= 0f) return true;

        float baseT = 1f / Mathf.Max(0.0001f, c.flickerHz);
        float t = baseT * (1f + c.flickerJitter * _jitterSign); // jitter fijo por luz
        float local = (Time.time + _phase) % t;
        return local < (t * Mathf.Clamp01(c.flickerDuty));
    }
}