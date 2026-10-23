using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// Offering flow:
/// 1. Lower screen = hand/reorder.
/// 2. Upper-middle = altar region; upper-left/right stay reserved.
/// 3. Dragging into altar aims the REAL card at OfferingBurnPoint.
/// 4. Releasing there lands the real card flat, burns/shrinks it away.
/// 5. Only after the burn finishes, a separate translucent visual preview
///    appears above the altar.
/// 6. Preview cards dynamically recenter: 1 centered, 2 centered pair,
///    3 with the middle card centered.
/// </summary>
public class CardOfferingController : MonoBehaviour
{
    [Header("Screen Interaction Regions")]
    [SerializeField] private bool useScreenRegions = true;

    [Range(0f, 1f)]
    [SerializeField] private float handAreaMaxViewportY = 0.42f;

    [Range(0f, 1f)]
    [SerializeField] private float altarLeftViewportX = 0.20f;

    [Range(0f, 1f)]
    [SerializeField] private float altarRightViewportX = 0.80f;

    [Header("Physical Altar Burn")]
    [Tooltip("Place this empty object in the altar recess. Its position and rotation define exactly where the real card lies flat before burning away.")]
    [SerializeField] private Transform offeringBurnPoint;

    [Tooltip("How long the released card takes to finish settling into the recess.")]
    [SerializeField] private float burnLandDuration = 0.08f;

    [Tooltip("How quickly the physical card flashes/shrinks away after landing.")]
    [SerializeField] private float burnAwayDuration = 0.18f;

    [Tooltip("Optional pixel-flame burst played at the altar the moment an offering is confirmed.")]
    [SerializeField] private AltarFlameEffect altarFlameEffect;

    [Header("Draw From Deck")]
    [Tooltip("Assign an empty Transform just above the physical deck. Fresh replacements start here.")]
    [SerializeField] private Transform deckDrawPoint;
    [SerializeField, Min(0f)] private float replacementDrawStagger = 0.07f;
    [SerializeField, Min(0.1f)] private float refillSettleDuration = 0.45f;
    public Transform DeckDrawPoint => deckDrawPoint;
    public float RefillSettleDuration => refillSettleDuration;
    public float ReplacementDrawStagger => replacementDrawStagger;

    // Exhausted/round-end slots stay outside the hand until the next round.
    private readonly Dictionary<Card3DInteractable, int> emptySlots =
        new Dictionary<Card3DInteractable, int>();
    private readonly HashSet<Card3DInteractable> returningThisOffer =
        new HashSet<Card3DInteractable>();

    [Header("Floating Preview")]
    [Tooltip("Controls the preview row center. Position = row center, rotation = base angle for the preview cards.")]
    [SerializeField] private Transform offeringPreviewCenter;

    [Tooltip("Additional Euler rotation applied to every preview card. Use X = 90 if you want the preview to face the camera more directly.")]
    [SerializeField] private Vector3 previewRotationOffsetEuler = new Vector3(90f, 0f, 0f);

    [Tooltip("How high above OfferingPreviewCenter the preview row appears.")]
    [SerializeField] private float previewHeightOffset = 0.12f;

    [Tooltip("Center-to-center spacing between preview cards.")]
    [SerializeField] private float previewCardSpacing = 0.13f;

    [Tooltip("Overall size of the translucent preview copies.")]
    [SerializeField] private float previewScale = 1f;

    [Range(0.05f, 1f)]
    [Tooltip("0 = invisible, 1 = fully solid. Around 0.55-0.65 works well for a ghost preview.")]
    [SerializeField] private float previewOpacity = 0.60f;

    [Header("Preview Tint / Emission")]
    [SerializeField] private Color previewTintColor = new Color(1f, 0.55f, 0.55f, 1f);
    [Range(0f, 1f)]
    [SerializeField] private float previewTintStrength = 0.30f;
    [SerializeField] private Color previewEmissionColor = new Color(0.9f, 0.2f, 0.1f, 1f);
    [SerializeField] private float previewEmissionIntensity = 0.55f;

    [Header("Preview Ash FX")]
    [SerializeField] private bool spawnAshFx = true;
    [SerializeField] private float ashRateOverTime = 7f;
    [SerializeField] private float ashLifetimeMin = 0.35f;
    [SerializeField] private float ashLifetimeMax = 0.8f;
    [SerializeField] private float ashSpeedMin = 0.015f;
    [SerializeField] private float ashSpeedMax = 0.05f;
    [SerializeField] private float ashSizeMin = 0.002f;
    [SerializeField] private float ashSizeMax = 0.006f;
    [SerializeField] private Vector3 ashLocalBox = new Vector3(0.09f, 0.01f, 0.13f);

    [Tooltip("How quickly existing preview cards re-center when another card is added/removed.")]
    [SerializeField] private float recenterTweenDuration = 0.11f;

    [Header("Preview Interaction")]
    [Tooltip("Optional. Leave empty to use Camera.main / the currently enabled camera.")]
    [SerializeField] private Camera previewInteractionCamera;

    [Tooltip("Maximum time a press can last and still count as a click-to-remove.")]
    [SerializeField] private float previewClickMaxDuration = 0.30f;

    [Tooltip("Mouse movement in pixels before a preview click becomes a reorder drag.")]
    [SerializeField] private float previewDragPixelThreshold = 8f;

    [Tooltip("How quickly the other preview cards snap into their new positions while reordering.")]
    [SerializeField] private float previewReorderTweenDuration = 0.09f;

    [Tooltip("Small pull toward the camera while dragging a preview card so it reads above its neighbors.")]
    [SerializeField] private float previewDragTowardCamera = 0.015f;

    [Tooltip("Extra local-space padding added to the automatically generated preview click/drag hitbox.")]
    [SerializeField] private Vector3 previewHitboxPadding = new Vector3(0.012f, 0.012f, 0.012f);

    [Header("Preview Hover Readability")]
    [Range(0.05f, 1f)]
    [Tooltip("Opacity while the cursor is over a preview card. The normal preview stays ghostly; hover becomes much easier to read.")]
    [SerializeField] private float previewHoverOpacity = 0.90f;

    [Tooltip("Small hover size increase. 1.07 = 7% larger.")]
    [SerializeField] private float previewHoverScaleMultiplier = 1.07f;

    [Tooltip("How quickly preview opacity and scale react to hover.")]
    [SerializeField] private float previewHoverTweenDuration = 0.08f;

    [Header("Preview Hover Text Focus")]
    [Tooltip("On hover, ALL face values and point values move toward this high-contrast color. This does not depend on the card material becoming transparent correctly.")]
    [SerializeField] private Color previewHoverValueColor = new Color(1f, 0.96f, 0.78f, 1f);

    [Tooltip("Red suit glyph color while the preview is hovered.")]
    [SerializeField] private Color previewHoverRedSuitColor = new Color(1f, 0.18f, 0.06f, 1f);

    [Tooltip("Black suit glyph color while the preview is hovered.")]
    [SerializeField] private Color previewHoverBlackSuitColor = new Color(0.92f, 0.92f, 0.86f, 1f);

    [SerializeField] private Color previewHoverOutlineColor = new Color(0.03f, 0.01f, 0.005f, 1f);

    [Range(0f, 1f)]
    [SerializeField] private float previewHoverOutlineWidth = 0.22f;

