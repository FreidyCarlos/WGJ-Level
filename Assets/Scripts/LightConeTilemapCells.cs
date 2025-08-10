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
    [Range(0, 180)] public float angle = 70f; // apunta por +X
    public LayerMask occluderMask = 0;

    [Header("Rendimiento")]
    public int maxCellsPerFrame = 200;   // diffs por frame
    public int losSamplesPerCell = 1;    // 1..3

    [Header("Efectos por CONO")]
    [Tooltip("Segundos que una celda permanece encendida tras salir del haz")]
    public float lingerSeconds = 0.20f;          // 0 = sin persistencia
    [Tooltip("Hz de titileo (0 = sin titileo)")]
    public float flickerHz = 0f;                 // ej. 6 = 6 veces por segundo
    [Tooltip("Porcentaje de tiempo encendido dentro del ciclo (0..1)")]
    [Range(0f, 1f)] public float flickerDuty = 0.5f;
    [Tooltip("Jitter aleatorio del periodo (0..1)")]
    [Range(0f, 1f)] public float flickerJitter = 0f;

    // ------- estado por instancia -------
    readonly HashSet<Vector3Int> _scanThisFrame = new HashSet<Vector3Int>();        // celdas vistas este frame (si el cono está encendido)
    readonly Dictionary<Vector3Int, float> _expiry = new Dictionary<Vector3Int, float>(); // celda -> Time.time límite
    float _phase; // desfase del flicker

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
        _phase = Random.value * 10f; // desfase inicial para no sincronizar todos
    }

    void OnDisable()
    {
        sSets.Remove(this);
        ApplyUnion(); // re-aplica sin este cono
    }

    void LateUpdate()
    {
        if (!sourceTilemap || !litTilemap) return;

        bool coneOn = IsConeOn();
        _scanThisFrame.Clear();

        if (coneOn) ScanBeamAndMarkExpiry();     // marca expiraciones para lo visto hoy
        BuildActiveSetFromExpiry();               // arma el set “vigente” (union usa esto)
        ApplyUnion();                             // aplicar unión una vez por frame (compartido)
    }

    // ---------- lógica por cono ----------
    bool IsConeOn()
    {
        if (flickerHz <= 0f) return true; // sin titileo
        // periodo con jitter
        float baseT = 1f / flickerHz;
        float t = baseT * (1f + Random.Range(-flickerJitter, flickerJitter));
        float local = (Time.time + _phase) % Mathf.Max(0.0001f, t);
        return local < (t * Mathf.Clamp01(flickerDuty));
    }

    void ScanBeamAndMarkExpiry()
    {
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

                if (to.magnitude > range) continue;
                if (Vector2.Angle(fwd, to) > half) continue;
                if (!HasLineOfSight(origin, wc, to.magnitude)) continue;

                _scanThisFrame.Add(cp);
                // marca/renueva expiración
                float until = lingerSeconds > 0f ? Time.time + lingerSeconds : Time.time;
                _expiry[cp] = until;
            }
    }

    void BuildActiveSetFromExpiry()
    {
        // purga expirados
        if (lingerSeconds > 0f)
        {
            // para evitar allocs, recolectamos a borrar
            var toDel = ListPool<Vector3Int>.Get();
            foreach (var kv in _expiry)
                if (kv.Value < Time.time) toDel.Add(kv.Key);
            for (int i = 0; i < toDel.Count; i++) _expiry.Remove(toDel[i]);
            ListPool<Vector3Int>.Release(toDel);
        }
        else
        {
            _expiry.Clear(); // sin linger: solo cuenta lo del frame
            foreach (var c in _scanThisFrame) _expiry[c] = Time.time;
        }

        // actualiza el set que aporta este cono a la unión
        var mySet = sSets[this];
        mySet.Clear();
        foreach (var kv in _expiry) mySet.Add(kv.Key);
    }

    // ---------- unión compartida ----------
    static void ApplyUnion()
    {
        if (Time.frameCount == sLastAppliedFrame) return; // solo 1 vez/frame
        sLastAppliedFrame = Time.frameCount;
        if (sLit == null || sSrc == null) return;

        // unión de todos los conos
        var union = HashSetPool<Vector3Int>.Get();
        foreach (var set in sSets.Values) union.UnionWith(set);

        // diffs vs. último estado aplicado
        var toAdd = ListPool<Vector3Int>.Get();
        var toDel = ListPool<Vector3Int>.Get();

        foreach (var c in union) if (!sLastUnion.Contains(c)) toAdd.Add(c);
        foreach (var c in sLastUnion) if (!union.Contains(c)) toDel.Add(c);

        int budget = 999999; // se puede limitar si quieres
        // agrega
        for (int i = 0; i < toAdd.Count && i < budget; i++)
        {
            var c = toAdd[i];
            var tile = sSrc.GetTile(c);
            sLit.SetTile(c, tile);
            sLit.SetColor(c, sSrc.GetColor(c));
        }
        // elimina
        for (int i = 0; i < toDel.Count && i < budget; i++)
            sLit.SetTile(toDel[i], null);

        if (sCol) sCol.ProcessTilemapChanges();

        // guarda unión
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

    // ---------- pools tontos para evitar GC ----------
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