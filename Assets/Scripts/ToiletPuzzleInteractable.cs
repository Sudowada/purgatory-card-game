using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// Four back-to-back uses of the toilet raise the water and reveal a clickable
/// sealed vial. Attach to the Toilet prefab root; place a WaterAnchor empty at
/// the intended starting water height inside the bowl.
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
    [SerializeField, Min(0.5f)] private float interactionDistance = 2.75f;
    [SerializeField, Min(0.3f)] private float peeDuration = 1.6f;

    [Header("Bowl water")]
    [Tooltip("Required: empty transform positioned at the center of the water, below the rim.")]
    [SerializeField] private Transform waterAnchor;
    [Tooltip("Optional: your own water mesh. A low-poly disk is generated when empty.")]
    [SerializeField] private Transform waterSurface;
    [Tooltip("Total world-space rise after all required uses. Keep below the bowl's rim.")]
    [SerializeField, Range(0.02f, 0.18f)] private float totalWaterRise = 0.10f;
    [SerializeField, Min(1)] private int usesToReveal = 4;
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
    private Vector3 initialWaterPosition;
    private float currentWaterRise;
    private float targetWaterRise;
    private float elapsedVialRise;
    private float streamElapsed;
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
        collected = (shop != null && shop.HasCollectible(VialCollectibleId)) ||
                    PlayerPrefs.GetInt("Purgatory.ToiletVial.v1", 0) == 1;
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

        if (peeing)
        {
            streamElapsed += Time.deltaTime;
            UpdateStreamVisuals();
            return;
        }

        if (!CanInteract()) return;

        var keys = Keyboard.current;
        var mouse = Mouse.current;
        bool pressedE = keys != null && keys.eKey.wasPressedThisFrame;
        bool clicked = mouse != null && mouse.leftButton.wasPressedThisFrame;
        if (!pressedE && !clicked) return;

        if (!TryGetLookHit(out var hit)) return;
        if (IsVialHit(hit.collider))
        {
            if (pressedE || clicked) CollectVial();
            return;
        }
        // Click is reserved for picking up the vial, never for spamming the toilet.
        if (pressedE && IsToiletHit(hit.collider))
            StartCoroutine(UseToilet());
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

    private IEnumerator UseToilet()
    {
        peeing = true;
        streamElapsed = 0f;
        if (stream != null) stream.enabled = true;
        if (ripple != null) ripple.enabled = true;
        Play(peeClip);

        float timer = 0f;
        while (timer < peeDuration)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (stream != null) stream.enabled = false;
        if (ripple != null) ripple.enabled = false;
        peeing = false;
        useCount++;
        Play(waterSound);

        targetWaterRise = totalWaterRise * Mathf.Min(useCount, usesToReveal) /
                          Mathf.Max(1, usesToReveal);

        // Show a small reaction, but don't require time or battles between uses.
        if (!collected && useCount == 2)
            Notify("The water isn't draining...");
        else if (!collected && useCount == usesToReveal - 1 && usesToReveal > 2)
            Notify("Something rattles beneath the water.");

        if (!revealing && !collected && useCount >= usesToReveal)
            StartCoroutine(RevealWhenWaterSettles());
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
        runtimeWater = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        runtimeWater.name = "ToiletWater_RuntimePlaceholder";
        runtimeWater.transform.SetParent(waterAnchor, false);
        runtimeWater.transform.localPosition = Vector3.zero;
        // Keep a ~30 x 36 cm surface even if the imported FBX root uses
        // a smaller Unity scale (such as 0.4).
        Vector3 scale = waterAnchor.lossyScale;
        runtimeWater.transform.localScale = new Vector3(
            0.30f / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
            0.006f / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
            0.36f / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
        var collider = runtimeWater.GetComponent<Collider>();
        if (collider != null) { collider.enabled = false; Destroy(collider); }
        var renderer = runtimeWater.GetComponent<Renderer>();
        renderer.sharedMaterial = waterMaterial != null ? waterMaterial :
            MakeMaterial(new Color(0.18f, 0.28f, 0.26f, 1f), false);
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

    private void UpdateStreamVisuals()
    {
        if (stream == null || waterSurface == null || playerCamera == null) return;
        Vector3 start = playerCamera.ViewportToWorldPoint(new Vector3(0.59f, 0.17f, 0.38f));
        Vector3 end = waterSurface.position + Vector3.up * 0.007f;
        Vector3 control = Vector3.Lerp(start, end, 0.46f) + Vector3.up * 0.10f;
        Vector3 right = playerCamera.transform.right;
        for (int i = 0; i < stream.positionCount; i++)
        {
            float t = i / (stream.positionCount - 1f);
            float q = 1f - t;
            Vector3 pos = q * q * start + 2f * q * t * control + t * t * end;
            pos += right * (Mathf.Sin(t * 29f - streamElapsed * 27f) *
                            0.003f * Mathf.Sin(t * Mathf.PI));
            stream.SetPosition(i, pos);
        }

        // A simple low-poly expanding ripple at the impact point.
        float phase = (streamElapsed % 0.42f) / 0.42f;
        float radius = Mathf.Lerp(0.014f, 0.060f, phase);
        Vector3 center = end + Vector3.up * 0.004f;
        for (int i = 0; i < 13; i++)
        {
            float angle = 2f * Mathf.PI * i / 13f;
            ripple.SetPosition(i, center +
                new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
        ripple.widthMultiplier = 0.005f * (1f - phase * 0.75f);
    }

    private Material MakeMaterial(Color color, bool transparent)
    {
        Shader shader = transparent ? Shader.Find("Sprites/Default") :
            Shader.Find("Universal Render Pipeline/Lit");
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
        if (peeing || !CanInteract() || !TryGetLookHit(out RaycastHit hit)) return;
        string hint = "";
        if (IsVialHit(hit.collider)) hint = "E / Left Click  -  Take sealed vial";
        else if (IsToiletHit(hit.collider)) hint = "E  -  Use toilet";
        if (hint.Length == 0) return;

        float width = 320f;
        GUI.Box(new Rect((Screen.width - width) / 2f, Screen.height * 0.74f,
                         width, 30f), hint);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        peeing = false;
        if (stream != null) stream.enabled = false;
        if (ripple != null) ripple.enabled = false;
    }

    private void OnDestroy()
    {
        if (stream != null) Destroy(stream.gameObject);
        if (ripple != null) Destroy(ripple.gameObject);
        if (runtimeWater != null) Destroy(runtimeWater);
        if (floatingVial != null) Destroy(floatingVial.gameObject);
        foreach (var material in generatedMaterials)
            if (material != null) Destroy(material);
    }
}
