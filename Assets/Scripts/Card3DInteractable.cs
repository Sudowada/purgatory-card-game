using DG.Tweening;

using System;

using System.Collections.Generic;

using UnityEngine;

using UnityEngine.InputSystem;

public enum CardPhysicalState

{

    InHand,

    Dragging,

    Offered,

    Sacrificing

}

public class Card3DInteractable : MonoBehaviour

{

    [Header("References")]

    [SerializeField] private Transform visualRoot;

    [SerializeField] private Camera interactionCamera;

    [SerializeField] private HorizontalCardHolder horizontalHandHolder;

    [SerializeField] private CardOfferingController offeringController;

    [Tooltip("Legacy/unused with HorizontalCardHolder. Kept so HandHolder3D.cs can remain in the project.")]

    [SerializeField] private HandHolder3D handHolder;

    [Header("Hand Root Motion")]

    [SerializeField] private float handMoveSpeed = 28f;

    [SerializeField] private float handRotateSpeed = 28f;

    [Header("Stable Hover Detection")]

    [SerializeField] private float hoverScreenRadius = 120f;

    [SerializeField] private float hoverIntensitySpeed = 14f;

    [Header("Idle - Safe Vertical Bob Only")]

    [SerializeField] private bool enableIdle = true;

    [Tooltip("Tiny bob along the table normal. No idle tilt/sideways movement, so cards cannot idle into their neighbors.")]

    [SerializeField] private float idleLiftAmount = 0.0008f;

    [SerializeField] private float idleDurationMin = 1.8f;

    [SerializeField] private float idleDurationMax = 2.6f;

    [SerializeField] private float idleStartDelayMin = 0f;

    [SerializeField] private float idleStartDelayMax = 1.2f;

    [Header("Primary Hover - Visual Root Only")]

    [SerializeField] private Vector3 hoverLocalOffset = new Vector3(0f, 0.008f, 0f);

    [SerializeField] private Vector3 hoverLocalRotation = new Vector3(-6f, 0f, 0f);

    [SerializeField] private float hoverInDuration = 0.06f;

    [SerializeField] private float hoverOutDuration = 0.08f;

    [SerializeField] private Ease hoverInEase = Ease.OutCubic;

    [SerializeField] private Ease hoverOutEase = Ease.OutQuad;

    [Header("Hover Audio")]

    [SerializeField] private AudioSource hoverAudioSource;

    [SerializeField] private AudioClip hoverSound;

    [Range(0f, 1f)]

    [SerializeField] private float hoverVolume = 0.7f;

    [SerializeField] private float minHoverPitch = 0.97f;

    [SerializeField] private float maxHoverPitch = 1.03f;

    [Header("Hover Brightness")]

    [Tooltip("Optional. Assign the card face/base renderers here. If left empty, renderers under Visual Root are found automatically.")]

    [SerializeField] private Renderer[] hoverBrightnessRenderers;

    [Tooltip("Subtle albedo boost while hovered. 1.0 = unchanged.")]

    [SerializeField] private float hoverBrightnessMultiplier = 1.12f;

    [Tooltip("Small emission contribution so the hovered card stays readable even when it moves away from the room light.")]

    [SerializeField] private float hoverEmissionStrength = 0.12f;

    [SerializeField] private float hoverBrightnessSpeed = 18f;

    [Header("Drag")]

    [Tooltip("A short click sends this hand card to the altar. Dragging still works.")]
    [SerializeField] private bool clickToOffer = true;

    [Tooltip("Legacy selection behavior, used only when Click To Offer is disabled.")]
    [SerializeField] private bool allowClickSelection = true;

    [SerializeField] private float clickTimeThreshold = 0.18f;

    [SerializeField] private float dragPixelThreshold = 6f;

    [SerializeField] private float dragLift = 0.012f;

    [Tooltip("During hand reordering, keep the card on a camera-facing plane and pull it slightly toward the camera. This prevents the ray from shooting through the back wall.")]

    [SerializeField] private float dragTowardCamera = 0.03f;

    [Header("Hand Drag Bounds")]

    [Tooltip("Prevents hand cards from being dragged completely off-screen while still leaving plenty of room to reorder and reach the altar.")]

    [SerializeField] private bool limitHandDragToViewport = true;

    [Tooltip("Normalized viewport rectangle used only while dragging in the hand. X = left/right, Y = bottom/top.")]

    [SerializeField] private Vector2 handDragViewportMin = new Vector2(0.08f, 0.16f);

    [SerializeField] private Vector2 handDragViewportMax = new Vector2(0.92f, 0.84f);

    [Header("Velocity Drag Lean")]

    [Tooltip("Re-enables the old physical-card feel: faster mouse movement produces more visual tilt, and slowing down lets the card settle back toward flat.")]

    [SerializeField] private bool enableDragLean = true;

    [Tooltip("Maximum side-to-side roll caused by horizontal drag speed.")]

    [SerializeField] private float dragLeanMaxRoll = 18f;

    [Tooltip("Maximum forward/back pitch caused by vertical drag speed.")]

    [SerializeField] private float dragLeanMaxPitch = 7f;

    [Tooltip("Mouse speed in pixels/second that reaches the maximum tilt.")]

    [SerializeField] private float dragLeanPixelsPerSecondForMax = 1200f;

    [Tooltip("Legacy smoothing value kept for Inspector compatibility. The new spring wobble below is now used for the actual drag motion.")]

    [SerializeField] private float dragLeanSmoothSpeed = 18f;

    [Header("Picked-Up Hanging Pose")]

    [Tooltip("While held, use the hand's neutral/center card rotation instead of the card's fanned slot rotation. This makes the held card hang straight without a camera-axis 90-degree flip.")]

    [SerializeField] private bool straightenCardWhileDragging = true;

