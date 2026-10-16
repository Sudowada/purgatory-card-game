using UnityEngine;

/// <summary>Independent effects follow the visual pose, never its burn scale or materials.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Card3DInteractable))]
public sealed class CardAttackVisuals : MonoBehaviour
{
    [Header("Card Anchors")]
    [SerializeField] private Transform cardVisualRoot;
    [SerializeField] private Transform attackAnchor;
    [SerializeField] private Transform shieldAnchor;
    [Header("Art Slots (Prefabs)")]
    [SerializeField] private GameObject attackIndicatorPrefab;
    [SerializeField] private GameObject shieldImpactPrefab;
    [Header("Offsets (local to moving card visual)")]
    [SerializeField] private Vector3 attackOffset = new Vector3(0f, 0.015f, 0.07f);
    [Tooltip("Moves attack indicators toward the active camera so card meshes cannot hide them.")]
    [SerializeField, Min(0f)] private float attackViewOffset = 0.005f;
    [SerializeField] private Vector3 shieldOffset = new Vector3(0f, 0.003f, 0f);
    [Header("Attack Float")]
    [SerializeField, Min(0f)] private float bobHeight = 0.003f;
    [SerializeField, Min(0f)] private float bobSpeed = 2.5f;
    [Header("Shield Tempo (seconds)")]
    [SerializeField, Min(0.01f)] private float shieldDuration = 0.35f;
    [SerializeField, Min(0.01f)] private float shieldRiseDuration = 0.07f;
    [SerializeField, Min(0.01f)] private float shieldExitDuration = 0.12f;
    [SerializeField, Min(0.01f)] private float undoExitDuration = 0.08f;
    [SerializeField, Min(1f)] private float shieldPopScale = 1.12f;

    private GameObject attackInstance, shieldInstance;
    private Vector3 attackScale, shieldScale;
    private float shieldStart, shieldExitStart, exitLength, exitScale;
    private bool shieldExiting, burning;
    private bool presentationVisible = true;
    private Card3DInteractable card;
    private static Sprite attackSprite, shieldSprite;
    private string intentText;
    public void SetIntent(string text)
    {
        intentText = text;
        if (attackInstance == null) return;
        var label = attackInstance.GetComponentInChildren<TextMesh>();
        if (label == null)
        {
            var owner = new GameObject("Attack Intent");
            owner.transform.SetParent(attackInstance.transform, false);
            owner.transform.localPosition = new Vector3(0f, 0.035f, 0f);
            label = owner.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 32;
            label.characterSize = 0.003f;
            label.color = Color.white;
        }
        label.text = text;
    }

    private void Awake() => card = GetComponent<Card3DInteractable>();
    private Transform Root => cardVisualRoot != null ? cardVisualRoot :
        (card != null && card.VisualRoot != null ? card.VisualRoot : transform);

    // Keep effects outside the cloned/shrinking visual hierarchy and outside its material cache.
    private GameObject CreateEffect(GameObject prefab, bool shield)
    {
        GameObject effect;
        if (prefab != null) effect = Instantiate(prefab, transform, false);
        else
        {
            effect = new GameObject(shield ? "Pixel Shield Placeholder" : "Pixel Attack Placeholder");
            effect.transform.SetParent(transform, false);
            var renderer = effect.AddComponent<SpriteRenderer>();
            renderer.sprite = GetPlaceholder(shield);
            renderer.color = shield ? new Color(0.4f, 0.85f, 1f) : new Color(1f, 0.35f, 0.15f);
            renderer.sortingOrder = 20;
        }
        // Art must never intercept card or preview raycasts.
        foreach (var collider in effect.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var collider in effect.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
        foreach (var renderer in effect.GetComponentsInChildren<Renderer>(true))
            renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, 30);
        effect.SetActive(presentationVisible);
        return effect;
    }

    public void SetPresentationVisible(bool visible)
    {
        presentationVisible = visible;
        if (attackInstance != null) attackInstance.SetActive(visible);
        if (shieldInstance != null) shieldInstance.SetActive(visible);
    }

    public void SetTargeted(bool targeted)
    {
        if (!targeted)
        {
            if (attackInstance != null) { attackInstance.SetActive(false); Destroy(attackInstance); }
            attackInstance = null;
        }
        else if (attackInstance == null)
        {
            attackInstance = CreateEffect(attackIndicatorPrefab, false);
            attackScale = attackInstance.transform.localScale;
            Follow(attackInstance, attackAnchor, attackOffset, attackScale, true);
            if (!string.IsNullOrEmpty(intentText)) SetIntent(intentText);
        }
    }

