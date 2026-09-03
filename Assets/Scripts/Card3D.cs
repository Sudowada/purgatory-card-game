using UnityEngine;
using TMPro;
using DG.Tweening;
using System;
 
/// <summary>
/// Physical 3D playing card — drives DATA and BEHAVIOR only.
/// Does NOT build, position, or move any visuals — you place everything
/// (mesh, TextMeshPro labels, etc.) manually in the scene, then drag each
/// specific text object from the Hierarchy into its named slot below.
///
/// SETUP:
///   1. Add this component to your card's root GameObject (the one that
///      already has the mesh/collider from your Blender import).
///   2. For each labeled slot below, drag the matching TextMeshPro object
///      from the Hierarchy (e.g. your "TopLeftValue" child object goes
///      into "Top Left Value"). Leave any slot empty if your card doesn't
///      have that element — nothing will break.
///   3. Drag the mesh's Renderer into "Model Renderer" if you want decay
///      to visually darken/tint the card (optional).
///   4. Set Card Value / Card Suit, or set them from GameManager when
///      dealing a hand.
///
/// Mirrors the public interface of the old UI-based Card.cs (cardValue,
/// cardSuit, decayLevel, isDecayed, selected, CardSelectedEvent,
/// Deselect(), InitCardStats(), ApplyDecay(), ForceFullDecay(),
/// GetDisplayValue()) so GameManager integration stays a swap, not a rewrite.
/// </summary>
public class Card3D : MonoBehaviour
{
    [Header("Card Data")]
    public int cardValue;      // 1–13 (Ace–King)
    public string cardSuit;    // "♠" "♥" "♦" "♣"
    public int decayLevel = 0;
    public bool isDecayed = false;
    public bool selected  = false;
 
    [Header("Value Text — drag the matching object from the Hierarchy")]
    [Tooltip("Top-left corner value text (e.g. '7').")]
    [SerializeField] private TextMeshPro topLeftValueLabel;
    [Tooltip("Bottom-right corner value text (usually upside-down on a real card).")]
    [SerializeField] private TextMeshPro bottomRightValueLabel;
    [Tooltip("Optional large center value text, if your card has one.")]
    [SerializeField] private TextMeshPro centerValueLabel;
 
    [Header("Suit Text — drag the matching object from the Hierarchy")]
    [Tooltip("Top-left corner suit symbol.")]
    [SerializeField] private TextMeshPro topLeftSuitLabel;
    [Tooltip("Bottom-right corner suit symbol.")]
    [SerializeField] private TextMeshPro bottomRightSuitLabel;
    [Tooltip("Large center suit symbol, if your card has one.")]
    [SerializeField] private TextMeshPro centerSuitLabel;
 
    [Header("Ink Color (applied to whichever labels above are assigned)")]
    [SerializeField] private Color redSuitColor   = new Color(0.55f, 0.08f, 0.07f);
    [SerializeField] private Color blackSuitColor = new Color(0.10f, 0.09f, 0.09f);
 
    [Header("Decay Tinting (optional)")]
    [Tooltip("Renderer whose material should darken as the card decays. Leave empty to skip.")]
    [SerializeField] private Renderer modelRenderer;
    [SerializeField] private Color decayedTint = new Color(0.45f, 0.42f, 0.35f);
    [SerializeField] private int maxDecayLevel = 3;
 
    [Header("Interaction Tuning")]
    [SerializeField] private float hoverLift = 0.04f;
    [SerializeField] private float selectLift = 0.12f;
    [SerializeField] private float selectTiltDeg = -8f;
    [SerializeField] private float animSpeed = 0.18f;
 
    /// <summary>Fired when selection state changes: (card, isSelected)</summary>
    public event Action<Card3D, bool> CardSelectedEvent;
 
    private Vector3 _baseLocalPos;
    private Quaternion _baseLocalRot;
    private bool _hovering;
 
    private Material _modelMat;
    private Color _originalMatColor;
 
    // ── Lifecycle ────────────────────────────────────────────────────────────
 
    void Awake()
    {
        _baseLocalPos = transform.localPosition;
        _baseLocalRot = transform.localRotation;
 
        if (GetComponentInChildren<Collider>() == null)
            Debug.LogWarning($"[Card3D] '{name}' has no Collider — hover/click won't work. Add one to the mesh.");
 
        if (modelRenderer != null)
        {
            // Instance the material at runtime so tinting this card doesn't
            // affect every other card sharing the same material asset.
            _modelMat = modelRenderer.material;
            _originalMatColor = _modelMat.color;
        }
    }
 
    void Start()
    {
        RefreshFace();
        ApplyDecayVisual();
    }
 
    // ── Public API (mirrors old UI Card.cs) ─────────────────────────────────
 