    [Tooltip("Optional fine-tuning applied after the neutral hand rotation. Leave at zero unless you deliberately want a different hanging angle.")]

    [SerializeField] private Vector3 dragHangEulerOffset = Vector3.zero;

    [Tooltip("Small drop below the cursor, helping the card feel like it is hanging from the point where you picked it up.")]

    [SerializeField] private float dragHangDownOffset = 0.025f;

    [Tooltip("How quickly the root rotates from its fanned table pose into the straight held pose.")]

    [SerializeField] private float dragHangRotateSpeed = 24f;

    [Header("Drag Swing / Wobble")]

    [Tooltip("Spring strength pulling the card back toward its velocity-driven hanging angle.")]

    [SerializeField] private float dragSwingSpring = 95f;

    [Tooltip("Lower values wobble longer; higher values settle faster. Around 10-13 gives a short physical swing after a sudden stop.")]

    [SerializeField] private float dragSwingDamping = 11f;

    [Tooltip("Extra headroom for the spring overshoot before angles are clamped.")]

    [SerializeField] private float dragSwingOvershootLimit = 1.35f;

    [Header("Snappy Altar Flight")]

    [Tooltip("Tiny anticipation pop before the card rockets toward the altar.")]

    [SerializeField] private float altarAnticipationDuration = 0.035f;

    [SerializeField] private float altarAnticipationTowardCamera = 0.018f;

    [SerializeField] private float altarAnticipationScale = 1.045f;

    [Tooltip("Main travel time from the hand to the altar preview. Keep short for the snappy/keyframed feel.")]

    [SerializeField] private float altarFlightDuration = 0.13f;

    [Tooltip("OutBack overshoot. Around 1.1-1.35 gives a small stylized overshoot without feeling rubbery.")]

    [SerializeField] private float altarFlightOvershoot = 1.22f;

    [Tooltip("Visual scale while the card is hovering at the altar preview.")]

    [SerializeField] private float altarPreviewScale = 1.025f;

    [Tooltip("How quickly a card snaps back to the near-camera hand plane if you drag it back out of the altar region.")]

    [SerializeField] private float altarReturnToHandSpeed = 24f;

    [Header("Offering Burn")]

    [Tooltip("Brief hot orange/red flash while the real card burns away in the altar recess.")]

    [SerializeField] private Color offeringBurnColor = new Color(1f, 0.22f, 0.04f, 1f);

    [SerializeField] private float offeringBurnEmissionStrength = 2.0f;

    [SerializeField] private float offeringBurnScalePop = 1.05f;

    [Header("Swap Punch")]

    [SerializeField] private float swapPunchDistance = 0.012f;

    [SerializeField] private float swapPunchDuration = 0.11f;

    private Card3DView cardView;

    private Vector3 basePosition;

    private Quaternion baseRotation;

    private bool hasBasePose;

    private Vector3 targetWaveWorldOffset;

    private Vector3 currentWaveWorldOffset;

    private float targetWaveScaleBoost;

    private float currentWaveScaleBoost;

    private Vector3 visualBaseLocalPosition;

    private Quaternion visualBaseLocalRotation;

    private Vector3 visualBaseLocalScale;

    private Tween idleMoveTween;

    private Sequence visualTween;

    private Collider hoverAnchor;

    private int handIndex = -1;

    private float hoverIntensity;

    private bool isPrimaryHover;

    private bool pointerDown;

    private float pointerDownTime;

    private Vector2 pointerDownScreenPosition;

    private Vector3 dragGrabOffset;

    private bool isDraggingOverOffering;

    private bool isReturningFromOffering;

    private Vector2 lastDragScreenPosition;

    private bool hasLastDragScreenPosition;

    private Vector2 currentDragLean;

    private Vector2 dragLeanAngularVelocity;

    private Sequence altarFlightTween;

    private bool isBurningVisual;

    private sealed class HoverRendererState

    {

        public Renderer renderer;

        public MaterialPropertyBlock block;

        public Color baseColor = Color.white;

        public Color baseEmission = Color.black;

        public bool hasBaseColor;

        public bool hasColor;

        public bool hasEmission;

    }

    private readonly List<HoverRendererState> hoverRendererStates =

        new List<HoverRendererState>();

    private float hoverBrightness01;

    public CardPhysicalState PhysicalState { get; private set; } = CardPhysicalState.InHand;

    public bool IsDragging => PhysicalState == CardPhysicalState.Dragging;

    public bool IsDraggingInHand =>

        PhysicalState == CardPhysicalState.Dragging &&

        !isDraggingOverOffering;

    public bool IsOffered => PhysicalState == CardPhysicalState.Offered;

    public bool IsSelected => SourceCard != null && SourceCard.selected;

    public float HoverIntensity => hoverIntensity;

    public int HandIndex => handIndex;

    public Card3DView CardView => cardView;

    public Transform VisualRoot => visualRoot;

    public Card SourceCard => cardView != null ? cardView.SourceCard : null;

    public HorizontalCardHolder HorizontalHandHolder => horizontalHandHolder;

    public HandHolder3D HandHolder => handHolder;

    private void Awake()

    {

        cardView = GetComponent<Card3DView>();

        if (interactionCamera == null)

            interactionCamera = Camera.main;

        if (visualRoot == null)

        {

            Transform found = transform.Find("Card");

            if (found != null)

                visualRoot = found;

        }

        if (hoverAudioSource == null)

            hoverAudioSource = GetComponent<AudioSource>();

        // Make hover audio self-contained. If the prefab does not already have

        // an AudioSource, create one automatically.

        if (hoverAudioSource == null)

            hoverAudioSource = gameObject.AddComponent<AudioSource>();

        hoverAudioSource.playOnAwake = false;

        hoverAudioSource.loop = false;

        hoverAudioSource.spatialBlend = 0f; // UI-like card riffle: always audible

        // Also allow the clip to be assigned directly on the AudioSource.

        if (hoverSound == null && hoverAudioSource.clip != null)

            hoverSound = hoverAudioSource.clip;

        if (visualRoot != null)

        {

            visualBaseLocalPosition = visualRoot.localPosition;

            visualBaseLocalRotation = visualRoot.localRotation;

            visualBaseLocalScale = visualRoot.localScale;

        }

        InitializeHoverBrightness();

        basePosition = transform.position;

        baseRotation = transform.rotation;

        hasBasePose = true;

    }

