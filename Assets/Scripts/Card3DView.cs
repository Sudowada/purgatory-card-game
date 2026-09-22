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
/// </summary>
public class Card3DView : MonoBehaviour
{
    [Header("Source Card")]
    [Tooltip("The existing 2D Card script this 3D card is visually representing.")]
    [SerializeField] private Card sourceCard;

    [Header("Text References")]
    [SerializeField] private TMP_Text faceValueText;
    [SerializeField] private TMP_Text suitTopLeftText;
    [SerializeField] private TMP_Text suitBottomRightText;
    [SerializeField] private TMP_Text pointValueText;
    [SerializeField] private TMP_Text decayText;

    [Header("Suit Colors")]
    [SerializeField] private Color redSuitColor = new Color(0.54f, 0.12f, 0.12f);
    [SerializeField] private Color blackSuitColor = new Color(0.11f, 0.09f, 0.07f);

    [Header("Decay Colors")]
    [SerializeField] private Color normalDecayColor = new Color(0.11f, 0.09f, 0.07f);
    [SerializeField] private Color fullyDecayedColor = new Color(0.35f, 0.05f, 0.05f);

    [Header("3D Text Sync")]
    [Tooltip("Keeps every FaceValue / PointValue / Suit / Decay TMP child synchronized with the real Card data. This runs after gameplay scripts have had a chance to assign the card for the frame.")]
    [SerializeField] private bool syncNamedMirroredText = true;

    private int lastCardValue = int.MinValue;
    private int lastPointValue = int.MinValue;
    private int lastDecayLevel = int.MinValue;
    private string lastSuit;

    public Card SourceCard => sourceCard;

    /// <summary>
    /// Call this when spawning/creating the 3D card to link it to an existing Card.
    /// </summary>
    public void Initialize(Card card)
    {
        sourceCard = card;
        InvalidateVisualSnapshot();
        RefreshVisuals();
    }

    private void LateUpdate()
    {
        if (sourceCard == null)
            return;

        int pointValue = sourceCard.PointValue;

        if (sourceCard.cardValue != lastCardValue ||
            pointValue != lastPointValue ||
            sourceCard.decayLevel != lastDecayLevel ||
            sourceCard.cardSuit != lastSuit)
        {
            RefreshVisuals();
        }
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
            InvalidateVisualSnapshot();
            return;
        }

        string displayValue = sourceCard.GetDisplayValue();
        string suit = sourceCard.cardSuit;
        int pointValue = sourceCard.PointValue;
        int decayLevel = sourceCard.decayLevel;
        Color suitColor = IsRedSuit(suit) ? redSuitColor : blackSuitColor;

        // Keep the original Inspector references intact.
        if (faceValueText != null)
            faceValueText.text = displayValue;

        if (pointValueText != null)
            pointValueText.text = pointValue.ToString();

        if (suitTopLeftText != null)
            ForceSuitVisible(suitTopLeftText, suit, suitColor);

        if (suitBottomRightText != null)
            ForceSuitVisible(suitBottomRightText, suit, suitColor);

        if (decayText != null)
        {
            decayText.text = decayLevel > 0 ? decayLevel.ToString() : "";
            decayText.color = sourceCard.isDecayed ? fullyDecayedColor : normalDecayColor;
        }

        // Your actual prefab has separate top-left AND bottom-right text
        // objects. Update all of them by name, but deliberately do not touch
        // their font materials, outline, glow, size, or layout.
        if (syncNamedMirroredText)
        {
            foreach (TMP_Text textMesh in GetComponentsInChildren<TMP_Text>(true))
            {
                if (textMesh == null)
                    continue;

                string lowerName = textMesh.gameObject.name.ToLowerInvariant();

                if (lowerName.Contains("facevalue"))
                {
                    textMesh.text = displayValue;
                }
                else if (lowerName.Contains("pointvalue"))
                {
                    textMesh.text = pointValue.ToString();
                }
                else if (lowerName.Contains("suit"))
                {
                    ForceSuitVisible(textMesh, suit, suitColor);
                }
                else if (lowerName.Contains("decay"))
                {
                    textMesh.text = decayLevel > 0 ? decayLevel.ToString() : "";
                    textMesh.color = sourceCard.isDecayed
                        ? fullyDecayedColor
                        : normalDecayColor;
                }
            }
        }

        lastCardValue = sourceCard.cardValue;
        lastPointValue = pointValue;
        lastDecayLevel = decayLevel;
        lastSuit = suit;
    }

    private void ForceSuitVisible(
        TMP_Text textMesh,
        string suit,
        Color suitColor)
    {
        if (textMesh == null)
            return;

        // A few of the recent preview/readability experiments could leave a
        // TMP object or its MeshRenderer disabled. The suit data itself was
        // still correct; the top-left glyph simply was not being drawn.
        if (!textMesh.gameObject.activeSelf)
            textMesh.gameObject.SetActive(true);

        textMesh.enabled = true;

        Renderer textRenderer = textMesh.GetComponent<Renderer>();
        if (textRenderer != null)
            textRenderer.enabled = true;

        Color visibleColor = suitColor;
        visibleColor.a = 1f;

        textMesh.text = suit;
        textMesh.color = visibleColor;

        // Rebuild immediately so a re-enabled top-left TMP doesn't wait a
        // frame before showing its glyph.
        textMesh.ForceMeshUpdate(true, true);
    }

    private void InvalidateVisualSnapshot()
    {
        lastCardValue = int.MinValue;
        lastPointValue = int.MinValue;
        lastDecayLevel = int.MinValue;
        lastSuit = null;
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