    public void InitCardStats()
    {
        RefreshFace();
        ApplyDecayVisual();
    }
 
    public string GetDisplayValue()
    {
        switch (cardValue)
        {
            case 1:  return "A";
            case 11: return "J";
            case 12: return "Q";
            case 13: return "K";
            default: return cardValue > 0 ? cardValue.ToString() : "?";
        }
    }
 
    /// <summary>Applies one stack of decay. Returns true if this call fully decayed the card.</summary>
    public bool ApplyDecay()
    {
        if (isDecayed) return false;
        decayLevel++;
        bool justFullyDecayed = decayLevel >= maxDecayLevel;
        if (justFullyDecayed) isDecayed = true;
        ApplyDecayVisual();
        return justFullyDecayed;
    }
 
    /// <summary>Immediately forces full decay (used when a relic makes rot 'stack' instantly).</summary>
    public bool ForceFullDecay()
    {
        bool wasAlready = isDecayed;
        decayLevel = maxDecayLevel;
        isDecayed  = true;
        ApplyDecayVisual();
        return !wasAlready;
    }
 
    public void Deselect()
    {
        if (!selected) return;
        selected = false;
        AnimateToRestState();
        CardSelectedEvent?.Invoke(this, false);
    }
 
    // ── Mouse interaction ────────────────────────────────────────────────────
 
    void OnMouseEnter()
    {
        _hovering = true;
        if (!selected)
            transform.DOLocalMoveY(_baseLocalPos.y + hoverLift, animSpeed).SetEase(Ease.OutQuad);
    }
 
    void OnMouseExit()
    {
        _hovering = false;
        if (!selected)
            transform.DOLocalMoveY(_baseLocalPos.y, animSpeed).SetEase(Ease.OutQuad);
    }
 
    void OnMouseDown()
    {
        selected = !selected;
 
        if (selected)
        {
            transform.DOLocalMoveY(_baseLocalPos.y + selectLift, animSpeed).SetEase(Ease.OutBack);
            transform.DOLocalRotate(
                _baseLocalRot.eulerAngles + new Vector3(selectTiltDeg, 0f, 0f),
                animSpeed).SetEase(Ease.OutBack);
        }
        else
        {
            AnimateToRestState();
        }
 
        CardSelectedEvent?.Invoke(this, selected);
    }
 
    void AnimateToRestState()
    {
        float targetY = _hovering ? _baseLocalPos.y + hoverLift : _baseLocalPos.y;
        transform.DOLocalMoveY(targetY, animSpeed).SetEase(Ease.OutQuad);
        transform.DOLocalRotateQuaternion(_baseLocalRot, animSpeed).SetEase(Ease.OutQuad);
    }
 
    /// <summary>Call this if the card's resting slot on the table moves (e.g. hand re-fanned).</summary>
    public void SetRestTransform(Vector3 localPos, Quaternion localRot)
    {
        _baseLocalPos = localPos;
        _baseLocalRot = localRot;
        if (!selected && !_hovering)
        {
            transform.DOLocalMove(localPos, animSpeed).SetEase(Ease.OutQuad);
            transform.DOLocalRotateQuaternion(localRot, animSpeed).SetEase(Ease.OutQuad);
        }
    }
 
    // ── Visual refresh (just sets .text/.color on whichever slots are assigned) ──
 
    void RefreshFace()
    {
        string display = GetDisplayValue();
        bool isRed = cardSuit == "♥" || cardSuit == "♦";
        Color inkColor = isRed ? redSuitColor : blackSuitColor;
        string suitDisplay = string.IsNullOrEmpty(cardSuit) ? "?" : cardSuit;
 
        SetLabel(topLeftValueLabel, display, inkColor);
        SetLabel(bottomRightValueLabel, display, inkColor);
        SetLabel(centerValueLabel, display, inkColor);
 
        SetLabel(topLeftSuitLabel, suitDisplay, inkColor);
        SetLabel(bottomRightSuitLabel, suitDisplay, inkColor);
        SetLabel(centerSuitLabel, suitDisplay, inkColor);
    }
 
    void SetLabel(TextMeshPro label, string text, Color color)
    {
        if (label == null) return;
        label.text  = text;
        label.color = color;
    }
 
    void ApplyDecayVisual()
    {
        if (_modelMat == null) return;
        float t = Mathf.Clamp01((float)decayLevel / maxDecayLevel);
        _modelMat.DOColor(Color.Lerp(_originalMatColor, decayedTint, t), 0.3f);
 
        if (isDecayed)
        {
            transform.DOKill(false);
            transform.DOLocalRotate(new Vector3(0f, 0f, 2.5f), 1.4f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }
    }
}