    private void Start()

    {

        if (horizontalHandHolder == null)

            horizontalHandHolder = GetComponentInParent<HorizontalCardHolder>();

        if (offeringController == null)

            offeringController = FindFirstObjectByType<CardOfferingController>();

        if (horizontalHandHolder == null)

        {

            if (handHolder == null)

                handHolder = FindFirstObjectByType<HandHolder3D>();

            if (handHolder != null)

                handHolder.RegisterCard(this);

        }

        StartIdle();

    }

    private void OnDestroy()

    {

        KillVisualTweens();

        KillAltarFlightTween();

        transform.DOKill(false);

        if (horizontalHandHolder == null && handHolder != null)

            handHolder.UnregisterCard(this);

    }

    private void Update()

    {

        UpdateHoverIntensity();

        HandlePointerInput();

        UpdateHandRootPose();

        UpdateWaveScale();

        UpdateHoverBrightness();

    }

    public void SetBasePose(Vector3 position, Quaternion rotation)

    {

        basePosition = position;

        baseRotation = rotation;

        hasBasePose = true;

    }

    public void SetHandIndex(int index)

    {

        handIndex = index;

    }

    public void SetHoverAnchor(Collider anchor)

    {

        hoverAnchor = anchor;

    }

    public void SetWaveEffect(Vector3 worldOffset, float scaleBoost)

    {

        if (PhysicalState != CardPhysicalState.InHand &&

            PhysicalState != CardPhysicalState.Dragging)

        {

            targetWaveWorldOffset = Vector3.zero;

            targetWaveScaleBoost = 0f;

            return;

        }

        targetWaveWorldOffset = IsDragging ? Vector3.zero : worldOffset;

        targetWaveScaleBoost = IsDragging ? 0f : scaleBoost;

    }

    public void SetPrimaryHover(bool active)

    {

        if (PhysicalState != CardPhysicalState.InHand)

            active = false;

        if (isPrimaryHover == active)

            return;

        isPrimaryHover = active;

        if (isPrimaryHover)

        {

            PlayHoverSound();

            AnimateHoverIn();

        }

        else if (!pointerDown && !IsDragging)

        {

            AnimateHoverOut();

        }

    }

    public void Deselect()

    {

        if (cardView != null)

            cardView.SetSourceSelected(false);

    }

    public void PunchSwap(float direction)

    {

        if (visualRoot == null || PhysicalState != CardPhysicalState.InHand)

            return;

        KillVisualTweens();

        Vector3 start = visualBaseLocalPosition;

        Vector3 side = Vector3.right * (swapPunchDistance * Mathf.Sign(direction));

        visualTween = DOTween.Sequence();

        visualTween.Append(

            visualRoot.DOLocalMove(start + side, swapPunchDuration * 0.35f)

                .SetEase(Ease.OutQuad)

        );

        visualTween.Append(

            visualRoot.DOLocalMove(start, swapPunchDuration * 0.65f)

                .SetEase(Ease.OutBack)

        );

        visualTween.OnComplete(() =>

        {

            if (isPrimaryHover)

                AnimateHoverIn();

            else

                StartIdle();

        });

    }

    private void UpdateHoverIntensity()

    {

        float target = 0f;

        if (PhysicalState == CardPhysicalState.InHand && hoverAnchor != null)

        {

            if (interactionCamera == null)

                interactionCamera = Camera.main;

            Mouse mouse = Mouse.current;

            if (interactionCamera != null && mouse != null)

            {

                Vector3 screen =

                    interactionCamera.WorldToScreenPoint(hoverAnchor.bounds.center);

                if (screen.z > 0f)

                {

                    Vector2 mousePos = mouse.position.ReadValue();

                    float distance = Vector2.Distance(mousePos, (Vector2)screen);

                    target = Mathf.Clamp01(

                        1f - distance / Mathf.Max(hoverScreenRadius, 1f)

                    );

                    target = target * target * (3f - 2f * target);

                }

            }

        }

        hoverIntensity = Mathf.MoveTowards(

            hoverIntensity,

            target,

            hoverIntensitySpeed * Time.deltaTime

        );

    }

    private void HandlePointerInput()

