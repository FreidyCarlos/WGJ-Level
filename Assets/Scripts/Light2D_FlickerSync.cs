using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.Universal; // URP 2D (Experimental)

[DisallowMultipleComponent]
public class Light2D_FlickerSync : MonoBehaviour
{
    [Header("Refs")]
    public LightConeTilemapCells cone;  // el del mismo LightCone
    public Light2D light2D;             // hijo con Freeform/Parametric/Point/Global

    [Header("Geometría (si aplica)")]
    [Tooltip("Copia range/angle del cono (no afecta Freeform).")]
    public bool syncRangeAndAngle = true;

    [Header("Intensidad")]
    public float onIntensity = 1.5f;
    public float offIntensity = 0f;
    public float intensityLerpSpeed = 12f;

    void Reset()
    {
        if (!cone) cone = GetComponent<LightConeTilemapCells>();
        if (!light2D) light2D = GetComponentInChildren<Light2D>();
    }

    void LateUpdate()
    {
        if (!cone || !light2D) return;

        // Copiar range/angle (solo tendrá efecto en tipos que lo usen; Freeform lo ignora)
        if (syncRangeAndAngle)
        {
            light2D.pointLightOuterRadius = cone.range;
            light2D.pointLightOuterAngle = cone.angle;
        }

        // SINCRONÍA PERFECTA: usar exactamente el estado del cono (flicker + linger)
        bool shouldShow = cone.IsVisuallyOnThisFrame();

        float target = shouldShow ? onIntensity : offIntensity;
        light2D.intensity = Mathf.Lerp(light2D.intensity, target, intensityLerpSpeed * Time.deltaTime);
    }
}