    [Header("Red-Suit Preview Contrast")]
    [Tooltip("Hearts/diamonds use a higher resting opacity so the red markings do not disappear into the red ghost tint.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float redSuitPreviewOpacity = 0.78f;

    [Range(0.05f, 1f)]
    [SerializeField] private float redSuitPreviewHoverOpacity = 0.98f;

    [Tooltip("Red previews use almost no red body wash. This keeps the card body neutral enough for red suit/value markings to separate clearly.")]
    [Range(0f, 1f)]
    [SerializeField] private float redSuitPreviewBodyTintStrength = 0.015f;

    [Tooltip("Red previews also use much weaker body emission so the whole card does not become one flat orange/red shape.")]
    [SerializeField] private Color redSuitPreviewBodyEmissionColor = new Color(1f, 0.55f, 0.28f, 1f);
    [SerializeField] private float redSuitPreviewBodyEmissionIntensity = 0.10f;

    [Tooltip("High-contrast warm ivory for face/point values on red-suit previews.")]
    [SerializeField] private Color redSuitPreviewValueColor = new Color(1f, 0.92f, 0.72f, 1f);

    [Tooltip("Bright coral-red used for heart/diamond symbols in the preview.")]
    [SerializeField] private Color redSuitPreviewSuitColor = new Color(1f, 0.24f, 0.08f, 1f);

    [Tooltip("Dark outline around red-preview text so it remains readable over the translucent card body.")]
    [SerializeField] private Color redSuitPreviewOutlineColor = new Color(0.08f, 0.015f, 0.01f, 1f);

    [Range(0f, 1f)]
    [SerializeField] private float redSuitPreviewOutlineWidth = 0.18f;

    [SerializeField] private Color redSuitPreviewGlowColor = new Color(1f, 0.18f, 0.05f, 1f);
    [SerializeField] private float redSuitPreviewGlowIntensity = 2.2f;

    [Header("Preview Drag Bounds")]
    [Tooltip("Keeps preview-card reordering in a compact area around the preview row instead of allowing cards to be dragged off-screen.")]
    [SerializeField] private bool limitPreviewDragToCenter = true;

    [Tooltip("How far beyond the outer preview slot the dragged preview may travel. 0.65 means roughly two-thirds of one card slot beyond the row.")]
    [Range(0f, 2f)]
    [SerializeField] private float previewDragExtraSlotAllowance = 0.65f;

    [Tooltip("Optional additional screen-space clamp. X values are normalized screen coordinates; 0.22 to 0.78 keeps the preview interaction in the middle 56% of the screen.")]
    [SerializeField] private Vector2 previewDragViewportXRange = new Vector2(0.22f, 0.78f);

    [Header("Play Count")]
    [Tooltip("Normally 3 cards are required before the Offering can be confirmed.")]
    [Min(1)]
    [SerializeField] private int minimumCardsToConfirm = 1;

    [Tooltip("Normal maximum number of cards that may be staged. A relic can override this at runtime, e.g. 4.")]
    [Min(1)]
    [SerializeField] private int baseMaximumCardsToOffer = 3;

    [Header("Drag Into Altar")]
    [Tooltip("While still holding the mouse in altar territory, keep the real card just slightly above the BurnPoint.")]
    [SerializeField] private float dragBurnPointLift = 0.012f;

    [Header("Optional World Drop Target")]
    [Tooltip("Only used when Use Screen Regions is disabled.")]
    [SerializeField] private Collider offeringDropZone;
    [SerializeField] private LayerMask pointerRaycastMask = ~0;

    [Header("Input")]
    [SerializeField] private bool escapeUndoesLast = true;
    [SerializeField] private bool spaceConfirms = true;

    [Header("Optional 3D Confirm Button")]
    [Tooltip("The physical Offer button. Space-to-confirm will trigger its press " +
             "so the mechanical animation and sounds play even from keyboard input.")]
    [SerializeField] private OfferButtonInteractable confirmButton;

    private readonly List<Card3DInteractable> offeredCards =
        new List<Card3DInteractable>(4);

    private readonly Dictionary<Card3DInteractable, int> originalHandIndices =
        new Dictionary<Card3DInteractable, int>();

    private sealed class PreviewTextState
    {
        public TMP_Text text;
        public Color baseColor;
        public Material material;
        public bool isSuit;
        public bool isDecay;
        public Color baseOutlineColor;
        public float baseOutlineWidth;
        public bool hasOutlineColor;
        public bool hasOutlineWidth;
    }

    private sealed class PreviewEntry
    {
        public Card3DInteractable sourceCard;

        // visual = stable layout/drag root. It NEVER scales on hover.
        public GameObject visual;

        // Only this child scales on hover, so the hitbox and layout position
        // cannot get stuck enlarged.
        public Transform scaleRoot;

        // Resting red ghost copy and high-readability hover copy.
        public GameObject ghostVisual;
        public GameObject focusVisual;
        public List<Renderer> ghostRenderers;

        public Vector3 baseWorldScale;
        public BoxCollider hitCollider;
        public bool hovered;
        public bool isRedSuit;
        public float baseOpacity;
        public float hoverOpacity;
        public float currentOpacity;
        public float currentTextFocus;
        public List<PreviewTextState> textStates;
        public Tween hoverScaleTween;
        public Tween hoverOpacityTween;
        public Tween hoverTextTween;
    }

    private readonly List<PreviewEntry> previewEntries =
        new List<PreviewEntry>(4);

    // We build the preview copy BEFORE the real card burns, while its visual
    // scale/material state is still intact. It stays hidden until the burn ends.
    private readonly Dictionary<Card3DInteractable, PreviewEntry> pendingPreviews =
        new Dictionary<Card3DInteractable, PreviewEntry>();

    private Transform runtimePreviewRoot;
    private bool inputLocked;
    private bool burnInProgress;
    private Card3DInteractable burningCard;

    // Runtime play-count override for relics / run modifiers.
    // -1 means "use baseMaximumCardsToOffer".
    private int maximumCardsOverride = -1;

    // Preview pointer interaction state.
    private PreviewEntry pressedPreview;
    private bool previewPointerDown;
    private bool previewDragging;
    private Vector2 previewPointerDownScreen;
    private float previewPointerDownTime;
    private Plane previewDragPlane;
    private Vector3 previewDragGrabOffset;
    private PreviewEntry currentHoveredPreview;

    public event Action<IReadOnlyList<Card>> OfferingConfirmed;

    public int OfferedCount => offeredCards.Count;
    public int MaximumCardsToOffer =>
        maximumCardsOverride > 0
            ? maximumCardsOverride
            : Mathf.Max(1, baseMaximumCardsToOffer);

    public bool IsFull => offeredCards.Count >= MaximumCardsToOffer;
    public bool CanConfirm =>
        !inputLocked &&
        !burnInProgress &&
        GameManager.Instance != null &&
        GameManager.Instance.CanPlayOffering &&
        deckDrawPoint != null &&
        offeredCards.Count >= Mathf.Max(1, minimumCardsToConfirm) &&
        offeredCards.Count <= MaximumCardsToOffer &&
        previewEntries.Count == offeredCards.Count;

    public bool InputLocked => inputLocked;

    /// <summary>
    /// TEMP DIAGNOSTIC: human-readable snapshot of everything CanConfirm
    /// depends on, so a locked/clanking Offer button can log exactly which
    /// condition is failing instead of failing silently.
    /// </summary>
    public string DebugOfferingState =>
        $"inputLocked={inputLocked}, burnInProgress={burnInProgress}, " +
        $"offeredCards={offeredCards.Count}, previewEntries={previewEntries.Count}, " +
        $"minimumCardsToConfirm={Mathf.Max(1, minimumCardsToConfirm)}, " +
        $"MaximumCardsToOffer={MaximumCardsToOffer}, " +
        $"deckDrawPointAssigned={deckDrawPoint != null}, " +
        $"gameplay={(GameManager.Instance != null ? GameManager.Instance.OfferingBlockReason : "GameManager missing")}";

    [ContextMenu("Log Offer State")]
    public void LogOfferState()
    {
        Debug.Log($"[Offering] {DebugOfferingState}");
    }

    private void Awake()
    {
        GameObject runtimeRoot = new GameObject("OfferingPreviewRuntime");
        runtimePreviewRoot = runtimeRoot.transform;
    }

    private void OnDestroy()
    {
        if (runtimePreviewRoot != null)
            Destroy(runtimePreviewRoot.gameObject);
    }

    private void Update()
    {
        // A rejected click must not leave the physical button permanently
        // locked. Its availability follows actual altar/gameplay readiness.
        if (confirmButton != null)
            confirmButton.SetReadyToOffer(CanConfirm);

        // Radio shares Space/Escape and pointer input with the altar.
        if (RadioInteractable.Active != null)
            return;

        if (inputLocked)
            return;

        HandlePreviewPointerInput();

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (escapeUndoesLast && keyboard.escapeKey.wasPressedThisFrame)
            UndoLastStage();

        if (spaceConfirms && keyboard.spaceKey.wasPressedThisFrame)
        {
            // Route through the physical button so Space gets the same
            // mechanical animation, sounds, and clank-on-invalid behavior
            // as clicking it. Falls back to a direct confirm if no button
            // is assigned yet (e.g. testing before the button is in-scene).
            if (confirmButton != null)
                confirmButton.Press();
            else if (CanConfirm)
                ConfirmOffering();
        }
    }

    public bool TryStageCard(Card3DInteractable card)
    {
        if (inputLocked ||
            (GameManager.Instance != null && !GameManager.Instance.CanPlayOffering) ||
            burnInProgress ||
            card == null ||
            card.IsOffered ||
            IsFull ||
            offeringBurnPoint == null ||
            offeringPreviewCenter == null)
        {
            return false;
        }

        HorizontalCardHolder hand = card.HorizontalHandHolder;
        if (hand == null)
            hand = card.GetComponentInParent<HorizontalCardHolder>();

        if (hand == null)
        {
            Debug.LogWarning("[Offering] Card has no HorizontalCardHolder.");
            return false;
        }

        int handIndex = hand.RemoveForOffering(card);
        if (handIndex < 0)
            return false;

        originalHandIndices[card] = handIndex;
        offeredCards.Add(card);
        card.SetOfferingController(this);
        GetComponent<RandomEnemyTelegraph>()?.ResolveOfferedCard(card);

        burnInProgress = true;
        burningCard = card;

        // Snapshot the card visual BEFORE the physical card shrinks to zero.
        PrepareHiddenPreviewFor(card);

        card.PlayOfferingBurn(
            offeringBurnPoint,
            burnLandDuration,
            burnAwayDuration,
            () =>
            {
                RevealPreparedPreviewFor(card);
                RecenterPreviewVisuals(recenterTweenDuration);
                if (burningCard == card) { burnInProgress = false; burningCard = null; }
            }
        );

        return true;
    }

    public void UndoLastStage()
    {
        if (inputLocked || offeredCards.Count == 0)
            return;

        Card3DInteractable card = offeredCards[offeredCards.Count - 1];
        offeredCards.RemoveAt(offeredCards.Count - 1);
        GetComponent<RandomEnemyTelegraph>()?.UndoStagedCard(card);
        if (burningCard == card) { burnInProgress = false; burningCard = null; }

        RemovePreviewFor(card);
        ReturnCardToHand(card);
        RecenterPreviewVisuals(recenterTweenDuration);
    }

    public void RemoveSpecificCard(Card3DInteractable card)
    {
        if (inputLocked || card == null || !offeredCards.Contains(card))
            return;

        // Clicking a preview means "de-select / unstage this card".
        // Keep the authoritative Card.selected state in sync too.
        card.Deselect();

        offeredCards.Remove(card);
        GetComponent<RandomEnemyTelegraph>()?.UndoStagedCard(card);
        if (burningCard == card) { burnInProgress = false; burningCard = null; }
        RemovePreviewFor(card);
        ReturnCardToHand(card);
        RecenterPreviewVisuals(recenterTweenDuration);
    }

    public void ConfirmOffering()
    {
        if (!CanConfirm)
        {
            Debug.LogWarning($"[Offering] Cannot confirm: {DebugOfferingState}");
            return;
        }

        inputLocked = true;
        GetComponent<RandomEnemyTelegraph>()?.ConfirmStagedBlocks();
        returningThisOffer.Clear();
        List<Card> confirmed = new List<Card>(MaximumCardsToOffer);

        foreach (Card3DInteractable physicalCard in offeredCards)
        {
            if (physicalCard == null)
                continue;

            if (CardReturnEffect.HasReturn(physicalCard.SourceCard))
                returningThisOffer.Add(physicalCard);
            physicalCard.EnterSacrificingState();

            if (physicalCard.SourceCard != null)
                confirmed.Add(physicalCard.SourceCard);
        }

        if (altarFlameEffect != null)
            altarFlameEffect.PlayBurst();

        OfferingConfirmed?.Invoke(confirmed);
        Debug.Log($"[Offering] Confirmed {confirmed.Count} cards in staged order.");

        if (GameManager.Instance != null)
        {
            GameManager.Instance.PlayOfferedCards(confirmed, CompleteOfferingAfterScoring);
        }
        else
        {
            Debug.LogWarning("[Offering] No GameManager found — clearing altar without scoring.");
            CompleteOfferingAfterScoring();
        }
    }

    /// <summary>
    /// Return cards retain their data and fly from the altar. Other physical
    /// objects are reused as display slots, with fresh data drawn from the deck.
    /// Their old card identities remain spent for this round.
    /// </summary>
    private void CompleteOfferingAfterScoring()
    {
        StartCoroutine(CompleteOfferingRoutine());
    }

    private IEnumerator CompleteOfferingRoutine()
    {
        var played = new List<Card3DInteractable>(offeredCards);
        foreach (Card3DInteractable physical in played)
            if (physical != null) RemovePreviewFor(physical);

        // Resolve every Return first; it occupies a hand slot and needs no draw.
        foreach (Card3DInteractable physical in played)
        {
            if (physical != null && returningThisOffer.Contains(physical))
                ReturnCardToHand(physical);
        }
        if (returningThisOffer.Count > 0)
            yield return new WaitForSeconds(refillSettleDuration);

        foreach (Card3DInteractable physical in played)
        {
            if (physical == null || returningThisOffer.Contains(physical)) continue;
            var manager = GameManager.Instance;
            if (manager != null && manager.TryDrawOfferingReplacement(physical.SourceCard))
            {
                // Teleport the still-hidden root BEFORE restoring the visual.
                // ReturnFromOffering restores its hand slot and normal movement.
                PlaceNewDrawAtDeck(physical);
                ReturnCardToHand(physical);
                yield return new WaitForSeconds(replacementDrawStagger);
            }
            else
            {
                int index = originalHandIndices.TryGetValue(physical, out int saved) ? saved : 999;
                emptySlots[physical] = index;
                originalHandIndices.Remove(physical);
                physical.gameObject.SetActive(false);
            }
        }
        yield return new WaitForSeconds(refillSettleDuration);

        offeredCards.Clear();
        originalHandIndices.Clear();
        previewEntries.Clear();
        returningThisOffer.Clear();
        ClearPreviewPointerState();
        currentHoveredPreview = null;
        inputLocked = false;
        GameManager.Instance?.RefreshOfferingHUD();
        if (confirmButton != null) confirmButton.SetReadyForNextTurn();
    }

    public void PlaceNewDrawAtDeck(Card3DInteractable physical)
    {
        if (physical == null || deckDrawPoint == null) return;
        physical.transform.DOKill(false);
        physical.transform.SetPositionAndRotation(deckDrawPoint.position, deckDrawPoint.rotation);
    }

    public void SetResolutionInputLocked(bool locked)
    {
        inputLocked = locked;
    }

    public void RestoreEmptySlotsForRound()
    {
        foreach (var entry in emptySlots)
        {
            if (entry.Key == null) continue;
            originalHandIndices[entry.Key] = entry.Value;
            PlaceNewDrawAtDeck(entry.Key);
            ReturnCardToHand(entry.Key);
            entry.Key.gameObject.SetActive(true);
            entry.Key.HorizontalHandHolder?.SetCardVisible(entry.Key.SourceCard, false);
        }
        emptySlots.Clear();
    }

    public void UnlockOfferingForTesting()
    {
        inputLocked = false;
    }

    /// <summary>
    /// Runtime hook for relics / run modifiers. Example: an extra-play relic
    /// can call SetMaximumCardsToOffer(4). We intentionally do not hard-code a
    /// RelicType here so the Offering stays decoupled from the relic system.
    /// </summary>
    public void SetMaximumCardsToOffer(int maximum)
    {
        maximumCardsOverride = Mathf.Max(1, maximum);
    }

    public void ClearMaximumCardsToOfferOverride()
    {
        maximumCardsOverride = -1;
    }

    /// <summary>
    /// Returns the REAL Card data in the exact left-to-right preview order.
    /// This is the list that should be passed to CardPatternSystem.CheckPatterns.
    /// </summary>
    public List<Card> BuildPlayedCardsInPreviewOrder()
    {
        List<Card> result = new List<Card>(offeredCards.Count);

        foreach (Card3DInteractable physicalCard in offeredCards)
        {
            if (physicalCard != null && physicalCard.SourceCard != null)
                result.Add(physicalCard.SourceCard);
        }

        return result;
    }

    public bool IsPointerOverOffering(Camera cameraToUse, Vector2 screenPosition)
    {
        if (cameraToUse == null)
            return false;

        if (useScreenRegions)
        {
            Vector3 viewport = cameraToUse.ScreenToViewportPoint(
                new Vector3(screenPosition.x, screenPosition.y, 0f)
            );

            bool aboveHand = viewport.y > handAreaMaxViewportY;
            bool insideMiddle =
                viewport.x >= altarLeftViewportX &&
                viewport.x <= altarRightViewportX;

            return aboveHand && insideMiddle;
        }

        if (offeringDropZone == null)
            return false;

        Ray ray = cameraToUse.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            100f,
            pointerRaycastMask,
            QueryTriggerInteraction.Collide
        );

        Transform dropTransform = offeringDropZone.transform;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (hit.collider == offeringDropZone)
                return true;

            Transform hitTransform = hit.collider.transform;

            if (hitTransform.IsChildOf(dropTransform) ||
                dropTransform.IsChildOf(hitTransform))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// While dragging in altar territory, the REAL card aims at the physical
    /// burn recess -- not the floating preview row.
    /// </summary>
    public bool TryGetDragPreviewPose(
        Camera cameraToUse,
        Vector2 screenPosition,
        out Vector3 worldPosition,
        out Quaternion worldRotation)
    {
        worldPosition = Vector3.zero;
        worldRotation = Quaternion.identity;

        if (inputLocked || burnInProgress || IsFull || offeringBurnPoint == null)
            return false;

        if (!IsPointerOverOffering(cameraToUse, screenPosition))
            return false;

        worldPosition = offeringBurnPoint.position + Vector3.up * dragBurnPointLift;
        worldRotation = offeringBurnPoint.rotation;
        return true;
    }

    private void PrepareHiddenPreviewFor(Card3DInteractable card)
    {
        if (card == null ||
            card.VisualRoot == null ||
            offeringPreviewCenter == null)
        {
            return;
        }

        if (pendingPreviews.TryGetValue(card, out PreviewEntry stale))
        {
            if (stale != null && stale.visual != null)
                Destroy(stale.visual);

            pendingPreviews.Remove(card);
        }

        Transform sourceVisual = card.VisualRoot;
        Vector3 sourceWorldScale = sourceVisual.lossyScale;

        // --------------------------------------------------------------
        // IMPORTANT STRUCTURE
        //
        // PreviewRoot       <- layout/drag + collider, NEVER hover-scaled
        //   ScaleRoot       <- ONLY object that scales on hover
        //     GhostVisual   <- red/translucent resting preview
        //     FocusVisual   <- original-color, opaque readability copy
        //
        // This removes the old conflict where layout tweens, hitboxes and
        // hover scaling were all fighting over the same Transform.
        // --------------------------------------------------------------
        GameObject root = new GameObject(
            $"OfferingPreviewRoot_Pending_{offeredCards.Count}"
        );

        if (runtimePreviewRoot != null)
            root.transform.SetParent(runtimePreviewRoot, false);

        root.transform.position = sourceVisual.position;
        root.transform.rotation = sourceVisual.rotation;
        root.transform.localScale = Vector3.one;

        GameObject scaleObject = new GameObject("ScaleRoot");
        scaleObject.transform.SetParent(root.transform, false);
        scaleObject.transform.localPosition = Vector3.zero;
        scaleObject.transform.localRotation = Quaternion.identity;
        scaleObject.transform.localScale = Vector3.one;

        // Red ghost copy.
        GameObject ghost = Instantiate(
            sourceVisual.gameObject,
            scaleObject.transform
        );
        ghost.name = "GhostVisual";
        ghost.transform.localPosition = Vector3.zero;
        ghost.transform.localRotation = Quaternion.identity;
        ghost.transform.localScale = sourceWorldScale * previewScale;
        CopyRendererPropertyBlocks(sourceVisual, ghost.transform, false);
        StripPreviewGameplayComponents(ghost);

        // Separate focus copy. This remains based on the ORIGINAL card
        // materials, so hover readability never depends on partially-mutated
        // transparent materials.
        GameObject focus = Instantiate(
            sourceVisual.gameObject,
            scaleObject.transform
        );
        focus.name = "FocusVisual";
        focus.transform.localPosition = Vector3.zero;
        focus.transform.localRotation = Quaternion.identity;
        focus.transform.localScale = sourceWorldScale * previewScale;
        CopyRendererPropertyBlocks(sourceVisual, focus.transform, true);
        StripPreviewGameplayComponents(focus);

        bool isRedSuit = IsRedSuitCard(card.SourceCard);

        // ALL previews get the SAME red ghost treatment.
        // The old special-case was why only some previews looked red.
        ApplyPreviewTransparency(
            ghost,
            previewOpacity,
            previewTintStrength,
            previewEmissionColor,
            previewEmissionIntensity
        );

        // Make every TMP label readable at rest, regardless of suit.
        ApplyUnifiedPreviewTextReadability(
            ghost,
            isRedSuit,
            false
        );

        // Hover copy remains opaque/original for maximum readability.
        ApplyUnifiedPreviewTextReadability(
            focus,
            isRedSuit,
            true
        );
        focus.SetActive(false);

        PreviewEntry entry = new PreviewEntry
        {
            sourceCard = card,
            visual = root,
            scaleRoot = scaleObject.transform,
            ghostVisual = ghost,
            focusVisual = focus,
            ghostRenderers = CollectNonParticleRenderers(ghost),
            baseWorldScale = Vector3.one,
            hovered = false,
            isRedSuit = isRedSuit,
            baseOpacity = previewOpacity,
            hoverOpacity = Mathf.Max(previewOpacity, previewHoverOpacity),
            currentOpacity = previewOpacity,
            currentTextFocus = 0f
        };

        entry.hitCollider = CreatePreviewHitbox(root);

        // The whole wrapper stays hidden until the physical burn completes.
        root.SetActive(false);

        pendingPreviews[card] = entry;
    }

    private void CopyRendererPropertyBlocks(
        Transform sourceRoot,
        Transform destinationRoot,
        bool preserveFaceArtTint)
    {
        if (sourceRoot == null || destinationRoot == null)
            return;

        int baseMapId = Shader.PropertyToID("_BaseMap");
        int mainTexId = Shader.PropertyToID("_MainTex");
        int baseColorId = Shader.PropertyToID("_BaseColor");
        int colorId = Shader.PropertyToID("_Color");
        var sourceBlock = new MaterialPropertyBlock();
        var destinationBlock = new MaterialPropertyBlock();

        foreach (Renderer sourceRenderer in sourceRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (sourceRenderer == null || sourceRenderer.GetComponent<TMP_Text>() != null)
                continue;

            string relativePath = GetRelativeTransformPath(sourceRoot, sourceRenderer.transform);
            if (relativePath == null)
                continue;

            Transform destinationTransform = string.IsNullOrEmpty(relativePath)
                ? destinationRoot
                : destinationRoot.Find(relativePath);
            if (destinationTransform == null)
                continue;

            Renderer destinationRenderer =
                destinationTransform.GetComponent(sourceRenderer.GetType()) as Renderer;
            if (destinationRenderer == null)
                continue;

            // Do not bring across source colors or opacity: the ghost copy applies
            // its own red hologram tint and alpha below.
            destinationRenderer.SetPropertyBlock(null);

            sourceRenderer.GetPropertyBlock(sourceBlock);
            Texture baseMap = sourceBlock.GetTexture(baseMapId);
            Texture mainTex = sourceBlock.GetTexture(mainTexId);
            if (baseMap == null && mainTex == null)
                continue;

            destinationBlock.Clear();
            if (baseMap != null)
                destinationBlock.SetTexture(baseMapId, baseMap);
            if (mainTex != null)
                destinationBlock.SetTexture(mainTexId, mainTex);

            if (preserveFaceArtTint)
            {
                Color faceArtTint = new Color(0.64f, 0.64f, 0.62f, 1f);
                Material material = destinationRenderer.sharedMaterial;
                if (material != null && material.HasProperty(baseColorId))
                    destinationBlock.SetColor(baseColorId, faceArtTint);
                if (material != null && material.HasProperty(colorId))
                    destinationBlock.SetColor(colorId, faceArtTint);
            }

            destinationRenderer.SetPropertyBlock(destinationBlock);
        }
    }

    private string GetRelativeTransformPath(Transform root, Transform target)
    {
        if (root == target)
            return string.Empty;

        string path = target.name;
        Transform current = target.parent;
        while (current != null && current != root)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return current == root ? path : null;
    }

    private void StripPreviewGameplayComponents(GameObject clone)
    {
        if (clone == null)
            return;

        foreach (Collider col in clone.GetComponentsInChildren<Collider>(true))
            Destroy(col);

        foreach (Card cardData in clone.GetComponentsInChildren<Card>(true))
            Destroy(cardData);

        foreach (AudioSource audioSource in clone.GetComponentsInChildren<AudioSource>(true))
            Destroy(audioSource);

        foreach (Card3DInteractable interactable in clone.GetComponentsInChildren<Card3DInteractable>(true))
            Destroy(interactable);

        foreach (Card3DView view in clone.GetComponentsInChildren<Card3DView>(true))
            Destroy(view);
    }

    private List<Renderer> CollectNonParticleRenderers(GameObject root)
    {
        List<Renderer> result = new List<Renderer>();

        if (root == null)
            return result;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;

            result.Add(renderer);
        }

        return result;
    }

    private void RevealPreparedPreviewFor(Card3DInteractable card)
    {
        if (card == null ||
            !pendingPreviews.TryGetValue(card, out PreviewEntry entry) ||
            entry == null ||
            entry.visual == null)
        {
            return;
        }

        pendingPreviews.Remove(card);
        previewEntries.Add(entry);

        int index = previewEntries.Count - 1;

        GetPreviewPose(
            index,
            previewEntries.Count,
            out Vector3 pos,
            out Quaternion rot
        );

        entry.visual.transform.position = pos;
        entry.visual.transform.rotation = rot;
        entry.visual.transform.localScale = Vector3.one;

        if (entry.scaleRoot != null)
            entry.scaleRoot.localScale = Vector3.one * 0.82f;

        SetPreviewFocusState(entry, false);
        entry.visual.SetActive(true);

        if (entry.scaleRoot != null)
        {
            entry.hoverScaleTween?.Kill(false);
            entry.hoverScaleTween = entry.scaleRoot.DOScale(
                Vector3.one,
                0.11f
            ).SetEase(Ease.OutBack, 1.16f);
        }
    }

    private void RemovePreviewFor(Card3DInteractable card)
    {
        if (pendingPreviews.TryGetValue(card, out PreviewEntry pending))
        {
            pendingPreviews.Remove(card);

            if (pending != null && pending.visual != null)
                Destroy(pending.visual);
        }

        int index = previewEntries.FindIndex(entry => entry.sourceCard == card);
        if (index < 0)
            return;

        PreviewEntry entry = previewEntries[index];
        previewEntries.RemoveAt(index);

        if (pressedPreview == entry)
            ClearPreviewPointerState();

        if (currentHoveredPreview == entry)
            currentHoveredPreview = null;

        entry.hoverScaleTween?.Kill(false);
        entry.hoverOpacityTween?.Kill(false);
        entry.hoverTextTween?.Kill(false);

        if (entry.scaleRoot != null)
            entry.scaleRoot.DOKill(false);

        if (entry.visual != null)
            Destroy(entry.visual);
    }

    private void RecenterPreviewVisuals(float duration, PreviewEntry skipEntry = null)
    {
        int count = previewEntries.Count;

        for (int i = 0; i < count; i++)
        {
            PreviewEntry entry = previewEntries[i];
            if (entry.visual == null || entry == skipEntry)
                continue;

            GetPreviewPose(i, count, out Vector3 position, out Quaternion rotation);

            entry.visual.transform.DOKill(false);
            entry.visual.transform.DOMove(position, duration)
                .SetEase(Ease.OutBack, 1.08f);
            entry.visual.transform.DORotateQuaternion(rotation, duration)
                .SetEase(Ease.OutCubic);
        }
    }

    private void HandlePreviewPointerInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || previewEntries.Count == 0)
        {
            SetHoveredPreview(null);
            return;
        }

