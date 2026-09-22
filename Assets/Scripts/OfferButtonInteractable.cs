using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Responsive, spring-loaded tabletop Offer button.
///
/// Behavior:
/// - Every click while the button is available adds a fresh downward mechanical impulse.
/// - Clicking again while the button is already moving immediately pushes it down again.
/// - The FIRST successful bottom-out commits the turn exactly once.
/// - After the turn is committed, the button becomes busy/locked.
/// - Clicks while busy still give a tiny physical bump and play the locked CLANK,
///   but they do not trigger the turn again.
/// - Call SetReadyForNextTurn() after score calculation / dealing finishes.
///
/// Put this component on the pressable Button child.
/// The same GameObject needs a Collider.
/// </summary>
public class OfferButtonInteractable : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private bool readyToOffer = true;

    [Header("Mechanical Press")]
    [Tooltip("Maximum local-space travel of the button.")]
    [SerializeField] private float pressDistance = 0.006f;

    [Tooltip("Fresh downward speed added by every valid click.")]
    [SerializeField] private float pressKickSpeed = 0.18f;

    [Tooltip("How strongly the internal spring pulls the button back to rest.")]
    [SerializeField] private float springStrength = 115f;

    [Tooltip("Mechanical damping. Higher values settle faster.")]
    [SerializeField] private float damping = 13f;

    [Range(0f, 1f)]
    [Tooltip("How much velocity survives the bottom impact and rebounds upward.")]
    [SerializeField] private float bottomBounce = 0.18f;

    [Tooltip("Maximum upward overshoot above the resting position.")]
    [SerializeField] private float returnOvershoot = 0.0007f;

    [Header("Locked / Scoring Click")]
    [Tooltip("Small downward speed added when clicked while the turn is resolving.")]
    [SerializeField] private float lockedKickSpeed = 0.025f;

    [Tooltip("Maximum visible movement for a locked click when the button is near rest.")]
    [SerializeField] private float lockedTravel = 0.0010f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Small mechanism sound played every time a valid click begins.")]
    [SerializeField] private AudioClip pressSound;

    [Tooltip("Heavy bottom-out sound when the button physically hits its stop.")]
    [SerializeField] private AudioClip impactClunkSound;

    [Tooltip("Dry metal CLANK played when clicked during score calculation / lockout.")]
    [SerializeField] private AudioClip lockedClankSound;

    [Tooltip("Optional quiet spring-return sound.")]
    [SerializeField] private AudioClip returnSound;

    [Range(0f, 1f)] [SerializeField] private float pressVolume = 0.65f;
    [Range(0f, 1f)] [SerializeField] private float impactVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float lockedVolume = 0.9f;
    [Range(0f, 1f)] [SerializeField] private float returnVolume = 0.45f;

    [Header("Camera Feedback")]
    [SerializeField] private CameraShakeImpulse cameraShake;

    [Tooltip("Screen shake strength on a successful heavy bottom-out.")]
    [SerializeField] private float impactShakeStrength = 0.018f;

    [SerializeField] private float impactShakeDuration = 0.085f;

    [Tooltip("Tiny shake for the rejected locked CLANK.")]
    [SerializeField] private float lockedShakeStrength = 0.0035f;

    [SerializeField] private float lockedShakeDuration = 0.045f;

    [Header("Gameplay")]
    [Tooltip("Invoked once when the first valid press bottoms out. Hook this to your turn-resolution method.")]
    public UnityEvent onOfferCommitted;

    private Vector3 restLocalPosition;

    // 0 = resting position.
    // Positive = pressed downward.
    private float travel;
    private float velocity;

    // A turn is queued on the first click, but not committed until bottom impact.
    // Extra clicks before impact remain mechanically responsive without firing multiple turns.
    private bool offerQueued;

    private bool wasMovingUp;
    private bool returnSoundArmed;

    public bool ReadyToOffer => readyToOffer;
    public float NormalizedPress => pressDistance <= 0f ? 0f : Mathf.Clamp01(travel / pressDistance);

    private void Awake()
    {
        restLocalPosition = transform.localPosition;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void Update()
    {
        SimulateButton(Time.deltaTime);
    }

    private void OnMouseDown()
    {
        Press();
    }

    /// <summary>
    /// Call this for a mouse click, raycast interaction, or another input source.
    /// It is intentionally re-entrant: every call affects the mechanism immediately.
    /// </summary>
    public void Press()
    {
        if (readyToOffer)
            ValidMechanicalPress();
        else
            LockedMechanicalPress();
    }

    private void ValidMechanicalPress()
    {
        // Queue gameplay only once, but NEVER block additional physical clicks.
        if (!offerQueued)
            offerQueued = true;

        // Every click gives the mechanism a fresh downward kick.
        // If it was already returning, this instantly reverses it.
        velocity = Mathf.Max(velocity, pressKickSpeed);

        PlayOneShot(pressSound, pressVolume);
    }

    private void LockedMechanicalPress()
    {
        // Still respond physically. It should never feel like a dead UI element.
        velocity = Mathf.Max(velocity, lockedKickSpeed);

        PlayOneShot(lockedClankSound, lockedVolume);

        if (cameraShake != null)
            cameraShake.Kick(lockedShakeStrength, lockedShakeDuration);
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

        // While locked and near rest, rejected clicks should only nudge a tiny distance.
        float currentMaxTravel = readyToOffer
            ? pressDistance
            : Mathf.Max(lockedTravel, travel > lockedTravel ? pressDistance : lockedTravel);

        // Hard mechanical stop at the bottom.
        if (travel >= currentMaxTravel)
        {
            travel = currentMaxTravel;

            bool hitFullValidStop = readyToOffer && currentMaxTravel >= pressDistance - 0.000001f;

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
            restLocalPosition + Vector3.down * travel;
    }

    private void OnValidBottomImpact()
    {
        PlayOneShot(impactClunkSound, impactVolume);

        if (cameraShake != null)
            cameraShake.Kick(impactShakeStrength, impactShakeDuration);

        // Only the first valid bottom-out starts gameplay.
        if (!offerQueued)
            return;

        offerQueued = false;

        // Immediately lock gameplay presses once the turn actually begins.
        // The mechanism itself remains clickable and will CLANK while locked.
        readyToOffer = false;

        onOfferCommitted?.Invoke();
    }

    /// <summary>
    /// Call this after score calculation / enemy resolution / dealing is finished.
    /// The next valid button impact can then start another turn.
    /// </summary>
    public void SetReadyForNextTurn()
    {
        readyToOffer = true;
        offerQueued = false;
    }

    /// <summary>
    /// Useful if another manager wants explicit control over the button state.
    /// </summary>
    public void SetReadyToOffer(bool ready)
    {
        readyToOffer = ready;

        if (!ready)
            offerQueued = false;
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (audioSource == null || clip == null)
            return;

        audioSource.PlayOneShot(clip, volume);
    }
}
