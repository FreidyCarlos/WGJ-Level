using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public class RevealTilemap : MonoBehaviour
{
    private static readonly HashSet<RevealTilemap> All = new HashSet<RevealTilemap>();

    [Header("Refs (auto si están vacías)")]
    [SerializeField] private Tilemap tilemap;                 // <- usa Tilemap para color
    [SerializeField] private TilemapRenderer tmRenderer;      // <- solo bounds / enabled
    [SerializeField] private TilemapCollider2D tmCollider;
    [SerializeField] private CompositeCollider2D compositeCollider;

    [Header("Apariencia")]
    [Range(0f, 1f)] public float offAlpha = 0f; // 0 = invisible cuando apagado
    private Color _baseColor = Color.white;

    private bool _isLit;

    public Bounds WorldBounds => tmRenderer ? tmRenderer.bounds : GetComponent<Renderer>().bounds;

    void Awake()
    {
        if (!tilemap) tilemap = GetComponent<Tilemap>();
        if (!tmRenderer) tmRenderer = GetComponent<TilemapRenderer>();
        if (!tmCollider) tmCollider = GetComponent<TilemapCollider2D>();
        if (!compositeCollider) compositeCollider = GetComponent<CompositeCollider2D>();

        if (tilemap) _baseColor = tilemap.color;

        SetLit(false); // arranca apagado
        All.Add(this);
    }

    void OnDestroy() => All.Remove(this);

    public void SetLit(bool lit)
    {
        if (_isLit == lit) return;
        _isLit = lit;

        // Visibilidad (tinte/alpha via Tilemap.color)
        if (tilemap)
        {
            var c = _baseColor;
            c.a = lit ? 1f : offAlpha;
            tilemap.color = c;
        }

        // Opcional: ocultar completamente el renderer cuando alpha ~ 0
        if (tmRenderer)
            tmRenderer.enabled = lit || offAlpha > 0.01f;

        // Colisión
        if (compositeCollider) compositeCollider.enabled = lit;
        if (tmCollider) tmCollider.enabled = lit;
    }

    internal static void DisableAllExcept(IList<RevealTilemap> keepLit)
    {
        foreach (var t in All) t.SetLit(false);
        for (int i = 0; i < keepLit.Count; i++) keepLit[i].SetLit(true);
    }

    public static IEnumerable<RevealTilemap> AllTilemaps => All;
}
