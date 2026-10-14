using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Responsive, spring-loaded tabletop Offer button. Talks directly to
/// CardOfferingController — no manual UnityEvent wiring required for the
/// core loop to work.
///
/// Behavior:
/// - Every click while a valid offer is possible (1 to MaximumCardsToOffer
///   cards staged, not mid-animation, not already resolving) adds a fresh
///   downward mechanical impulse.
/// - Clicking again while the button is already moving immediately pushes
///   it down again.
/// - The FIRST successful bottom-out with a valid offer calls
///   CardOfferingController.ConfirmOffering() exactly once.
/// - If the offer is NOT valid right now (most commonly: 0 cards staged),
///   the button still responds physically with a small mechanical bump
///   and plays the locked CLANK — it never feels like a dead UI element,
///   it just refuses to confirm.
/// - Call SetReadyForNextTurn() after score calculation / dealing finishes
///   to make the button confirmable again next turn (on top of whatever
///   CardOfferingController itself already requires).
///
/// Put this component on the pressable Button child. The same GameObject
/// needs a Collider for OnMouseDown to fire.
/// </summary>
public class OfferButtonInteractable : MonoBehaviour
{
    [Header("Offering")]
    [Tooltip("Auto-found via FindFirstObjectByType if left empty.")]
    [SerializeField] private CardOfferingController offeringController;

    [Tooltip("Extra external lock on top of CardOfferingController's own state — " +
             "e.g. GameManager can hold this false while enemy/scoring resolves.")]
    [SerializeField] private bool readyForNextTurn = true;

    [Header("Mechanical Press")]
    [Tooltip("Local-space direction the button travels when pressed. " +
             "Try Down/Forward/Back/Left/Right (or a negative axis) until " +
             "it visually presses INTO the housing rather than out toward the player.")]
    [SerializeField] private Vector3 pressLocalAxis = Vector3.back;

    [Tooltip("Maximum local-space travel of the button.")]
    [SerializeField] private float pressDistance = 0.024f;

    [Tooltip("Fresh downward speed added by every valid click.")]
    [SerializeField] private float pressKickSpeed = 0.72f;

    [Tooltip("How strongly the internal spring pulls the button back to rest.")]
    [SerializeField] private float springStrength = 115f;

    [Tooltip("Mechanical damping. Higher values settle faster.")]
    [SerializeField] private float damping = 13f;

    [Range(0f, 1f)]
    [Tooltip("How much velocity survives the bottom impact and rebounds upward.")]
    [SerializeField] private float bottomBounce = 0.18f;

    [Tooltip("Maximum upward overshoot above the resting position.")]
    [SerializeField] private float returnOvershoot = 0.0007f;

    [Header("Locked / Clank Click")]
    [Tooltip("Small downward speed added when clicked while the offer isn't valid (e.g. 0 cards staged).")]
    [SerializeField] private float lockedKickSpeed = 0.025f;

    [Tooltip("Maximum visible movement for a locked click when the button is near rest.")]
    [SerializeField] private float lockedTravel = 0.0010f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Small mechanism sound played every time a valid click begins.")]
    [SerializeField] private AudioClip pressSound;

    [Tooltip("Heavy bottom-out sound when the button physically hits its stop on a valid confirm.")]
    [SerializeField] private AudioClip impactClunkSound;

    [Tooltip("Dry metal CLANK played when clicked with no valid offer (e.g. 0 cards selected).")]
    [SerializeField] private AudioClip lockedClankSound;

    [Tooltip("Deliberate pitch shift applied ONLY to the locked clank sound, on top of " +
             "the normal random Pitch Variation below. 1 = unchanged, 0.75 = notably deeper/duller.")]
    [SerializeField] private float lockedClankPitchMultiplier = 0.75f;

    [Tooltip("Adds a reverb tail to the locked clank specifically, so it reads as a " +
             "distinct rejected-metal sound even when reusing the same clip as another sound.")]
    [SerializeField] private bool lockedClankReverb = true;

    [SerializeField] private AudioReverbPreset lockedClankReverbPreset = AudioReverbPreset.Generic;

    [Tooltip("Optional quiet spring-return sound.")]
    [SerializeField] private AudioClip returnSound;

    [Range(0f, 1f)] [SerializeField] private float pressVolume = 0.65f;
    [Range(0f, 1f)] [SerializeField] private float impactVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float lockedVolume = 0.9f;
    [Range(0f, 1f)] [SerializeField] private float returnVolume = 0.45f;

    [Tooltip("Seconds to skip from the start of every clip when played, to cut " +
             "leading silence/lead-in so hits feel more immediate.")]
    [SerializeField] private float clipStartOffsetSeconds = 0f;

    [Tooltip("Random pitch variation applied per play, e.g. 0.05 = ±5%, so repeated " +
             "presses don't sound identical.")]
    [SerializeField] private float pitchVariation = 0.05f;

