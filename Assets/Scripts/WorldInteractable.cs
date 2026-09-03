using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

/// <summary>
/// Shared base for every interactable object in the world — the deck,
/// notes, items, levers, whatever comes next. Handles:
///
///   1. Proximity-gated world-space icon: invisible until the player is
///      within detectionRadius, so the screen isn't cluttered with dots
///      at all times. Fades in/out smoothly with distance.
///   2. Type-differentiated icon shape: Play / Read / Take / Examine / Open
///      each render as a distinct placeholder silhouette (triangle,
///      document, diamond, magnifying glass, arrow) so HOW you interact
///      with something is legible before you've read a word of text.
///      Swap "customIcon" in the Inspector once you've designed real
///      TLOU-style icon art per category — no code changes needed then.
///   3. A small [E] key glyph that fades in directly BELOW the icon —
///      anchored to the object, not the screen — only when the player is
///      looking directly at it. Matches the reference composition: icon
///      above, action-button prompt below, both attached to the object.
///
/// The separate ScreenReticle (small always-on aiming dot at screen
/// center) is unrelated to this — it exists purely so the player knows
/// exactly where they're looking, independent of what's interactable.
///
/// USAGE: don't put this on objects directly for gameplay — subclass it
/// (see TableInteractable.cs for an example) and override OnInteract()
/// with what pressing E should actually do.
/// </summary>
public abstract class WorldInteractable : MonoBehaviour
{
    public enum InteractionType { Play, Read, Take, Examine, Open, Custom }

    [Header("Interaction")]
    public InteractionType interactionType = InteractionType.Examine;
    public float interactDistance = 3.5f;

    [Header("Discoverability Icon")]
    [Tooltip("How close the player needs to be before the world icon starts fading in. Icon is fully hidden outside this range.")]
    public float detectionRadius = 6f;
    [Tooltip("World-space point the icon hovers at. Leave null to auto-place above the object's bounds.")]
    public Transform iconAnchor;
    [Tooltip("Optional — assign your own icon sprite per interaction type once designed. Leave null to use the placeholder silhouette.")]
    public Sprite customIcon;
    public Color iconIdleColor = new Color(0.85f, 0.72f, 0.35f, 0.6f);
    public Color iconFocusedColor = new Color(1f, 0.92f, 0.6f, 0.95f);
    public float iconIdleSize = 0.05f;
    public float iconFocusedSize = 0.075f;

    [Header("Key Prompt (appears below icon when focused)")]
    public string interactKeyLabel = "E";
    public float keyPromptOffset = -0.035f; // local units below the icon

    protected Camera _cam;
    protected bool _isLooking = false;
    protected Transform _player;

    private GameObject _iconGO;
    private SpriteRenderer _iconSR;
    private GameObject _keyPromptGO;
    private TextMeshPro _keyPromptTMP;
    private float _keyPromptTargetAlpha;

    protected virtual void Awake()
    {
        _cam = Camera.main;
    }

    protected virtual void Start()
    {
        ScreenReticle.EnsureExists();
        BuildIcon();

        var playerGO = GameObject.FindGameObjectWithTag("Player");
        _player = playerGO != null ? playerGO.transform : (_cam != null ? _cam.transform : null);
    }

    protected virtual void Update()
    {
        if (GameStateManager.Instance == null) return;
        if (GameStateManager.Instance.CurrentState != GameState.Exploration)
        {
            SetIconVisible(false);
            return;
        }

        CheckLook();
        UpdateIcon();

        var kb = Keyboard.current;
        if (_isLooking && kb != null && kb.eKey.wasPressedThisFrame)
            DoInteract();
    }