    public void PlayShieldBlock()
    {
        SetTargeted(false);
        if (shieldInstance != null) { shieldInstance.SetActive(false); Destroy(shieldInstance); }
        shieldInstance = CreateEffect(shieldImpactPrefab, true);
        shieldScale = shieldInstance.transform.localScale;
        shieldStart = Time.time;
        shieldExiting = false;
        Follow(shieldInstance, shieldAnchor, shieldOffset, shieldScale * 0.65f);
    }

    public void SetBurning(bool value) => burning = value;

    public void CancelShieldBlock()
    {
        burning = false;
        if (shieldInstance != null && !shieldExiting) BeginExit(undoExitDuration);
    }

    private float EntryScale()
    {
        float t = Mathf.Clamp01((Time.time - shieldStart) / Mathf.Max(0.01f, shieldRiseDuration));
        if (t < 0.65f) return Mathf.Lerp(0.65f, shieldPopScale, Mathf.SmoothStep(0f, 1f, t / 0.65f));
        return Mathf.Lerp(shieldPopScale, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.65f) / 0.35f));
    }

    private void BeginExit(float duration)
    {
        shieldExiting = true;
        shieldExitStart = Time.time;
        exitLength = Mathf.Max(0.01f, duration);
        exitScale = EntryScale();
    }

    private void Follow(
        GameObject effect,
        Transform anchor,
        Vector3 offset,
        Vector3 scale,
        bool keepInFrontOfCamera = false)
    {
        Transform root = Root;
        Vector3 safeOffset = offset;
        if (keepInFrontOfCamera)
            safeOffset.z = Mathf.Min(safeOffset.z, -0.004f);

        effect.transform.position = anchor != null ? anchor.position : root.TransformPoint(safeOffset);
        effect.transform.rotation = anchor != null ? anchor.rotation : root.rotation;
        if (effect.GetComponent<SpriteRenderer>() != null)
            effect.transform.rotation *= Quaternion.Euler(90f, 0f, 0f);

        if (keepInFrontOfCamera && Camera.main != null)
            effect.transform.position -= Camera.main.transform.forward * attackViewOffset;

        effect.transform.localScale = scale;
    }

    private void FaceIntentLabelToCamera()
    {
        if (attackInstance == null || Camera.main == null)
            return;

        TextMesh label = attackInstance.GetComponentInChildren<TextMesh>();
        if (label == null)
            return;

        Vector3 towardCamera = Camera.main.transform.position - label.transform.position;
        if (towardCamera.sqrMagnitude > 0.000001f)
            label.transform.rotation = Quaternion.LookRotation(
                towardCamera.normalized,
                Camera.main.transform.up);
    }

    private void LateUpdate()
    {
        if (attackInstance != null)
        {
            Follow(attackInstance, attackAnchor, attackOffset, attackScale, true);
            attackInstance.transform.position += Root.up *
                (Mathf.Sin(Time.time * bobSpeed * Mathf.PI * 2f) * bobHeight);
            FaceIntentLabelToCamera();
        }
        if (shieldInstance == null) return;
        if (!shieldExiting && !burning && Time.time >= shieldStart + shieldDuration)
            BeginExit(shieldExitDuration);
        float scale = EntryScale();
        if (shieldExiting)
        {
            float t = Mathf.Clamp01((Time.time - shieldExitStart) / exitLength);
            scale = exitScale * (1f - Mathf.SmoothStep(0f, 1f, t));
            if (t >= 1f) { Destroy(shieldInstance); shieldInstance = null; return; }
        }
        Follow(shieldInstance, shieldAnchor, shieldOffset, shieldScale * scale);
    }

    private static Sprite GetPlaceholder(bool shield)
    {
        if (shield ? shieldSprite != null : attackSprite != null) return shield ? shieldSprite : attackSprite;
        string[] rows = shield
            ? new[] { "011111110", "111111111", "110000011", "110000011", "110000011", "011000110", "001101100", "000111000", "000010000" }
            : new[] { "000010000", "000111000", "001111100", "011111110", "000111000", "000111000", "000111000", "000111000", "000000000" };
        var texture = new Texture2D(9, 9, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < 9; y++)
            for (int x = 0; x < 9; x++) texture.SetPixel(x, 8 - y, rows[y][x] == '1' ? Color.white : Color.clear);
        texture.Apply();
        var sprite = Sprite.Create(texture, new Rect(0, 0, 9, 9), new Vector2(0.5f, 0.5f), 220f);
        if (shield) shieldSprite = sprite; else attackSprite = sprite;
        return sprite;
    }

    private void OnDisable()
    {
        SetTargeted(false);
        if (shieldInstance != null) Destroy(shieldInstance);
        shieldInstance = null;
        burning = false;
    }
}
