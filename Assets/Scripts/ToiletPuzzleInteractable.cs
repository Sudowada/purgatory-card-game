using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// Click-to-use prison toilet puzzle. In Exploration, left-click the toilet
/// from anywhere it is visible. A brief first-person use animation and pee
/// stream start together; each completed use raises the water one stage.
/// After four uses a sealed vial surfaces. Attach to the Toilet root and
/// place WaterAnchor inside the bowl.
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
    [Tooltip("Add non-trigger MeshColliders to imported toilet meshes that don't already have a collider. Allows clicking the toilet directly, as with the radio.")]
    [SerializeField] private bool autoAddClickColliders = true;
    [Tooltip("Print diagnostic click information to the Console during Play Mode.")]
    [SerializeField] private bool debugClicks;
    [Tooltip("Only for collecting the vial. Clicking the toilet has no distance limit.")]
    [SerializeField, Min(0.5f)] private float interactionDistance = 2.75f;

    [Header("Use animation")]
    [SerializeField, Min(0.3f)] private float peeDuration = 1.6f;
    [Tooltip("Optional Animator to trigger when use begins; the pee stream starts the same frame.")]
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
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip peeClip;
    [SerializeField] private AudioClip waterSound;
    [SerializeField] private AudioClip revealClip;
    [SerializeField] private AudioClip pickupClip;

    private readonly List<Material> generatedMaterials = new List<Material>();
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
    private bool peeing;
    private bool cameraPoseSaved;
    private bool previousControllerEnabled;
    private Vector3 cameraLocalStartPosition;
    private Quaternion cameraLocalStartRotation;
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
        if (autoAddClickColliders) EnsureToiletClickColliders();

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

        // The coroutine owns the camera and stream during the use animation.
        if (peeing || !CanInteract()) return;

        var mouse = Mouse.current;
        var keys = Keyboard.current;
        bool clicked = mouse != null && mouse.leftButton.wasPressedThisFrame;
        bool pressedE = keys != null && keys.eKey.wasPressedThisFrame;
        if (!clicked && !pressedE) return;

        // Same raycast strategy as RadioInteractable: center-screen when
        // locked, otherwise the actual mouse pointer. Click a toilet mesh to
        // start the animation, regardless of distance (within camera range).
        if (clicked && TryGetLookHit(out RaycastHit lookHit, playerCamera.farClipPlane))
        {
            if (IsToiletHit(lookHit.collider))
            {
                if (debugClicks) Debug.Log("[ToiletPuzzle] Toilet clicked: " + lookHit.collider.name, this);
                StartCoroutine(UseToilet());
                return;
            }
            if (debugClicks)
                Debug.Log("[ToiletPuzzle] Click hit something else: " + lookHit.collider.name, this);
        }
        else if (clicked && debugClicks)
        {
            Debug.Log("[ToiletPuzzle] Click raycast hit no collider. Check toilet colliders and camera.", this);
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
        cameraLocalStartPosition = playerCamera.transform.localPosition;
        cameraLocalStartRotation = playerCamera.transform.localRotation;
        cameraPoseSaved = true;
        previousControllerEnabled = playerController != null && playerController.enabled;
        if (playerController != null) playerController.enabled = false;

        if (useAnimator != null && !string.IsNullOrEmpty(useAnimationTrigger))
            useAnimator.SetTrigger(useAnimationTrigger);
        BeginStream(); // Starts when animation begins, not when it finishes.

        float elapsed = 0f;
        bool completed = false;
        while (elapsed < peeDuration)
        {
            if (GameStateManager.Instance != null &&
                GameStateManager.Instance.CurrentState != GameState.Exploration)
                break;
            if (SettingsUI.Instance != null && SettingsUI.Instance.IsOpen)
                break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, peeDuration));
            // Subtle lean toward the bowl, then back. No animator required.
            float lean = Mathf.Sin(t * Mathf.PI);
            playerCamera.transform.localPosition = cameraLocalStartPosition +
                Vector3.down * cameraDip * lean;
            playerCamera.transform.localRotation = cameraLocalStartRotation *
                Quaternion.Euler(cameraTilt * lean, 0f, 0f);

            streamElapsed += Time.deltaTime;
            UpdateStreamVisuals();
            yield return null;
        }

        completed = elapsed >= peeDuration;
        FinishUseAnimation();

        if (!completed) yield break;
        Play(waterSound);
        useCount++;
        targetWaterRise = totalWaterRise * Mathf.Min(useCount, usesToReveal) /
                          Mathf.Max(1, usesToReveal);

        if (!collected && useCount == 2)
            Notify("The water isn't draining...");
        else if (!collected && useCount == usesToReveal - 1 && usesToReveal > 2)
            Notify("Something rattles beneath the water.");

        if (!collected && !revealing && useCount >= usesToReveal)
            StartCoroutine(RevealWhenWaterSettles());
    }

    private void BeginStream()
    {
        if (stream != null) stream.enabled = true;
        if (ripple != null) ripple.enabled = true;
        Play(peeClip);
        UpdateStreamVisuals();
    }

    private void FinishUseAnimation()
    {
        if (!peeing && !cameraPoseSaved) return;
        peeing = false;
        if (stream != null) stream.enabled = false;
        if (ripple != null) ripple.enabled = false;

        bool exploring = GameStateManager.Instance == null ||
                         GameStateManager.Instance.CurrentState == GameState.Exploration;
        if (cameraPoseSaved && exploring && playerCamera != null)
        {
            playerCamera.transform.localPosition = cameraLocalStartPosition;
            playerCamera.transform.localRotation = cameraLocalStartRotation;
        }
        if (playerController != null && exploring)
            playerController.enabled = previousControllerEnabled;
        cameraPoseSaved = false;
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
        // Like the radio, click input works with either a locked center
        // crosshair or an unlocked visible mouse cursor.
        return true;
    }

    private bool TryGetLookHit(out RaycastHit hit, float maxDistance)
    {
        // Follow RadioInteractable's ScreenPointToRay convention exactly:
        // first-person view uses the center crosshair; if the cursor is
        // unlocked, use its on-screen position instead.
        Vector2 point = Cursor.lockState == CursorLockMode.Locked ||
                        Mouse.current == null
            ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
            : Mouse.current.position.ReadValue();
        Ray ray = playerCamera.ScreenPointToRay(point);
        return Physics.Raycast(ray, out hit, maxDistance,
                               Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }

    private void EnsureToiletClickColliders()
    {
        // FBX meshes don't always have a collider automatically. Add one on
        // each porcelain/seat mesh so clicking any part behaves like the radio.
        // Never alter existing colliders or add to empty WaterAnchor objects.
        int added = 0;
        foreach (MeshFilter meshFilter in GetComponentsInChildren<MeshFilter>(true))
        {
            if (meshFilter.sharedMesh == null ||
                meshFilter.GetComponent<Collider>() != null) continue;
            MeshCollider collider = meshFilter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = meshFilter.sharedMesh;
            added++;
        }
        if (debugClicks)
            Debug.Log("[ToiletPuzzle] Generated " + added + " toilet click collider(s).", this);
    }

    private bool IsToiletHit(Collider hit)
    {
        if (hit == null || (floatingVial != null &&
                           hit.transform.IsChildOf(floatingVial))) return false;
        return hit.transform == transform || hit.transform.IsChildOf(transform);
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
    /// An automatically targeted, subtly wavy low-poly stream that ends
    /// inside the bowl. We no longer require the player to aim or hold a key.
    /// </summary>
    private void UpdateStreamVisuals()
    {
        if (stream == null || ripple == null || waterSurface == null || playerCamera == null)
            return;

        Vector3 start = playerCamera.ViewportToWorldPoint(
            new Vector3(0.58f, 0.16f, 0.34f));
        Vector3 end = waterSurface.position + Vector3.up * 0.007f;
        Vector3 control = Vector3.Lerp(start, end, 0.45f) + Vector3.up * 0.12f;
        Vector3 right = playerCamera.transform.right;

        for (int i = 0; i < stream.positionCount; i++)
        {
            float t = i / (float)(stream.positionCount - 1);
            float q = 1f - t;
            Vector3 point = q * q * start + 2f * q * t * control + t * t * end;
            point += right * (Mathf.Sin(t * 32f - streamElapsed * 25f) *
                     0.0025f * Mathf.Sin(t * Mathf.PI));
            stream.SetPosition(i, point);
        }

        float phase = (streamElapsed % 0.42f) / 0.42f;
        float radius = Mathf.Lerp(0.012f, 0.06f, phase);
        Vector3 center = end + Vector3.up * 0.004f;
        for (int i = 0; i < ripple.positionCount; i++)
        {
            float angle = 2f * Mathf.PI * i / ripple.positionCount;
            ripple.SetPosition(i, center + new Vector3(
                Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
        ripple.widthMultiplier = 0.005f * (1f - phase * 0.75f);
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
        if (peeing || !CanInteract()) return;

        string hint = "";
        if (TryGetLookHit(out RaycastHit hit, interactionDistance) &&
            IsVialHit(hit.collider))
            hint = "E / Left Click  -  Take sealed vial";
        else if (TryGetLookHit(out hit, playerCamera.farClipPlane) &&
                 IsToiletHit(hit.collider))
            hint = "Left Click  -  Use toilet";

        if (hint.Length == 0) return;
        float width = 320f;
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
        if (ripple != null) Destroy(ripple.gameObject);
        if (runtimeWater != null) Destroy(runtimeWater);
        if (runtimeWaterMesh != null) Destroy(runtimeWaterMesh);
        if (floatingVial != null) Destroy(floatingVial.gameObject);
        foreach (var material in generatedMaterials)
            if (material != null) Destroy(material);
    }
}
