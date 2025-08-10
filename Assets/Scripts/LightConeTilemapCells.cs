using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public class LightConeTilemapCells : MonoBehaviour
{
    [Header("Refs (mismas en TODOS los conos)")]
    public Tilemap sourceTilemap;   // Platforms_Source (lleno)
    public Tilemap litTilemap;      // Platforms_Lit (vacío al inicio)
    public TilemapCollider2D litCollider;

    [Header("Cono")]
    public float range = 12f;
    [Range(0, 180)] public float angle = 70f; // apunta por +X
    public LayerMask occluderMask = 0;

    [Header("Rendimiento")]
    public int maxCellsPerFrame = 200;   // aplica diffs por frame
    public int losSamplesPerCell = 1;    // 1..3
    public bool onlyWhenChanged = true;

    // ---- estado por instancia ----
    readonly HashSet<Vector3Int> _myScan = new HashSet<Vector3Int>();

    // ---- estado compartido (todos los conos cooperan) ----
    static Tilemap sLit;
    static Tilemap sSrc;
    static TilemapCollider2D sCol;
    static readonly Dictionary<LightConeTilemapCells, HashSet<Vector3Int>> sSets =
        new Dictionary<LightConeTilemapCells, HashSet<Vector3Int>>();
    static readonly HashSet<Vector3Int> sLastUnion = new HashSet<Vector3Int>();
    static int sLastAppliedFrame = -1;

    void OnEnable()
    {
        if (litTilemap) sLit = litTilemap;
        if (sourceTilemap) sSrc = sourceTilemap;
        if (litCollider) sCol = litCollider;

        sSets[this] = _myScan;
    }

    void OnDisable()
    {
        sSets.Remove(this);
        // fuerza aplicar unión sin este cono
        ApplyUnion();
    }

    void LateUpdate()
    {
        if (!sourceTilemap || !litTilemap) return;

        // 1) escaneo de ESTE cono
        ScanMyCone();

        // 2) aplicar unión (cualquiera puede invocarlo; se limita por frame)
        ApplyUnion();
    }

    void ScanMyCone()
    {
        _myScan.Clear();

        Vector2 origin = transform.position;
        Vector2 fwd = transform.right.normalized;
        float half = angle * 0.5f;
        float r = Mathf.Max(0.1f, range);

        Vector3 wmin = origin + new Vector2(-r, -r);
        Vector3 wmax = origin + new Vector2(+r, +r);
        Vector3Int cmin = sourceTilemap.WorldToCell(wmin);
        Vector3Int cmax = sourceTilemap.WorldToCell(wmax);
        if (cmax.x < cmin.x) { var t = cmin.x; cmin.x = cmax.x; cmax.x = t; }
        if (cmax.y < cmin.y) { var t = cmin.y; cmin.y = cmax.y; cmax.y = t; }

        for (int y = cmin.y; y <= cmax.y; y++)
            for (int x = cmin.x; x <= cmax.x; x++)
            {
                var cp = new Vector3Int(x, y, 0);
                if (!sourceTilemap.HasTile(cp)) continue;

                Vector3 wc = sourceTilemap.GetCellCenterWorld(cp);
                Vector2 to = (Vector2)wc - origin;

                float dist = to.magnitude;
                if (dist > range) continue;
                if (Vector2.Angle(fwd, to) > half) continue;
                if (!HasLineOfSight(origin, wc, dist)) continue;

                _myScan.Add(cp);
            }
    }

    static void ApplyUnion()
    {
        if (Time.frameCount == sLastAppliedFrame) return; // una vez por frame
        sLastAppliedFrame = Time.frameCount;
        if (sLit == null || sSrc == null) return;

        // construir unión actual
        HashSet<Vector3Int> union = new HashSet<Vector3Int>();
        foreach (var set in sSets.Values)
            union.UnionWith(set);

        // diffs vs. última unión aplicada
        List<Vector3Int> toAdd = new List<Vector3Int>();
        List<Vector3Int> toDel = new List<Vector3Int>();

        foreach (var c in union)
            if (!sLastUnion.Contains(c)) toAdd.Add(c);
        foreach (var c in sLastUnion)
            if (!union.Contains(c)) toDel.Add(c);

        int budget = 0;
        // agrega
        for (int i = 0; i < toAdd.Count && budget < GetAny().maxCellsPerFrame; i++, budget++)
        {
            var c = toAdd[i];
            var tile = sSrc.GetTile(c);
            sLit.SetTile(c, tile);
            sLit.SetColor(c, sSrc.GetColor(c));
        }
        // borra
        for (int i = 0; i < toDel.Count && budget < GetAny().maxCellsPerFrame; i++, budget++)
        {
            sLit.SetTile(toDel[i], null);
        }

        if (sCol) sCol.ProcessTilemapChanges();

        // guarda unión
        sLastUnion.Clear();
        sLastUnion.UnionWith(union);
    }

    static LightConeTilemapCells GetAny()
    {
        foreach (var kv in sSets) return kv.Key;
        return null;
    }

    bool HasLineOfSight(Vector2 origin, Vector3 worldTarget, float dist)
    {
        var hit = Physics2D.Raycast(origin, ((Vector2)worldTarget - origin).normalized, dist, occluderMask);
        if (hit.collider == null) return true;
        if (losSamplesPerCell <= 1) return false;

        var halfCell = sourceTilemap.cellSize * 0.5f;
        Vector3 c = worldTarget;
        Vector2[] pts = {
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
            if (h.collider == null) return true;
        }
        return false;
    }
}