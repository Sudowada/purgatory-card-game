using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Pure card data — no UI, no drag handling, no auto-spawned visual.
/// Card3DInteractable owns all hover/select/drag behavior in 3D; this
/// class just holds the numbers and fires the two events GameManager and
/// Card3DView actually use when selection changes.
/// </summary>
public class Card : MonoBehaviour
{
    // ── Card Game Data ────────────────────────────────────────────────────────

    [Header("Card Data")]
    public int    cardValue;    // 1=Ace, 2-10, 11=J, 12=Q, 13=K  (identity, used for combo detection)
    public string cardSuit;     // "♠" "♥" "♦" "♣"

    /// <summary>Score value — reversed scale. Ace=14pts, King=2pts (formula: 15 - cardValue).</summary>
    public int PointValue => 15 - cardValue;

    [Header("Face & Rot")]
    [Tooltip("Used ONLY for diff scoring vs enemy. Starts = cardValue. Upgradeable by collectibles.")]
    public int faceValue = 0;

    [Tooltip("Decay stacks needed to fully rot. Starts = cardValue. Only changed by relics.")]
    public int rotThreshold = 0;

    // ── Decay ─────────────────────────────────────────────────────────────────

    [Header("Decay")]
    public int  decayLevel = 0;     // How many decay ticks applied
    public bool isDecayed  = false; // True when decayLevel >= rotThreshold

    // ── Selection ─────────────────────────────────────────────────────────────

    [Header("Selection")]
    public bool selected;

    // ── Events ────────────────────────────────────────────────────────────────
    // Only these two are actually used anywhere in the 3D system:
    // Card3DInteractable -> Card3DView.SetSourceSelected() -> invokes both
    // of these -> GameManager.OnCardSelectionChanged listens to CardSelectedEvent.

    [Header("Events")]
    [HideInInspector] public UnityEvent<Card, bool> SelectEvent;
    [HideInInspector] public UnityEvent<Card, bool> CardSelectedEvent;

    // ── Game Logic ────────────────────────────────────────────────────────────

    /// <summary>
    /// Call after setting cardValue (e.g. in AssignNextCard).
    /// Initialises faceValue and rotThreshold from cardValue,
    /// then applies any active relic modifiers.
    /// </summary>
    public void InitCardStats()
    {
        faceValue    = cardValue;   // base face value equals identity
        rotThreshold = cardValue;   // base rot threshold equals identity

        // Apply relic rot-threshold modifiers (e.g. future ThickBark relic)
        if (RelicSystem.Instance != null)
            rotThreshold = RelicSystem.Instance.GetRotThreshold(rotThreshold);
    }

    /// <summary>
    /// Apply one tick of decay. Returns true if card just became fully decayed.
    /// Uses rotThreshold, not cardValue.
    /// </summary>
    public bool ApplyDecay()
    {
        if (isDecayed) return false;
        decayLevel++;
        if (decayLevel >= rotThreshold)
            isDecayed = true;
        return isDecayed;
    }

    /// <summary>Instantly fully decays this card. Uses rotThreshold.</summary>
    public bool ForceFullDecay()
    {
        if (isDecayed) return false;
        decayLevel = rotThreshold;
        isDecayed  = true;
        return true;
    }

    /// <summary>Returns the card's scoring point value.</summary>
    public int GetScoringValue() => PointValue;

    /// <summary>A, 2-10, J, Q, K display string.</summary>
    public string GetDisplayValue() => cardValue switch
    {
        1  => "A",
        11 => "J",
        12 => "Q",
        13 => "K",
        _  => cardValue.ToString()
    };

    /// <summary>
    /// Sets selected state and fires both selection events. Card3DView
    /// calls this via SetSourceSelected() when the player clicks a
    /// physical card in the 3D scene.
    /// </summary>
    public void SetSelected(bool value)
    {
        if (selected == value) return;
        selected = value;
        SelectEvent?.Invoke(this, selected);
        CardSelectedEvent?.Invoke(this, selected);
    }

    public void Deselect()
    {
        if (selected)
            SetSelected(false);
    }
}