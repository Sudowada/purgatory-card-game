using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Native 3D hand holder — replaces the old UI/Canvas-based version.
///
/// Same class name as before so GameManager's existing
/// [SerializeField] private HorizontalCardHolder handHolder;
/// field keeps working with zero changes.
///
/// This GameObject IS the fan anchor — position it directly on the table
/// where you want the hand to sit. No Canvas, no RectTransform, no
/// off-screen trick required.
///
/// SETUP:
///   1. Position this GameObject on the table where the hand should sit.
///   2. Assign Card 3D Prefab -> your combined Card3DRoot prefab (mesh +
///      Card3DView + Card3DInteractable + BoxCollider + a child GameObject
///      with the Card data component on it).
///   3. Set Cards To Spawn to match your hand size.
///   4. In GameManager's Inspector, assign Hand Holder -> this object.
///
/// Card3DView.Initialize() gets called with the Card living right inside
/// the same spawned prefab instance — no external linking, no separate
/// invisible 2D twin anywhere.
/// </summary>
public class HorizontalCardHolder : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject card3DPrefab;
    [SerializeField] private int cardsToSpawn = 7;

    [Tooltip("Optional — if assigned, cards spawn here first and animate into their hand slot (a 'dealt from the deck' feel). Leave null to just appear directly in place.")]
    [SerializeField] private Transform dealFromPoint;

    [Header("Fan Layout — Inscryption-style: grounded, physically dealt feel")]
    [Tooltip("The card's own resting tilt (e.g. lying flat facing up) — gets combined with the fan spread rotation instead of being overwritten by it. Try (90, 0, 0) first if your cards are invisible/edge-on; adjust to match how your prefab looks correct in the Scene view.")]
    [SerializeField] private Vector3 cardRestEuler = new Vector3(90f, 0f, 0f);
    [Tooltip("Base spacing between card centers. Lower = more overlap, like cards fresh off the deck rather than fanned wide in a UI.")]
    [SerializeField] private float cardSpacing = 0.115f;
    [Tooltip("Total angle (degrees) the fan spans from leftmost to rightmost card.")]
    [SerializeField] private float fanAngleTotal = 18f;
    [Tooltip("Shapes how rotation builds up across the hand. 1 = linear (equal angle steps). Higher = center cards stay nearly flat and rotation ramps up more toward the edges, like a real fan.")]
    [SerializeField] private float fanAngleCurvePower = 1.8f;
    [Tooltip("How far below each card's own center the rotation pivot sits, roughly matching a real card's half-length (~0.08-0.12). This is what makes bottoms converge and tops spread — bigger = more dramatic convergence, 0 = flat rotate-in-place like before.")]
    [SerializeField] private float cardPivotOffset = 0.09f;
    [SerializeField] private float fanRiseCenter = 0.032f;
    [SerializeField] private float cardHeightAboveTable = 0.01f;
    [Tooltip("Each successive card sits slightly further forward than the last (along this holder's forward axis) — stops overlapping cards from clipping/z-fighting into each other.")]
    [SerializeField] private float cardDepthStagger = 0.013f;

    [Tooltip("Tiny vertical layering between overlapping cards. This gives every card a consistent physical top/bottom order so idle cards do not phase through each other.")]
    [SerializeField] private float cardVerticalLayerStep = 0.0012f;

    [Header("Dealt-Hand Jitter — small per-slot randomness so cards look tossed down, not machine-placed")]
    [Tooltip("Max random position offset (meters) applied per hand slot, sideways and forward/back.")]
    [SerializeField] private float jitterPositionAmount = 0.006f;
    [Tooltip("Max random rotation offset (degrees) applied per hand slot.")]
    [SerializeField] private float jitterRotationAmount = 2f;
    [Tooltip("Fixed seed so the jitter pattern is reproducible — change this to get a different-looking scatter without touching code.")]
    [SerializeField] private int jitterSeed = 1337;

    [Header("Hover Wave — closest-to-cursor card rises, comes forward, and scales up; neighbors get a smaller version, tapering with distance")]
    [Tooltip("How many card-slots away the wave effect reaches. 1 = only immediate neighbors get any of it, 2 = neighbors-of-neighbors get a smaller trace, etc.")]
    [SerializeField] private float waveRadius = 2f;
    [Tooltip("How much the active card rises (meters). Neighbors get a fraction of this based on distance.")]
    [SerializeField] private float waveLiftAmount = 0.05f;
    [Tooltip("How much the active card comes forward/toward the camera (meters) — this is what brings it in front of neighbors so it's never clipped/obscured.")]
    [SerializeField] private float waveForwardAmount = 0.05f;
    [Tooltip("Extra scale multiplier on the active card (e.g. 0.06 = +6% size at the peak of the wave).")]
    [SerializeField] private float waveScaleBoost = 0.06f;
    [Tooltip("Sideways push applied only to the immediate left/right neighbors, making a bit of room for the active card.")]
    [SerializeField] private float neighborNudgeAmount = 0.028f;
    [Tooltip("Minimum smoothed hover proximity (0-1) a card needs before it can become the wave's center.")]
    [SerializeField] private float neighborNudgeIntensityThreshold = 0.2f;

    /// <summary>Real card data — GameManager reads/writes this exactly like before.</summary>
    public List<Card> cards = new List<Card>();

    private class CardSlot
    {
        public GameObject go;
        public Card3DInteractable interactable;
        public Card3DView view;
        public Card card;
        /// <summary>Static, never-animated collider used for all hover/click hit-testing — see the field comment on Card3DInteractable._hoverAnchor for why.</summary>
        public Transform hoverAnchor;
    }

    private readonly List<CardSlot> _slots = new List<CardSlot>();

    // Cards staged above the altar are temporarily removed from _slots/cards,
    // but their complete slot data is stored here so Undo can restore them.
    private readonly Dictionary<Card3DInteractable, CardSlot> _offeredSlots =
        new Dictionary<Card3DInteractable, CardSlot>();

    private int _draggingIndex = -1;

    void Start()
    {
        SpawnHand();
    }

    void Update()
    {
        HandleDragReorder();
        HandleDeselectAll();
        HandleHoverWave();
    }

    // ── Spawning ──────────────────────────────────────────────────────────────

    void SpawnHand()
    {
        if (card3DPrefab == null)
        {
            Debug.LogError("[HorizontalCardHolder] Card 3D Prefab is not assigned.");
            return;
        }

        for (int i = 0; i < cardsToSpawn; i++)
        {
            GameObject instance = Instantiate(card3DPrefab, transform);
            instance.name = $"Card3D_{i}";

            var view = instance.GetComponent<Card3DView>();
            var interactable = instance.GetComponent<Card3DInteractable>();
            var card = instance.GetComponentInChildren<Card>();

            if (card == null)
            {
                Debug.LogError("[HorizontalCardHolder] Card 3D Prefab has no Card component anywhere in its hierarchy — can't spawn real card data.");
                Destroy(instance);
                continue;
            }
            if (view == null)
            {
                Debug.LogError("[HorizontalCardHolder] Card 3D Prefab has no Card3DView component.");
                Destroy(instance);
                continue;
            }

            card.name = i.ToString();

            (Vector3 localPos, Quaternion localRot) = GetFanSlot(i, cardsToSpawn);
            Vector3 worldPos = WorldFromLocalOffset(localPos);
            Quaternion worldRot = transform.rotation * localRot;

            if (dealFromPoint != null)
                instance.transform.position = dealFromPoint.position;
            else
                instance.transform.position = worldPos;

            instance.transform.rotation = worldRot;

            view.Initialize(card);

            if (interactable != null)
            {
                interactable.SetBasePose(worldPos, worldRot);
                interactable.SetHandIndex(i);
            }

            // Static, non-animated hover anchor — parented to this holder
            // itself (which never moves/animates), so raycasting against it
            // gives a perfectly stable result every frame regardless of how
            // the visual card is rising, nudging, tilting, or swaying.
            // Only moves when a real re-fan happens (deal/reorder).
            var anchorGO = new GameObject($"HoverAnchor_{i}");
            anchorGO.transform.SetParent(transform, false);
            anchorGO.transform.position = worldPos;
            anchorGO.transform.rotation = worldRot;
            anchorGO.layer = instance.layer;

            var anchorCollider = anchorGO.AddComponent<BoxCollider>();
            var sourceCollider = instance.GetComponent<BoxCollider>();
            if (sourceCollider != null)
            {
                anchorCollider.size   = sourceCollider.size;
                anchorCollider.center = sourceCollider.center;
            }
            anchorCollider.isTrigger = true;

            interactable?.SetHoverAnchor(anchorCollider);

            _slots.Add(new CardSlot { go = instance, interactable = interactable, view = view, card = card, hoverAnchor = anchorGO.transform });
            cards.Add(card);
        }
    }

    // ── Drag-to-reorder ───────────────────────────────────────────────────────

    void HandleDragReorder()
    {
        if (_slots.Count < 2) return;

        int currentlyDraggingIndex = -1;
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].interactable != null && _slots[i].interactable.IsDraggingInHand)
            {
                currentlyDraggingIndex = i;
                break;
            }
        }

        if (currentlyDraggingIndex == -1)
        {
            if (_draggingIndex != -1)
            {
                _draggingIndex = -1;
                RefanAll();
            }
            return;
        }

        _draggingIndex = currentlyDraggingIndex;

        float draggedLocalX = GetLocalX(_slots[_draggingIndex].go);

        for (int i = 0; i < _slots.Count; i++)
        {
            if (i == _draggingIndex) continue;
            float otherLocalX = GetLocalX(_slots[i].go);

            bool crossedRight = draggedLocalX > otherLocalX && _draggingIndex < i;
            bool crossedLeft  = draggedLocalX < otherLocalX && _draggingIndex > i;

            if (crossedRight || crossedLeft)
            {
                var displacedInteractable = _slots[i].interactable; // the card about to move into _draggingIndex's old slot

                (_slots[_draggingIndex], _slots[i]) = (_slots[i], _slots[_draggingIndex]);
                (cards[_draggingIndex], cards[i]) = (cards[i], cards[_draggingIndex]);
                _draggingIndex = i;
                RefanExcept(_draggingIndex);

                displacedInteractable?.PunchSwap(crossedRight ? -1f : 1f);
                break;
            }
        }
    }

    /// <summary>Projects a card's world position onto this holder's right axis, in true meters — consistent with WorldFromLocalOffset regardless of parent scale.</summary>
    float GetLocalX(GameObject card)
    {
        Vector3 offset = card.transform.position - transform.position;
        return Vector3.Dot(offset, transform.right);
    }

    void RefanExcept(int skipIndex)
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (i == skipIndex || _slots[i].interactable == null) continue;

            (Vector3 localPos, Quaternion localRot) = GetFanSlot(i, _slots.Count);
            Vector3 worldPos = WorldFromLocalOffset(localPos);
            Quaternion worldRot = transform.rotation * localRot;

            _slots[i].interactable.SetBasePose(worldPos, worldRot);
            _slots[i].interactable.SetHandIndex(i);

            if (_slots[i].hoverAnchor != null)
            {
                _slots[i].hoverAnchor.position = worldPos;
                _slots[i].hoverAnchor.rotation = worldRot;
            }
        }
    }

    /// <summary>Snaps every card (including the just-dropped one) into its finalized fan slot.</summary>
    public void RefanAll()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].interactable == null) continue;

            (Vector3 localPos, Quaternion localRot) = GetFanSlot(i, _slots.Count);
            Vector3 worldPos = WorldFromLocalOffset(localPos);
            Quaternion worldRot = transform.rotation * localRot;

            _slots[i].interactable.SetBasePose(worldPos, worldRot);
            _slots[i].interactable.SetHandIndex(i);

            if (_slots[i].hoverAnchor != null)
            {
                _slots[i].hoverAnchor.position = worldPos;
                _slots[i].hoverAnchor.rotation = worldRot;
            }
        }
    }


    // ── Offering / altar integration ──────────────────────────────────────────

    /// <summary>
    /// Temporarily removes a physical card from the hand while it is staged
    /// above the altar. Returns its previous hand index for Undo.
    /// </summary>
    public int RemoveForOffering(Card3DInteractable interactable)
    {
        if (interactable == null)
            return -1;

        int index = _slots.FindIndex(
            slot => slot.interactable == interactable
        );

        if (index < 0)
            return -1;

        CardSlot slot = _slots[index];

        _slots.RemoveAt(index);

        if (index >= 0 && index < cards.Count)
            cards.RemoveAt(index);
        else
            cards.Remove(slot.card);

        _offeredSlots[interactable] = slot;

        if (slot.hoverAnchor != null)
            slot.hoverAnchor.gameObject.SetActive(false);

        _draggingIndex = -1;
        RefanAll();

        return index;
    }

    /// <summary>
    /// Returns an offered card to the hand, normally to the slot it occupied
    /// before being staged.
    /// </summary>
    public void ReturnFromOffering(
        Card3DInteractable interactable,
        int preferredIndex)
    {
        if (interactable == null)
            return;

        if (!_offeredSlots.TryGetValue(interactable, out CardSlot slot))
        {
            Debug.LogWarning(
                "[HorizontalCardHolder] ReturnFromOffering could not find the stored card slot."
            );
            return;
        }

        int index =
            Mathf.Clamp(preferredIndex, 0, _slots.Count);

        _slots.Insert(index, slot);
        cards.Insert(index, slot.card);

        _offeredSlots.Remove(interactable);

        if (slot.hoverAnchor != null)
            slot.hoverAnchor.gameObject.SetActive(true);

        interactable.ReturnToHandState();

        RefanAll();
    }

    /// <summary>
    /// Converts a small local offset (in meters, relative to this holder's
    /// own orientation) into a world position WITHOUT applying any inherited
    /// parent scale — transform.TransformPoint() multiplies by parent scale,
    /// which silently blows up spacing if Table (or anything above it) isn't
    /// scale (1,1,1). Direction vectors stay normalized regardless of scale.
    /// </summary>
    Vector3 WorldFromLocalOffset(Vector3 localOffset)
    {
        return transform.position
            + transform.right   * localOffset.x
            + transform.up      * localOffset.y
            + transform.forward * localOffset.z;
    }

    // ── Deselect-all ──────────────────────────────────────────────────────────

    void HandleDeselectAll()
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.wasPressedThisFrame)
        {
            foreach (var slot in _slots)
                slot.interactable?.Deselect();
        }
    }

    // ── Neighbor nudge ───────────────────────────────────────────────────────

    /// <summary>
    /// Finds whichever card is currently hovered or being dragged, and
    /// pushes its immediate left/right neighbors sideways to make room —
    /// makes the hand read as one connected mechanical system instead of
    /// independent cards. Every other card's nudge gets cleared back to
    /// zero, so it only ever affects the two cards next to the active one.
    /// </summary>
    /// <summary>
    /// Whichever card is closest to the cursor (highest smoothed proximity,
    /// or being dragged) becomes the wave's center: it rises, comes forward
    /// toward the camera (so it's never clipped/obscured by neighbors), and
    /// scales up slightly. Neighbors within waveRadius get a smaller
    /// version of the same effect, tapering off with distance — this is
    /// what makes the whole hand feel like one connected system.
    /// </summary>
    void HandleHoverWave()
    {
        int activeIndex = -1;

        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].interactable != null && _slots[i].interactable.IsDraggingInHand)
            {
                activeIndex = i;
                break;
            }
        }

        if (activeIndex == -1)
        {
            float bestIntensity = neighborNudgeIntensityThreshold;
            for (int i = 0; i < _slots.Count; i++)
            {
                var interactable = _slots[i].interactable;
                if (interactable == null) continue;

                float intensity = interactable.HoverIntensity;
                if (intensity > bestIntensity)
                {
                    bestIntensity = intensity;
                    activeIndex = i;
                }
            }
        }

        for (int i = 0; i < _slots.Count; i++)
        {
            var interactable = _slots[i].interactable;
            if (interactable == null) continue;

            // Exactly one primary hover card. This is what makes the riffle
            // sound fire once when the cursor moves A -> B -> C.
            interactable.SetPrimaryHover(i == activeIndex);

            if (activeIndex == -1)
            {
                interactable.SetWaveEffect(Vector3.zero, 0f);
                continue;
            }

            int dist = Mathf.Abs(i - activeIndex);
            float linearInfluence = Mathf.Clamp01(1f - dist / waveRadius);
            // Smoothstep instead of a hard linear taper — softer handoff
            // between "full effect" and "fading out" across neighbors.
            float influence = linearInfluence * linearInfluence * (3f - 2f * linearInfluence);

            Vector3 offset = transform.up * (waveLiftAmount * influence)
                            + transform.forward * (waveForwardAmount * influence);

            if (i == activeIndex - 1)
                offset += -transform.right * neighborNudgeAmount * influence;
            else if (i == activeIndex + 1)
                offset += transform.right * neighborNudgeAmount * influence;

            interactable.SetWaveEffect(offset, waveScaleBoost * influence);
        }
    }

    /// <summary>
    /// Neutral card rotation for a card being held/dragged.
    /// Uses the same serialized Card Rest Euler as the hand, but removes the
    /// per-slot fan angle and jitter. This keeps the card visually "straight"
    /// without introducing a camera-axis 90-degree flip.
    /// </summary>
    public Quaternion GetStraightHandRotation()
    {
        return transform.rotation * Quaternion.Euler(cardRestEuler);
    }

    // ── Fan math ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Inscryption-style hand layout: cards overlap more than a clean UI
    /// fan, and each slot gets a small, STABLE random jitter — same seed
    /// every time GetFanSlot(index, ...) is called for that index, so
    /// re-fanning during a reorder doesn't make cards visibly jump to a
    /// new random offset. The jitter is per SLOT POSITION, not per card
    /// identity, so a card passing through a given slot always picks up
    /// that slot's characteristic little tilt — like real dealt cards.
    /// </summary>
    (Vector3 pos, Quaternion rot) GetFanSlot(int index, int total)
    {
        if (total <= 1)
            return (Vector3.up * cardHeightAboveTable, Quaternion.identity);

        float t = (index / (float)(total - 1)) * 2f - 1f; // -1..1, 0 = center

        float xOffset = t * cardSpacing * (total - 1) * 0.5f;
        // Eased rotation curve — near-center cards stay close to flat, and
        // rotation builds up more steeply toward the edges (fanAngleCurvePower
        // > 1), instead of every card stepping by the same fixed angle.
        float curvedT = Mathf.Sign(t) * Mathf.Pow(Mathf.Abs(t), fanAngleCurvePower);
        float yRot    = -curvedT * (fanAngleTotal * 0.5f); // reversed direction
        float rise    = fanRiseCenter * (1f - Mathf.Abs(t));

        // Pivot the rotation around a point near each card's own BOTTOM edge
        // (toward the player, -Z) instead of its center — this is what
        // actually makes bottoms converge and tops spread outward, like a
        // real held fan of cards. cardPivotOffset is small (~card
        // half-length), unlike the old external-pivot attempt which used a
        // huge radius and sent the whole hand flying off the table.
        float angleRad = yRot * Mathf.Deg2Rad;
        float pivotDX = cardPivotOffset * Mathf.Sin(angleRad);
        float pivotDZ = cardPivotOffset * (Mathf.Cos(angleRad) - 1f); // 0 at center, negative (pulls toward pivot) at the edges

        // Deterministic per-slot jitter — uses its own System.Random instance
        // seeded from (jitterSeed, index) so it never touches or gets
        // affected by UnityEngine.Random's global state (deck shuffling etc.).
        var rng = new System.Random(jitterSeed + index * 7919);
        float jitterX   = ((float)rng.NextDouble() * 2f - 1f) * jitterPositionAmount;
        float jitterZ   = ((float)rng.NextDouble() * 2f - 1f) * jitterPositionAmount;
        float jitterRot = ((float)rng.NextDouble() * 2f - 1f) * jitterRotationAmount;

        Vector3 pos = new Vector3(
            xOffset + pivotDX + jitterX,
            cardHeightAboveTable + rise + index * cardVerticalLayerStep,
            index * cardDepthStagger + pivotDZ + jitterZ);

        Quaternion rot = Quaternion.Euler(cardRestEuler) * Quaternion.Euler(0f, yRot + jitterRot, 0f);

        return (pos, rot);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.7f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, 0.05f);
    }
#endif
}