    [Header("Camera Feedback")]
    [SerializeField] private CameraShakeImpulse cameraShake;

    [Tooltip("Screen shake strength on a successful heavy bottom-out.")]
    [SerializeField] private float impactShakeStrength = 0.018f;

    [SerializeField] private float impactShakeDuration = 0.085f;

    [Tooltip("Tiny shake for the rejected locked CLANK.")]
    [SerializeField] private float lockedShakeStrength = 0.0035f;

    [SerializeField] private float lockedShakeDuration = 0.045f;

    [Header("Events")]
    [Tooltip("Invoked once, right after ConfirmOffering() is called on a successful bottom-out. " +
             "Optional hook for relic effects / extra polish — not required for the core loop.")]
    public UnityEvent onOfferCommitted;

    private Vector3 restLocalPosition;

    // 0 = resting position. Positive = pressed downward.
    private float travel;
    private float velocity;

    // Latches which kind of press is currently happening so the bottom-out
    // reacts correctly even if state changes mid-animation.
    private bool pendingValidConfirm;

    private bool wasMovingUp;
    private bool returnSoundArmed;

    /// <summary>
    /// True only when there's actually something confirmable right now:
    /// CardOfferingController allows it (1..MaximumCardsToOffer cards staged,
    /// not mid-burn, not already resolving) AND no external lock is held.
    /// </summary>
    public bool CanOfferRightNow =>
        readyForNextTurn &&
        offeringController != null &&
        offeringController.CanConfirm;

    public float NormalizedPress => pressDistance <= 0f ? 0f : Mathf.Clamp01(travel / pressDistance);

    private void Awake()
    {
        restLocalPosition = transform.localPosition;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (offeringController == null)
            offeringController = FindFirstObjectByType<CardOfferingController>();
    }

    private void Update()
    {
        CheckManualRaycastClick();
        SimulateButton(Time.deltaTime);
    }

    private void OnMouseDown()
    {
        Press();
    }

    /// <summary>
    /// Explicit raycast-based click check as a diagnostic/fallback alongside
    /// OnMouseDown(). Logs exactly what the ray hits so a silent OnMouseDown
    /// failure (wrong layer, blocked collider, no MainCamera, etc.) becomes
    /// visible in the Console instead of doing nothing with no explanation.
    /// Safe to leave in — TEMP_DIAGNOSTIC_LOG can be set false to go quiet
    /// once the button is confirmed working.
    /// </summary>
    private const bool TEMP_DIAGNOSTIC_LOG = true;

    private void CheckManualRaycastClick()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            return;

        Camera cam = Camera.main;
        if (cam == null)
        {
            if (TEMP_DIAGNOSTIC_LOG)
                Debug.LogWarning("[OfferButton] Click detected but Camera.main is null.");
            return;
        }

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        bool didHit = Physics.Raycast(ray, out RaycastHit hit, 1000f);

        if (!didHit)
        {
            if (TEMP_DIAGNOSTIC_LOG)
                Debug.Log("[OfferButton] Click ray hit nothing at all.");
            return;
        }

