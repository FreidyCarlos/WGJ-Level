using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public class LightConeTilemapCells : MonoBehaviour
{
    [Header("Refs (mismas en TODOS los conos)")]
    public Tilemap sourceTilemap;      // Platforms_Source (lleno)
    public Tilemap litTilemap;         // Platforms_Lit (vacío al inicio)
    public TilemapCollider2D litCollider;

    [Header("Cono")]
    public float range = 12f;
    [Range(0, 180)] public float angle = 70f; // mira por +X del transform
    public LayerMask occluderMask = 0;

    [Header("Rendimiento")]
    public int maxCellsPerFrame = 200;   // diffs por frame (al aplicar unión)
    public int losSamplesPerCell = 1;    // 1..3

    [Header("Efectos por CONO")]
    [Tooltip("Segs que una celda permanece encendida tras salir del haz")]
    public float lingerSeconds = 0.20f;
    [Tooltip("Hz de titileo (0 = sin titileo)")]
    public float flickerHz = 0f;
    [Tooltip("Porcentaje de tiempo encendido dentro del ciclo (0..1)")]
    [Range(0f, 1f)] public float flickerDuty = 0.5f;
    [Tooltip("Jitter aleatorio del periodo (0..1)")]
    [Range(0f, 1f)] public float flickerJitter = 0f;

    // --- Debug ---
    [Header("Gizmos (debug)")]
    public bool showGizmos = true;                                  // mostrar cono
    public Color gizmoFill = new Color(1f, 1f, 0f, 0.15f);          // amarillo translúcido
    public Color gizmoEdge = new Color(1f, 0.9f, 0.2f, 0.9f);       // borde

    // ---------- estado público de sincronía ----------
    [HideInInspector] public bool coneOnNow;          // true = este frame el cono está "encendido"
    float _visualLingerUntil = -999f;                 // linger visual (para luz 2D sincronizada)
    public bool IsVisuallyOnThisFrame()
    {
        if (coneOnNow && lingerSeconds > 0f)
            _visualLingerUntil = Time.time + lingerSeconds;
        return coneOnNow || (lingerSeconds > 0f && Time.time <= _visualLingerUntil);
    }

    // ------- estado por instancia -------
    readonly HashSet<Vector3Int> _scanThisFrame = new HashSet<Vector3Int>();
    readonly Dictionary<Vector3Int, float> _expiry = new Dictionary<Vector3Int, float>();
    float _phase;

    // ------- estado compartido (unión entre conos) -------
    static Tilemap sLit, sSrc;
    static TilemapCollider2D sCol;
    static readonly Dictionary<LightConeTilemapCells, HashSet<Vector3Int>> sSets = new Dictionary<LightConeTilemapCells, HashSet<Vector3Int>>();
    static readonly HashSet<Vector3Int> sLastUnion = new HashSet<Vector3Int>();
    static int sLastAppliedFrame = -1;

    void OnEnable()
    {
        if (litTilemap) sLit = litTilemap;
        if (sourceTilemap) sSrc = sourceTilemap;
        if (litCollider) sCol = litCollider;

        sSets[this] = new HashSet<Vector3Int>();
        _phase = Random.value * 10f;
    }

    void OnDisable()
    {
        sSets.Remove(this);
        ApplyUnion(); // re-aplica sin este cono
    }

    void LateUpdate()
    {
        if (!sourceTilemap || !litTilemap) return;

        // 1) calcular ON/OFF del flicker una sola vez (para sincronía)
        coneOnNow = IsConeOn();

        // 2) escanear solo si está encendido
        _scanThisFrame.Clear();
        if (coneOnNow) ScanBeamAndMarkExpiry();

        // 3) construir set activo desde expiraciones (linger)
        BuildActiveSetFromExpiry();

        // 4) aplicar la UNIÓN entre todos los conos (una vez por frame)
        ApplyUnion();
    }

    // ---------- flicker local ----------
    bool IsConeOn()
    {
        if (flickerHz <= 0f) return true;
        float baseT = 1f / Mathf.Max(0.0001f, flickerHz);
        float t = baseT * (1f + Random.Range(-flickerJitter, flickerJitter)); // jitter por frame para look “sucio”
        float local = (Time.time + _phase) % t;
        return local < (t * Mathf.Clamp01(flickerDuty));
    }

    // ---------- escaneo del haz ----------
    void ScanBeamAndMarkExpiry()
    {
        Vector2 origin = transform.position;
        Vector2 fwd = transform.right.normalized; // el cono mira por +X
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

                if (to.magnitude > range) continue;
                if (Vector2.Angle(fwd, to) > half) continue;
                if (!HasLineOfSight(origin, wc, to.magnitude)) continue;

                _scanThisFrame.Add(cp);
                float until = lingerSeconds > 0f ? Time.time + lingerSeconds : Time.time;
                _expiry[cp] = until;
            }
    }

    void BuildActiveSetFromExpiry()
    {
        if (lingerSeconds > 0f)
        {
            var toDel = ListPool<Vector3Int>.Get();
            foreach (var kv in _expiry) if (kv.Value < Time.time) toDel.Add(kv.Key);
            for (int i = 0; i < toDel.Count; i++) _expiry.Remove(toDel[i]);
            ListPool<Vector3Int>.Release(toDel);
        }
        else
        {
            _expiry.Clear();
            foreach (var c in _scanThisFrame) _expiry[c] = Time.time;
        }

        var mySet = sSets[this];
        mySet.Clear();
        foreach (var kv in _expiry) mySet.Add(kv.Key);
    }

    // ---------- unión compartida ----------
    static void ApplyUnion()
    {
        if (Time.frameCount == sLastAppliedFrame) return; // 1 vez/frame
        sLastAppliedFrame = Time.frameCount;
        if (sLit == null || sSrc == null) return;

        var union = HashSetPool<Vector3Int>.Get();
        foreach (var set in sSets.Values) union.UnionWith(set);

        var toAdd = ListPool<Vector3Int>.Get();
        var toDel = ListPool<Vector3Int>.Get();

        foreach (var c in union) if (!sLastUnion.Contains(c)) toAdd.Add(c);
        foreach (var c in sLastUnion) if (!union.Contains(c)) toDel.Add(c);

        int budget = 0;
        int maxOps = 999999; // si quieres, cámbialo por GetAny().maxCellsPerFrame

        for (int i = 0; i < toAdd.Count && budget < maxOps; i++, budget++)
        {
            var c = toAdd[i];
            var tile = sSrc.GetTile(c);
            sLit.SetTile(c, tile);
            sLit.SetColor(c, sSrc.GetColor(c));
        }
        for (int i = 0; i < toDel.Count && budget < maxOps; i++, budget++)
            sLit.SetTile(toDel[i], null);

        if (sCol) sCol.ProcessTilemapChanges();

        sLastUnion.Clear();
        sLastUnion.UnionWith(union);

        HashSetPool<Vector3Int>.Release(union);
        ListPool<Vector3Int>.Release(toAdd);
        ListPool<Vector3Int>.Release(toDel);
    }

    bool HasLineOfSight(Vector2 origin, Vector3 target, float dist)
    {
        var hit = Physics2D.Raycast(origin, ((Vector2)target - origin).normalized, dist, occluderMask);
        if (hit.collider == null) return true;
        if (losSamplesPerCell <= 1) return false;

        var halfCell = sourceTilemap.cellSize * 0.5f;
        Vector3 c = target;
        Vector2[] pts =
        {
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

    // ---------- Gizmos (debug visual del cono) ----------
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showGizmos) return;
        DrawConeGizmo(filled:true, alphaScale:0.6f); // siempre visible en escena
    }

    void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;
        DrawConeGizmo(filled:true, alphaScale:1f); // más marcado al seleccionar
    }

    void DrawConeGizmo(bool filled, float alphaScale)
    {
        var o = transform.position;
        var f = (Vector2)transform.right.normalized; // misma dirección que la lógica
        float half = angle * 0.5f;

        // Bordes
        Vector2 dirA = Quaternion.Euler(0, 0, +half) * f;
        Vector2 dirB = Quaternion.Euler(0, 0, -half) * f;

        // Relleno con Handles si estamos en editor
        var fillCol = gizmoFill; fillCol.a *= alphaScale;
        var edgeCol = gizmoEdge;

        // Borde con líneas
        Gizmos.color = edgeCol;
        Gizmos.DrawLine(o, o + (Vector3)(dirA * range));
        Gizmos.DrawLine(o, o + (Vector3)(dirB * range));

        // Arco con segmentos
        int steps = Mathf.Clamp(Mathf.RoundToInt(angle), 8, 128);
        Vector2 prev = dirA;
        for (int i = 1; i <= steps; i++)
        {
            float t = Mathf.Lerp(+half, -half, i / (float)steps);
            Vector2 dir = Quaternion.Euler(0, 0, t) * f;
            Gizmos.DrawLine(o + (Vector3)(prev * range), o + (Vector3)(dir * range));
            prev = dir;
        }

        // Relleno sólido (solo en editor)
#if UNITY_EDITOR
        if (filled)
        {
            UnityEditor.Handles.color = fillCol;
            UnityEditor.Handles.DrawSolidArc(
                o,
                Vector3.forward,                // eje Z (2D)
                dirB,                           // empieza en borde inferior
                angle,                          // recorre hasta borde superior
                range
            );
        }
#endif
    }
#endif

    // ---------- pools para evitar GC ----------
    static class ListPool<T>
    {
        static readonly Stack<List<T>> pool = new Stack<List<T>>();
        public static List<T> Get() => pool.Count > 0 ? pool.Pop() : new List<T>();
        public static void Release(List<T> list) { list.Clear(); pool.Push(list); }
    }
    static class HashSetPool<T>
    {
        static readonly Stack<HashSet<T>> pool = new Stack<HashSet<T>>();
        public static HashSet<T> Get() => pool.Count > 0 ? pool.Pop() : new HashSet<T>();
        public static void Release(HashSet<T> set) { set.Clear(); pool.Push(set); }
    }
}