    void CheckLook()
    {
        if (_cam == null) { _cam = Camera.main; return; }
        Ray ray = _cam.ScreenPointToRay(new Vector2(Screen.width / 2f, Screen.height / 2f));
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            bool hitting = hit.transform == transform || hit.transform.IsChildOf(transform);
            _isLooking = hitting;
        }
        else
        {
            _isLooking = false;
        }
    }

    void DoInteract()
    {
        _isLooking = false;
        SetIconVisible(false);
        OnInteract();
    }

    /// <summary>Override this with what pressing E on this object should do.</summary>
    protected abstract void OnInteract();

    // ── Discoverability icon + key prompt ────────────────────────────────────

    void BuildIcon()
    {
        Vector3 worldPos;
        if (iconAnchor == null)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(transform.position, Vector3.one);
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            worldPos = new Vector3(bounds.center.x, bounds.max.y + 0.18f, bounds.center.z);
        }
        else
        {
            worldPos = iconAnchor.position;
        }

        _iconGO = new GameObject($"InteractIcon_{interactionType}");
        _iconGO.transform.position = worldPos;
        _iconSR = _iconGO.AddComponent<SpriteRenderer>();
        _iconSR.sprite = customIcon != null ? customIcon : GetPlaceholderIcon(interactionType);
        _iconSR.color = iconIdleColor;
        _iconGO.transform.localScale = Vector3.one * iconIdleSize;

        // Key prompt — child of the icon so it billboards and follows in sync.
        _keyPromptGO = new GameObject("KeyPrompt");
        _keyPromptGO.transform.SetParent(_iconGO.transform, false);
        _keyPromptGO.transform.localPosition = new Vector3(0f, keyPromptOffset / iconIdleSize, 0f);
        _keyPromptTMP = _keyPromptGO.AddComponent<TextMeshPro>();
        _keyPromptTMP.text = $"[{interactKeyLabel}]";
        _keyPromptTMP.fontSize = 3f;
        _keyPromptTMP.alignment = TextAlignmentOptions.Center;
        _keyPromptTMP.color = new Color(1f, 0.95f, 0.85f, 0f);
        _keyPromptTMP.fontStyle = FontStyles.Bold;
        _keyPromptTargetAlpha = 0f;
    }

    void UpdateIcon()
    {
        if (_iconGO == null) return;

        if (iconAnchor != null)
            _iconGO.transform.position = iconAnchor.position;

        if (_cam != null)
            _iconGO.transform.rotation = Quaternion.LookRotation(_iconGO.transform.position - _cam.transform.position);

        float dist = _player != null ? Vector3.Distance(_player.position, _iconGO.transform.position) : 0f;
        bool inRange = dist <= detectionRadius;

        float proximityAlpha = inRange
            ? Mathf.InverseLerp(detectionRadius, detectionRadius * 0.4f, dist) * 0.6f + 0.2f
            : 0f;
        float targetAlpha = _isLooking ? 1f : proximityAlpha;

        Color targetColor = _isLooking ? iconFocusedColor : iconIdleColor;
        targetColor.a = Mathf.Min(targetColor.a, targetAlpha);

        _iconSR.color = Color.Lerp(_iconSR.color, targetColor, Time.deltaTime * 8f);

        float targetScale = _isLooking ? iconFocusedSize : iconIdleSize;
        _iconGO.transform.localScale = Vector3.Lerp(_iconGO.transform.localScale, Vector3.one * targetScale, Time.deltaTime * 8f);

        // Key prompt only appears when directly focused, faded independently of the icon's own alpha.
        _keyPromptTargetAlpha = _isLooking ? 1f : 0f;
        var kc = _keyPromptTMP.color;
        kc.a = Mathf.Lerp(kc.a, _keyPromptTargetAlpha, Time.deltaTime * 10f);
        _keyPromptTMP.color = kc;
    }

    void SetIconVisible(bool visible)
    {
        if (_iconSR == null) return;
        var c = _iconSR.color;
        c.a = visible ? c.a : 0f;
        _iconSR.color = c;

        if (_keyPromptTMP == null) return;
        var kc = _keyPromptTMP.color;
        kc.a = visible ? kc.a : 0f;
        _keyPromptTMP.color = kc;
    }

    // ── Placeholder icon shapes (swap for real art via customIcon later) ────

    static Sprite _cachedPlay, _cachedRead, _cachedTake, _cachedExamine, _cachedOpen;

    static Sprite GetPlaceholderIcon(InteractionType type)
    {
        switch (type)
        {
            case InteractionType.Play:    return _cachedPlay    ??= DrawTriangle();
            case InteractionType.Read:    return _cachedRead    ??= DrawDocument();
            case InteractionType.Take:    return _cachedTake    ??= DrawDiamond();
            case InteractionType.Examine: return _cachedExamine ??= DrawMagnifier();
            case InteractionType.Open:    return _cachedOpen    ??= DrawArrow();
            default:                      return _cachedExamine ??= DrawMagnifier();
        }
    }

    static Texture2D NewTex(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var clear = new Color(0, 0, 0, 0);
        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
        tex.SetPixels(pixels);
        return tex;
    }

    static Sprite ToSprite(Texture2D tex)
    {
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite DrawTriangle()
    {
        int size = 64;
        var tex = NewTex(size);
        for (int y = 0; y < size; y++)
        {
            float t = y / (float)size;
            float halfWidth = t * (size * 0.42f);
            int cx = size / 2;
            for (int x = 0; x < size; x++)
            {
                if (Mathf.Abs(x - cx) < halfWidth)
                    tex.SetPixel(x, y, Color.white);
            }
        }
        return ToSprite(tex);
    }

    static Sprite DrawDocument()
    {
        int size = 64;
        var tex = NewTex(size);
        int left = 16, right = 48, top = 8, bottom = 56;
        for (int y = top; y < bottom; y++)
            for (int x = left; x < right; x++)
                tex.SetPixel(x, y, Color.white);

        Color clear = new Color(0, 0, 0, 0);
        for (int lineY = top + 10; lineY < bottom - 6; lineY += 10)
            for (int x = left + 6; x < right - 6; x++)
                for (int t = 0; t < 3; t++)
                    tex.SetPixel(x, lineY + t, clear);

        return ToSprite(tex);
    }

    static Sprite DrawDiamond()
    {
        int size = 64;
        var tex = NewTex(size);
        int cx = size / 2, cy = size / 2, r = size / 2 - 4;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                if (Mathf.Abs(x - cx) + Mathf.Abs(y - cy) < r)
                    tex.SetPixel(x, y, Color.white);
        return ToSprite(tex);
    }

    static Sprite DrawMagnifier()
    {
        int size = 64;
        var tex = NewTex(size);
        int cx = 26, cy = 38, r = 16;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (dist < r && dist > r - 5)
                    tex.SetPixel(x, y, Color.white);
            }
        }
        for (int i = 0; i < 18; i++)
        {
            int hx = cx + 12 + i;
            int hy = cy - 12 - i;
            for (int w = -2; w <= 2; w++)
                if (hx + w >= 0 && hx + w < size && hy >= 0 && hy < size)
                    tex.SetPixel(hx + w, hy, Color.white);
        }
        return ToSprite(tex);
    }

    static Sprite DrawArrow()
    {
        int size = 64;
        var tex = NewTex(size);
        int cx = size / 2;
        for (int y = 20; y < 44; y++)
            for (int x = cx - 4; x < cx + 4; x++)
                tex.SetPixel(x, y, Color.white);
        for (int y = 0; y < 24; y++)
        {
            float t = y / 24f;
            float halfWidth = (1f - t) * 16f;
            for (int x = 0; x < size; x++)
                if (Mathf.Abs(x - cx) < halfWidth)
                    tex.SetPixel(x, y, Color.white);
        }
        return ToSprite(tex);
    }
}