        if (hit.collider != null && hit.collider.gameObject == gameObject)
        {
            if (TEMP_DIAGNOSTIC_LOG)
                Debug.Log("[OfferButton] Manual raycast hit THIS button — calling Press().");

            Press();
        }
        else if (TEMP_DIAGNOSTIC_LOG)
        {
            Debug.Log($"[OfferButton] Click ray hit '{hit.collider?.gameObject.name}' instead of this button.");
        }
    }

    /// <summary>
    /// Call this for a mouse click, raycast interaction, or another input source
    /// (e.g. CardOfferingController's Space-to-confirm hotkey).
    /// It is intentionally re-entrant: every call affects the mechanism immediately.
    /// </summary>
    public void Press()
    {
        if (CanOfferRightNow)
            ValidMechanicalPress();
        else
            LockedMechanicalPress();
    }

    private void ValidMechanicalPress()
    {
        pendingValidConfirm = true;

        // Every click gives the mechanism a fresh downward kick.
        // If it was already returning, this instantly reverses it.
        velocity = Mathf.Max(velocity, pressKickSpeed);

        PlayOneShot(pressSound, pressVolume);
    }

    private void LockedMechanicalPress()
    {
        // Still respond physically. It should never feel like a dead UI element —
        // most commonly this fires when 0 cards are staged.
        velocity = Mathf.Max(velocity, lockedKickSpeed);

        PlayOneShot(
            lockedClankSound,
            lockedVolume,
            extraPitchMultiplier: lockedClankPitchMultiplier,
            addReverb: lockedClankReverb,
            reverbPreset: lockedClankReverbPreset);

        if (cameraShake != null)
            cameraShake.Kick(lockedShakeStrength, lockedShakeDuration);

        if (offeringController != null)
            Debug.Log($"[OfferButton] Locked/clank press — {offeringController.DebugOfferingState}");
        else
            Debug.Log("[OfferButton] Locked/clank press — offeringController is NULL.");
    }

    private void SimulateButton(float dt)
    {
        if (dt <= 0f)
            return;

        // Spring force always wants travel to return to zero.
        float acceleration =
            (-springStrength * travel) -
            (damping * velocity);

        velocity += acceleration * dt;
        travel += velocity * dt;

        // Allow only a tiny upward overshoot for that metal-spring snap.
        float minTravel = -Mathf.Abs(returnOvershoot);

        if (travel < minTravel)
        {
            travel = minTravel;

            if (velocity < 0f)
                velocity *= -0.15f;
        }

        bool currentlyValid = CanOfferRightNow;

        // While there's no valid offer and we're near rest, rejected clicks
        // should only nudge a tiny distance rather than the full press depth.
        float currentMaxTravel = currentlyValid
            ? pressDistance
            : Mathf.Max(lockedTravel, travel > lockedTravel ? pressDistance : lockedTravel);

        // Hard mechanical stop at the bottom.
        if (travel >= currentMaxTravel)
        {
            travel = currentMaxTravel;

            bool hitFullValidStop = currentlyValid && currentMaxTravel >= pressDistance - 0.000001f;

            if (velocity > 0f)
            {
                velocity = -velocity * bottomBounce;

                if (hitFullValidStop)
                    OnValidBottomImpact();
            }
        }

        // Detect the return phase so we can optionally make a quiet spring sound once.
        bool movingUpNow = velocity < -0.002f;

        if (movingUpNow && !wasMovingUp)
            returnSoundArmed = true;

        if (returnSoundArmed &&
            Mathf.Abs(travel) < 0.00035f &&
            Mathf.Abs(velocity) < 0.04f)
        {
            PlayOneShot(returnSound, returnVolume);
            returnSoundArmed = false;
        }

        wasMovingUp = movingUpNow;

        transform.localPosition =
            restLocalPosition + NormalizedPressAxis * travel;
    }

    private Vector3 NormalizedPressAxis =>
        pressLocalAxis.sqrMagnitude > 0.0001f
            ? pressLocalAxis.normalized
            : Vector3.back;

    private void OnValidBottomImpact()
    {
        PlayOneShot(impactClunkSound, impactVolume);

        if (cameraShake != null)
            cameraShake.Kick(impactShakeStrength, impactShakeDuration);

        // Only a press that was valid when the click happened commits a turn —
        // and only re-check + fire once per queued press.
        if (!pendingValidConfirm)
            return;

        pendingValidConfirm = false;

        // Re-validate at the moment of impact in case something changed
        // mid-animation (a card was deselected elsewhere, etc).
        if (offeringController == null || !offeringController.CanConfirm)
            return;

        offeringController.ConfirmOffering();
        onOfferCommitted?.Invoke();
    }

    /// <summary>
    /// Call this after score calculation / enemy resolution / dealing is finished
    /// to allow the button to confirm again next turn.
    /// </summary>
    public void SetReadyForNextTurn()
    {
        readyForNextTurn = true;
    }

    /// <summary>
    /// Useful if GameManager wants explicit external control over the button,
    /// independent of whatever CardOfferingController itself is doing
    /// (e.g. holding it locked during an enemy-turn animation).
    /// </summary>
    public void SetReadyToOffer(bool ready)
    {
        readyForNextTurn = ready;
    }

    private void PlayOneShot(
        AudioClip clip,
        float volume,
        float extraPitchMultiplier = 1f,
        bool addReverb = false,
        AudioReverbPreset reverbPreset = AudioReverbPreset.Generic)
    {
        if (clip == null)
            return;

        // A short-lived dedicated AudioSource so overlapping plays (e.g. a
        // quick double-click) each get their own independent pitch and
        // start offset, rather than fighting over one shared AudioSource's
        // .pitch/.time — which PlayOneShot alone can't control per-call.
        GameObject temp = new GameObject("OfferButtonOneShot");
        temp.transform.position = transform.position;

        AudioSource src = temp.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.pitch = extraPitchMultiplier * (1f + Random.Range(-pitchVariation, pitchVariation));

        if (audioSource != null)
        {
            src.spatialBlend = audioSource.spatialBlend;
            src.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
            src.rolloffMode = audioSource.rolloffMode;
            src.minDistance = audioSource.minDistance;
            src.maxDistance = audioSource.maxDistance;
        }

        if (addReverb)
        {
            AudioReverbFilter reverb = temp.AddComponent<AudioReverbFilter>();
            reverb.reverbPreset = reverbPreset;
        }

        float offset = Mathf.Clamp(clipStartOffsetSeconds, 0f, Mathf.Max(0f, clip.length - 0.02f));
        src.time = offset;
        src.Play();

        float remainingRealTime =
            Mathf.Max(0.02f, clip.length - offset) / Mathf.Max(0.05f, src.pitch);

        Destroy(temp, remainingRealTime + 0.1f);
    }
}