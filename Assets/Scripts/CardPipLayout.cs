using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Procedurally arranges suit pips on number cards (2–10) using the
/// standard playing-card pip grid, plus a single large pip for Ace.
/// Face cards (J/Q/K) show personalized art only for their matching suit
/// and use generic rank art for all other suits.
/// </summary>
public class CardPipLayout : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private Card sourceCard;

    [Header("Pip Template (disabled at runtime, used to clone from)")]
    [SerializeField] private TextMeshPro pipTemplate;

    [Header("Face Art Overlay")]
    [Tooltip("Optional parent object for the transparent art overlay.")]
    [SerializeField] private GameObject faceArtRoot;

    [Tooltip("Renderer on the transparent art plane/Quad. Assign a material that supports alpha.")]
    [SerializeField] private Renderer faceArtRenderer;

    [Header("Generic Face Art")]
    [SerializeField] private Texture2D genericJackArt;
    [SerializeField] private Texture2D genericQueenArt;
    [SerializeField] private Texture2D genericKingArt;

    [Header("Character-Specific Face Art")]
    [SerializeField] private Texture2D yuriJackOfClubsArt;
    [SerializeField] private Texture2D liraQueenOfHeartsArt;
    [SerializeField] private Texture2D miraQueenOfDiamondsArt;
    [SerializeField] private Texture2D brahmKingOfSpadesArt;

    [Header("Grid Tuning")]
    [SerializeField] private float xScale = 0.025f;   // half-spread of the left/right pip columns
    [SerializeField] private float ySpacing = 0.0525f;  // half-spread of the top/bottom pip rows
    [SerializeField] private float pipLiftY = 0.002f;

    [Tooltip("Multiplies the Pip Template's own scale.")]
    [SerializeField] private float normalPipScaleMultiplier = 1f;

    [SerializeField] private float acePipScaleMultiplier = 2.2f;

    [Tooltip("Swaps which local axis carries the pip row offset.")]
    [SerializeField] private bool useYAxisForRows = true;

    private struct PipSpot
    {
        public float x;
        public float y;
        public bool rotated;

        public PipSpot(float x, float y, bool rotated)
        {
            this.x = x;
            this.y = y;
            this.rotated = rotated;
        }
    }

    // Standard layouts: x is left/center/right and y is bottom to top.
    // Bottom-half pips rotate 180 degrees for two-way card readability.
    private static readonly Dictionary<int, List<PipSpot>> Layouts =
        new Dictionary<int, List<PipSpot>>
    {
        { 2, new List<PipSpot> {
            new PipSpot(0, 1, false), new PipSpot(0, -1, true)
        }},
        { 3, new List<PipSpot> {
            new PipSpot(0, 1, false), new PipSpot(0, 0, false),
            new PipSpot(0, -1, true)
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

    private readonly List<TextMeshPro> spawnedPips = new List<TextMeshPro>();
    private Transform pipContainer;
    private MaterialPropertyBlock artPropertyBlock;

    private int lastValue = int.MinValue;
    private string lastSuit;

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    private void Awake()
    {
        artPropertyBlock = new MaterialPropertyBlock();

        if (faceArtRenderer == null && faceArtRoot != null)
            faceArtRenderer = faceArtRoot.GetComponentInChildren<Renderer>(true);

        if (pipTemplate == null)
        {
            Debug.LogWarning(
                "[CardPipLayout] Pip Template is not assigned. Face art can still work, but pips cannot be created.",
                this);
        }
        else
        {
            pipTemplate.gameObject.SetActive(false);

            GameObject containerObject = new GameObject("Pips");
            // Match the template's parent so spawned pips inherit its orientation.
            containerObject.transform.SetParent(pipTemplate.transform.parent, false);
            pipContainer = containerObject.transform;
        }
    }

    private void Update()
    {
        if (sourceCard == null)
            return;

        if (sourceCard.cardValue != lastValue || sourceCard.cardSuit != lastSuit)
        {
            RefreshPips();
            lastValue = sourceCard.cardValue;
            lastSuit = sourceCard.cardSuit;
        }
    }

    public void RefreshPips()
    {
        ClearPips();

        if (sourceCard == null)
        {
            ApplyFaceArt(null);
            return;
        }

        int value = sourceCard.cardValue;
        bool isFaceCard = value >= 11 && value <= 13;

        if (isFaceCard)
        {
            ApplyFaceArt(GetFaceArt());
            return;
        }

        ApplyFaceArt(null);

        if (pipTemplate == null || pipContainer == null)
            return;

        if (value == 1)
        {
            SpawnPip(0f, 0f, false, acePipScaleMultiplier);
            return;
        }

        // Zero is the undealt/default state. Number cards are 2–10 here.
        if (value < 1 || value > 10)
            return;

        if (!Layouts.TryGetValue(value, out List<PipSpot> spots))
        {
            Debug.LogWarning($"[CardPipLayout] No pip layout defined for value {value}.", this);
            return;
        }

        foreach (PipSpot spot in spots)
            SpawnPip(spot.x, spot.y, spot.rotated, normalPipScaleMultiplier);
    }

    private Texture2D GetFaceArt()
    {
        switch (sourceCard.cardValue)
        {
            case 11: // Jack
                if (MatchesSuit("♣", "club") && yuriJackOfClubsArt != null)
                    return yuriJackOfClubsArt;
                return genericJackArt;

            case 12: // Queen
                if (MatchesSuit("♥", "heart") && liraQueenOfHeartsArt != null)
                    return liraQueenOfHeartsArt;

                if (MatchesSuit("♦", "diamond") && miraQueenOfDiamondsArt != null)
                    return miraQueenOfDiamondsArt;

                return genericQueenArt;

            case 13: // King
                if (MatchesSuit("♠", "spade") && brahmKingOfSpadesArt != null)
                    return brahmKingOfSpadesArt;
                return genericKingArt;

            default:
                return null;
        }
    }

    private bool MatchesSuit(string symbol, string suitName)
    {
        if (sourceCard == null || string.IsNullOrWhiteSpace(sourceCard.cardSuit))
            return false;

        string suit = sourceCard.cardSuit.Trim();

        return suit == symbol ||
               suit.Equals(suitName, StringComparison.OrdinalIgnoreCase) ||
               suit.Equals(suitName + "s", StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyFaceArt(Texture2D art)
    {
        bool showArt = art != null;

        if (faceArtRoot != null)
            faceArtRoot.SetActive(showArt);
        else if (faceArtRenderer != null)
            faceArtRenderer.gameObject.SetActive(showArt);

        if (!showArt)
            return;

        if (faceArtRenderer == null)
        {
            Debug.LogWarning(
                "[CardPipLayout] Face art is assigned, but Face Art Renderer is not. Assign the Renderer on the transparent art plane.",
                this);
            return;
        }

        Material sharedMaterial = faceArtRenderer.sharedMaterial;

        if (sharedMaterial == null)
        {
            Debug.LogWarning("[CardPipLayout] Face Art Renderer has no material.", this);
            return;
        }

        int textureProperty;

        if (sharedMaterial.HasProperty(BaseMapId))
            textureProperty = BaseMapId;
        else if (sharedMaterial.HasProperty(MainTexId))
            textureProperty = MainTexId;
        else
        {
            Debug.LogWarning(
                "[CardPipLayout] Face art material needs a _BaseMap or _MainTex texture property.",
                this);
            return;
        }

        faceArtRenderer.GetPropertyBlock(artPropertyBlock);
        artPropertyBlock.SetTexture(textureProperty, art);
        faceArtRenderer.SetPropertyBlock(artPropertyBlock);
    }

    private void SpawnPip(
        float normalizedX,
        float normalizedY,
        bool rotated,
        float scaleMultiplier)
    {
        TextMeshPro instance = Instantiate(pipTemplate, pipContainer);
        instance.gameObject.SetActive(true);
        instance.gameObject.name = "Pip";

        instance.text = sourceCard.cardSuit;

        bool isRed = sourceCard.cardSuit == "♥" || sourceCard.cardSuit == "♦";
        instance.color = isRed
            ? new Color(0.55f, 0.08f, 0.07f)
            : new Color(0.10f, 0.09f, 0.09f);

        Vector3 localPosition = useYAxisForRows
            ? new Vector3(normalizedX * xScale, normalizedY * ySpacing, pipLiftY)
            : new Vector3(normalizedX * xScale, pipLiftY, normalizedY * ySpacing);

        instance.transform.localPosition = localPosition;

        Vector3 baseEuler = pipTemplate.transform.localEulerAngles;
        instance.transform.localRotation = Quaternion.Euler(
            baseEuler.x,
            baseEuler.y,
            rotated ? baseEuler.z + 180f : baseEuler.z);

        instance.transform.localScale =
            pipTemplate.transform.localScale * scaleMultiplier;

        spawnedPips.Add(instance);
    }

    private void ClearPips()
    {
        foreach (TextMeshPro pip in spawnedPips)
        {
            if (pip != null)
                Destroy(pip.gameObject);
        }

        spawnedPips.Clear();
    }
}