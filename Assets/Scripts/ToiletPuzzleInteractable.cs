using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// Free-aim toilet mini-game. Hold P while exploring to spray a gravity-driven
/// arc in the camera's aim direction, from anywhere in the room.
/// Only actual stream impacts inside the bowl fill it. Four completed fill
/// stages raise the water and reveal a clickable sealed vial.
/// Attach to Toilet root and put WaterAnchor inside the bowl.
///
/// The stream, placeholder water and placeholder vial are generated at runtime,
/// so this works before the final low-poly artwork/sounds are ready.
/// </summary>
[DisallowMultipleComponent]
public sealed class ToiletPuzzleInteractable : MonoBehaviour
{
    public const string VialCollectibleId = "collectible_toilet_vial";

    [Header("Interaction")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private FirstPersonController playerController;
    [Tooltip("Optional. When empty, ANY child collider of the toilet can be used.")]
    [SerializeField] private Collider toiletInteractionCollider;
    [Tooltip("Pickup distance for the surfaced vial; NOT a pee range limit.")]
    [SerializeField, Min(0.5f)] private float interactionDistance = 2.75f;

    [Header("Free-aim urination mini-game")]
    [Tooltip("Hold P to pee from anywhere during Exploration. Move the mouse normally to aim.")]
    [SerializeField, Min(0.1f)] private float secondsPerFillStage = 1.2f;
    [Tooltip("Initial stream speed in m/s. Increase for a flatter/longer arc.")]
    [SerializeField, Range(3f, 20f)] private float streamLaunchSpeed = 9.5f;
    [Tooltip("Downward acceleration in m/s². Higher gravity means aiming above the bowl from farther away.")]
    [SerializeField, Range(2f, 28f)] private float streamGravity = 13f;
    [Tooltip("Maximum time of flight represented by the visible stream, in seconds.")]
    [SerializeField, Range(0.5f, 3f)] private float streamFlightTime = 2f;
    [Tooltip("Effective bowl water ellipse radius on the local X axis, in world meters.")]
    [SerializeField, Range(0.04f, 0.35f)] private float bowlHitRadiusX = 0.13f;
    [Tooltip("Effective bowl water ellipse radius on the local Z axis, in world meters.")]
    [SerializeField, Range(0.04f, 0.35f)] private float bowlHitRadiusZ = 0.16f;

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
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip peeClip;
    [SerializeField] private AudioClip waterSound;
    [SerializeField] private AudioClip revealClip;
    [SerializeField] private AudioClip pickupClip;

    private readonly List<Material> generatedMaterials = new List<Material>();
    private const int StreamSegments = 48;
    private readonly RaycastHit[] obstacleHits = new RaycastHit[24];
    private LineRenderer stream;
    private LineRenderer ripple;
    private Transform floatingVial;
    private Collider floatingVialCollider;
    private GameObject runtimeWater;
    private Mesh runtimeWaterMesh;
    private Vector3 initialWaterPosition;
    private float currentWaterRise;
    private float targetWaterRise;
    private float elapsedVialRise;
    private float streamElapsed;
    private float accumulatedBowlHitTime;
    private Vector3 splashPosition;
    private bool streamInBowl;
    private bool peeing;
    private bool revealing;
    private bool collected;
    private int useCount;

    public int UseCount => useCount;
    public bool IsRevealed => revealing && !collected;
    public bool HasCollectedVial => collected;

    private void Start()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerController == null)
            playerController = FindFirstObjectByType<FirstPersonController>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

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

        if (!CanInteract())
        {
            StopStream();
            return;
        }

        var keys = Keyboard.current;
        var mouse = Mouse.current;
        bool holdingP = keys != null && keys.pKey.isPressed;
        if (holdingP)
        {
            if (!peeing) BeginStream();
            streamElapsed += Time.deltaTime;
            UpdateStreamVisuals();

            // Award fill only when the simulated arc physically hits the water.
            // The player can hold P for several stages or release and try again.
            if (streamInBowl && !collected && useCount < usesToReveal)
                AccumulateBowlHit(Time.deltaTime);
        }
        else
        {
            StopStream();
        }