    {

        Mouse mouse = Mouse.current;

        if (mouse == null)
            return;

        if (GameManager.Instance != null && !GameManager.Instance.CanPlayOffering)
        {
            // Clear interrupted presses so reopening card mode cannot offer a card.
            pointerDown = false;
            return;
        }

        // D discards only the hovered healthy card; replacement is synchronous.
        if (PhysicalState == CardPhysicalState.InHand && isPrimaryHover && !pointerDown &&
            Keyboard.current != null && Keyboard.current.dKey.wasPressedThisFrame)
        {
            GameManager.Instance?.TryDiscardCard(SourceCard);
            return;
        }

        if (PhysicalState == CardPhysicalState.InHand &&

            isPrimaryHover &&

            mouse.leftButton.wasPressedThisFrame)

        {

            pointerDown = true;

            pointerDownTime = Time.time;

            pointerDownScreenPosition = mouse.position.ReadValue();

            if (TryGetDragPlanePoint(pointerDownScreenPosition, out Vector3 dragPoint))

                dragGrabOffset = transform.position - dragPoint;

            else

                dragGrabOffset = Vector3.zero;

        }

        if (!pointerDown)

            return;

        if (mouse.leftButton.isPressed)

        {

            if (PhysicalState == CardPhysicalState.InHand)

            {

                float pixels =

                    Vector2.Distance(

                        pointerDownScreenPosition,

                        mouse.position.ReadValue()

                    );

                if (pixels >= dragPixelThreshold)

                    BeginDrag();

            }

            if (PhysicalState == CardPhysicalState.Dragging)

                UpdateDrag(mouse.position.ReadValue());

        }

        if (mouse.leftButton.wasReleasedThisFrame)

        {

            pointerDown = false;

            if (PhysicalState == CardPhysicalState.Dragging)

            {

                FinishDrag();

                return;

            }

            bool wasClick = PhysicalState == CardPhysicalState.InHand &&
                isPrimaryHover &&
                Time.time - pointerDownTime <= clickTimeThreshold &&
                Vector2.Distance(pointerDownScreenPosition, mouse.position.ReadValue()) < dragPixelThreshold;

            if (wasClick && clickToOffer)
            {
                if (TryOfferToAltar()) return;
                // A full or resolving altar rejects the click without selecting the card.
            }
            else if (wasClick && allowClickSelection && cardView != null)
                cardView.ToggleSourceSelected();

            if (isPrimaryHover)

                AnimateHoverIn();

            else

                AnimateHoverOut();

        }

    }

    private void BeginDrag()

    {

        PhysicalState = CardPhysicalState.Dragging;

        isDraggingOverOffering = false;

        isReturningFromOffering = false;

        lastDragScreenPosition =

            Mouse.current != null

                ? Mouse.current.position.ReadValue()

                : pointerDownScreenPosition;

        hasLastDragScreenPosition = true;

        currentDragLean = Vector2.zero;

        dragLeanAngularVelocity = Vector2.zero;

        KillVisualTweens();

        KillAltarFlightTween();

        ResetVisualImmediate();

        targetWaveWorldOffset = Vector3.zero;

        currentWaveWorldOffset = Vector3.zero;

        targetWaveScaleBoost = 0f;

        currentWaveScaleBoost = 0f;

        transform.DOKill(false);

        if (horizontalHandHolder == null && handHolder != null)

            handHolder.BeginDrag(this);

    }

    private void UpdateDrag(Vector2 screenPosition)

    {

        if (interactionCamera == null)

            interactionCamera = Camera.main;

        bool overOffering = false;

        Vector3 altarPosition = Vector3.zero;

        Quaternion altarRotation = Quaternion.identity;

        if (offeringController != null &&

            interactionCamera != null)

        {

            overOffering =

                offeringController.TryGetDragPreviewPose(

                    interactionCamera,

                    screenPosition,

                    out altarPosition,

                    out altarRotation

                );

        }

        // ENTER ALTAR REGION:

        // one fast anticipation -> fly -> tiny overshoot/settle.

        if (overOffering)

        {

            if (!isDraggingOverOffering)

            {

                isDraggingOverOffering = true;

                isReturningFromOffering = false;

                ResetDragLeanImmediate();

                BeginSnappyAltarFlight(

                    altarPosition,

                    altarRotation

                );

            }

            // While the mouse remains in altar territory, the card is owned by

            // the altar preview animation instead of following the cursor.

            return;

        }

        // EXIT ALTAR REGION:

        // cancel the altar preview and snap back toward the near-camera hand

        // plane. Once it catches up, normal left/right reordering continues.

        if (isDraggingOverOffering)

        {

            isDraggingOverOffering = false;

            isReturningFromOffering = true;

            KillAltarFlightTween();

            if (visualRoot != null)

            {

                visualRoot.DOKill(false);

                visualRoot.DOScale(

                    visualBaseLocalScale,

                    0.06f

                ).SetEase(Ease.OutCubic);

            }

        }

        Vector2 handDragScreenPosition =

            ClampHandDragScreenPosition(screenPosition);

        UpdateDragLean(handDragScreenPosition);

        if (TryGetDragPlanePoint(

            handDragScreenPosition,

            out Vector3 worldPoint))

        {

            Vector3 up =

                horizontalHandHolder != null

                    ? horizontalHandHolder.transform.up

                    : Vector3.up;

            Vector3 towardCamera =

                interactionCamera != null

                    ? -interactionCamera.transform.forward * dragTowardCamera

                    : Vector3.zero;

            Vector3 hangingDown =

                interactionCamera != null

                    ? -interactionCamera.transform.up * dragHangDownOffset

                    : Vector3.zero;

            Vector3 handDragTarget =

                worldPoint +

                dragGrabOffset +

                up * dragLift +

                towardCamera +

                hangingDown;

            Quaternion handDragRotation = GetHandDragHangRotation();

            if (isReturningFromOffering)

            {

                float returnT =

                    1f - Mathf.Exp(

                        -altarReturnToHandSpeed *

                        Time.deltaTime

                    );

                transform.position =

                    Vector3.Lerp(

                        transform.position,

                        handDragTarget,

                        returnT

                    );

                transform.rotation =

                    Quaternion.Slerp(

                        transform.rotation,

                        handDragRotation,

                        returnT

                    );

                if (Vector3.Distance(

                    transform.position,

                    handDragTarget) < 0.002f)

                {

                    isReturningFromOffering = false;

                }

            }

            else

            {

                transform.position =

                    handDragTarget;

                float hangRotateT =

                    1f - Mathf.Exp(-dragHangRotateSpeed * Time.deltaTime);

                transform.rotation =

                    Quaternion.Slerp(

                        transform.rotation,

                        handDragRotation,

                        hangRotateT

                    );

            }

            if (horizontalHandHolder == null &&

                handHolder != null)

            {

                handHolder.UpdateDraggedCard(this);

            }

        }

    }

