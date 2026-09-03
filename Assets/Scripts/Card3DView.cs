using UnityEngine;
using TMPro;

/// <summary>
/// 3D visual bridge for your existing 2D Card script.
///
/// Put this script on Card3DRoot.
/// It does NOT replace your existing Card script.
/// It reads from an existing Card object and updates the 3D card text.
///
/// Expected hierarchy:
///
/// Card3DRoot
/// ├── CardMesh / card visual mesh
/// ├── FaceValueText
/// ├── PointValueText
/// ├── SuitText_TopLeft
/// ├── SuitText_BottomRight
/// └── DecayText
///
/// Card3DRoot should also have a BoxCollider for future hover/click behavior.
///
/// RUNTIME AUTO-REFRESH: Update() cheaply checks whether the source Card's
/// value/suit/decay has changed since the last frame (e.g. GameManager
/// reused this hand slot and dealt a new card into it via AssignNextCard)
/// and re-pulls the visuals automatically. This means GameManager doesn't
/// need any changes to support the 3D layer — it can keep writing to the
/// old Card exactly like it always has.
/// </summary>
public class Card3DView : MonoBehaviour
{
    [Header("Source Card")]
    [Tooltip("The existing 2D Card script this 3D card is visually representing.")]
    [SerializeField] private Card sourceCard;

    [Header("Text References")]
    [SerializeField] private TMP_Text faceValueText;
    [SerializeField] private TMP_Text faceValueTextBottomRight;
    [SerializeField] private TMP_Text suitTopLeftText;
    [SerializeField] private TMP_Text suitBottomRightText;
    [SerializeField] private TMP_Text pointValueText;
    [SerializeField] private TMP_Text pointValueTextBottomRight;
    [SerializeField] private TMP_Text decayText;

    [Header("Suit Colors")]
    [SerializeField] private Color redSuitColor = new Color(0.54f, 0.12f, 0.12f);
    [SerializeField] private Color blackSuitColor = new Color(0.11f, 0.09f, 0.07f);

    [Header("Decay Colors")]
    [SerializeField] private Color normalDecayColor = new Color(0.20f, 0.65f, 0.25f);
    [SerializeField] private Color fullyDecayedColor = new Color(0.85f, 0.15f, 0.15f);

    public Card SourceCard => sourceCard;

    // Cache for cheap change-detection in Update()
    private int _lastValue = int.MinValue;
    private string _lastSuit = null;
    private int _lastDecayLevel = int.MinValue;
    private bool _lastIsDecayed;

    /// <summary>
    /// Call this when spawning/creating the 3D card to link it to an existing Card.
    /// </summary>
    public void Initialize(Card card)
    {
        sourceCard = card;
        RefreshVisuals();
        CacheState();
    }

    void Update()
    {
        if (sourceCard == null) return;

        // Cheap diff — only touch TMP text (which allocates/relayouts) when
        // something actually changed, e.g. GameManager just dealt a new
        // card into this reused hand slot.
        if (sourceCard.cardValue != _lastValue ||
            sourceCard.cardSuit  != _lastSuit  ||
            sourceCard.decayLevel != _lastDecayLevel ||
            sourceCard.isDecayed  != _lastIsDecayed)
        {
            RefreshVisuals();
            CacheState();
        }
    }

    void CacheState()
    {
        if (sourceCard == null) return;
        _lastValue      = sourceCard.cardValue;
        _lastSuit       = sourceCard.cardSuit;
        _lastDecayLevel = sourceCard.decayLevel;
        _lastIsDecayed  = sourceCard.isDecayed;
    }

    /// <summary>
    /// Call this any time the source card changes:
    /// value, suit, decay, rot state, etc.
    /// </summary>
    public void RefreshVisuals()
    {
        if (sourceCard == null)
        {
            ClearVisuals();
            return;
        }

        string displayValue = sourceCard.GetDisplayValue();
        string suit = sourceCard.cardSuit;
        int pointValue = sourceCard.PointValue;
        int decayLevel = sourceCard.decayLevel;

        if (faceValueText != null)
        {
            faceValueText.text = displayValue;
            faceValueText.color = IsRedSuit(suit) ? redSuitColor : blackSuitColor;
        }

        if (faceValueTextBottomRight != null)
        {
            faceValueTextBottomRight.text = displayValue;
            faceValueTextBottomRight.color = IsRedSuit(suit) ? redSuitColor : blackSuitColor;
        }

        if (pointValueText != null)
            pointValueText.text = pointValue.ToString();

        if (pointValueTextBottomRight != null)
            pointValueTextBottomRight.text = pointValue.ToString();

        Color suitColor = IsRedSuit(suit) ? redSuitColor : blackSuitColor;

        if (suitTopLeftText != null)
        {
            suitTopLeftText.text = suit;
            suitTopLeftText.color = suitColor;
        }

        if (suitBottomRightText != null)
        {
            suitBottomRightText.text = suit;
            suitBottomRightText.color = suitColor;
        }

        if (decayText != null)
        {
            decayText.text = decayLevel > 0 ? decayLevel.ToString() : "";
            decayText.color = sourceCard.isDecayed ? fullyDecayedColor : normalDecayColor;
        }
    }

    /// <summary>
    /// Lets the 3D card set/toggle the existing card's selected state
    /// without calling the old UI pointer functions.
    /// </summary>
    public void SetSourceSelected(bool selected)
    {
        if (sourceCard == null)
            return;

        if (sourceCard.selected == selected)
            return;

        sourceCard.selected = selected;

        sourceCard.SelectEvent?.Invoke(sourceCard, selected);
        sourceCard.CardSelectedEvent?.Invoke(sourceCard, selected);
    }

    public void ToggleSourceSelected()
    {
        if (sourceCard == null)
            return;

        SetSourceSelected(!sourceCard.selected);
    }

    private void ClearVisuals()
    {
        if (faceValueText != null)
            faceValueText.text = "";

        if (pointValueText != null)
            pointValueText.text = "";

        if (suitTopLeftText != null)
            suitTopLeftText.text = "";

        if (suitBottomRightText != null)
            suitBottomRightText.text = "";

        if (decayText != null)
            decayText.text = "";
    }

    private bool IsRedSuit(string suit)
    {
        if (string.IsNullOrWhiteSpace(suit))
            return false;

        return suit == "♥" ||
               suit == "♦" ||
               suit.ToLower().Contains("heart") ||
               suit.ToLower().Contains("diamond");
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        RefreshVisuals();
    }
#endif
}