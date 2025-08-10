using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public class LightConeTilemapCells : MonoBehaviour
{
    [Header("Refs")]
    public Tilemap sourceTilemap;   // Platforms_Source (con todas las plataformas)
    public Tilemap litTilemap;      // Platforms_Lit (vacío al inicio)
    public TilemapCollider2D litCollider; // del Platforms_Lit (opcional auto)

    [Header("Cono")]
    public float range = 12f;
    [Range(0, 180)] public float angle = 70f; // mira por +X (transform.right)
    public LayerMask occluderMask;            // paredes que bloquean

    [Header("Rendimiento")]
    public int maxCellsPerFrame = 200;        // limita cambios por frame
    public int losSamplesPerCell = 1;         // 1..3 (centro + esquinas si quieres)
    public bool onlyWhenChanged = true;       // no reprocesar si no cambió

    // Estado
    private readonly HashSet<Vector3Int> _litCells = new HashSet<Vector3Int>();
    private readonly HashSet<Vector3Int> _curScan = new HashSet<Vector3Int>();
    private readonly List<Vector3Int> _toAdd = new List<Vector3Int>();
    private readonly List<Vector3Int> _toRemove = new List<Vector3Int>();

    void Reset()
    {
        if (!litCollider && litTilemap) litCollider = litTilemap.GetComponent<TilemapCollider2D>();
    }

    void LateUpdate()
    {
        if (!sourceTilemap || !litTilemap) return;

        Vector3 o3 = transform.position;
        Vector2 origin = o3;
        Vector2 fwd = transform.right.normalized;
        float half = angle * 0.5f;

        // 1) delimitar AABB de celdas alrededor del cono
        float r = Mathf.Max(0.1f, range);
        Vector3 wmin = origin + new Vector2(-r, -r);
        Vector3 wmax = origin + new Vector2(+r, +r);
        Vector3Int cmin = sourceTilemap.WorldToCell(wmin);
        Vector3Int cmax = sourceTilemap.WorldToCell(wmax);
        if (cmax.x < cmin.x) { var t = cmin.x; cmin.x = cmax.x; cmax.x = t; }
        if (cmax.y < cmin.y) { var t = cmin.y; cmin.y = cmax.y; cmax.y = t; }

        _curScan.Clear();

        // 2) escanear celdas dentro del AABB
        int changedBudget = maxCellsPerFrame;
        for (int y = cmin.y; y <= cmax.y; y++)
        {
            for (int x = cmin.x; x <= cmax.x; x++)
            {
                var cpos = new Vector3Int(x, y, 0);
                if (!sourceTilemap.HasTile(cpos)) continue;

                // centro de la celda
                Vector3 wc = sourceTilemap.GetCellCenterWorld(cpos);
                Vector2 to = (Vector2)wc - origin;

                // distancia
                float dist = to.magnitude;
                if (dist > range) continue;

                // ángulo
                if (Vector2.Angle(fwd, to) > half) continue;

                // línea de vista (centro; opcional: esquinas)
                if (!HasLineOfSight(origin, wc, dist)) continue;

                _curScan.Add(cpos);
            }
        }

        // 3) diferencias (qué agregar / qué quitar)
        _toAdd.Clear();
        _toRemove.Clear();

        foreach (var c in _curScan)
            if (!_litCells.Contains(c)) _toAdd.Add(c);

        foreach (var c in _litCells)
            if (!_curScan.Contains(c)) _toRemove.Add(c);

        if (onlyWhenChanged && _toAdd.Count == 0 && _toRemove.Count == 0)
            return;

        // 4) aplicar cambios limitando por frame
        int ops = 0;

        for (int i = 0; i < _toAdd.Count && ops < changedBudget; i++)
        {
            var c = _toAdd[i];
            var tile = sourceTilemap.GetTile(c);
            litTilemap.SetTile(c, tile);
            // opcional: igualar color del source
            litTilemap.SetColor(c, sourceTilemap.GetColor(c));
            _litCells.Add(c);
            ops++;
        }

        for (int i = 0; i < _toRemove.Count && ops < changedBudget; i++)
        {
            var c = _toRemove[i];
            litTilemap.SetTile(c, null);
            _litCells.Remove(c);
            ops++;
        }

        // 5) avisar al collider
        if (litCollider) litCollider.ProcessTilemapChanges();
    }

    bool HasLineOfSight(Vector2 origin, Vector3 worldTarget, float dist)
    {
        // centro
        var hit = Physics2D.Raycast(origin, ((Vector2)worldTarget - origin).normalized, dist, occluderMask);
        if (hit.collider == null) return true;

        if (losSamplesPerCell <= 1) return false;

        // esquinas (para celdas grandes)
        var halfCell = sourceTilemap.cellSize * 0.5f;
        Vector3 c = worldTarget;
        Vector2[] pts = new Vector2[] {
            new Vector2(c.x - halfCell.x, c.y - halfCell.y),
            new Vector2(c.x + halfCell.x, c.y - halfCell.y),
            new Vector2(c.x - halfCell.x, c.y + halfCell.y),
            new Vector2(c.x + halfCell.x, c.y + halfCell.y),
        };

        int samples = Mathf.Min(losSamplesPerCell - 1, pts.Length);
        for (int i = 0; i < samples; i++)
        {
            float d = Vector2.Distance(origin, pts[i]);
            var h = Physics2D.Raycast(origin, (pts[i] - origin).normalized, d, occluderMask);
            if (h.collider == null) return true; // al menos una esquina con vista
        }
        return false;
    }

    void OnDrawGizmosSelected()
    {
        // gizmo del cono para depurar
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