    private void BeginSnappyAltarFlight(

        Vector3 targetPosition,

        Quaternion targetRotation)

    {

        KillAltarFlightTween();

        KillVisualTweens();

        Vector3 anticipationPosition =

            transform.position;

        if (interactionCamera != null)

        {

            anticipationPosition +=

                -interactionCamera.transform.forward *

                altarAnticipationTowardCamera;

        }

        altarFlightTween =

            DOTween.Sequence();

        // 1) Tiny anticipation pop toward the player.

        altarFlightTween.Append(

            transform.DOMove(

                anticipationPosition,

                altarAnticipationDuration

            ).SetEase(Ease.OutQuad)

        );

        if (visualRoot != null)

        {

            altarFlightTween.Join(

                visualRoot.DOScale(

                    visualBaseLocalScale *

                    altarAnticipationScale,

                    altarAnticipationDuration

                ).SetEase(Ease.OutQuad)

            );

        }

        // 2) Rocket to the altar with a small overshoot.

        altarFlightTween.Append(

            transform.DOMove(

                targetPosition,

                altarFlightDuration

            ).SetEase(

                Ease.OutBack,

                altarFlightOvershoot

            )

        );

        altarFlightTween.Join(

            transform.DORotateQuaternion(

                targetRotation,

                altarFlightDuration

            ).SetEase(Ease.OutCubic)

        );

        if (visualRoot != null)

        {

            altarFlightTween.Join(

                visualRoot.DOScale(

                    visualBaseLocalScale *

                    altarPreviewScale,

                    altarFlightDuration

                ).SetEase(Ease.OutCubic)

            );

        }

    }

    private void KillAltarFlightTween()

    {

        if (altarFlightTween != null &&

            altarFlightTween.IsActive())

        {

            altarFlightTween.Kill(false);

        }

        altarFlightTween = null;

    }

    private bool TryOfferToAltar()
    {
        if (offeringController == null)
            offeringController = FindFirstObjectByType<CardOfferingController>();
        if (offeringController == null || !offeringController.TryStageCard(this))
            return false;
        if (SourceCard != null) SourceCard.Deselect();
        return true;
    }

    private void FinishDrag()

    {

        if (interactionCamera == null)

            interactionCamera = Camera.main;

        bool staged = false;

        if (offeringController != null && interactionCamera != null)

        {

            Vector2 pointer =

                Mouse.current != null

                    ? Mouse.current.position.ReadValue()

                    : (Vector2)Input.mousePosition;

            if (offeringController.IsPointerOverOffering(

                interactionCamera,

                pointer))

            {

                staged = TryOfferToAltar();

            }

        }

        if (staged)

            return;

        KillAltarFlightTween();

        ResetDragLeanImmediate();

        hasLastDragScreenPosition = false;

        isDraggingOverOffering = false;

        isReturningFromOffering = false;

        PhysicalState = CardPhysicalState.InHand;

        if (horizontalHandHolder != null)

            horizontalHandHolder.RefanAll();

        else if (handHolder != null)

            handHolder.EndDrag(this);

        StartIdle();

    }

    private Vector2 ClampHandDragScreenPosition(Vector2 screenPosition)

    {

        if (!limitHandDragToViewport || interactionCamera == null)

            return screenPosition;

        Vector3 viewport = interactionCamera.ScreenToViewportPoint(

            new Vector3(screenPosition.x, screenPosition.y, 0f)

        );

        float minX = Mathf.Clamp01(Mathf.Min(handDragViewportMin.x, handDragViewportMax.x));

        float maxX = Mathf.Clamp01(Mathf.Max(handDragViewportMin.x, handDragViewportMax.x));

        float minY = Mathf.Clamp01(Mathf.Min(handDragViewportMin.y, handDragViewportMax.y));

        float maxY = Mathf.Clamp01(Mathf.Max(handDragViewportMin.y, handDragViewportMax.y));

        viewport.x = Mathf.Clamp(viewport.x, minX, maxX);

        viewport.y = Mathf.Clamp(viewport.y, minY, maxY);

        Vector3 clamped = interactionCamera.ViewportToScreenPoint(viewport);

        return new Vector2(clamped.x, clamped.y);

    }

    private void UpdateDragLean(Vector2 screenPosition)

    {

        if (!enableDragLean || visualRoot == null)

            return;

        if (!hasLastDragScreenPosition)

        {

            lastDragScreenPosition = screenPosition;

            hasLastDragScreenPosition = true;

            return;

        }

        float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0.0001f, 0.033f);

        Vector2 pointerVelocity =

            (screenPosition - lastDragScreenPosition) / dt;

        lastDragScreenPosition = screenPosition;

        float speedForMax =

            Mathf.Max(1f, dragLeanPixelsPerSecondForMax);

        // The card lags behind the hand: move right -> card swings left,

        // move up -> bottom of card trails downward.

        float targetRoll =

            Mathf.Clamp(

                -pointerVelocity.x / speedForMax,

                -1f,

                1f

            ) * dragLeanMaxRoll;

        float targetPitch =

            Mathf.Clamp(

                pointerVelocity.y / speedForMax,

                -1f,

                1f

            ) * dragLeanMaxPitch;

        Vector2 targetLean = new Vector2(targetPitch, targetRoll);

        // Underdamped spring. When the mouse suddenly stops, targetLean drops

        // toward zero but angular velocity remains, creating the little swing

        // past center and back that you wanted.

        Vector2 acceleration =

            (targetLean - currentDragLean) * dragSwingSpring -

            dragLeanAngularVelocity * dragSwingDamping;

        dragLeanAngularVelocity += acceleration * dt;

        currentDragLean += dragLeanAngularVelocity * dt;

        float pitchLimit =

            Mathf.Max(0.1f, dragLeanMaxPitch * dragSwingOvershootLimit);