        Camera cameraToUse = GetPreviewInteractionCamera();
        if (cameraToUse == null)
        {
            SetHoveredPreview(null);
            return;
        }

        Vector2 pointer = mouse.position.ReadValue();

        PreviewEntry hoveredNow =
            previewPointerDown && pressedPreview != null
                ? pressedPreview
                : FindPreviewUnderPointer(cameraToUse, pointer);

        SetHoveredPreview(hoveredNow);

        if (!previewPointerDown && mouse.leftButton.wasPressedThisFrame)
        {
            PreviewEntry hit = FindPreviewUnderPointer(cameraToUse, pointer);
            if (hit != null)
            {
                previewPointerDown = true;
                previewDragging = false;
                pressedPreview = hit;
                previewPointerDownScreen = pointer;
                previewPointerDownTime = Time.time;

                previewDragPlane = new Plane(
                    cameraToUse.transform.forward,
                    hit.visual.transform.position
                );

                Ray pressRay = cameraToUse.ScreenPointToRay(pointer);
                if (previewDragPlane.Raycast(pressRay, out float pressEnter))
                {
                    Vector3 planePoint = pressRay.GetPoint(pressEnter);
                    previewDragGrabOffset =
                        hit.visual.transform.position - planePoint;
                }
                else
                {
                    previewDragGrabOffset = Vector3.zero;
                }
            }
        }

