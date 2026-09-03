using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Procedurally arranges suit pips on the card face for number cards
/// (2–10) using the standard playing-card pip grid, plus a single large
/// pip for Ace. Face cards (J/Q/K, cardValue 11–13) hide all pips —
/// intended to be replaced by your custom illustrated art for those.
///
/// SETUP:
///   1. Add this component to Card3Droot (alongside Card3DView).
///   2. Assign "Source Card" — same Card object Card3DView.SourceCard
///      points to (drag the same "Card" child in).
///   3. Assign "Pip Template" — drag in your SuitTopLeft object (or any
///      TextMeshPro suit-symbol object with the font/size/color you
///      want pips to use). It gets disabled at runtime and only used
///      as a stencil to clone from — it won't render itself.
///   4. Optionally assign "Face Art Root" — a GameObject holding your
///      J/Q/K custom art, shown instead of pips for cardValue 11–13.
///
/// TUNING: X Scale / Y Scale control how far apart the pip grid columns
/// and rows spread across the card face — tweak these first if pips
/// overlap the corner rank/suit text or run off the card edges.
/// </summary>
public class CardPipLayout : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private Card sourceCard;

    [Header("Pip Template (disabled at runtime, only used to clone from)")]
    [SerializeField] private TextMeshPro pipTemplate;

    [Header("Optional — shown instead of pips for J/Q/K")]
    [SerializeField] private GameObject faceArtRoot;

    [Header("Grid Tuning")]
    [SerializeField] private float xScale = 0.032f;   // half-spread of the left/right pip columns
    [SerializeField] private float ySpacing = 0.07f;  // half-spread of the top/bottom pip rows
    [SerializeField] private float pipLiftY = 0.002f;
    [Tooltip("Multiplies the Pip Template's own scale, instead of using a fixed size — keeps pips consistent with whatever scale you already tuned SuitTopLeft to.")]
    [SerializeField] private float normalPipScaleMultiplier = 1f;
    [SerializeField] private float acePipScaleMultiplier = 2.2f;
    [Tooltip("If pips are spreading in the wrong direction (e.g. perpendicular to the card face instead of across it), toggle this — it swaps which local axis (Y vs Z) carries the row offset.")]
    [SerializeField] private bool useYAxisForRows = true;

    private struct PipSpot
    {
        public float x, y;
        public bool rotated;
        public PipSpot(float x, float y, bool rotated) { this.x = x; this.y = y; this.rotated = rotated; }
    }

    // Standard pip layouts, x in {-1, 0, 1} (left/center/right column),
    // y in [-1, 1] (bottom to top). Bottom-half pips (y < 0) are rotated
    // 180 so they read correctly regardless of which way the card faces.
    private static readonly Dictionary<int, List<PipSpot>> Layouts = new Dictionary<int, List<PipSpot>>
    {
        { 2, new List<PipSpot> {
            new PipSpot(0, 1, false), new PipSpot(0, -1, true)
        }},
        { 3, new List<PipSpot> {
            new PipSpot(0, 1, false), new PipSpot(0, 0, false), new PipSpot(0, -1, true)
        }},
        { 4, new List<PipSpot> {
            new PipSpot(-1, 1, false), new PipSpot(1, 1, false),
            new PipSpot(-1, -1, true), new PipSpot(1, -1, true)
        }},
        { 5, new List<PipSpot> {
            new PipSpot(-1, 1, false), new PipSpot(1, 1, false),
            new PipSpot(0, 0, false),
            new PipSpot(-1, -1, true), new PipSpot(1, -1, true)
        }},
        { 6, new List<PipSpot> {
            new PipSpot(-1, 1, false), new PipSpot(1, 1, false),
            new PipSpot(-1, 0, false), new PipSpot(1, 0, false),
            new PipSpot(-1, -1, true), new PipSpot(1, -1, true)
        }},
        { 7, new List<PipSpot> {
            new PipSpot(-1, 1, false), new PipSpot(1, 1, false),
            new PipSpot(0, 0.5f, false),
            new PipSpot(-1, 0, false), new PipSpot(1, 0, false),
            new PipSpot(-1, -1, true), new PipSpot(1, -1, true)
        }},
        { 8, new List<PipSpot> {
            new PipSpot(-1, 1, false), new PipSpot(1, 1, false),
            new PipSpot(0, 0.5f, false),
            new PipSpot(-1, 0, false), new PipSpot(1, 0, false),
            new PipSpot(0, -0.5f, true),
            new PipSpot(-1, -1, true), new PipSpot(1, -1, true)
        }},
        { 9, new List<PipSpot> {
            new PipSpot(-1, 1, false), new PipSpot(1, 1, false),
            new PipSpot(-1, 0.33f, false), new PipSpot(1, 0.33f, false),
            new PipSpot(0, 0, false),
            new PipSpot(-1, -0.33f, true), new PipSpot(1, -0.33f, true),
            new PipSpot(-1, -1, true), new PipSpot(1, -1, true)
        }},
        { 10, new List<PipSpot> {
            new PipSpot(-1, 1, false), new PipSpot(1, 1, false),
            new PipSpot(0, 0.65f, false),
            new PipSpot(-1, 0.33f, false), new PipSpot(1, 0.33f, false),
            new PipSpot(-1, -0.33f, true), new PipSpot(1, -0.33f, true),
            new PipSpot(0, -0.65f, true),
            new PipSpot(-1, -1, true), new PipSpot(1, -1, true)
        }},
    };

    private readonly List<TextMeshPro> _spawnedPips = new List<TextMeshPro>();
    private Transform _pipContainer;

    private int _lastValue = int.MinValue;
    private string _lastSuit = null;

    void Awake()
    {
        if (pipTemplate == null)
        {
            Debug.LogWarning("[CardPipLayout] Pip Template is not assigned — can't build the pip container yet.");
            return;
        }

        pipTemplate.gameObject.SetActive(false);

        var containerGO = new GameObject("Pips");
        // Match the template's own parent (not this script's GameObject) so
        // cloned pips inherit the exact same tilt/orientation the template
        // already has, regardless of which object CardPipLayout is attached to.
        containerGO.transform.SetParent(pipTemplate.transform.parent, false);
        _pipContainer = containerGO.transform;
    }

    void Update()
    {
        if (sourceCard == null) return;

        if (sourceCard.cardValue != _lastValue || sourceCard.cardSuit != _lastSuit)
        {
            RefreshPips();
            _lastValue = sourceCard.cardValue;
            _lastSuit = sourceCard.cardSuit;
        }
    }

    public void RefreshPips()
    {
        ClearPips();

        if (sourceCard == null || pipTemplate == null) return;

        int value = sourceCard.cardValue;
        bool isFaceCard = value >= 11 && value <= 13;

        if (faceArtRoot != null)
            faceArtRoot.SetActive(isFaceCard);

        if (isFaceCard) return; // custom art handles J/Q/K, no pips

        if (value == 1)
        {
            SpawnPip(0f, 0f, false, acePipScaleMultiplier);
            return;
        }

        if (value < 1 || value > 10)
        {
            // 0 (undealt/default) or anything outside the number-card
            // range — nothing to draw, and not worth warning about since
            // decorative/template card instances legitimately sit at
            // cardValue 0 before ever being dealt.
            return;
        }

        if (!Layouts.TryGetValue(value, out var spots))
        {
            Debug.LogWarning($"[CardPipLayout] No pip layout defined for value {value}.");
            return;
        }

        foreach (var spot in spots)
            SpawnPip(spot.x, spot.y, spot.rotated, normalPipScaleMultiplier);
    }

    void SpawnPip(float normalizedX, float normalizedY, bool rotated, float scaleMultiplier)
    {
        var instance = Instantiate(pipTemplate, _pipContainer);
        instance.gameObject.SetActive(true);
        instance.gameObject.name = "Pip";

        instance.text = sourceCard.cardSuit;
        bool isRed = sourceCard.cardSuit == "♥" || sourceCard.cardSuit == "♦";
        instance.color = isRed ? new Color(0.55f, 0.08f, 0.07f) : new Color(0.10f, 0.09f, 0.09f);

        Vector3 localPos = useYAxisForRows
            ? new Vector3(normalizedX * xScale, normalizedY * ySpacing, pipLiftY)
            : new Vector3(normalizedX * xScale, pipLiftY, normalizedY * ySpacing);
        instance.transform.localPosition = localPos;

        Vector3 baseEuler = pipTemplate.transform.localEulerAngles;
        instance.transform.localRotation = Quaternion.Euler(
            baseEuler.x, baseEuler.y, rotated ? baseEuler.z + 180f : baseEuler.z);

        // Scale relative to the template's own tuned scale, not an absolute
        // number — keeps pips visually consistent with SuitTopLeft.
        instance.transform.localScale = pipTemplate.transform.localScale * scaleMultiplier;

        _spawnedPips.Add(instance);
    }

    void ClearPips()
    {
        foreach (var pip in _spawnedPips)
            if (pip != null) Destroy(pip.gameObject);
        _spawnedPips.Clear();
    }
}