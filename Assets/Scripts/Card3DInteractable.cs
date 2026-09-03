using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;

/// <summary>
/// Basic 3D card interaction for the new physical card prefab.
///
/// Put this script on Card3DRoot.
/// Card3DRoot also needs:
/// - BoxCollider
/// - Card3DView
///
/// IMPORTANT: this uses MANUAL per-frame raycasting via the new Input
/// System instead of Unity's legacy OnMouseEnter/OnMouseDown/OnMouseDrag/
/// OnMouseUp/OnMouseExit magic methods. Those legacy methods silently do
/// nothing when a project's Active Input Handling is set to "Input System
/// Package" only (rather than "Both") — which is exactly this project's
/// setting.
///
/// HOVER MODEL: hover is a continuous 0-1 "proximity" value based on
/// screen-space distance from the cursor to the card's STABLE resting
/// position (basePosition — never moves while hovering), not a binary
/// raycast hit/miss on the live, moving card. As you approach, the card
/// eases upward smoothly; only exact clicks still need a precise collider
/// raycast (handled separately, just to know which card was clicked).
/// Using the stable base position instead of the live transform is what
/// prevents the old feedback-loop jiggle — the reference point the
/// proximity is measured against never moves, so there's nothing for the
/// motion to destabilize.
///
/// This handles:
/// - proximity-based hover rise + tilt + scale pop
/// - click selection
/// - optional dragging across a collider surface, like your table
/// - idle sway + hover/select punch feel (ported from CardVisual.cs)
///
/// This does NOT replace your old Card script.
/// It uses Card3DView to toggle the existing source Card selected state.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Card3DInteractable : MonoBehaviour
{
    [Header("Hover (proximity-based — rises smoothly as cursor approaches)")]
    [SerializeField] private float hoverLift = 0.000625f;
    [SerializeField] private float selectedLift = 0.06f;
    [SerializeField] private float hoverTiltX = -1.5f;
    [Tooltip("Screen-space radius (pixels) around the card's resting position where proximity hover starts kicking in.")]
    [SerializeField] private float proximityRadius = 220f;
    [SerializeField] private float proximitySmoothSpeed = 7f;

    [Header("Exact Click Detection")]
    [Tooltip("How far the precise click raycast checks. Should comfortably cover the distance from the camera to the table.")]
    [SerializeField] private float raycastMaxDistance = 100f;
    [SerializeField] private LayerMask raycastMask = ~0;

    [Header("Dynamic Hover Tilt (ported from CardVisual — card leans toward your cursor)")]
    [SerializeField] private bool enableDynamicHoverTilt = true;
    [SerializeField] private float dynamicTiltAmount = 3f;
    [SerializeField] private float dynamicTiltSmoothSpeed = 10f;

    [Header("Scale Pop (ported from CardVisual)")]
    [Tooltip("Card scales up slightly on hover/select, same feel as the old 2D CardVisual. Dragging does NOT use this — see Drag Start Punch below instead.")]
    [SerializeField] private bool enableScalePop = true;
    [SerializeField] private float scaleOnHover = 1.03f;
    [SerializeField] private float scaleOnSelect = 1.08f;
    [SerializeField] private float scaleTransitionSpeed = 12f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 9f;
    [SerializeField] private float rotateSpeed = 9f;

    [Header("Drag")]
    [SerializeField] private bool allowDragging = true;
    [Tooltip("World-space height (Y) of the flat plane cards drag along. Should match your table's surface height. Using a fixed math plane instead of raycasting against real scene colliders means dragging can't accidentally snap to a wall, another card, or anything else in the way.")]
    [SerializeField] private float dragPlaneHeight = 0.75f;
    [SerializeField] private float dragHeightOffset = 0.03f;
    [SerializeField] private float clickTimeThreshold = 0.2f;
    [SerializeField] private float dragDistanceThreshold = 0.015f;
    [Tooltip("Hard cap (meters) on how far a card can travel from its resting slot while dragging. A mouse-ray-vs-plane intersection becomes wildly sensitive near grazing angles (looking toward the horizon) — this stops that from sending the card flying arbitrarily far.")]
    [SerializeField] private float maxDragDistance = 0.4f;
    [Tooltip("One-time 'impact' shake fired the instant a drag begins — NOT a continuous effect. Small scale punch amount (e.g. 0.06 = brief 6% pop that immediately settles back).")]
    [SerializeField] private float dragStartPunchAmount = 0.06f;
    [SerializeField] private float dragStartPunchAngle = 6f;
    [SerializeField] private float dragStartPunchDuration = 0.18f;

    [Header("Idle Sway (ported from CardVisual)")]
    [Tooltip("Enable the subtle continuous breathing/sway motion cards have at rest.")]
    [SerializeField] private bool enableIdleSway = true;
    [SerializeField] private float idleSwayAmount = 0.003f;
    [SerializeField] private float idleTiltAmount = 0.7f;
    [Tooltip("Set by Card3DTableSpawner so each card's sway is offset in time (avoids every card swaying in sync).")]
    [SerializeField] private int handIndex = 0;

    [Header("Hover/Select Punch (ported from CardVisual)")]
    [SerializeField] private float hoverPunchAngle = 4f;
    [SerializeField] private float hoverPunchDuration = 0.15f;
    [Tooltip("Small combined position shake on top of the rotation punch when hover starts — makes it read as an actual little 'shake' rather than just a tilt.")]
    [SerializeField] private float hoverShakeAmount = 0.01f;
    [SerializeField] private int hoverShakeVibrato = 14;
    [SerializeField] private float selectPunchAmount = 0.015f;
    [SerializeField] private float selectPunchDuration = 0.15f;

    [Header("Drag Lean (ported from CardVisual.FollowRotation)")]
    [Tooltip("While dragging, the card tilts based on how fast/which direction you're moving it — gives it a bit of weight.")]
    [SerializeField] private bool enableDragLean = true;
    [SerializeField] private float dragLeanAmount = 8f;
    [SerializeField] private float dragLeanSmoothSpeed = 15f;
    [SerializeField] private float dragLeanMaxAngle = 35f;

    [Header("Swap Punch (ported from CardVisual.Swap)")]
    [SerializeField] private float swapRotationAngle = 25f;
    [SerializeField] private float swapPunchDuration = 0.15f;
    [SerializeField] private int swapVibrato = 5;

    private Camera mainCamera;
    private Card3DView cardView;

    /// <summary>
    /// A STATIC, never-animated trigger collider (created and positioned by
    /// HorizontalCardHolder) used for all hover/click hit-testing instead
    /// of this card's own collider — which moves every frame from rise,
    /// nudge, tilt, and sway. Raycasting against a fixed anchor means the
    /// hit result only changes when a real re-fan happens (deal/reorder),
    /// never from animation, which is what eliminates the overlap-boundary
    /// flicker entirely.
    /// </summary>
    private Collider _hoverAnchor;

    private Vector3 basePosition;
    private Quaternion baseRotation;
    private Vector3 baseScale = Vector3.one;
    private Vector3 _dynamicTiltCurrent = Vector3.zero;
    private Vector3 _prevFramePosition;
    private float _dragLeanCurrent = 0f;

    private Vector3 targetPosition;
    private Quaternion targetRotation;

    /// <summary>Smoothed 0-1 proximity value — the real driver of hover visuals now.</summary>
    private float _hoverIntensity = 0f;

    private bool isSelected;
    private bool isDragging;

    // Tracks whether the mouse button went down while this specific card
    // was the exact one under the cursor — needed since drag can move the
    // cursor off this card while the button stays held.
    private bool _pointerDownOnThisCard;
    private float pointerDownTime;
    private Vector3 pointerDownWorldPosition;
    private Vector3 dragOffset;

    /// <summary>Whether the cursor's exact raycast is directly over this card's own collider right now — used for the hover sound/punch instead of the smooth proximity value, so it fires the instant you're anywhere over the card, not just near-perfectly centered.</summary>
    private bool _exactHover;

    /// <summary>Combined position offset (lift + forward-push + sideways nudge) applied by HorizontalCardHolder's hover-wave system — see SetWaveEffect.</summary>
    private Vector3 _waveOffset;
    /// <summary>Extra scale multiplier contribution from the hover-wave system, added on top of the normal hover/select scale.</summary>
    private float _waveScaleBoost;

    private void Awake()
    {
        mainCamera = Camera.main;
        cardView = GetComponent<Card3DView>();

        basePosition = transform.position;
        baseRotation = transform.rotation;
        baseScale = transform.localScale;
        _prevFramePosition = transform.position;

        targetPosition = basePosition;
        targetRotation = baseRotation;
    }

    private void Update()
    {
        UpdateProximityHover();
        HandleExactClickDetection();

        if (isDragging && allowDragging)
        {
            UpdateDragTarget();
        }
        else
        {
            RefreshPose();
        }

        Vector3 swayPos = Vector3.zero;
        Quaternion swayRot = Quaternion.identity;

        if (enableIdleSway && !isDragging)
        {
            float sine   = Mathf.Sin(Time.time + handIndex);
            float cosine = Mathf.Cos(Time.time + handIndex);
            float damp   = Mathf.Lerp(1f, 0.2f, _hoverIntensity); // calm down as hover intensifies

            swayPos = new Vector3(0f, sine * idleSwayAmount * damp, 0f);
            swayRot = Quaternion.Euler(sine * idleTiltAmount * damp, cosine * idleTiltAmount * damp, 0f);
        }

        Vector3 targetDynamicTilt = Vector3.zero;
        if (enableDynamicHoverTilt && _hoverIntensity > 0.01f && mainCamera != null && Mouse.current != null)
        {
            Vector3 screenPos = mainCamera.WorldToScreenPoint(basePosition);
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Vector3 offset = (Vector3)mousePos - screenPos;
            float nx = Mathf.Clamp(offset.x / (Screen.width * 0.5f), -1f, 1f);
            float ny = Mathf.Clamp(offset.y / (Screen.height * 0.5f), -1f, 1f);
            targetDynamicTilt = new Vector3(-ny * dynamicTiltAmount, 0f, nx * dynamicTiltAmount) * _hoverIntensity;
        }
        _dynamicTiltCurrent = Vector3.Lerp(_dynamicTiltCurrent, targetDynamicTilt, Time.deltaTime * dynamicTiltSmoothSpeed);
        Quaternion dynamicTiltRot = Quaternion.Euler(_dynamicTiltCurrent);

        float targetDragLean = 0f;
        if (enableDragLean && isDragging)
        {
            Vector3 frameMovement = transform.position - _prevFramePosition;
            targetDragLean = Mathf.Clamp(frameMovement.x * dragLeanAmount * 100f, -dragLeanMaxAngle, dragLeanMaxAngle);
        }
        _dragLeanCurrent = Mathf.LerpAngle(_dragLeanCurrent, targetDragLean, Time.deltaTime * dragLeanSmoothSpeed);
        Quaternion dragLeanRot = Quaternion.Euler(0f, 0f, _dragLeanCurrent);

        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition + swayPos,
            Time.deltaTime * moveSpeed
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation * swayRot * dynamicTiltRot * dragLeanRot,
            Time.deltaTime * rotateSpeed
        );

        if (enableScalePop)
        {
            // No continuous scale-up while dragging — that was the source of
            // the "rising toward camera" feeling. Dragging now only gets the
            // one-time punch fired in BeginDrag(); ongoing scale is driven
            // purely by select/hover state, same as when not dragging.
            float hoverScale = Mathf.Lerp(1f, scaleOnHover, _hoverIntensity);
            float scaleMult = (isSelected ? scaleOnSelect : hoverScale) + _waveScaleBoost;
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                baseScale * scaleMult,
                Time.deltaTime * scaleTransitionSpeed
            );
        }

        _prevFramePosition = transform.position;
    }

    // ── Proximity hover (replaces the old binary raycast hover) ─────────────

    private void UpdateProximityHover()
    {
        float targetIntensity = 0f;

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera != null && Mouse.current != null && !isDragging)
        {
            // Project the STABLE resting position, not the live (moving)
            // transform — this is the key fix. Projecting the live
            // position would make the proximity value shrink as the card
            // rose toward the cursor, then grow again as it eased back,
            // causing an oscillating feedback loop (the "jiggle").
            Vector3 screenPos = mainCamera.WorldToScreenPoint(basePosition);
            if (screenPos.z > 0f) // only if the point is in front of the camera
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                float dist = Vector2.Distance(mousePos, new Vector2(screenPos.x, screenPos.y));
                float linear = Mathf.Clamp01(1f - dist / proximityRadius);
                // Smoothstep instead of linear falloff — eases in gently near
                // the outer edge of the radius instead of ramping up abruptly.
                targetIntensity = linear * linear * (3f - 2f * linear);
            }
        }
        else if (isDragging)
        {
            targetIntensity = 1f;
        }

        _hoverIntensity = Mathf.Lerp(_hoverIntensity, targetIntensity, Time.deltaTime * proximitySmoothSpeed);
    }

    // ── Exact click detection (separate from the smooth proximity hover) ────

    private void HandleExactClickDetection()
    {
        bool hitThisCard = false;

        if (mainCamera != null && Mouse.current != null)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePos);

            if (Physics.Raycast(ray, out RaycastHit hit, raycastMaxDistance, raycastMask, QueryTriggerInteraction.Collide))
            {
                hitThisCard = _hoverAnchor != null && hit.collider == _hoverAnchor;
            }
        }

        // Hover sound/punch fires the instant the cursor is anywhere over
        // this card's actual collider — not tied to the smooth proximity
        // value, which only reaches its "fully hovered" threshold near the
        // card's exact center. This is a plain edge-trigger on the real hit
        // test, so it plays as soon as you're on the card at all.
        // Hover just plays the sound now — no shake here anymore (moved to
        // selection instead, see ToggleSelected()).
        if (hitThisCard && !_exactHover)
        {
            AudioManager.Instance?.Play("card_hover");
        }
        _exactHover = hitThisCard;

        var mouse = Mouse.current;
        if (mouse == null) return;

        if (hitThisCard && mouse.leftButton.wasPressedThisFrame && !_pointerDownOnThisCard)
        {
            _pointerDownOnThisCard = true;
            OnPointerDown();
        }

        if (_pointerDownOnThisCard && allowDragging && mouse.leftButton.isPressed && !isDragging)
        {
            // Only actual mouse MOVEMENT should start a drag — holding the
            // button still should never by itself trigger dragging.
            float movedDistance = Vector3.Distance(transform.position, pointerDownWorldPosition);
            if (movedDistance > dragDistanceThreshold)
                BeginDrag();
        }

        if (_pointerDownOnThisCard && mouse.leftButton.wasReleasedThisFrame)
        {
            OnPointerUp();
            _pointerDownOnThisCard = false;
        }
    }

    private void OnPointerDown()
    {
        pointerDownTime = Time.time;
        pointerDownWorldPosition = transform.position;

        if (TryGetDragPlanePoint(out Vector3 hitPoint))
            dragOffset = transform.position - hitPoint;
        else
            dragOffset = Vector3.zero;
    }

    /// <summary>
    /// Fires once, the exact frame a drag actually begins — a quick one-shot
    /// "impact" shake, not a continuous effect. Dragging itself applies NO
    /// ongoing scale-up or extra lift beyond this single punch; the card
    /// just tracks the drag plane at its normal size from here on.
    /// </summary>
    private void BeginDrag()
    {
        isDragging = true;

        transform.DOKill(false);
        transform.DOPunchScale(Vector3.one * dragStartPunchAmount, dragStartPunchDuration, 8, 0.6f);
        transform.DOPunchRotation(Vector3.forward * dragStartPunchAngle, dragStartPunchDuration, 8, 0.6f);
    }

    private void OnPointerUp()
    {
        bool wasClick = Time.time - pointerDownTime <= clickTimeThreshold && !isDragging;

        if (wasClick)
            ToggleSelected();

        if (isDragging)
        {
            isDragging = false;
            basePosition = transform.position;
        }
    }

    private void ToggleSelected()
    {
        isSelected = !isSelected;

        if (cardView != null)
            cardView.SetSourceSelected(isSelected);

        // Shake on select/deselect — moved here from hover. Combined into a
        // single position punch (upward lift + multi-axis rattle merged
        // into one vector, since two separate DOPunchPosition calls on the
        // same property would fight each other) plus a separate rotation
        // punch (different property, safe to run alongside).
        transform.DOKill(false);
        Vector3 upDir = isSelected ? Vector3.up * selectPunchAmount : Vector3.zero;
        Vector3 shake = new Vector3(hoverShakeAmount, hoverShakeAmount * 0.6f, hoverShakeAmount * 0.4f);
        transform.DOPunchPosition(upDir + shake, selectPunchDuration, hoverShakeVibrato, 0.9f);
        transform.DOPunchRotation(Vector3.forward * hoverPunchAngle, hoverPunchDuration, 10, 1f);
    }

    private void RefreshPose()
    {
        float lift = Mathf.Lerp(0f, hoverLift, _hoverIntensity);
        if (isSelected)
            lift = Mathf.Max(lift, selectedLift);

        targetPosition = basePosition + Vector3.up * lift + _waveOffset;

        float tiltAmount = Mathf.Lerp(0f, hoverTiltX, _hoverIntensity);
        targetRotation = baseRotation * Quaternion.Euler(tiltAmount, 0f, 0f);
    }

    private void UpdateDragTarget()
    {
        if (!TryGetDragPlanePoint(out Vector3 hitPoint))
            return;

        Vector3 rawTarget = hitPoint + dragOffset + Vector3.up * dragHeightOffset;

        // Hard-clamp displacement from the resting slot — this is what
        // actually fixes "flies through the wall": no matter how extreme
        // the raw plane math gets near grazing angles, the card can never
        // travel further than maxDragDistance from where it started.
        Vector3 clampedOffset = Vector3.ClampMagnitude(rawTarget - basePosition, maxDragDistance);

        targetPosition = basePosition + clampedOffset;
        targetRotation = baseRotation;
    }

    /// <summary>
    /// Where the cursor ray crosses a fixed horizontal math plane at
    /// dragPlaneHeight — NOT a physics raycast against real scene
    /// colliders. This is what keeps dragging on one uniform, predictable
    /// surface: a physics raycast could hit a wall, another card, or a gap
    /// in the table depending on exactly where the cursor points, causing
    /// the card to jump wildly. A plane can't be "missed" or hit the wrong
    /// thing — it's infinite and always at the same height.
    /// </summary>
    private bool TryGetDragPlanePoint(out Vector3 worldPoint)
    {
        worldPoint = Vector3.zero;

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null || Mouse.current == null)
            return false;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);

        Plane dragPlane = new Plane(Vector3.up, new Vector3(0f, dragPlaneHeight, 0f));

        if (dragPlane.Raycast(ray, out float enter))
        {
            worldPoint = ray.GetPoint(enter);
            return true;
        }

        return false;
    }

    public void SetBasePose(Vector3 position, Quaternion rotation)
    {
        basePosition = position;
        baseRotation = rotation;
        RefreshPose();
    }

    public void SetSelectedVisual(bool selected)
    {
        isSelected = selected;
        RefreshPose();
    }

    /// <summary>Forces this card to deselected state (e.g. right-click "deselect all"), keeping cardView in sync.</summary>
    public void Deselect()
    {
        if (!isSelected) return;
        isSelected = false;
        if (cardView != null)
            cardView.SetSourceSelected(false);
        RefreshPose();
    }

    /// <summary>Called by Card3DTableSpawner so each card's idle sway is staggered in time.</summary>
    public void SetHandIndex(int index)
    {
        handIndex = index;
    }

    /// <summary>Punch-rotation flourish when this card gets displaced during a hand reorder, ported from CardVisual.Swap(). dir = -1 or 1 for which way it's being pushed.</summary>
    public void PunchSwap(float dir = 1f)
    {
        transform.DOKill(false);
        transform.DOPunchRotation(Vector3.forward * swapRotationAngle * dir, swapPunchDuration, swapVibrato, 1f);
    }

    /// <summary>Called once by HorizontalCardHolder right after creating this card's static hover anchor — see the _hoverAnchor field comment for why hit-testing uses a separate, non-animated collider instead of this card's own.</summary>
    public void SetHoverAnchor(Collider anchorCollider)
    {
        _hoverAnchor = anchorCollider;
    }

    /// <summary>True while the player is actively dragging this card.</summary>
    public bool IsDragging => isDragging;

    /// <summary>True while the cursor's exact raycast is directly over this card — used by HorizontalCardHolder to detect which card should push its neighbors aside.</summary>
    public bool IsHoveredExact => _exactHover;

    /// <summary>Smoothed 0-1 proximity value — stable frame-to-frame (unlike a raw raycast hit test, which can flicker between two overlapping cards right at their boundary). Used by HorizontalCardHolder to pick the neighbor-nudge target smoothly.</summary>
    public float HoverIntensity => _hoverIntensity;

    /// <summary>Called by HorizontalCardHolder's hover-wave system every frame — combined lift/forward/sideways position offset, plus an extra scale multiplier, both tapering with distance from whichever card is closest to the cursor.</summary>
    public void SetWaveEffect(Vector3 positionOffset, float scaleBoost)
    {
        _waveOffset = positionOffset;
        _waveScaleBoost = scaleBoost;
    }
}