        if (!previewPointerDown || pressedPreview == null)
            return;

        if (mouse.leftButton.isPressed)
        {
            float pixels = Vector2.Distance(
                previewPointerDownScreen,
                pointer
            );

            if (!previewDragging && pixels >= previewDragPixelThreshold)
                previewDragging = true;

            if (previewDragging)
                UpdatePreviewDrag(cameraToUse, pointer);
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            PreviewEntry releasedEntry = pressedPreview;
            bool wasDrag = previewDragging;
            bool wasClick =
                !wasDrag &&
                Time.time - previewPointerDownTime <= previewClickMaxDuration;

            ClearPreviewPointerState();

            if (wasClick && releasedEntry != null)
            {
                RemoveSpecificCard(releasedEntry.sourceCard);
                return;
            }

            if (wasDrag)
                RecenterPreviewVisuals(previewReorderTweenDuration);
        }
    }

    private void SetHoveredPreview(PreviewEntry entry)
    {
        if (currentHoveredPreview == entry)
            return;

        PreviewEntry previous = currentHoveredPreview;
        currentHoveredPreview = entry;

        if (previous != null)
            AnimatePreviewHover(previous, false);

        if (currentHoveredPreview != null)
            AnimatePreviewHover(currentHoveredPreview, true);
    }

    private void AnimatePreviewHover(PreviewEntry entry, bool hovered)
    {
        if (entry == null || entry.visual == null)
            return;

        entry.hovered = hovered;

        // Instantly swap which visual copy is being rendered:
        // - rest = red/translucent ghost
        // - hover = opaque/original-color focus copy
        //
        // This is intentionally NOT a per-material opacity tween anymore.
        // That old approach was why only some labels/materials changed.
        SetPreviewFocusState(entry, hovered);

        float duration = Mathf.Max(0.01f, previewHoverTweenDuration);
        float multiplier = hovered
            ? Mathf.Max(1f, previewHoverScaleMultiplier)
            : 1f;

        if (entry.scaleRoot != null)
        {
            entry.hoverScaleTween?.Kill(false);
            entry.scaleRoot.DOKill(false);

            entry.hoverScaleTween = entry.scaleRoot.DOScale(
                Vector3.one * multiplier,
                duration
            ).SetEase(Ease.OutCubic);
        }
    }

    private void SetPreviewFocusState(PreviewEntry entry, bool focused)
    {
        if (entry == null)
            return;

        // Keep the ghost GameObject alive so its ash particle system can keep
        // running. Only hide/show its non-particle renderers.
        if (entry.ghostRenderers != null)
        {
            foreach (Renderer renderer in entry.ghostRenderers)
            {
                if (renderer != null)
                    renderer.enabled = !focused;
            }
        }

        if (entry.focusVisual != null)
            entry.focusVisual.SetActive(focused);
    }

    private void ApplyUnifiedPreviewTextReadability(
        GameObject previewRoot,
        bool redSuitCard,
        bool focused)
    {
        if (previewRoot == null)
            return;

        foreach (TMP_Text textMesh in previewRoot.GetComponentsInChildren<TMP_Text>(true))
        {
            if (textMesh == null)
                continue;

            string lowerName = textMesh.gameObject.name.ToLowerInvariant();
            string visible = textMesh.text != null
                ? textMesh.text.Trim().ToLowerInvariant()
                : string.Empty;

            if (lowerName.Contains("decay"))
                continue;

            bool isSuit =
                lowerName.Contains("suit") ||
                visible == "♥" || visible == "♦" ||
                visible == "♣" || visible == "♠" ||
                visible.Contains("heart") ||
                visible.Contains("diamond") ||
                visible.Contains("club") ||
                visible.Contains("spade");

            Color target;

            if (isSuit)
            {
                target = redSuitCard
                    ? previewHoverRedSuitColor
                    : previewHoverBlackSuitColor;
            }
            else
            {
                target = previewHoverValueColor;
            }

            // Ghost labels stay very readable even though the BODY is ghostly.
            // Focus labels are completely opaque.
            target.a = focused ? 1f : 0.98f;
            textMesh.color = target;

            Material sourceMaterial = textMesh.fontSharedMaterial;
            if (sourceMaterial == null)
                continue;

            Material unique = new Material(sourceMaterial);
            textMesh.fontMaterial = unique;

            if (unique.HasProperty("_OutlineColor"))
            {
                unique.SetColor(
                    "_OutlineColor",
                    isSuit && redSuitCard
                        ? Color.white
                        : previewHoverOutlineColor
                );
            }

            if (unique.HasProperty("_OutlineWidth"))
            {
                unique.SetFloat(
                    "_OutlineWidth",
                    Mathf.Max(
                        focused ? previewHoverOutlineWidth : 0.15f,
                        0.15f
                    )
                );
            }

            unique.EnableKeyword("GLOW_ON");

            if (unique.HasProperty("_GlowColor"))
            {
                Color glow = isSuit && redSuitCard
                    ? previewHoverRedSuitColor
                    : target;

                unique.SetColor(
                    "_GlowColor",
                    glow * (focused ? 1.9f : 1.35f)
                );
            }

            if (unique.HasProperty("_GlowOuter"))
                unique.SetFloat("_GlowOuter", focused ? 0.15f : 0.10f);

            if (unique.HasProperty("_GlowPower"))
                unique.SetFloat("_GlowPower", 0.70f);

            textMesh.SetMaterialDirty();
        }
    }

    private List<PreviewTextState> CapturePreviewTextStates(GameObject previewRoot)
    {
        List<PreviewTextState> states = new List<PreviewTextState>();

        if (previewRoot == null)
            return states;

        foreach (TMP_Text textMesh in previewRoot.GetComponentsInChildren<TMP_Text>(true))
        {
            if (textMesh == null)
                continue;

            string lowerName = textMesh.gameObject.name.ToLowerInvariant();
            string visible = textMesh.text != null
                ? textMesh.text.Trim().ToLowerInvariant()
                : string.Empty;

            bool isDecay = lowerName.Contains("decay");
            bool isSuit =
                lowerName.Contains("suit") ||
                visible == "♥" || visible == "♦" ||
                visible == "♣" || visible == "♠" ||
                visible.Contains("heart") ||
                visible.Contains("diamond") ||
                visible.Contains("club") ||
                visible.Contains("spade");

            Material shared = textMesh.fontSharedMaterial;
            Material unique = shared != null
                ? new Material(shared)
                : null;

            if (unique != null)
                textMesh.fontMaterial = unique;

            PreviewTextState state = new PreviewTextState
            {
                text = textMesh,
                baseColor = textMesh.color,
                material = unique,
                isSuit = isSuit,
                isDecay = isDecay
            };

            if (unique != null && unique.HasProperty("_OutlineColor"))
            {
                state.hasOutlineColor = true;
                state.baseOutlineColor = unique.GetColor("_OutlineColor");
            }

            if (unique != null && unique.HasProperty("_OutlineWidth"))
            {
                state.hasOutlineWidth = true;
                state.baseOutlineWidth = unique.GetFloat("_OutlineWidth");
            }

            states.Add(state);
        }

        return states;
    }

    private void ApplyPreviewTextFocus(PreviewEntry entry, float focus)
    {
        if (entry == null || entry.textStates == null)
            return;

        focus = Mathf.Clamp01(focus);

        foreach (PreviewTextState state in entry.textStates)
        {
            if (state == null || state.text == null || state.isDecay)
                continue;

            Color targetColor;

            if (state.isSuit)
            {
                targetColor = entry.isRedSuit
                    ? previewHoverRedSuitColor
                    : previewHoverBlackSuitColor;
            }
            else
            {
                targetColor = previewHoverValueColor;
            }

            // Every non-decay label gets the same hover-focus treatment,
            // including BOTH top-left and bottom-right values.
            Color c = Color.Lerp(state.baseColor, targetColor, focus);
            c.a = Mathf.Lerp(
                Mathf.Clamp01(state.baseColor.a),
                1f,
                focus
            );
            state.text.color = c;

            if (state.material != null)
            {
                if (state.hasOutlineColor)
                {
                    state.material.SetColor(
                        "_OutlineColor",
                        Color.Lerp(
                            state.baseOutlineColor,
                            previewHoverOutlineColor,
                            focus
                        )
                    );
                }

                if (state.hasOutlineWidth)
                {
                    state.material.SetFloat(
                        "_OutlineWidth",
                        Mathf.Lerp(
                            state.baseOutlineWidth,
                            Mathf.Max(
                                state.baseOutlineWidth,
                                previewHoverOutlineWidth
                            ),
                            focus
                        )
                    );
                }
            }

            state.text.SetMaterialDirty();
        }
    }

    private void SetPreviewOpacity(GameObject previewRoot, float opacity)
    {
        if (previewRoot == null)
            return;

        opacity = Mathf.Clamp01(opacity);

        HashSet<Renderer> tmpRenderers = new HashSet<Renderer>();
        foreach (TMP_Text textMesh in previewRoot.GetComponentsInChildren<TMP_Text>(true))
        {
            if (textMesh != null && textMesh.GetComponent<Renderer>() != null)
                tmpRenderers.Add(textMesh.GetComponent<Renderer>());
        }

        foreach (Renderer renderer in previewRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null ||
                renderer is ParticleSystemRenderer ||
                tmpRenderers.Contains(renderer))
            {
                continue;
            }

            Material[] materials = renderer.materials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                    continue;

                if (material.HasProperty("_BaseColor"))
                {
                    Color c = material.GetColor("_BaseColor");
                    c.a = opacity;
                    material.SetColor("_BaseColor", c);
                }

                if (material.HasProperty("_Color"))
                {
                    Color c = material.GetColor("_Color");
                    c.a = opacity;
                    material.SetColor("_Color", c);
                }
            }

            renderer.materials = materials;
        }

        // TMP text is intentionally NOT changed here. Text readability is
        // controlled independently by ApplyPreviewTextFocus(), so every value
        // reacts consistently even if the card-body shader handles alpha in a
        // different way.
    }

    private void UpdatePreviewDrag(Camera cameraToUse, Vector2 pointer)
    {
        if (pressedPreview == null ||
            pressedPreview.visual == null ||
            offeringPreviewCenter == null)
        {
            return;
        }

        Vector2 dragPointer = pointer;

        if (limitPreviewDragToCenter)
        {
            float minX = Mathf.Clamp01(Mathf.Min(
                previewDragViewportXRange.x,
                previewDragViewportXRange.y
            ));
            float maxX = Mathf.Clamp01(Mathf.Max(
                previewDragViewportXRange.x,
                previewDragViewportXRange.y
            ));

            Vector3 viewport = cameraToUse.ScreenToViewportPoint(
                new Vector3(pointer.x, pointer.y, 0f)
            );

            viewport.x = Mathf.Clamp(viewport.x, minX, maxX);

            Vector3 clampedScreen = cameraToUse.ViewportToScreenPoint(viewport);
            dragPointer.x = clampedScreen.x;
        }

        Ray ray = cameraToUse.ScreenPointToRay(dragPointer);
        if (!previewDragPlane.Raycast(ray, out float enter))
            return;

        Vector3 rawTarget = ray.GetPoint(enter) + previewDragGrabOffset;
        Vector3 rowCenter =
            offeringPreviewCenter.position +
            Vector3.up * previewHeightOffset;

        Vector3 rowRight = offeringPreviewCenter.right.normalized;
        float horizontal = Vector3.Dot(rawTarget - rowCenter, rowRight);

        if (limitPreviewDragToCenter)
        {
            float halfRowWidth =
                Mathf.Max(0f, (previewEntries.Count - 1) * 0.5f) *
                Mathf.Abs(previewCardSpacing);

            float maxHorizontal =
                halfRowWidth +
                Mathf.Abs(previewCardSpacing) * previewDragExtraSlotAllowance;

            horizontal = Mathf.Clamp(
                horizontal,
                -maxHorizontal,
                maxHorizontal
            );
        }

        // Constrain preview reordering to one clean horizontal lane.
        Vector3 target =
            rowCenter +
            rowRight * horizontal -
            cameraToUse.transform.forward * previewDragTowardCamera;

        pressedPreview.visual.transform.DOKill(false);
        pressedPreview.visual.transform.position = target;

        GetPreviewPose(
            previewEntries.IndexOf(pressedPreview),
            previewEntries.Count,
            out _,
            out Quaternion targetRotation
        );
        pressedPreview.visual.transform.rotation = targetRotation;

        int oldIndex = previewEntries.IndexOf(pressedPreview);
        if (oldIndex < 0)
            return;

        int newIndex = GetNearestPreviewIndexForWorldPosition(target);
        if (newIndex == oldIndex)
            return;

        // IMPORTANT: previewEntries and offeredCards move together.
        // offeredCards therefore becomes the authoritative play-order list.
        PreviewEntry visualEntry = previewEntries[oldIndex];
        Card3DInteractable physicalCard = offeredCards[oldIndex];

        previewEntries.RemoveAt(oldIndex);
        offeredCards.RemoveAt(oldIndex);

        newIndex = Mathf.Clamp(newIndex, 0, previewEntries.Count);
        previewEntries.Insert(newIndex, visualEntry);
        offeredCards.Insert(newIndex, physicalCard);

        RecenterPreviewVisuals(
            previewReorderTweenDuration,
            pressedPreview
        );
    }

    private int GetNearestPreviewIndexForWorldPosition(Vector3 worldPosition)
    {
        int count = previewEntries.Count;
        if (count <= 1 || offeringPreviewCenter == null)
            return 0;

        Vector3 rowCenter =
            offeringPreviewCenter.position +
            Vector3.up * previewHeightOffset;

        Vector3 rowRight = offeringPreviewCenter.right.normalized;
        float spacing = Mathf.Max(Mathf.Abs(previewCardSpacing), 0.0001f);
        float horizontal = Vector3.Dot(worldPosition - rowCenter, rowRight);

        float floatingIndex =
            horizontal / spacing +
            (count - 1) * 0.5f;

        return Mathf.Clamp(
            Mathf.RoundToInt(floatingIndex),
            0,
            count - 1
        );
    }

    private PreviewEntry FindPreviewUnderPointer(
        Camera cameraToUse,
        Vector2 screenPosition)
    {
        Ray ray = cameraToUse.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            100f,
            ~0,
            QueryTriggerInteraction.Collide
        );

        PreviewEntry bestEntry = null;
        float bestDistance = float.PositiveInfinity;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            foreach (PreviewEntry entry in previewEntries)
            {
                if (entry == null || entry.hitCollider == null)
                    continue;

                if (hit.collider == entry.hitCollider &&
                    hit.distance < bestDistance)
                {
                    bestEntry = entry;
                    bestDistance = hit.distance;
                }
            }
        }

        return bestEntry;
    }

    private Camera GetPreviewInteractionCamera()
    {
        if (previewInteractionCamera != null &&
            previewInteractionCamera.isActiveAndEnabled)
        {
            return previewInteractionCamera;
        }

        if (Camera.main != null && Camera.main.isActiveAndEnabled)
            return Camera.main;

        foreach (Camera candidate in Camera.allCameras)
        {
            if (candidate != null && candidate.isActiveAndEnabled)
                return candidate;
        }

        return null;
    }

    private void ClearPreviewPointerState()
    {
        previewPointerDown = false;
        previewDragging = false;
        pressedPreview = null;
        previewDragGrabOffset = Vector3.zero;
    }

    private BoxCollider CreatePreviewHitbox(GameObject previewRoot)
    {
        if (previewRoot == null)
            return null;

        BoxCollider collider = previewRoot.AddComponent<BoxCollider>();
        collider.isTrigger = true;

        Renderer[] renderers =
            previewRoot.GetComponentsInChildren<Renderer>(true);

        bool hasBounds = false;
        Bounds localBounds = new Bounds(Vector3.zero, Vector3.zero);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;

            Bounds worldBounds = renderer.bounds;
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;

            Vector3[] corners = new Vector3[]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z),
            };

            foreach (Vector3 corner in corners)
            {
                Vector3 local =
                    previewRoot.transform.InverseTransformPoint(corner);

                if (!hasBounds)
                {
                    localBounds = new Bounds(local, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(local);
                }
            }
        }

        if (hasBounds)
        {
            collider.center = localBounds.center;
            collider.size = localBounds.size + previewHitboxPadding;
        }
        else
        {
            // Safe fallback roughly matching the current physical card size.
            collider.center = Vector3.zero;
            collider.size = new Vector3(0.14f, 0.01f, 0.18f);
        }

        return collider;
    }

    private void GetPreviewPose(
        int index,
        int totalCount,
        out Vector3 worldPosition,
        out Quaternion worldRotation)
    {
        float centeredIndex = index - (totalCount - 1) * 0.5f;

        worldPosition =
            offeringPreviewCenter.position +
            Vector3.up * previewHeightOffset +
            offeringPreviewCenter.right * (centeredIndex * previewCardSpacing);

        worldRotation =
            offeringPreviewCenter.rotation *
            Quaternion.Euler(previewRotationOffsetEuler);
    }

    private void ReturnCardToHand(Card3DInteractable card)
    {
        int handIndex = 999;

        if (originalHandIndices.TryGetValue(card, out int storedIndex))
            handIndex = storedIndex;

        originalHandIndices.Remove(card);

        HorizontalCardHolder hand = card.HorizontalHandHolder;
        if (hand == null)
            hand = FindFirstObjectByType<HorizontalCardHolder>();

        if (hand != null)
            hand.ReturnFromOffering(card, handIndex);
    }

    private bool IsRedSuitCard(Card cardData)
    {
        if (cardData == null || string.IsNullOrWhiteSpace(cardData.cardSuit))
            return false;

        string suit = cardData.cardSuit.ToLowerInvariant();
        return cardData.cardSuit == "♥" ||
               cardData.cardSuit == "♦" ||
               suit.Contains("heart") ||
               suit.Contains("diamond");
    }

    private void ApplyRedSuitPreviewReadability(GameObject previewRoot)
    {
        if (previewRoot == null)
            return;

        foreach (TMP_Text textMesh in previewRoot.GetComponentsInChildren<TMP_Text>(true))
        {
            if (textMesh == null)
                continue;

            string lowerName = textMesh.gameObject.name.ToLowerInvariant();
            string visibleText = textMesh.text != null
                ? textMesh.text.Trim().ToLowerInvariant()
                : string.Empty;

            bool isDecay = lowerName.Contains("decay");
            if (isDecay)
                continue;

            bool isSuit =
                lowerName.Contains("suit") ||
                visibleText == "♥" ||
                visibleText == "♦" ||
                visibleText == "heart" ||
                visibleText == "hearts" ||
                visibleText == "diamond" ||
                visibleText == "diamonds";

            // On a red-suit preview, every non-decay card label gets explicit
            // high contrast. Suit glyphs stay vivid red; values become ivory.
            Color target = isSuit
                ? redSuitPreviewSuitColor
                : redSuitPreviewValueColor;

            target.a = Mathf.Max(textMesh.color.a, 0.92f);
            textMesh.color = target;

            // Give each cloned TMP object its own material before changing
            // outline/glow so one label cannot accidentally affect another.
            Material sourceMaterial = textMesh.fontSharedMaterial;
            if (sourceMaterial == null)
                continue;

            Material material = new Material(sourceMaterial);
            textMesh.fontMaterial = material;

            if (material.HasProperty("_OutlineColor"))
            {
                material.SetColor(
                    "_OutlineColor",
                    isSuit ? Color.white : redSuitPreviewOutlineColor
                );
            }

            if (material.HasProperty("_OutlineWidth"))
            {
                material.SetFloat(
                    "_OutlineWidth",
                    isSuit
                        ? Mathf.Max(redSuitPreviewOutlineWidth, 0.14f)
                        : Mathf.Max(redSuitPreviewOutlineWidth, 0.18f)
                );
            }

            material.EnableKeyword("GLOW_ON");

            if (material.HasProperty("_GlowColor"))
            {
                Color glow = isSuit
                    ? redSuitPreviewGlowColor
                    : redSuitPreviewValueColor;

                material.SetColor(
                    "_GlowColor",
                    glow * Mathf.Max(1f, redSuitPreviewGlowIntensity)
                );
            }

            if (material.HasProperty("_GlowOuter"))
                material.SetFloat("_GlowOuter", 0.14f);

            if (material.HasProperty("_GlowPower"))
                material.SetFloat("_GlowPower", 0.70f);

            textMesh.SetMaterialDirty();
        }
    }

    private void ApplyPreviewTransparency(
        GameObject previewRoot,
        float opacity,
        float tintStrength,
        Color bodyEmissionColor,
        float bodyEmissionIntensity)
    {
        opacity = Mathf.Clamp01(opacity);
        tintStrength = Mathf.Clamp01(tintStrength);

        HashSet<Renderer> tmpRenderers = new HashSet<Renderer>();
        foreach (TMP_Text textMesh in previewRoot.GetComponentsInChildren<TMP_Text>(true))
        {
            if (textMesh != null && textMesh.GetComponent<Renderer>() != null)
                tmpRenderers.Add(textMesh.GetComponent<Renderer>());
        }

        foreach (Renderer renderer in previewRoot.GetComponentsInChildren<Renderer>(true))
        {
            // TMP gets handled separately below. Do not red-tint / emit the
            // text material together with the physical card body.
            if (renderer == null || tmpRenderers.Contains(renderer))
                continue;

            Material[] materials = renderer.materials;

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                    continue;

                if (material.HasProperty("_BaseColor"))
                {
                    Color c = material.GetColor("_BaseColor");
                    c = Color.Lerp(c, previewTintColor, tintStrength);
                    c.a *= opacity;
                    material.SetColor("_BaseColor", c);
                }

                if (material.HasProperty("_Color"))
                {
                    Color c = material.GetColor("_Color");
                    c = Color.Lerp(c, previewTintColor, tintStrength);
                    c.a *= opacity;
                    material.SetColor("_Color", c);
                }

                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor(
                        "_EmissionColor",
                        bodyEmissionColor * Mathf.Max(0f, bodyEmissionIntensity)
                    );
                }

                // URP Lit / many Shader Graph materials.
                if (material.HasProperty("_Surface"))
                    material.SetFloat("_Surface", 1f);

                if (material.HasProperty("_ZWrite"))
                    material.SetFloat("_ZWrite", 0f);

                if (material.HasProperty("_SrcBlend"))
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);

                if (material.HasProperty("_DstBlend"))
                    material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);

                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                if (material.HasProperty("_AlphaClip"))
                    material.SetFloat("_AlphaClip", 0f);
                material.DisableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)RenderQueue.Transparent;
            }

            renderer.materials = materials;
        }

        foreach (TMP_Text textMesh in previewRoot.GetComponentsInChildren<TMP_Text>(true))
        {
            Color c = textMesh.color;
            // Keep text color mostly independent from the body tint so labels
            // retain contrast. Red-suit previews get their explicit colors in
            // ApplyRedSuitPreviewReadability().
            c.a = opacity;
            textMesh.color = c;
        }

        if (spawnAshFx)
            AddAshFx(previewRoot);
    }

    private void AddAshFx(GameObject previewRoot)
    {
        if (previewRoot == null)
            return;

        GameObject ash = new GameObject("PreviewAshFX");
        ash.transform.SetParent(previewRoot.transform, false);
        ash.transform.localPosition = Vector3.zero;
        ash.transform.localRotation = Quaternion.identity;

        ParticleSystem ps = ash.AddComponent<ParticleSystem>();

        // AddComponent can begin the ParticleSystem immediately.
        // Stop/clear it BEFORE changing properties such as duration.
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(
            ashLifetimeMin,
            ashLifetimeMax
        );
        main.startSpeed = new ParticleSystem.MinMaxCurve(
            ashSpeedMin,
            ashSpeedMax
        );
        main.startSize = new ParticleSystem.MinMaxCurve(
            ashSizeMin,
            ashSizeMax
        );
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;

        Gradient grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.95f, 0.32f, 0.18f), 0f),
                new GradientColorKey(new Color(0.35f, 0.07f, 0.05f), 0.4f),
                new GradientColorKey(new Color(0.08f, 0.08f, 0.08f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(0.75f, 0f),
                new GradientAlphaKey(0.28f, 0.55f),
                new GradientAlphaKey(0f, 1f),
            }
        );
        main.startColor = new ParticleSystem.MinMaxGradient(grad);

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = ashRateOverTime;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = ashLocalBox;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;

        // Unity requires X/Y/Z velocity curves to use the same curve mode.
        // Keep all three in Constant mode.
        velocity.x = new ParticleSystem.MinMaxCurve(0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.045f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0.15f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.material = null;

        ps.Play();
    }
}