        float rollLimit =

            Mathf.Max(0.1f, dragLeanMaxRoll * dragSwingOvershootLimit);

        currentDragLean.x = Mathf.Clamp(

            currentDragLean.x,

            -pitchLimit,

            pitchLimit

        );

        currentDragLean.y = Mathf.Clamp(

            currentDragLean.y,

            -rollLimit,

            rollLimit

        );

        visualRoot.localRotation =

            visualBaseLocalRotation *

            Quaternion.Euler(

                currentDragLean.x,

                0f,

                currentDragLean.y

            );

    }

    private Quaternion GetHandDragHangRotation()

    {

        Quaternion neutralRotation = baseRotation;

        if (straightenCardWhileDragging && horizontalHandHolder != null)

        {

            neutralRotation =

                horizontalHandHolder.GetStraightHandRotation();

        }

        return neutralRotation *

               Quaternion.Euler(dragHangEulerOffset);

    }

    private void ResetDragLeanImmediate()

    {

        currentDragLean = Vector2.zero;

        dragLeanAngularVelocity = Vector2.zero;

        hasLastDragScreenPosition = false;

        if (visualRoot != null)

        {

            visualRoot.DOKill(false);

            visualRoot.localRotation = visualBaseLocalRotation;

        }

    }

    private bool TryGetDragPlanePoint(Vector2 screenPosition, out Vector3 worldPoint)

    {

        worldPoint = Vector3.zero;

        if (interactionCamera == null)

            interactionCamera = Camera.main;

        if (interactionCamera == null)

            return false;

        if (horizontalHandHolder != null)

        {

            // IMPORTANT: use a CAMERA-FACING plane for in-hand reordering.

            // A table-parallel plane becomes nearly parallel to the camera ray,

            // which is what caused dragged cards to shoot through the back wall.

            Vector3 normal = interactionCamera.transform.forward;

            Plane plane = new Plane(normal, basePosition);

            Ray ray = interactionCamera.ScreenPointToRay(screenPosition);

            if (!plane.Raycast(ray, out float enter))

                return false;

            worldPoint = ray.GetPoint(enter);

            return true;

        }

        if (handHolder != null)

        {

            return handHolder.TryGetDragWorldPoint(

                interactionCamera,

                screenPosition,

                out worldPoint

            );

        }

        Plane fallback = new Plane(Vector3.up, basePosition);

        Ray fallbackRay = interactionCamera.ScreenPointToRay(screenPosition);

        if (!fallback.Raycast(fallbackRay, out float fallbackEnter))

            return false;

        worldPoint = fallbackRay.GetPoint(fallbackEnter);

        return true;

    }

    public void EnterOfferedState(Transform targetSlot, float moveDuration)

    {

        // Compatibility method retained for older callers. New altar flow uses

        // PlayOfferingBurn() instead.

        pointerDown = false;

        isPrimaryHover = false;

        isDraggingOverOffering = false;

        PhysicalState = CardPhysicalState.Offered;

        targetWaveWorldOffset = Vector3.zero;

        currentWaveWorldOffset = Vector3.zero;

        targetWaveScaleBoost = 0f;

        currentWaveScaleBoost = 0f;

        KillVisualTweens();

        KillAltarFlightTween();

        transform.DOKill(false);

        if (targetSlot != null)

        {

            float settleDuration = Mathf.Min(moveDuration, 0.09f);

            transform.DOMove(targetSlot.position, settleDuration)

                .SetEase(Ease.OutBack, 1.10f);

            transform.DORotateQuaternion(targetSlot.rotation, settleDuration)

                .SetEase(Ease.OutCubic);

        }

    }

    /// <summary>

    /// The REAL card lands flat in the altar recess, flashes hot, shrinks, and

    /// disappears. CardOfferingController creates the separate translucent

    /// preview only after this finishes.

    /// </summary>

    public void PlayOfferingBurn(

        Transform burnPoint,

        float landDuration,

        float burnDuration,

        Action onComplete)

    {

        pointerDown = false;

        isPrimaryHover = false;

        isDraggingOverOffering = false;

        isReturningFromOffering = false;

        PhysicalState = CardPhysicalState.Offered;

        isBurningVisual = true;
        GetComponent<CardAttackVisuals>()?.SetBurning(true);

        targetWaveWorldOffset = Vector3.zero;

        currentWaveWorldOffset = Vector3.zero;

        targetWaveScaleBoost = 0f;

        currentWaveScaleBoost = 0f;

        KillVisualTweens();

        KillAltarFlightTween();

        transform.DOKill(false);

        if (visualRoot != null)

        {

            visualRoot.gameObject.SetActive(true);

            visualRoot.DOKill(false);

            visualRoot.localScale = visualBaseLocalScale;

        }

        if (burnPoint == null)

        {

            isBurningVisual = false;
            GetComponent<CardAttackVisuals>()?.SetBurning(false);

            onComplete?.Invoke();

            return;

        }

        Sequence burnSequence = DOTween.Sequence();

        altarFlightTween = burnSequence;

        // Land physically in the recess and rotate to the BurnPoint's flat pose.

        burnSequence.Append(

            transform.DOMove(

                burnPoint.position,

                landDuration

            ).SetEase(Ease.OutBack, 1.10f)

        );

        burnSequence.Join(

            transform.DORotateQuaternion(

                burnPoint.rotation,

                landDuration

            ).SetEase(Ease.OutCubic)

        );

        // Tiny impact/pop before the card collapses into the burn.

        if (visualRoot != null)

        {

            burnSequence.Append(

                visualRoot.DOScale(

                    visualBaseLocalScale * offeringBurnScalePop,

                    0.045f

                ).SetEase(Ease.OutQuad)

            );

        }

        float burnT = 0f;

        Tween burnColorTween = DOTween.To(

            () => burnT,

            value =>

            {

                burnT = value;

                ApplyOfferingBurnVisual(value);

            },

            1f,

            burnDuration

        ).SetEase(Ease.InQuad);

        burnSequence.Append(burnColorTween);

        if (visualRoot != null)

        {

            burnSequence.Join(

                visualRoot.DOScale(

                    Vector3.zero,

                    burnDuration

                ).SetEase(Ease.InBack)

            );

        }

        burnSequence.Join(

            transform.DOPunchRotation(

                new Vector3(0f, 0f, 2.5f),

                burnDuration,

                6,

                0.25f

            )

        );

        burnSequence.OnComplete(() =>

        {

            ResetOfferingBurnVisual();

            isBurningVisual = false;
            GetComponent<CardAttackVisuals>()?.SetBurning(false);

            if (visualRoot != null)

                visualRoot.gameObject.SetActive(false);

            altarFlightTween = null;

            onComplete?.Invoke();

        });

    }

    private void ApplyOfferingBurnVisual(float t)

    {

        foreach (HoverRendererState state in hoverRendererStates)

        {

            if (state.renderer == null)

                continue;

            state.renderer.GetPropertyBlock(state.block);

            Color hotBase = Color.Lerp(

                state.baseColor,

                offeringBurnColor,

                Mathf.Clamp01(t * 1.15f)

            );

            hotBase.a = state.baseColor.a;

            if (state.hasBaseColor)

                state.block.SetColor("_BaseColor", hotBase);

            if (state.hasColor)

                state.block.SetColor("_Color", hotBase);

            if (state.hasEmission)

            {

                Color hotEmission =

                    state.baseEmission +

                    offeringBurnColor *

                    (offeringBurnEmissionStrength * t);

                state.block.SetColor("_EmissionColor", hotEmission);

            }

            state.renderer.SetPropertyBlock(state.block);

        }

    }

    private void ResetOfferingBurnVisual()

    {

        foreach (HoverRendererState state in hoverRendererStates)

        {

            if (state.renderer == null)

                continue;

            state.renderer.GetPropertyBlock(state.block);

            if (state.hasBaseColor)

                state.block.SetColor("_BaseColor", state.baseColor);

            if (state.hasColor)

                state.block.SetColor("_Color", state.baseColor);

            if (state.hasEmission)

                state.block.SetColor("_EmissionColor", state.baseEmission);

            state.renderer.SetPropertyBlock(state.block);

        }

    }

    public void ReturnToHandState()

    {

        pointerDown = false;

        isPrimaryHover = false;

        isDraggingOverOffering = false;

        isReturningFromOffering = false;

        KillAltarFlightTween();

        PhysicalState = CardPhysicalState.InHand;

        isBurningVisual = false;
        GetComponent<CardAttackVisuals>()?.SetBurning(false);

        if (visualRoot != null)

            visualRoot.gameObject.SetActive(true);

        ResetOfferingBurnVisual();

        ResetVisualImmediate();

        StartIdle();

    }

    public void EnterSacrificingState()

    {

        pointerDown = false;

        isPrimaryHover = false;

        isDraggingOverOffering = false;

        isReturningFromOffering = false;

        KillAltarFlightTween();

        PhysicalState = CardPhysicalState.Sacrificing;

        KillVisualTweens();

    }

    public void SetOfferingController(CardOfferingController controller)

    {

        offeringController = controller;

    }

    public void SetHandHolder(HandHolder3D newHandHolder)

    {

        handHolder = newHandHolder;

    }

    public void TweenRootTo(

        Vector3 position,

        Quaternion rotation,

        float duration,

        Ease ease)

    {

        SetBasePose(position, rotation);

    }

    private void UpdateHandRootPose()

    {

        if (!hasBasePose || PhysicalState != CardPhysicalState.InHand)

            return;

        float waveT = 1f - Mathf.Exp(-22f * Time.deltaTime);

        currentWaveWorldOffset = Vector3.Lerp(

            currentWaveWorldOffset,

            targetWaveWorldOffset,

            waveT

        );

        Vector3 targetPosition =

            basePosition + currentWaveWorldOffset;

        float moveT =

            1f - Mathf.Exp(-handMoveSpeed * Time.deltaTime);

        float rotateT =

            1f - Mathf.Exp(-handRotateSpeed * Time.deltaTime);

        transform.position = Vector3.Lerp(

            transform.position,

            targetPosition,

            moveT

        );

        transform.rotation = Quaternion.Slerp(

            transform.rotation,

            baseRotation,

            rotateT

        );

    }

    private void UpdateWaveScale()

    {

        if (visualRoot == null)

            return;

        float target =

            PhysicalState == CardPhysicalState.InHand

                ? targetWaveScaleBoost

                : 0f;

        float scaleT = 1f - Mathf.Exp(-22f * Time.deltaTime);

        currentWaveScaleBoost = Mathf.Lerp(

            currentWaveScaleBoost,

            target,

            scaleT

        );

        visualRoot.localScale =

            visualBaseLocalScale * (1f + currentWaveScaleBoost);

    }

    private void AnimateHoverIn()

    {

        if (visualRoot == null ||

            PhysicalState != CardPhysicalState.InHand)

            return;

        KillVisualTweens();

        Vector3 targetPosition =

            visualBaseLocalPosition + hoverLocalOffset;

        Quaternion targetRotation =

            visualBaseLocalRotation *

            Quaternion.Euler(hoverLocalRotation);

        visualTween = DOTween.Sequence();

        visualTween.Join(

            visualRoot.DOLocalMove(

                targetPosition,

                hoverInDuration

            ).SetEase(hoverInEase)

        );

        visualTween.Join(

            visualRoot.DOLocalRotateQuaternion(

                targetRotation,

                hoverInDuration

            ).SetEase(hoverInEase)

        );

    }

    private void AnimateHoverOut()

    {

        if (visualRoot == null ||

            PhysicalState != CardPhysicalState.InHand)

            return;

        KillVisualTweens();

        visualTween = DOTween.Sequence();

        visualTween.Join(

            visualRoot.DOLocalMove(

                visualBaseLocalPosition,

                hoverOutDuration

            ).SetEase(hoverOutEase)

        );

        visualTween.Join(

            visualRoot.DOLocalRotateQuaternion(

                visualBaseLocalRotation,

                hoverOutDuration

            ).SetEase(hoverOutEase)

        );

        visualTween.OnComplete(StartIdle);

    }

    private void StartIdle()

    {

        if (!enableIdle ||

            visualRoot == null ||

            PhysicalState != CardPhysicalState.InHand ||

            isPrimaryHover)

            return;

        KillIdleTweens();

        float duration =

            UnityEngine.Random.Range(idleDurationMin, idleDurationMax);

        float delay =

            UnityEngine.Random.Range(idleStartDelayMin, idleStartDelayMax);

        // Bob ONLY along the table normal. This keeps the card's visual child

        // from drifting sideways or tilting through neighboring cards.

        Vector3 worldTableUp =

            horizontalHandHolder != null

                ? horizontalHandHolder.transform.up

                : Vector3.up;

        Vector3 safeLocalDirection =

            transform.InverseTransformDirection(worldTableUp).normalized;

        Vector3 positionTarget =

            visualBaseLocalPosition + safeLocalDirection * idleLiftAmount;

        idleMoveTween = visualRoot

            .DOLocalMove(positionTarget, duration)

            .SetDelay(delay)

            .SetEase(Ease.InOutSine)

            .SetLoops(-1, LoopType.Yoyo);

    }

    private void InitializeHoverBrightness()

    {

        hoverRendererStates.Clear();

        Renderer[] renderers = hoverBrightnessRenderers;

        if (renderers == null || renderers.Length == 0)

        {

            renderers =

                visualRoot != null

                    ? visualRoot.GetComponentsInChildren<Renderer>(true)

                    : GetComponentsInChildren<Renderer>(true);

        }

        foreach (Renderer rendererToUse in renderers)

        {

            if (rendererToUse == null)

                continue;

            Material material = rendererToUse.sharedMaterial;

            if (material == null)

                continue;

            HoverRendererState state =

                new HoverRendererState();

            state.renderer = rendererToUse;

            state.block = new MaterialPropertyBlock();

            if (material.HasProperty("_BaseColor"))

            {

                state.hasBaseColor = true;

                state.baseColor = material.GetColor("_BaseColor");

            }

            else if (material.HasProperty("_Color"))

            {

                state.hasColor = true;

                state.baseColor = material.GetColor("_Color");

            }

            if (material.HasProperty("_EmissionColor"))

            {

                state.hasEmission = true;

                state.baseEmission = material.GetColor("_EmissionColor");

                // URP Lit needs this keyword enabled before a per-renderer

                // emission color can actually contribute light.

                material.EnableKeyword("_EMISSION");

            }

            if (state.hasBaseColor ||

                state.hasColor ||

                state.hasEmission)

            {

                hoverRendererStates.Add(state);

            }

        }

    }

    private void UpdateHoverBrightness()

    {

        if (isBurningVisual)

            return;

        float target =

            (isPrimaryHover || PhysicalState == CardPhysicalState.Dragging)

                ? 1f

                : 0f;

        hoverBrightness01 = Mathf.MoveTowards(

            hoverBrightness01,

            target,

            hoverBrightnessSpeed * Time.deltaTime

        );

        if (hoverRendererStates.Count == 0)

            return;

        foreach (HoverRendererState state in hoverRendererStates)

        {

            if (state.renderer == null)

                continue;

            state.renderer.GetPropertyBlock(state.block);

            Color brightColor =

                state.baseColor * hoverBrightnessMultiplier;

            // Preserve transparency if the card material uses alpha.

            brightColor.a = state.baseColor.a;

            Color currentBase =

                Color.Lerp(

                    state.baseColor,

                    brightColor,

                    hoverBrightness01

                );

            if (state.hasBaseColor)

                state.block.SetColor("_BaseColor", currentBase);

            if (state.hasColor)

                state.block.SetColor("_Color", currentBase);

            if (state.hasEmission)

            {

                Color hoverEmission =

                    state.baseEmission +

                    state.baseColor * hoverEmissionStrength;

                hoverEmission.a = 1f;

                state.block.SetColor(

                    "_EmissionColor",

                    Color.Lerp(

                        state.baseEmission,

                        hoverEmission,

                        hoverBrightness01

                    )

                );

            }

            state.renderer.SetPropertyBlock(state.block);

        }

    }

    private void PlayHoverSound()

    {

        if (hoverAudioSource == null || hoverSound == null)

            return;

        hoverAudioSource.pitch =

            UnityEngine.Random.Range(minHoverPitch, maxHoverPitch);

        hoverAudioSource.PlayOneShot(

            hoverSound,

            hoverVolume

        );

    }

    private void ResetVisualImmediate()

    {

        if (visualRoot == null)

            return;

        KillVisualTweens();

        visualRoot.localPosition = visualBaseLocalPosition;

        visualRoot.localRotation = visualBaseLocalRotation;

        visualRoot.localScale = visualBaseLocalScale;

    }

    private void KillVisualTweens()

    {

        if (visualTween != null && visualTween.IsActive())

            visualTween.Kill(false);

        visualTween = null;

        KillIdleTweens();

    }

    private void KillIdleTweens()

    {

        if (idleMoveTween != null && idleMoveTween.IsActive())

            idleMoveTween.Kill(false);

        idleMoveTween = null;

    }

}