/// <summary>
/// A small, always-visible aiming dot fixed at screen center. Purely for
/// letting the player know exactly where they're looking — independent
/// of whether anything interactable is in view. No text, minimal reaction.
/// </summary>
public class ScreenReticle : MonoBehaviour
{
    public static ScreenReticle Instance { get; private set; }

    [Header("Dot")]
    [SerializeField] private float dotSize = 4f;
    [SerializeField] private Color dotColor = new Color(1f, 1f, 1f, 0.45f);

    public static void EnsureExists()
    {
        if (Instance != null) return;
        var go = new GameObject("ScreenReticle");
        go.AddComponent<ScreenReticle>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Build();
    }

    void Build()
    {
        var canvasGO = new GameObject("ReticleCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        canvasGO.AddComponent<CanvasScaler>();

        var dotGO = new GameObject("Dot");
        dotGO.transform.SetParent(canvasGO.transform, false);
        var dotRT = dotGO.AddComponent<RectTransform>();
        dotRT.anchorMin = dotRT.anchorMax = dotRT.pivot = new Vector2(0.5f, 0.5f);
        dotRT.sizeDelta = new Vector2(dotSize, dotSize);
        var dotImage = dotGO.AddComponent<Image>();
        dotImage.color = dotColor;
        dotImage.raycastTarget = false;
    }

    /// <summary>Hide the reticle entirely, e.g. while in card mode with the cursor unlocked.</summary>
    public void SetVisible(bool visible)
    {
        var canvas = GetComponentInChildren<Canvas>();
        if (canvas != null) canvas.enabled = visible;
    }
}