        bool pressedE = keys != null && keys.eKey.wasPressedThisFrame;
        bool clicked = mouse != null && mouse.leftButton.wasPressedThisFrame;
        if ((!pressedE && !clicked) || !TryGetLookHit(out var hit)) return;
        if (IsVialHit(hit.collider)) CollectVial();
    }

    private void BeginStream()
    {
        peeing = true;
        streamElapsed = 0f;
        if (stream != null) stream.enabled = true;
        // Ripple appears only when the stream actually touches water.
        if (ripple != null) ripple.enabled = false;
        Play(peeClip);
    }

    private void StopStream()
    {
        if (!peeing) return;
        peeing = false;
        streamInBowl = false;
        if (stream != null) stream.enabled = false;
        if (ripple != null) ripple.enabled = false;
    }

    private void AccumulateBowlHit(float seconds)
    {
        accumulatedBowlHitTime += seconds;
        while (accumulatedBowlHitTime >= secondsPerFillStage && useCount < usesToReveal)
        {
            accumulatedBowlHitTime -= secondsPerFillStage;
            useCount++;
            Play(waterSound);
            targetWaterRise = totalWaterRise * useCount / Mathf.Max(1, usesToReveal);

            if (useCount == 2)
                Notify("The water isn't draining...");
            else if (useCount == usesToReveal - 1 && usesToReveal > 2)
                Notify("Something rattles beneath the water.");

            if (!revealing && useCount >= usesToReveal)
                StartCoroutine(RevealWhenWaterSettles());
        }
    }

    private bool CanInteract()
    {
        if (playerCamera == null || waterSurface == null) return false;
        if (GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState != GameState.Exploration) return false;
        if (playerController != null && !playerController.enabled) return false;
        if (RadioInteractable.Active != null) return false;
        if (SettingsUI.Instance != null && SettingsUI.Instance.IsOpen) return false;
        if (GameManager.Instance != null &&
            (GameManager.Instance.IsShopOpen || GameManager.Instance.IsInputBlocked)) return false;
        if (Cursor.lockState != CursorLockMode.Locked) return false;
        return true;
    }

    private bool TryGetLookHit(out RaycastHit hit)
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        return Physics.Raycast(ray, out hit, interactionDistance,
                               Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }

    private bool IsToiletHit(Collider hit)
    {
        if (hit == null || (floatingVial != null && hit.transform.IsChildOf(floatingVial)))
            return false;
        return toiletInteractionCollider != null
            ? (hit == toiletInteractionCollider ||
               hit.transform.IsChildOf(toiletInteractionCollider.transform))
            : hit.transform == transform || hit.transform.IsChildOf(transform);
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
        GameObject go = new GameObject("ToiletPeeStream_Runtime");
        go.transform.SetParent(transform, true);
        stream = go.AddComponent<LineRenderer>();
        stream.useWorldSpace = true;
        stream.positionCount = 16;
        stream.widthMultiplier = streamWidth;
        stream.widthCurve = AnimationCurve.Linear(0f, 0.4f, 1f, 0.7f);
        stream.numCornerVertices = 0;
        stream.numCapVertices = 0;
        stream.shadowCastingMode = ShadowCastingMode.Off;
        stream.receiveShadows = false;
        stream.sharedMaterial = streamMaterial != null ? streamMaterial :
            MakeMaterial(new Color(0.78f, 0.72f, 0.34f, 0.92f), true);
        stream.enabled = false;

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
        ripple.sharedMaterial = stream.sharedMaterial;
        ripple.enabled = false;
    }

    /// <summary>
    /// Visual and scoring use the SAME simulated trajectory. Aim is the normal
    /// first-person mouse look. Gravity makes distant targets require aiming
    /// above the toilet; the stream never auto-targets the bowl.
    /// </summary>
    private void UpdateStreamVisuals()
    {
        if (stream == null || waterSurface == null || playerCamera == null) return;

        // A slight offset below/right of the camera suggests a first-person
        // source without requiring a full character animation.
        Vector3 origin = playerCamera.ViewportToWorldPoint(new Vector3(0.55f, 0.15f, 0.34f));
        Vector3 velocity = playerCamera.transform.forward * streamLaunchSpeed;
        float step = streamFlightTime / (StreamSegments - 1f);
        streamInBowl = false;
        if (ripple != null) ripple.enabled = false;

        Vector3 previous = origin;
        stream.positionCount = 1;
        stream.SetPosition(0, origin);
        int count = 1;

        for (int i = 1; i < StreamSegments; i++)
        {
            float t = step * i;
            Vector3 next = origin + velocity * t + Vector3.down * (0.5f * streamGravity * t * t);
            if (TryFindStreamImpact(previous, next, out Vector3 impact, out bool landedInBowl))
            {
                stream.positionCount = count + 1;
                stream.SetPosition(count, impact);
                count++;
                streamInBowl = landedInBowl;
                splashPosition = impact;
                break;
            }

            // Fine visual shimmer without steering the physically scored arc.
            Vector3 visualPoint = next +
                playerCamera.transform.right * (Mathf.Sin(t * 30f - streamElapsed * 24f)
                * 0.002f * Mathf.Sin(Mathf.Min(1f, t / 0.15f) * Mathf.PI));
            stream.positionCount = count + 1;
            stream.SetPosition(count, visualPoint);
            count++;
            previous = next;
            if (next.y < -2f) break;
        }

        if (streamInBowl && ripple != null)
        {
            ripple.enabled = true;
            float phase = (streamElapsed % 0.43f) / 0.43f;
            float radius = Mathf.Lerp(0.014f, 0.055f, phase);
            Vector3 center = splashPosition + Vector3.up * 0.004f;
            for (int i = 0; i < ripple.positionCount; i++)
            {
                float angle = 2f * Mathf.PI * i / ripple.positionCount;
                ripple.SetPosition(i, center +
                    new Vector3(Mathf.Cos(angle) * radius, 0f,
                                Mathf.Sin(angle) * radius));
            }
            ripple.widthMultiplier = 0.006f * (1f - phase * 0.75f);
        }
    }

    /// <summary>
    /// Check the next piece of the parabolic stream against the elliptical
    /// water surface and the nearest scene collider. A wall or rim in front
    /// of the water prevents a hit, so you can't pee through geometry.
    /// </summary>
    private bool TryFindStreamImpact(Vector3 from, Vector3 to,
        out Vector3 impact, out bool isWater)
    {
        impact = default;
        isWater = false;
        Vector3 travel = to - from;
        float length = travel.magnitude;
        if (length <= 0.0001f) return false;

        float waterDistance = float.PositiveInfinity;
        Vector3 waterContact = default;
        float level = waterSurface.position.y;
        // Only score the downward part of the arc crossing the water.
        if (from.y >= level && to.y <= level && from.y > to.y)
        {
            float fraction = Mathf.Clamp01((from.y - level) / (from.y - to.y));
            Vector3 candidate = Vector3.Lerp(from, to, fraction);
            Vector3 flat = Quaternion.Inverse(waterSurface.rotation) *
                           (candidate - waterSurface.position);
            float x = flat.x / Mathf.Max(0.001f, bowlHitRadiusX);
            float z = flat.z / Mathf.Max(0.001f, bowlHitRadiusZ);
            if (x * x + z * z <= 1f)
            {
                waterDistance = length * fraction;
                waterContact = candidate;
            }
        }

        float obstacleDistance = float.PositiveInfinity;
        Vector3 obstacleContact = default;
        int found = Physics.RaycastNonAlloc(from, travel / length, obstacleHits,
            length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < found; i++)
        {
            Collider collider = obstacleHits[i].collider;
            if (collider == null) continue;
            // Do not let the player's own collision volume intercept the stream.
            if (playerController != null &&
                collider.transform.IsChildOf(playerController.transform)) continue;
            // The water mesh may be custom; its surface is handled above rather
            // than treated as an obstacle.
            if (collider.transform == waterSurface ||
                collider.transform.IsChildOf(waterSurface)) continue;
            if (obstacleHits[i].distance >= obstacleDistance) continue;
            obstacleDistance = obstacleHits[i].distance;
            obstacleContact = obstacleHits[i].point;
        }

        if (waterDistance < obstacleDistance && !float.IsPositiveInfinity(waterDistance))
        {
            isWater = true;
            impact = waterContact;
            return true;
        }
        if (!float.IsPositiveInfinity(obstacleDistance))
        {
            impact = obstacleContact;
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
        if (!CanInteract()) return;

        string hint = "";
        if (peeing)
        {
            hint = streamInBowl
                ? "IN THE BOWL!  Fill " + useCount + "/" + usesToReveal +
                  "  (hold P and keep aiming)"
                : "MISS  -  Move mouse to aim; aim higher from farther away";
        }
        else
        {
            // Show the prompt anywhere the toilet is visible, not just when
            // standing next to it. Pressing P works without a hit raycast.
            Vector3 viewport = playerCamera.WorldToViewportPoint(waterSurface.position);
            if (viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f &&
                viewport.y >= 0f && viewport.y <= 1f)
                hint = "Hold P - Pee | Move mouse to aim (gravity affects the arc)";
        }

        if (!peeing && TryGetLookHit(out RaycastHit hit) && IsVialHit(hit.collider))
            hint = "E / Left Click  -  Take sealed vial";

        if (hint.Length == 0) return;
        float width = 430f;
        GUI.Box(new Rect((Screen.width - width) / 2f, Screen.height * 0.74f,
                         width, 30f), hint);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        StopStream();
    }

    private void OnDestroy()
    {
        if (stream != null) Destroy(stream.gameObject);
        if (ripple != null) Destroy(ripple.gameObject);
        if (runtimeWater != null) Destroy(runtimeWater);
        if (runtimeWaterMesh != null) Destroy(runtimeWaterMesh);
        if (floatingVial != null) Destroy(floatingVial.gameObject);
        foreach (var material in generatedMaterials)
            if (material != null) Destroy(material);
    }
}
