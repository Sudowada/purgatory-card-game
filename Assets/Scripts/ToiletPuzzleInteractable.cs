using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// Click-to-use prison toilet puzzle. In Exploration, left-click the toilet
/// to begin peeing. Mouse look stays active to aim a gravity-curved stream;
/// movement is temporarily locked. A use raises water when the stream hits porcelain or
/// bowl water for most of its active duration. Four successful uses reveal a clickable sealed vial.
/// Attach to the Toilet root and place WaterAnchor inside the bowl.
///
/// The stream, placeholder water and placeholder vial are generated at runtime,
/// so this works before the final low-poly artwork/sounds are ready.
/// </summary>
[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public sealed class ToiletPuzzleInteractable : MonoBehaviour
{
    public const string VialCollectibleId = "collectible_toilet_vial";

    [Header("Interaction")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private FirstPersonController playerController;
    [Tooltip("Optional. Assign the root of the actual imported Toilet mesh hierarchy if it is NOT a child of the object holding this script. Otherwise leave empty.")]
    [SerializeField] private Transform toiletModelRoot;
    [Tooltip("Optional, like ShopWindowInteractable's Interaction Collider. Assign ONE specific non-trigger toilet collider to click. Leave empty to accept any collider on the toilet mesh root or its children.")]
    [SerializeField] private Collider interactionCollider;
    [Tooltip("Add non-trigger MeshColliders to imported toilet meshes that don't already have a collider. Allows clicking the toilet directly, as with the shop window.")]
    [SerializeField] private bool autoAddClickColliders = true;
    [Tooltip("Print shop-style click diagnostics, including the hit collider and any blocker.")]
    [SerializeField] private bool debugClicks = true;
    [Tooltip("Only for collecting the vial. Clicking the toilet has no distance limit.")]
    [SerializeField, Min(0.5f)] private float interactionDistance = 2.75f;

    [Header("Use animation")]
    [SerializeField, Min(0.3f)] private float peeDuration = 2.5f;
    [Tooltip("Seconds after clicking before the visible pee stream and audio start. The camera/use animation begins immediately.")]
    [SerializeField, Range(0f, 1.5f)] private float peeStartDelay = 0.4f;
    [Tooltip("Minimum fraction of active pee stream time contacting porcelain or bowl water. 0.60 means 60% on target; the startup delay is excluded.")]
    [SerializeField, Range(0.51f, 1f)] private float requiredToiletHitFraction = 0.60f;
    [Tooltip("Pee stream launch speed. Higher values travel farther with less drop.")]
    [SerializeField, Range(3f, 20f)] private float streamLaunchSpeed = 9.5f;
    [Tooltip("Pee stream downward acceleration. From farther away you'll need to aim a little higher.")]
    [SerializeField, Range(2f, 28f)] private float streamGravity = 9f;
    [Tooltip("Maximum simulated flight time in seconds.")]
    [SerializeField, Range(0.5f, 3f)] private float streamFlightTime = 1.6f;
    [Tooltip("Bowl target's half-width (world meters). Should fit inside the visible water surface.")]
    [SerializeField, Range(0.04f, 0.35f)] private float bowlHitRadiusX = 0.13f;
    [Tooltip("Bowl target's half-length (world meters). Should fit inside the visible water surface.")]
    [SerializeField, Range(0.04f, 0.35f)] private float bowlHitRadiusZ = 0.16f;
    [Tooltip("Forgiving hit detection just above the water. Allows a visually centered stream to count before its ray reaches the exact surface.")]
    [SerializeField, Range(0f, 0.12f)] private float bowlCatchHeight = 0.07f;
    [Tooltip("Multiplier for bowl hit ellipse when detecting the stream near the water (1.2 = 20% larger).")]
    [SerializeField, Range(1f, 1.5f)] private float bowlCatchWidth = 1.20f;
    [Tooltip("Optional Animator to trigger immediately on click. The pee stream begins after Pee Start Delay.")]
    [SerializeField] private Animator useAnimator;
    [SerializeField] private string useAnimationTrigger = "Use";
    [Tooltip("Gentle camera bob in meters while using the toilet.")]
    [SerializeField, Range(0f, 0.12f)] private float cameraDip = 0.035f;
    [Tooltip("Gentle camera downward tilt, in degrees.")]
    [SerializeField, Range(0f, 15f)] private float cameraTilt = 5f;

    [Header("Bowl water")]
    [Tooltip("Required: empty transform positioned at the center of the water, below the rim.")]
    [SerializeField] private Transform waterAnchor;
    [Tooltip("Optional: your own water mesh. A low-poly disk is generated when empty.")]
    [SerializeField] private Transform waterSurface;
    [Tooltip("Total world-space rise after all required uses. Keep below the bowl's rim.")]
    [SerializeField, Range(0.02f, 0.18f)] private float totalWaterRise = 0.10f;
    [SerializeField, Min(1)] private int usesToReveal = 4;
    [Tooltip("Playtest only: lets you rediscover the vial without clearing your saved collectibles.")]
    [SerializeField] private bool ignoreSavedCompletionForTesting;
    [SerializeField] private Material waterMaterial;

    [Header("Floating reward")]
    [Tooltip("Optional prefab. Without one, a low-poly placeholder glass vial is built.")]
    [SerializeField] private GameObject vialPrefab;
    [Tooltip("Optional: resting location of the surfaced vial. Otherwise follows the water.")]
    [SerializeField] private Transform vialRestPoint;
    [SerializeField, Min(0.2f)] private float vialRiseDuration = 1.0f;
    [SerializeField] private UnityEvent onVialCollected;

    [Header("Urination visual")]
    [Tooltip("Optional transparent material. Uses a basic unlit material by default.")]
    [SerializeField] private Material streamMaterial;
    [SerializeField, Range(0.004f, 0.025f)] private float streamWidth = 0.011f;
    [Header("Close-range bowl assist")]
    [Tooltip("At or under this distance, looking toward the bowl gently guides the stream into the water.")]
    [SerializeField, Range(0.5f, 3f)] private float closeAimDistance = 2.4f;
    [Tooltip("How far downward you must look for close-range bowl assist.")]
    [SerializeField, Range(0.05f, 0.9f)] private float closeAimDownward = 0.12f;
    [Tooltip("How closely the view must point toward the bowl to activate close-range assist. Smaller values forgive more error, but aiming away will still miss.")]
    [SerializeField, Range(0.45f, 0.99f)] private float closeAimAlignment = 0.64f;
    [Tooltip("How much of your original aim is preserved while the stream gradually bends toward the bowl (0 = direct line, 0.35 = gently curved).")]
    [SerializeField, Range(0f, 0.5f)] private float closeAimBend = 0.28f;
    [Header("Animated stream")]
    [Tooltip("Speed of small pixel droplets moving along the stream (cycles per second).")]
    [SerializeField, Range(0.5f, 8f)] private float dropletFlowSpeed = 3.2f;
    [Tooltip("How much the stream wriggles as it flows (world meters).")]
    [SerializeField, Range(0f, 0.015f)] private float streamWobble = 0.004f;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip peeClip;
    [SerializeField] private AudioClip waterSound;
    [SerializeField] private AudioClip revealClip;
    [SerializeField] private AudioClip pickupClip;

    private readonly List<Material> generatedMaterials = new List<Material>();
    private const int StreamSegments = 36;
    private const int DropletCount = 7;
    private const int SplashCount = 4;
    private readonly RaycastHit[] obstacleHits = new RaycastHit[24];
    private readonly Vector3[] drawnStreamPoints = new Vector3[StreamSegments];
    private readonly Transform[] droplets = new Transform[DropletCount];
    private readonly Transform[] splashes = new Transform[SplashCount];
    private int drawnStreamCount;
    private bool assistedBowlAim;
    private LineRenderer stream;
    private LineRenderer streamHighlight;
    private LineRenderer ripple;
    private Transform particleRoot;
    private Transform floatingVial;
    private Collider floatingVialCollider;
    private GameObject runtimeWater;
    private Mesh runtimeWaterMesh;
    private Vector3 initialWaterPosition;
    private float currentWaterRise;
    private float targetWaterRise;
    private float elapsedVialRise;
    private float streamElapsed;
    private float streamActiveTimeThisUse;
    private float toiletHitTimeThisUse;
    private Vector3 splashPosition;
    private bool streamInBowl;
    private bool streamHittingToilet;
    private bool streamStarted;
    private bool peeing;
    private bool cameraPoseSaved;
    private bool previousMovementLocked;
    private float previousLookPitchOffset;
    private Vector3 cameraLocalStartPosition;
    private bool revealing;
    private bool collected;
    private int useCount;

    public int UseCount => useCount;
    public bool IsRevealed => revealing && !collected;
    public bool HasCollectedVial => collected;

    private void Start()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        // Resolve the controller from this camera first, as ShopWindow does.
        if (playerController == null && playerCamera != null)
            playerController = playerCamera.GetComponentInParent<FirstPersonController>();
        if (playerController == null)
            playerController = FindFirstObjectByType<FirstPersonController>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        // An empty MeshCollider has no physical surface for the click ray
        // to hit. The Unity inspector can still display it as assigned.
        // Repair it from a colocated MeshFilter, or fall back to the
        // model-root/children collider strategy used by the shop window.
        if (interactionCollider is MeshCollider targetMeshCollider &&
            targetMeshCollider.sharedMesh == null)
        {
            MeshFilter targetFilter = targetMeshCollider.GetComponent<MeshFilter>();
            if (targetFilter != null && targetFilter.sharedMesh != null)
            {
                targetMeshCollider.sharedMesh = targetFilter.sharedMesh;
                Debug.Log("[ToiletClick] Filled the assigned MeshCollider from its MeshFilter.", this);
            }
            else
            {
                Debug.LogWarning("[ToiletClick] Assigned Interaction Collider '" +
                                 targetMeshCollider.name + "' has Mesh: None. " +
                                 "Using toilet root/children fallback instead. " +
                                 "For a fixed target, assign a collider with a real mesh.", this);
                interactionCollider = null;
            }
        }

        if (autoAddClickColliders && interactionCollider == null)
            EnsureToiletClickColliders();
        if (debugClicks)
            Debug.Log("[ToiletClick] Ready on '" + name + "'. Camera='" +
                      (playerCamera != null ? playerCamera.name : "MISSING") +
                      "', collider='" +
                      (interactionCollider != null ? interactionCollider.name : "root/children fallback") +
                      "', max distance=camera far clip (" +
                      (playerCamera != null ? playerCamera.farClipPlane.ToString("0.##") : "?") +
                      "m).", this);

        // One startup message even when Debug Clicks is disabled. If this
        // never appears, the component isn't attached/enabled in this scene.
        Transform model = toiletModelRoot != null ? toiletModelRoot : transform;
        Debug.Log("[ToiletPuzzle] Started on '" + name + "'; mesh root='" +
                  model.name + "'; camera='" +
                  (playerCamera != null ? playerCamera.name : "MISSING") +
                  "'; WaterAnchor='" +
                  (waterAnchor != null ? waterAnchor.name : "MISSING") +
                  "'; active=" + isActiveAndEnabled + ".", this);

        if (waterSurface == null && waterAnchor != null)
            CreatePlaceholderWater();
        if (waterSurface == null)
        {
            Debug.LogWarning("[ToiletPuzzle] Assign Water Anchor (or Water Surface) before playing.", this);
            enabled = false;
            return;
        }
        if (waterAnchor == null) waterAnchor = waterSurface;
        initialWaterPosition = waterSurface.position;

        var shop = ShopManager.Instance;
        collected = !ignoreSavedCompletionForTesting &&
                    ((shop != null && shop.HasCollectible(VialCollectibleId)) ||
                     PlayerPrefs.GetInt("Purgatory.ToiletVial.v1", 0) == 1);
        if (collected && shop != null && !shop.HasCollectible(VialCollectibleId))
            shop.AddCollectible(VialCollectibleId);

        CreateStreamVisuals();
    }

    private void Update()
    {
        if (waterSurface != null)
        {
            currentWaterRise = Mathf.MoveTowards(currentWaterRise, targetWaterRise,
                                                Time.deltaTime * 0.09f);
            waterSurface.position = initialWaterPosition + Vector3.up * currentWaterRise;
        }

        if (floatingVial != null && !collected)
            AnimateVial();

        // Read click input before early-exit guards, so Debug Clicks can
        // explain why the toilet didn't react in the current scene.
        var mouse = Mouse.current;
        var keys = Keyboard.current;
        bool clicked = mouse != null && mouse.leftButton.wasPressedThisFrame;
        bool pressedE = keys != null && keys.eKey.wasPressedThisFrame;

        if (peeing)
        {
            if (clicked && debugClicks)
                Debug.Log("[ToiletPuzzle] Click ignored: use animation still playing.", this);
            return;
        }

        string blockedBy = GetInteractionBlockReason();
        if (blockedBy != null)
        {
            if (clicked && debugClicks)
                Debug.Log("[ToiletPuzzle] Click blocked: " + blockedBy, this);
            return;
        }
        if (!clicked && !pressedE) return;

        if (clicked)
        {
            if (debugClicks) Debug.Log("[ToiletClick] Interaction input received.", this);

            // Same ScreenPointToRay + first-hit collider test as ShopWindow.
            // The first hit must be the configured toilet collider (if set)
            // or part of its mesh hierarchy, never an object behind it.
            if (!TryGetLookHit(out RaycastHit lookHit, playerCamera.farClipPlane))
            {
                if (debugClicks)
                {
                    Debug.Log("[ToiletClick] No collider hit using camera '" +
                              playerCamera.name + "'.", this);
                    if (interactionCollider != null && interactionCollider.isTrigger)
                        Debug.LogWarning("[ToiletClick] Interaction Collider is a trigger; uncheck Is Trigger.", this);
                }
            }
            else if (IsToiletHit(lookHit.collider))
            {
                if (debugClicks)
                    Debug.Log("[ToiletClick] Toilet hit at " +
                              lookHit.distance.ToString("0.##") + "m: '" +
                              lookHit.collider.name + "'. Starting pee animation.", this);
                StartCoroutine(UseToilet());
                return;
            }
            else if (debugClicks)
            {
                string expected = interactionCollider != null
                    ? interactionCollider.name
                    : (toiletModelRoot != null ? toiletModelRoot.name : name);
                Debug.Log("[ToiletClick] Hit '" + lookHit.collider.name + "' at " +
                          lookHit.distance.ToString("0.##") + "m, expected '" +
                          expected + "'. Actual hierarchy: " +
                          GetHierarchyPath(lookHit.collider.transform), this);
            }
        }

        // Preserve the original close-range pickup control for the surfaced vial.
        if ((clicked || pressedE) &&
            TryGetLookHit(out RaycastHit pickupHit, interactionDistance) &&
            IsVialHit(pickupHit.collider))
            CollectVial();
    }

    private IEnumerator UseToilet()
    {
        peeing = true;
        streamElapsed = 0f;
        streamActiveTimeThisUse = 0f;
        toiletHitTimeThisUse = 0f;
        streamInBowl = false;
        streamHittingToilet = false;
        assistedBowlAim = false;
        streamStarted = false;
        cameraLocalStartPosition = playerCamera.transform.localPosition;
        cameraPoseSaved = true;

        // Keep FirstPersonController running so mouse movement continues to
        // rotate the view, but suppress walking until the stream finishes.
        if (playerController != null)
        {
            previousMovementLocked = playerController.MovementLocked;
            previousLookPitchOffset = playerController.LookPitchOffset;
            playerController.MovementLocked = true;
        }

        // The use/camera animation begins on click. The visible stream and
        // pee sound start shortly afterward, rather than on the click frame.
        if (useAnimator != null && !string.IsNullOrEmpty(useAnimationTrigger))
            useAnimator.SetTrigger(useAnimationTrigger);

        float effectiveStartDelay = Mathf.Min(peeStartDelay, Mathf.Max(0f, peeDuration - 0.05f));
        if (debugClicks)
            Debug.Log("[ToiletPuzzle] Use animation started; pee stream in " +
                      effectiveStartDelay.ToString("0.00") + "s.", this);

        float elapsed = 0f;
        bool completed = false;
        while (elapsed < peeDuration)
        {
            if (GameStateManager.Instance != null &&
                GameStateManager.Instance.CurrentState != GameState.Exploration)
                break;
            if (SettingsUI.Instance != null && SettingsUI.Instance.IsOpen)
                break;
            if (RadioInteractable.Active != null) break;
            // Gameplay input may be blocked during Exploration while
            // environmental interaction is still allowed (like ShopWindow).
            // Do not cancel an already-started pee animation for that flag.
            if (GameManager.Instance != null && GameManager.Instance.IsShopOpen)
                break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, peeDuration));
            float lean = Mathf.Sin(t * Mathf.PI);

            // Only animate camera position/added pitch. Never overwrite the
            // current mouse-look rotation or force it back to the old angle.
            playerCamera.transform.localPosition = cameraLocalStartPosition +
                Vector3.down * (cameraDip * lean);
            if (playerController != null)
                playerController.LookPitchOffset =
                    previousLookPitchOffset + cameraTilt * lean;

            if (!streamStarted && elapsed >= effectiveStartDelay)
            {
                streamStarted = true;
                BeginStream();
                if (debugClicks)
                    Debug.Log("[ToiletPuzzle] Pee stream started after " +
                              elapsed.ToString("0.00") + "s.", this);
            }
            if (streamStarted)
            {
                streamElapsed += Time.deltaTime;
                UpdateStreamVisuals();
                // Only score time when the stream is actually flowing.
                streamActiveTimeThisUse += Time.deltaTime;
                if (streamHittingToilet)
                    toiletHitTimeThisUse += Time.deltaTime;
            }
            yield return null;
        }

        completed = elapsed >= peeDuration;
        FinishUseAnimation();

        // The player must land on the toilet for MOST of the stream duration.
        // Both porcelain and water qualify; a last-second hit is insufficient.
        if (!completed) yield break;
        float hitFraction = streamActiveTimeThisUse > 0.0001f
            ? toiletHitTimeThisUse / streamActiveTimeThisUse : 0f;
        bool successfulUse = streamActiveTimeThisUse > 0f &&
                             hitFraction >= requiredToiletHitFraction;
        if (debugClicks)
            Debug.Log("[ToiletPuzzle] Pee complete: toilet contact " +
                      (hitFraction * 100f).ToString("0") + "% (" +
                      toiletHitTimeThisUse.ToString("0.00") + "s / " +
                      streamActiveTimeThisUse.ToString("0.00") +
                      "s), required " +
                      (requiredToiletHitFraction * 100f).ToString("0") +
                      "% — " + (successfulUse ? "SUCCESS" : "MISS") + ".", this);
        if (!successfulUse) yield break;

        Play(waterSound);
        useCount++;
        targetWaterRise = totalWaterRise * Mathf.Min(useCount, usesToReveal) /
                          Mathf.Max(1, usesToReveal);

        if (!collected && useCount == 1)
            Notify("The water rises slightly.");
        else if (!collected && useCount == 2)
            Notify("The water isn't draining...");
        else if (!collected && useCount == usesToReveal - 1 && usesToReveal > 2)
            Notify("Something rattles beneath the water.");

        if (!collected && !revealing && useCount >= usesToReveal)
            StartCoroutine(RevealWhenWaterSettles());
    }

    private void BeginStream()
    {
        if (stream != null) stream.enabled = true;
        if (streamHighlight != null) streamHighlight.enabled = true;
        if (ripple != null) ripple.enabled = false;
        Play(peeClip);
        UpdateStreamVisuals();
    }

    private void FinishUseAnimation()
    {
        if (!peeing && !cameraPoseSaved) return;
        peeing = false;
        streamInBowl = false;
        streamHittingToilet = false;
        assistedBowlAim = false;
        streamStarted = false;
        if (stream != null) stream.enabled = false;
        if (streamHighlight != null) streamHighlight.enabled = false;
        if (ripple != null) ripple.enabled = false;
        HideStreamParticles();

        if (cameraPoseSaved && playerCamera != null &&
            (GameStateManager.Instance == null ||
             GameStateManager.Instance.CurrentState == GameState.Exploration))
            playerCamera.transform.localPosition = cameraLocalStartPosition;

        if (playerController != null)
        {
            playerController.LookPitchOffset = previousLookPitchOffset;
            playerController.MovementLocked = previousMovementLocked;
        }
        cameraPoseSaved = false;
    }

    private bool CanInteract() => GetInteractionBlockReason() == null;

    private string GetInteractionBlockReason()
    {
        if (playerCamera == null) return "Player Camera is missing (set FPCamera)";
        if (waterSurface == null) return "water setup is missing (assign WaterAnchor)";
        if (GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState != GameState.Exploration)
            return "game state is not Exploration";
        if (playerController != null && !playerController.enabled)
            return "FirstPersonController is disabled";
        if (RadioInteractable.Active != null)
            return "radio interaction is active";
        if (SettingsUI.Instance != null && SettingsUI.Instance.IsOpen)
            return "Settings UI is open";
        if (GameManager.Instance != null && GameManager.Instance.IsShopOpen)
            return "shop is open";
        // GameManager.IsInputBlocked gates card-game input. It can stay true
        // in Exploration; ShopWindowInteractable allows those clicks too.
        // Exploration state, shop, radio and settings are guarded above.
        return null;
    }

    private static string GetHierarchyPath(Transform item)
    {
        if (item == null) return "<null>";
        string path = item.name;
        while (item.parent != null)
        {
            item = item.parent;
            path = item.name + "/" + path;
        }
        return path;
    }

    private bool TryGetLookHit(out RaycastHit hit, float maxDistance)
    {
        // ShopWindowInteractable uses ScreenPointToRay and the first Physics
        // raycast hit. Keep the toilet's agreed fixed-center crosshair target
        // even when the OS cursor is temporarily unlocked.
        Vector2 point = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Ray ray = playerCamera.ScreenPointToRay(point);
        return Physics.Raycast(ray, out hit, maxDistance,
                               Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }

    private void EnsureToiletClickColliders()
    {
        // Some imported FBX meshes have no collider. Others have an empty
        // MeshCollider or a trigger-only collider which the click ray ignores.
        // Repair or supplement those, but preserve existing valid colliders.
        int added = 0, repaired = 0;
        Transform root = toiletModelRoot != null ? toiletModelRoot : transform;
        foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (meshFilter.sharedMesh == null) continue;
            bool hasUsableCollider = false;
            foreach (Collider existing in meshFilter.GetComponents<Collider>())
            {
                if (existing is MeshCollider mesh && mesh.sharedMesh == null)
                {
                    mesh.sharedMesh = meshFilter.sharedMesh;
                    repaired++;
                }
                if (existing.enabled && !existing.isTrigger &&
                    (!(existing is MeshCollider) ||
                     ((MeshCollider)existing).sharedMesh != null))
                    hasUsableCollider = true;
            }
            if (hasUsableCollider) continue;

            MeshCollider collider = meshFilter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = meshFilter.sharedMesh;
            added++;
        }
        if (debugClicks)
            Debug.Log("[ToiletClick] Added " + added + " and repaired " +
                      repaired + " toilet mesh collider(s).", this);
    }

    private bool IsToiletHit(Collider hit)
    {
        if (hit == null || (floatingVial != null &&
                           hit.transform.IsChildOf(floatingVial))) return false;

        // Same explicit collider match + root/children fallback as ShopWindow.
        if (interactionCollider != null)
            return hit == interactionCollider;

        Transform root = toiletModelRoot != null ? toiletModelRoot : transform;
        return hit.transform == root || hit.transform.IsChildOf(root);
    }

    // Unlike the specific click target, scoring accepts every toilet mesh.
    // Do not mistake other room geometry or the collected vial for a hit.
    private bool IsToiletBodyCollider(Collider hit)
    {
        if (hit == null) return false;
        if (floatingVial != null && hit.transform.IsChildOf(floatingVial))
            return false;
        if (waterSurface != null &&
            (hit.transform == waterSurface || hit.transform.IsChildOf(waterSurface)))
            return false;
        Transform root = toiletModelRoot != null ? toiletModelRoot : transform;
        return hit.transform == root || hit.transform.IsChildOf(root);
    }

    private bool IsVialHit(Collider hit)
    {
        return !collected && floatingVial != null && floatingVialCollider != null &&
               floatingVialCollider.enabled && hit != null &&
               (hit == floatingVialCollider || hit.transform.IsChildOf(floatingVial));
    }

    private IEnumerator RevealWhenWaterSettles()
    {
        revealing = true;
        while (currentWaterRise + 0.002f < targetWaterRise)
            yield return null;
        if (collected) yield break;

        SpawnVial();
        Play(revealClip);
        Notify("Something has floated to the surface. Look down and click it.");
    }

    private void AnimateVial()
    {
        elapsedVialRise += Time.deltaTime;
        float t = Mathf.Clamp01(elapsedVialRise / Mathf.Max(0.1f, vialRiseDuration));
        float eased = t * t * (3f - 2f * t);
        Vector3 rest = vialRestPoint != null
            ? vialRestPoint.position
            : waterSurface.position + Vector3.up * 0.047f;
        floatingVial.position = rest + Vector3.down * ((1f - eased) * 0.12f) +
                                Vector3.up * (Mathf.Sin(Time.time * 2.8f) * 0.004f * eased);
        floatingVial.Rotate(Vector3.up, Time.deltaTime * 10f, Space.World);

        if (floatingVialCollider != null)
            floatingVialCollider.enabled = t >= 0.98f;
    }

    private void SpawnVial()
    {
        if (vialPrefab != null)
        {
            var go = Instantiate(vialPrefab, waterSurface.position, Quaternion.identity);
            go.name = "ToiletVial_Reward";
            floatingVial = go.transform;
        }
        else
        {
            floatingVial = MakePlaceholderVial();
        }

        floatingVialCollider = floatingVial.GetComponentInChildren<Collider>();
        if (floatingVialCollider == null)
            floatingVialCollider = floatingVial.gameObject.AddComponent<CapsuleCollider>();
        floatingVialCollider.enabled = false;
        elapsedVialRise = 0f;
    }

    private Transform MakePlaceholderVial()
    {
        // A small, pale glass-colored vial with a dark cork. Replace with a
        // Blender prefab later; the pickup logic is identical.
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "ToiletVial_Reward_Placeholder";
        body.transform.localScale = new Vector3(0.032f, 0.038f, 0.032f);
        body.GetComponent<Renderer>().sharedMaterial =
            MakeMaterial(new Color(0.43f, 0.65f, 0.61f, 1f), false);

        GameObject cork = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cork.name = "Cork";
        cork.transform.SetParent(body.transform, false);
        cork.transform.localPosition = new Vector3(0, 0.97f, 0);
        cork.transform.localScale = new Vector3(0.85f, 0.13f, 0.85f);
        cork.GetComponent<Renderer>().sharedMaterial =
            MakeMaterial(new Color(0.23f, 0.14f, 0.09f, 1f), false);
        var collider = cork.GetComponent<Collider>();
        if (collider != null) { collider.enabled = false; Destroy(collider); }
        return body.transform;
    }

    private void CollectVial()
    {
        if (collected) return;
        collected = true;
        PlayerPrefs.SetInt("Purgatory.ToiletVial.v1", 1);
        PlayerPrefs.Save();
        ShopManager.Instance?.AddCollectible(VialCollectibleId);
        Play(pickupClip);
        Notify("You retrieved a sealed vial from the toilet.");
        if (floatingVial != null) Destroy(floatingVial.gameObject);
        floatingVial = null;
        onVialCollected?.Invoke(); // A future puzzle may open or inspect the vial.
    }

    private void CreatePlaceholderWater()
    {
        // Twelve-sided flat surface, not a stock Unity cylinder: fewer polygons,
        // no collider to steal the toilet or vial raycast, and no UV dependencies.
        runtimeWater = new GameObject("ToiletWater_RuntimePlaceholder",
            typeof(MeshFilter), typeof(MeshRenderer));
        runtimeWater.transform.SetParent(waterAnchor, false);
        runtimeWater.transform.localPosition = Vector3.zero;
        // Keep a ~30 x 36 cm surface even if the imported FBX root uses
        // a smaller Unity scale (such as 0.4).
        Vector3 scale = waterAnchor.lossyScale;
        runtimeWater.transform.localScale = new Vector3(
            0.30f / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
            1f,
            0.36f / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
        const int sides = 12;
        Vector3[] vertices = new Vector3[sides + 1];
        Vector2[] uv = new Vector2[sides + 1];
        int[] triangles = new int[sides * 3];
        vertices[0] = Vector3.zero;
        uv[0] = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < sides; i++)
        {
            float angle = Mathf.PI * 2f * i / sides;
            float x = Mathf.Cos(angle) * 0.5f;
            float z = Mathf.Sin(angle) * 0.5f;
            vertices[i + 1] = new Vector3(x, 0f, z);
            uv[i + 1] = new Vector2(x + 0.5f, z + 0.5f);
            triangles[3 * i] = 0;
            triangles[3 * i + 1] = (i + 1) % sides + 1;
            triangles[3 * i + 2] = i + 1;
        }
        runtimeWaterMesh = new Mesh { name = "ToiletWater_12Sided" };
        runtimeWaterMesh.vertices = vertices;
        runtimeWaterMesh.uv = uv;
        runtimeWaterMesh.triangles = triangles;
        runtimeWaterMesh.RecalculateNormals();
        runtimeWater.GetComponent<MeshFilter>().sharedMesh = runtimeWaterMesh;
        runtimeWater.GetComponent<MeshRenderer>().sharedMaterial =
            waterMaterial != null ? waterMaterial :
            MakeMaterial(new Color(0.13f, 0.21f, 0.20f, 1f), false);
        waterSurface = runtimeWater.transform;
    }

    private void CreateStreamVisuals()
    {
        // Main ribbon plus a narrow brighter moving highlight: a stylized
        // low-poly stream, not a fluid simulator or external visual asset.
        GameObject go = new GameObject("ToiletPeeStream_Runtime");
        go.transform.SetParent(transform, true);
        stream = go.AddComponent<LineRenderer>();
        stream.useWorldSpace = true;
        stream.positionCount = StreamSegments;
        stream.widthMultiplier = streamWidth;
        stream.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.55f), new Keyframe(0.3f, 1f),
            new Keyframe(1f, 0.45f));
        stream.numCornerVertices = 0;
        stream.numCapVertices = 0;
        stream.shadowCastingMode = ShadowCastingMode.Off;
        stream.receiveShadows = false;
        stream.sharedMaterial = streamMaterial != null ? streamMaterial :
            MakeMaterial(new Color(0.80f, 0.75f, 0.37f, 0.92f), true);
        stream.enabled = false;

        GameObject highlightObject = new GameObject("ToiletPeeHighlight_Runtime");
        highlightObject.transform.SetParent(transform, true);
        streamHighlight = highlightObject.AddComponent<LineRenderer>();
        streamHighlight.useWorldSpace = true;
        streamHighlight.positionCount = StreamSegments;
        streamHighlight.widthMultiplier = streamWidth * 0.30f;
        streamHighlight.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.3f), new Keyframe(0.45f, 1f),
            new Keyframe(1f, 0.2f));
        streamHighlight.numCornerVertices = 0;
        streamHighlight.numCapVertices = 0;
        streamHighlight.shadowCastingMode = ShadowCastingMode.Off;
        streamHighlight.receiveShadows = false;
        Material brighterPee = MakeMaterial(new Color(1f, 0.93f, 0.58f, 0.95f), true);
        streamHighlight.sharedMaterial = brighterPee;
        streamHighlight.enabled = false;

        GameObject circle = new GameObject("ToiletPeeRipple_Runtime");
        circle.transform.SetParent(transform, true);
        ripple = circle.AddComponent<LineRenderer>();
        ripple.useWorldSpace = true;
        ripple.positionCount = 13;
        ripple.loop = true;
        ripple.widthMultiplier = 0.005f;
        ripple.numCornerVertices = 0;
        ripple.shadowCastingMode = ShadowCastingMode.Off;
        ripple.receiveShadows = false;
        ripple.sharedMaterial = brighterPee;
        ripple.enabled = false;

        // Parent is counter-scaled so the imported 0.4-scale FBX does not
        // shrink droplets. All particles use shared cube meshes/materials,
        // are pooled, and are on Ignore Raycast so they cannot steal clicks.
        GameObject particles = new GameObject("ToiletPeeParticles_Runtime");
        particleRoot = particles.transform;
        particleRoot.SetParent(transform, false);
        Vector3 scale = transform.lossyScale;
        particleRoot.localScale = new Vector3(
            1f / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
            1f / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
            1f / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
        for (int i = 0; i < DropletCount; i++)
            droplets[i] = CreatePeeParticle("StreamDroplet_" + i, brighterPee);
        for (int i = 0; i < SplashCount; i++)
            splashes[i] = CreatePeeParticle("SplashDroplet_" + i, brighterPee);
    }

    private Transform CreatePeeParticle(string particleName, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = particleName;
        go.layer = 2; // Ignore Raycast
        go.transform.SetParent(particleRoot, false);
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        Renderer renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        go.SetActive(false);
        return go.transform;
    }

    private void HideStreamParticles()
    {
        foreach (Transform droplet in droplets)
            if (droplet != null) droplet.gameObject.SetActive(false);
        foreach (Transform splash in splashes)
            if (splash != null) splash.gameObject.SetActive(false);
    }

    private void SetVisualPoint(int index, Vector3 point)
    {
        drawnStreamPoints[index] = point;
        stream.SetPosition(index, point);
        streamHighlight.SetPosition(index,
            point + playerCamera.transform.up * 0.0015f);
    }

    /// <summary>
    /// Guide a reasonably well-aimed, close, downward shot gently toward
    /// the actual bowl position. Reject attempts aimed away from the toilet
    /// and don't allow the guidance to shoot through other room geometry.
    /// </summary>
    private bool TryGetCloseBowlTarget(Vector3 origin, out Vector3 target)
    {
        target = waterSurface.position + Vector3.up * 0.006f;
        if (Vector3.Distance(playerCamera.transform.position, target) > closeAimDistance)
            return false;

        Vector3 direction = target - origin;
        float distance = direction.magnitude;
        if (distance < 0.03f || target.y >= origin.y) return false;
        Vector3 aim = playerCamera.transform.forward;
        if (aim.y > -closeAimDownward) return false;
        if (Vector3.Dot(aim, direction / distance) < closeAimAlignment)
            return false;

        int hits = Physics.RaycastNonAlloc(origin, direction / distance, obstacleHits,
            distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; i++)
        {
            Collider c = obstacleHits[i].collider;
            if (c == null || !c.enabled) continue;
            if (playerController != null &&
                c.transform.IsChildOf(playerController.transform)) continue;
            if (c.transform == waterSurface ||
                c.transform.IsChildOf(waterSurface)) continue;
            if (IsToiletBodyCollider(c)) continue;
            if (obstacleHits[i].distance < distance - 0.02f)
                return false;
        }
        return true;
    }

    private Vector3 GetGuidedPoint(Vector3 origin, Vector3 bowl, float fraction)
    {
        Vector3 direct = bowl - origin;
        Vector3 look = playerCamera.transform.forward * direct.magnitude;
        // The correction fades in along the flight rather than snapping the
        // stream instantly to the center. The tip still lands in the bowl.
        float arc = Mathf.Sin(fraction * Mathf.PI);
        return Vector3.Lerp(origin, bowl, fraction) +
               (look - direct) * (closeAimBend * arc) +
               Vector3.up * (0.015f * arc);
    }

    private bool GuidedPathHasRoomObstacle(Vector3 origin, Vector3 bowl)
    {
        const int sections = 8;
        Vector3 previous = origin;
        for (int i = 1; i <= sections; i++)
        {
            Vector3 next = GetGuidedPoint(origin, bowl, i / (float)sections);
            Vector3 move = next - previous;
            float distance = move.magnitude;
            if (distance > 0.0001f)
            {
                int hits = Physics.RaycastNonAlloc(previous, move / distance,
                    obstacleHits, distance, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
                for (int h = 0; h < hits; h++)
                {
                    Collider c = obstacleHits[h].collider;
                    if (c == null) continue;
                    if (playerController != null &&
                        c.transform.IsChildOf(playerController.transform)) continue;
                    if (c.transform == waterSurface ||
                        c.transform.IsChildOf(waterSurface)) continue;
                    if (IsToiletBodyCollider(c)) continue;
                    if (obstacleHits[h].distance < distance - 0.01f)
                        return true;
                }
            }
            previous = next;
        }
        return false;
    }

    /// <summary>
    /// Simulate the stream's contact and draw an animated wavy ribbon.
    /// Recycled glints travel down the drawn path, with splash flecks at
    /// porcelain/water impact. All rendering uses the same gameplay path.
    /// </summary>
    private void UpdateStreamVisuals()
    {
        if (stream == null || streamHighlight == null || ripple == null ||
            waterSurface == null || playerCamera == null)
            return;

        Vector3 origin = playerCamera.ViewportToWorldPoint(
            new Vector3(0.56f, 0.18f, 0.34f));
        Vector3 right = playerCamera.transform.right;
        Vector3 up = playerCamera.transform.up;
        streamInBowl = false;
        streamHittingToilet = false;
        assistedBowlAim = false;
        ripple.enabled = false;

        if (TryGetCloseBowlTarget(origin, out Vector3 closeTarget) &&
            !GuidedPathHasRoomObstacle(origin, closeTarget))
        {
            assistedBowlAim = true;
            streamInBowl = true;
            streamHittingToilet = true;
            splashPosition = closeTarget;
            stream.positionCount = StreamSegments;
            streamHighlight.positionCount = StreamSegments;
            drawnStreamCount = StreamSegments;

            // A nearly straight, gently flowing downward stream.
            for (int i = 0; i < StreamSegments; i++)
            {
                float u = i / (StreamSegments - 1f);
                float envelope = Mathf.Sin(u * Mathf.PI);
                Vector3 point = GetGuidedPoint(origin, closeTarget, u);
                point += right * (Mathf.Sin(u * 43f - streamElapsed * 30f) *
                                  streamWobble * envelope);
                point += up * (Mathf.Cos(u * 29f - streamElapsed * 25f) *
                               streamWobble * 0.3f * envelope);
                SetVisualPoint(i, point);
            }
        }
        else
        {
            Vector3 velocity = playerCamera.transform.forward * streamLaunchSpeed;
            float step = streamFlightTime / (StreamSegments - 1f);
            stream.positionCount = 1;
            streamHighlight.positionCount = 1;
            SetVisualPoint(0, origin);
            drawnStreamCount = 1;
            Vector3 previous = origin;

            for (int i = 1; i < StreamSegments; i++)
            {
                float flightTime = step * i;
                Vector3 next = origin + velocity * flightTime +
                               Vector3.down * (0.5f * streamGravity *
                                               flightTime * flightTime);
                // Physical collision is evaluated BEFORE visual wobble.
                if (TryFindStreamImpact(previous, next,
                                        out Vector3 impact, out bool hitWater,
                                        out bool hitToilet))
                {
                    stream.positionCount = i + 1;
                    streamHighlight.positionCount = i + 1;
                    SetVisualPoint(i, impact);
                    drawnStreamCount = i + 1;
                    streamInBowl = hitWater;
                    streamHittingToilet = hitToilet;
                    splashPosition = impact;
                    break;
                }

                float u = i / (StreamSegments - 1f);
                float envelope = Mathf.Sin(u * Mathf.PI);
                Vector3 point = next +
                    right * (Mathf.Sin(u * 43f - streamElapsed * 30f) *
                             streamWobble * envelope) +
                    up * (Mathf.Cos(u * 29f - streamElapsed * 25f) *
                          streamWobble * 0.3f * envelope);
                stream.positionCount = i + 1;
                streamHighlight.positionCount = i + 1;
                SetVisualPoint(i, point);
                drawnStreamCount = i + 1;
                previous = next;
                if (next.y < -2f) break;
            }
        }

        // Small pulses keep the line alive even when the aim stays still.
        stream.widthMultiplier = streamWidth *
            (0.93f + 0.09f * Mathf.Sin(streamElapsed * 31f));
        streamHighlight.widthMultiplier = streamWidth * 0.30f *
            (0.85f + 0.15f * Mathf.Sin(streamElapsed * 37f));
        AnimatePeeParticles();

        if (!streamInBowl) return;
        // Ring ripple is water-only; dry porcelain receives splash flecks.
        ripple.enabled = true;
        float phase = (streamElapsed % 0.42f) / 0.42f;
        float radius = Mathf.Lerp(0.012f, 0.06f, phase);
        Vector3 center = splashPosition + Vector3.up * 0.004f;
        for (int i = 0; i < ripple.positionCount; i++)
        {
            float angle = 2f * Mathf.PI * i / ripple.positionCount;
            ripple.SetPosition(i, center + new Vector3(
                Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
        ripple.widthMultiplier = 0.005f * (1f - phase * 0.75f);
    }

    private void AnimatePeeParticles()
    {
        if (drawnStreamCount < 2)
        {
            HideStreamParticles();
            return;
        }
        for (int i = 0; i < DropletCount; i++)
        {
            Transform droplet = droplets[i];
            if (droplet == null) continue;
            float u = Mathf.Repeat(streamElapsed * dropletFlowSpeed +
                                   (float)i / DropletCount, 1f);
            float position = u * (drawnStreamCount - 1f);
            int index = Mathf.Min(Mathf.FloorToInt(position), drawnStreamCount - 2);
            float blend = position - index;
            Vector3 forward = drawnStreamPoints[index + 1] - drawnStreamPoints[index];
            droplet.position = Vector3.Lerp(drawnStreamPoints[index],
                                            drawnStreamPoints[index + 1], blend);
            if (forward.sqrMagnitude > 0.00001f)
                droplet.rotation = Quaternion.FromToRotation(Vector3.up, forward.normalized);
            float pulse = 0.75f + 0.25f * Mathf.Sin(streamElapsed * 23f + i * 2f);
            float size = streamWidth * pulse;
            droplet.localScale = new Vector3(size * 0.55f, size * 1.4f, size * 0.55f);
            if (!droplet.gameObject.activeSelf) droplet.gameObject.SetActive(true);
        }

        for (int i = 0; i < SplashCount; i++)
        {
            Transform splash = splashes[i];
            if (splash == null) continue;
            if (!streamHittingToilet)
            {
                if (splash.gameObject.activeSelf) splash.gameObject.SetActive(false);
                continue;
            }
            float phase = Mathf.Repeat(streamElapsed * 4.5f + i * 0.23f, 1f);
            float angle = i * 2f * Mathf.PI / SplashCount;
            float spread = 0.040f * phase;
            splash.position = splashPosition +
                new Vector3(Mathf.Cos(angle) * spread,
                            0.005f + Mathf.Sin(phase * Mathf.PI) * 0.025f,
                            Mathf.Sin(angle) * spread);
            float size = streamWidth * (0.7f - 0.4f * phase);
            splash.localScale = new Vector3(size, size, size);
            if (!splash.gameObject.activeSelf) splash.gameObject.SetActive(true);
        }
    }

    private bool TryFindStreamImpact(Vector3 from, Vector3 to,
                                    out Vector3 impact, out bool isWater,
                                     out bool isToilet)
    {
        impact = default;
        isWater = false;
        isToilet = false;
        Vector3 motion = to - from;
        float length = motion.magnitude;
        if (length < 0.0001f) return false;

        // Forgiving capture region immediately above the visible water.
        // This uses world-space dimensions, independent of FBX parent scale,
        // so a stream visibly entering the bowl doesn't miss a mathematically
        // exact, thin water plane by a couple of centimeters.
        float waterDistance = float.PositiveInfinity;
        Vector3 waterPoint = default;
        float y = waterSurface.position.y + bowlCatchHeight;
        if (from.y >= y && to.y <= y && to.y < from.y)
        {
            float fraction = Mathf.Clamp01((from.y - y) / (from.y - to.y));
            Vector3 crossing = Vector3.Lerp(from, to, fraction);
            Vector3 flat = Quaternion.Inverse(waterSurface.rotation) *
                           (crossing - waterSurface.position);
            float x = flat.x / Mathf.Max(0.001f, bowlHitRadiusX * bowlCatchWidth);
            float z = flat.z / Mathf.Max(0.001f, bowlHitRadiusZ * bowlCatchWidth);
            if (x * x + z * z <= 1f)
            {
                waterDistance = length * fraction;
                // Keep the visual splash at the actual water height while the
                // gameplay catch volume accepts slightly higher trajectories.
                waterPoint = new Vector3(crossing.x,
                    waterSurface.position.y + 0.006f, crossing.z);
            }
        }

        float obstacleDistance = float.PositiveInfinity;
        Vector3 obstaclePoint = default;
        Collider nearestObstacle = null;
        int n = Physics.RaycastNonAlloc(from, motion / length, obstacleHits,
                                        length, Physics.DefaultRaycastLayers,
                                        QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = obstacleHits[i].collider;
            if (c == null) continue;
            if (playerController != null &&
                c.transform.IsChildOf(playerController.transform)) continue;
            if (c.transform == waterSurface ||
                c.transform.IsChildOf(waterSurface)) continue;
            if (obstacleHits[i].distance >= obstacleDistance) continue;
            obstacleDistance = obstacleHits[i].distance;
            obstaclePoint = obstacleHits[i].point;
            nearestObstacle = c;
        }

        if (waterDistance < obstacleDistance && !float.IsPositiveInfinity(waterDistance))
        {
            impact = waterPoint;
            isWater = true;
            isToilet = true; // Water in the toilet bowl counts as contact.
            return true;
        }
        if (!float.IsPositiveInfinity(obstacleDistance))
        {
            impact = obstaclePoint;
            isToilet = IsToiletBodyCollider(nearestObstacle);
            return true;
        }
        return false;
    }

    private Material MakeMaterial(Color color, bool transparent)
    {
        Shader shader = transparent ? Shader.Find("Sprites/Default") :
            Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        generatedMaterials.Add(material);
        return material;
    }

    private void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    private void Notify(string message)
    {
        GameManager.Instance?.ShowMessage(message);
        Debug.Log("[ToiletPuzzle] " + message, this);
    }

    private void OnGUI()
    {
        if (!CanInteract() && !peeing) return;

        string hint = "";
        if (peeing)
        {
            if (!streamStarted)
                hint = "GETTING READY  |  PEE STARTING...";
            else
            {
                int hitPercent = Mathf.RoundToInt(
                    100f * toiletHitTimeThisUse /
                    Mathf.Max(0.0001f, streamActiveTimeThisUse));
                int neededPercent = Mathf.RoundToInt(requiredToiletHitFraction * 100f);
                string hitLabel = streamHittingToilet
                    ? (assistedBowlAim ? "IN THE BOWL" : "HITTING TOILET")
                    : "MISSED TOILET";
                hint = hitLabel + "  |  " + hitPercent +
                       "% ON TARGET (NEED " + neededPercent + "%)";
            }
        }
        else if (TryGetLookHit(out RaycastHit hit, interactionDistance) &&
                 IsVialHit(hit.collider))
        {
            hint = "E / Left Click  -  Take sealed vial";
        }
        else if (TryGetLookHit(out hit, playerCamera.farClipPlane) &&
                 IsToiletHit(hit.collider))
        {
            hint = "Left Click  -  Use toilet";
        }

        if (hint.Length == 0) return;
        float width = 430f;
        GUI.Box(new Rect((Screen.width - width) / 2f,
                         Screen.height * 0.74f, width, 30f), hint);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        FinishUseAnimation();
    }

    private void OnDestroy()
    {
        if (stream != null) Destroy(stream.gameObject);
        if (streamHighlight != null) Destroy(streamHighlight.gameObject);
        if (ripple != null) Destroy(ripple.gameObject);
        if (particleRoot != null) Destroy(particleRoot.gameObject);
        if (runtimeWater != null) Destroy(runtimeWater);
        if (runtimeWaterMesh != null) Destroy(runtimeWaterMesh);
        if (floatingVial != null) Destroy(floatingVial.gameObject);
        foreach (var material in generatedMaterials)
            if (material != null) Destroy(material);
    }
}
