using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LightCone2D_Tilemap : MonoBehaviour
{
    [Header("Geometría del cono")]
    public float range = 12f;
    [Range(0f, 180f)] public float angle = 70f;   // apertura total
    [Tooltip("El cono apunta por +X (transform.right)")]
    public bool showGizmos = true;

    [Header("Capas")]
    public LayerMask occluderMask; // capa de paredes/rocas que bloquean luz

    [Header("Rendimiento")]
    public int maxChecksPerFrame = 64;  // cuántos tilemaps evalúo por frame
    public int losSamplePoints = 3;     // 1..5 -> centro + esquinas

    private readonly List<RevealTilemap> _litThisFrame = new List<RevealTilemap>(64);

    void LateUpdate()
    {
        Vector2 origin = transform.position;
        Vector2 forward = transform.right.normalized; // +X
        float half = angle * 0.5f;

        _litThisFrame.Clear();
        int checkedCount = 0;

        foreach (var rt in RevealTilemap.AllTilemaps)
        {
            if (checkedCount++ >= maxChecksPerFrame) break;

            var b = rt.WorldBounds;

            // Distancia (al centro)
            Vector2 center = b.center;
            float dist = Vector2.Distance(origin, center);
            if (dist > range) continue;

            // Ángulo (al centro)
            Vector2 toCenter = center - origin;
            if (Vector2.Angle(forward, toCenter) > half) continue;

            // Línea de vista: al menos un punto sin bloqueo
            if (HasLineOfSight(origin, b, forward, half))
                _litThisFrame.Add(rt);
        }

        RevealTilemap.DisableAllExcept(_litThisFrame);
    }

    bool HasLineOfSight(Vector2 origin, Bounds b, Vector2 forward, float half)
    {
        Vector2[] pts = (losSamplePoints <= 1)
            ? new Vector2[] { b.center }
            : new Vector2[] {
                b.center,
                new Vector2(b.min.x, b.min.y),
                new Vector2(b.max.x, b.min.y),
                new Vector2(b.min.x, b.max.y),
                new Vector2(b.max.x, b.max.y)
              };

        int samples = Mathf.Clamp(losSamplePoints, 1, pts.Length);

        for (int i = 0; i < samples; i++)
        {
            Vector2 target = pts[i];
            Vector2 dir = target - origin;
            float dist = dir.magnitude;
            if (dist <= 0.001f) return true;

            if (Vector2.Angle(forward, dir) > half) continue;

            // Si NO golpea oclusor, hay visión
            var hit = Physics2D.Raycast(origin, dir.normalized, dist, occluderMask);
            if (hit.collider == null) return true;
        }
        return false;
    }

    void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;
        Gizmos.color = Color.yellow;

        Vector3 o = transform.position;
        Vector2 f = transform.right.normalized;
        float half = angle * 0.5f;

        Vector2 dirA = Quaternion.Euler(0, 0, half) * f;
        Vector2 dirB = Quaternion.Euler(0, 0, -half) * f;

        Gizmos.DrawLine(o, o + (Vector3)(dirA * range));
        Gizmos.DrawLine(o, o + (Vector3)(dirB * range));

        int steps = 16;
        Vector2 prev = dirA;
        for (int i = 1; i <= steps; i++)
        {
            float t = Mathf.Lerp(half, -half, i / (float)steps);
            Vector2 dir = Quaternion.Euler(0, 0, t) * f;
            Gizmos.DrawLine(o + (Vector3)(prev * range), o + (Vector3)(dir * range));
            prev = dir;
        }
    }
}
