using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;
using System.Collections;

/// <summary>
/// MainMenuController — Immersive dark-room main menu.
///
/// SCENE SETUP:
///   Create a new Scene called "MainMenu".
///   Add an empty GameObject called "MainMenu" and attach this script.
///   Assign the Inspector slots below with your assets when ready.
///   The script builds placeholder visuals automatically until you assign sprites/prefabs.
///
/// LAYOUT (all world-space on a 2D orthographic camera):
///   Centre     — Table with deck of cards (click to PLAY)
///   Top-right  — Electrical box (click to open Settings)
///   Left       — Record player (click+drag to adjust volume)
///   Right      — Candle (click+drag up/down for brightness)
///   Top-centre — Faint face in darkness (idle breathing animation)
///
/// INTERACTIONS:
///   Deck        → click → scene transition to game
///   Elec box    → click → settings panel slides in
///   Record      → click+drag left/right → volume
///   Candle      → click+drag up/down → brightness (affects scene lighting)
///   Face        → hover → very faint glow, occasional blink
/// </summary>
public class MainMenuController : MonoBehaviour
{
    // =========================================================================
    // INSPECTOR — drag your assets in here when ready
    // =========================================================================

    [Header("── Scene ───────────────────────────────────")]
    [Tooltip("Name of the game scene to load when Play is clicked")]
    public string gameSceneName = "AltarScene";

    [Tooltip("Main camera for this scene — set to Orthographic, Size ~5")]
    public Camera mainCamera;

    [Header("── 3D Sprite / Prefab Placeholders ─────────")]
    [Tooltip("Table surface sprite/prefab. Placeholder: flat dark rectangle.")]
    public GameObject tablePrefab;

    [Tooltip("Deck of cards sprite/prefab. Placeholder: stacked rectangles.")]
    public GameObject deckPrefab;

    [Tooltip("Record player sprite/prefab. Placeholder: circle with arm.")]
    public GameObject recordPlayerPrefab;

    [Tooltip("Candle sprite/prefab. Placeholder: thin rectangle with flame particle.")]
    public GameObject candlePrefab;

    [Tooltip("Electrical box sprite/prefab. Placeholder: square with panel lines.")]
    public GameObject electricalBoxPrefab;

    [Tooltip("Face sprite — very faint, partially transparent. Placeholder: oval.")]
    public GameObject facePrefab;

    [Header("── Lighting ─────────────────────────────────")]
    [Tooltip("Point light over the table (candle light). Leave null to auto-create.")]
    public Light candleLight;

    [Tooltip("Ambient light level at minimum brightness (0–1)")]
    [Range(0f, 0.5f)] public float minAmbient = 0.02f;

    [Tooltip("Ambient light level at maximum brightness (0–1)")]
    [Range(0f, 1f)]   public float maxAmbient = 0.18f;

    [Header("── Audio ───────────────────────────────────")]
    [Tooltip("Background ambience clip (wind, distant dripping, etc.)")]
    public AudioClip ambienceClip;

    [Tooltip("Card shuffle/hover sound")]
    public AudioClip cardHoverClip;

    [Tooltip("Card click/deal sound")]
    public AudioClip cardClickClip;

    [Tooltip("Record player crackle/scratch sound")]
    public AudioClip recordScratchClip;

    [Tooltip("Electrical hum / spark sound")]
    public AudioClip electricalHumClip;

    [Tooltip("Candle flicker sound")]
    public AudioClip candleFlickerClip;

    // =========================================================================
    // PRIVATE STATE
    // =========================================================================

    // Scene objects
    private GameObject _table;
    private GameObject _deck;
    private GameObject _recordPlayer;
    private GameObject _candle;
    private GameObject _electricalBox;
    private GameObject _face;

    // Placeholder visuals (used when prefabs are null)
    private SpriteRenderer _deckRenderer;
    private SpriteRenderer _candleFlame;
    private ParticleSystem _candleParticles;
    private Light          _candlePointLight;

    // Record player state
    private bool  _draggingRecord = false;
    private float _recordDragStartX;
    private float _recordArmStartAngle;
    private float _masterVolume = 1f;

    // Candle state
    private bool  _draggingCandle = false;
    private float _candleDragStartY;
    private float _brightness = 1f;

    // Deck animation
    private bool _deckHovered = false;
    private Coroutine _deckIdleCoroutine;

    // Face animation
    private Coroutine _faceCoroutine;

    // UI Canvas
    private Canvas           _canvas;
    private GameObject       _settingsPanel;
    private bool             _settingsOpen = false;
    private CanvasGroup      _settingsCG;

    // Audio
    private AudioSource _ambienceSource;
    private AudioSource _sfxSource;

    // Palette
    static readonly Color c_blood    = new Color(0.55f, 0.05f, 0.04f);
    static readonly Color c_gold     = new Color(0.78f, 0.60f, 0.12f);
    static readonly Color c_parch    = new Color(0.82f, 0.73f, 0.52f);
    static readonly Color c_darkRoom = new Color(0.04f, 0.03f, 0.02f);
    static readonly Color c_ironDark = new Color(0.11f, 0.10f, 0.09f);

    // =========================================================================
    // LIFECYCLE
    // =========================================================================

    void Start()
    {
        SetupCamera();
        SetupLighting();
        SetupAudio();
        BuildScene();
        BuildUI();
        StartCoroutine(IntroSequence());
    }

    void Update()
    {
        HandleRecordDrag();
        HandleCandleDrag();
        HandleHover();
    }

    // =========================================================================
    // SETUP
    // =========================================================================

    void SetupCamera()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null)
        {
            var camGO = new GameObject("MainCamera");
            mainCamera = camGO.AddComponent<Camera>();
            camGO.tag = "MainCamera";
        }
        mainCamera.orthographic     = true;
        mainCamera.orthographicSize = 5f;
        mainCamera.transform.position = new Vector3(0f, 0f, -10f);
        mainCamera.backgroundColor  = c_darkRoom;
        mainCamera.clearFlags       = CameraClearFlags.SolidColor;
    }

    void SetupLighting()
    {
        // Kill default directional light — this scene is dark
        var dirLight = FindFirstObjectByType<Light>();
        if (dirLight != null && dirLight.type == LightType.Directional)
            dirLight.intensity = 0.02f;

        RenderSettings.ambientMode      = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientIntensity = minAmbient;
        RenderSettings.ambientLight     = new Color(0.06f, 0.04f, 0.03f);

        // Candle point light
        if (candleLight == null)
        {
            var lightGO = new GameObject("CandleLight");
            lightGO.transform.position = new Vector3(2.2f, -0.8f, -1f);
            candleLight = lightGO.AddComponent<Light>();
        }
        candleLight.type      = LightType.Point;
        candleLight.color     = new Color(1f, 0.72f, 0.35f);
        candleLight.intensity = 1.4f;
        candleLight.range     = 7f;
        _candlePointLight     = candleLight;

        // Subtle flicker coroutine
        StartCoroutine(CandleFlicker());
    }

    void SetupAudio()
    {
        _ambienceSource = gameObject.AddComponent<AudioSource>();
        _ambienceSource.loop         = true;
        _ambienceSource.volume       = 0.35f;
        _ambienceSource.spatialBlend = 0f;
        if (ambienceClip != null)
        {
            _ambienceSource.clip = ambienceClip;
            _ambienceSource.Play();
        }

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.spatialBlend = 0f;
    }

    // =========================================================================
    // SCENE BUILDING
    // =========================================================================

    void BuildScene()
    {
        BuildTable();
        BuildDeck();
        BuildRecordPlayer();
        BuildCandle();
        BuildElectricalBox();
        BuildFace();
    }

    void BuildTable()
    {
        _table = tablePrefab != null
            ? Instantiate(tablePrefab, new Vector3(0f, -3.0f, 0f), Quaternion.identity)
            : CreatePlaceholderTable();
        _table.name = "Table";
    }

    void BuildDeck()
    {
        _deck = deckPrefab != null
            ? Instantiate(deckPrefab, new Vector3(0f, -1.2f, -0.1f), Quaternion.identity)
            : CreatePlaceholderDeck();
        _deck.name = "Deck";

        // Collider for click detection
        if (_deck.GetComponent<Collider2D>() == null)
        {
            var col = _deck.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.2f, 1.8f);
        }

        // Idle float animation
        _deckIdleCoroutine = StartCoroutine(DeckIdleFloat());
    }

    void BuildRecordPlayer()
    {
        _recordPlayer = recordPlayerPrefab != null
            ? Instantiate(recordPlayerPrefab, new Vector3(-3.8f, -1.6f, -0.1f), Quaternion.identity)
            : CreatePlaceholderRecord();
        _recordPlayer.name = "RecordPlayer";

        if (_recordPlayer.GetComponent<Collider2D>() == null)
        {
            var col = _recordPlayer.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.8f, 1.4f);
        }

        // Slow record spin
        StartCoroutine(RecordSpin());
    }

    void BuildCandle()
    {
        _candle = candlePrefab != null
            ? Instantiate(candlePrefab, new Vector3(2.2f, -1.2f, -0.1f), Quaternion.identity)
            : CreatePlaceholderCandle();
        _candle.name = "Candle";

        if (_candle.GetComponent<Collider2D>() == null)
        {
            var col = _candle.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.5f, 1.4f);
        }
    }

    void BuildElectricalBox()
    {
        _electricalBox = electricalBoxPrefab != null
            ? Instantiate(electricalBoxPrefab, new Vector3(4.2f, 2.8f, -0.1f), Quaternion.identity)
            : CreatePlaceholderElecBox();
        _electricalBox.name = "ElectricalBox";

        if (_electricalBox.GetComponent<Collider2D>() == null)
        {
            var col = _electricalBox.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.4f, 1.8f);
        }
    }

    void BuildFace()
    {
        _face = facePrefab != null
            ? Instantiate(facePrefab, new Vector3(0f, 4.0f, 0.5f), Quaternion.identity)
            : CreatePlaceholderFace();
        _face.name = "Face";

        // Start very invisible
        var sr = _face.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = new Color(1f, 1f, 1f, 0.02f);

        _faceCoroutine = StartCoroutine(FaceIdleBreathe());
    }

    // =========================================================================
    // PLACEHOLDER VISUALS
    // These are automatically replaced when you assign real prefabs in Inspector.
    // =========================================================================

    GameObject CreatePlaceholderTable()
    {
        var go  = new GameObject("Table_Placeholder");
        var sr  = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateRect(7f, 1.2f, new Color(0.12f, 0.08f, 0.05f));
        sr.sortingOrder = 0;
        go.transform.position = new Vector3(0f, -3.0f, 0f);

        // Table edge highlight
        var edge = new GameObject("Edge");
        edge.transform.SetParent(go.transform, false);
        var esr = edge.AddComponent<SpriteRenderer>();
        esr.sprite = CreateRect(7f, 0.06f, new Color(0.28f, 0.18f, 0.08f));
        esr.sortingOrder = 1;
        edge.transform.localPosition = new Vector3(0f, 0.57f, 0f);
        return go;
    }

    GameObject CreatePlaceholderDeck()
    {
        var go  = new GameObject("Deck_Placeholder");
        go.transform.position = new Vector3(0f, -1.2f, -0.1f);

        // Stack of card sprites — offset slightly for depth
        for (int i = 0; i < 8; i++)
        {
            var card = new GameObject($"Card_{i}");
            card.transform.SetParent(go.transform, false);
            card.transform.localPosition = new Vector3(
                Random.Range(-0.01f, 0.01f),
                i * 0.018f,
                -i * 0.001f);
            card.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-0.8f, 0.8f));

            var sr = card.AddComponent<SpriteRenderer>();
            sr.sprite       = CreateRect(0.95f, 1.35f, i == 7
                ? new Color(0.18f, 0.12f, 0.08f)
                : new Color(0.10f, 0.08f, 0.06f));
            sr.sortingOrder = i;

            // Card back pattern on top card
            if (i == 7)
            {
                var pattern = new GameObject("Pattern");
                pattern.transform.SetParent(card.transform, false);
                var psr = pattern.AddComponent<SpriteRenderer>();
                psr.sprite       = CreateRect(0.72f, 1.12f, new Color(0.35f, 0.08f, 0.07f));
                psr.sortingOrder = i + 1;

                var inner = new GameObject("Inner");
                inner.transform.SetParent(card.transform, false);
                var isr = inner.AddComponent<SpriteRenderer>();
                isr.sprite       = CreateRect(0.55f, 0.92f, new Color(0.22f, 0.05f, 0.04f));
                isr.sortingOrder = i + 2;
            }
        }

        // Label — "PLAY" floats above deck
        var labelGO = new GameObject("PlayLabel");
        labelGO.transform.SetParent(go.transform, false);
        labelGO.transform.localPosition = new Vector3(0f, 1.05f, 0f);
        labelGO.transform.localScale    = Vector3.one * 0.02f;
        var canvas = labelGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.WorldSpace;
        canvas.sortingOrder = 20;
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text      = "P L A Y";
        labelTMP.fontSize  = 120f;
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.color     = new Color(c_gold.r, c_gold.g, c_gold.b, 0.85f);
        labelTMP.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;

        _deckRenderer = go.GetComponentInChildren<SpriteRenderer>();
        return go;
    }

    GameObject CreatePlaceholderRecord()
    {
        var go = new GameObject("Record_Placeholder");
        go.transform.position = new Vector3(-3.8f, -1.6f, -0.1f);

        // Base
        var baseGO = new GameObject("Base");
        baseGO.transform.SetParent(go.transform, false);
        var bsr = baseGO.AddComponent<SpriteRenderer>();
        bsr.sprite       = CreateRect(1.8f, 1.2f, new Color(0.14f, 0.10f, 0.07f));
        bsr.sortingOrder = 1;

        // Record disc
        var disc = new GameObject("Disc");
        disc.transform.SetParent(go.transform, false);
        disc.transform.localPosition = new Vector3(-0.3f, 0.1f, 0f);
        var dsr = disc.AddComponent<SpriteRenderer>();
        dsr.sprite       = CreateCircle(0.48f, new Color(0.08f, 0.07f, 0.06f));
        dsr.sortingOrder = 2;
        disc.name        = "RecordDisc"; // referenced for spin

        // Disc label
        var dlabel = new GameObject("DiscLabel");
        dlabel.transform.SetParent(disc.transform, false);
        var dlsr = dlabel.AddComponent<SpriteRenderer>();
        dlsr.sprite       = CreateCircle(0.14f, new Color(0.55f, 0.08f, 0.07f));
        dlsr.sortingOrder = 3;

        // Tonearm
        var arm = new GameObject("Arm");
        arm.transform.SetParent(go.transform, false);
        arm.transform.localPosition = new Vector3(0.5f, 0.18f, 0f);
        arm.transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
        arm.name = "RecordArm";
        var asr = arm.AddComponent<SpriteRenderer>();
        asr.sprite       = CreateRect(0.06f, 0.55f, new Color(0.55f, 0.45f, 0.30f));
        asr.sortingOrder = 4;

        // Volume label
        var vlabelGO = new GameObject("VolumeLabel");
        vlabelGO.transform.SetParent(go.transform, false);
        vlabelGO.transform.localPosition = new Vector3(0f, -0.72f, 0f);
        vlabelGO.transform.localScale    = Vector3.one * 0.012f;
        var vc = vlabelGO.AddComponent<Canvas>();
        vc.renderMode = RenderMode.WorldSpace; vc.sortingOrder = 20;
        var vt = vlabelGO.AddComponent<TextMeshProUGUI>();
        vt.text = "VOLUME"; vt.fontSize = 80f;
        vt.alignment = TextAlignmentOptions.Center;
        vt.color = new Color(c_parch.r, c_parch.g, c_parch.b, 0.5f);
        vt.fontStyle = FontStyles.SmallCaps | FontStyles.Bold;

        return go;
    }

    GameObject CreatePlaceholderCandle()
    {
        var go = new GameObject("Candle_Placeholder");
        go.transform.position = new Vector3(2.2f, -1.2f, -0.1f);

        // Candle body
        var body = new GameObject("Body");
        body.transform.SetParent(go.transform, false);
        var bsr = body.AddComponent<SpriteRenderer>();
        bsr.sprite       = CreateRect(0.22f, 0.9f, new Color(0.92f, 0.88f, 0.78f));
        bsr.sortingOrder = 2;

        // Wax drips
        var drip = new GameObject("Drip");
        drip.transform.SetParent(go.transform, false);
        drip.transform.localPosition = new Vector3(0.06f, 0.1f, 0f);
        var dsr = drip.AddComponent<SpriteRenderer>();
        dsr.sprite       = CreateRect(0.06f, 0.3f, new Color(0.90f, 0.86f, 0.76f));
        dsr.sortingOrder = 3;

        // Flame — layered ellipses
        var flameBase = new GameObject("FlameBase");
        flameBase.transform.SetParent(go.transform, false);
        flameBase.transform.localPosition = new Vector3(0f, 0.52f, 0f);
        _candleFlame = flameBase.GetComponent<SpriteRenderer>();
        var fbsr = flameBase.AddComponent<SpriteRenderer>();
        fbsr.sprite       = CreateCircle(0.12f, new Color(1f, 0.85f, 0.30f));
        fbsr.sortingOrder = 4;
        _candleFlame = fbsr;

        var flameTip = new GameObject("FlameTip");
        flameTip.transform.SetParent(go.transform, false);
        flameTip.transform.localPosition = new Vector3(0f, 0.62f, 0f);
        var ftsr = flameTip.AddComponent<SpriteRenderer>();
        ftsr.sprite       = CreateCircle(0.06f, new Color(1f, 0.96f, 0.75f));
        ftsr.sortingOrder = 5;

        // Brightness label
        var blabelGO = new GameObject("BrightnessLabel");
        blabelGO.transform.SetParent(go.transform, false);
        blabelGO.transform.localPosition = new Vector3(0f, -0.62f, 0f);
        blabelGO.transform.localScale    = Vector3.one * 0.012f;
        var bc = blabelGO.AddComponent<Canvas>();
        bc.renderMode = RenderMode.WorldSpace; bc.sortingOrder = 20;
        var bt = blabelGO.AddComponent<TextMeshProUGUI>();
        bt.text = "LIGHT"; bt.fontSize = 80f;
        bt.alignment = TextAlignmentOptions.Center;
        bt.color = new Color(c_parch.r, c_parch.g, c_parch.b, 0.5f);
        bt.fontStyle = FontStyles.SmallCaps | FontStyles.Bold;

        return go;
    }

    GameObject CreatePlaceholderElecBox()
    {
        var go = new GameObject("ElecBox_Placeholder");
        go.transform.position = new Vector3(4.2f, 2.8f, -0.1f);

        // Box body
        var body = new GameObject("Body");
        body.transform.SetParent(go.transform, false);
        var bsr = body.AddComponent<SpriteRenderer>();
        bsr.sprite       = CreateRect(1.3f, 1.7f, new Color(0.16f, 0.15f, 0.13f));
        bsr.sortingOrder = 2;

        // Panel lines
        for (int i = 0; i < 3; i++)
        {
            var line = new GameObject($"Line_{i}");
            line.transform.SetParent(go.transform, false);
            line.transform.localPosition = new Vector3(0f, 0.35f - i * 0.35f, 0f);
            var lsr = line.AddComponent<SpriteRenderer>();
            lsr.sprite       = CreateRect(0.85f, 0.04f, new Color(0.30f, 0.28f, 0.22f));
            lsr.sortingOrder = 3;
        }

        // Warning indicator — red dot
        var dot = new GameObject("Indicator");
        dot.transform.SetParent(go.transform, false);
        dot.transform.localPosition = new Vector3(0.38f, 0.62f, 0f);
        var dsr = dot.AddComponent<SpriteRenderer>();
        dsr.sprite       = CreateCircle(0.07f, new Color(0.8f, 0.1f, 0.08f));
        dsr.sortingOrder = 4;
        StartCoroutine(IndicatorBlink(dsr));

        // Label
        var lGO = new GameObject("Label");
        lGO.transform.SetParent(go.transform, false);
        lGO.transform.localPosition = new Vector3(0f, -1.05f, 0f);
        lGO.transform.localScale    = Vector3.one * 0.012f;
        var lc = lGO.AddComponent<Canvas>();
        lc.renderMode = RenderMode.WorldSpace; lc.sortingOrder = 20;
        var lt = lGO.AddComponent<TextMeshProUGUI>();
        lt.text = "SETTINGS"; lt.fontSize = 80f;
        lt.alignment = TextAlignmentOptions.Center;
        lt.color = new Color(c_parch.r, c_parch.g, c_parch.b, 0.45f);
        lt.fontStyle = FontStyles.SmallCaps | FontStyles.Bold;

        return go;
    }

    GameObject CreatePlaceholderFace()
    {
        var go = new GameObject("Face_Placeholder");
        go.transform.position = new Vector3(0f, 4.0f, 0.5f);
        go.transform.localScale = new Vector3(2.2f, 2.8f, 1f);

        // Head silhouette
        var head = new GameObject("Head");
        head.transform.SetParent(go.transform, false);
        var hsr = head.AddComponent<SpriteRenderer>();
        hsr.sprite       = CreateCircle(0.5f, new Color(0.06f, 0.05f, 0.04f));
        hsr.color        = new Color(1f, 1f, 1f, 0.03f);
        hsr.sortingOrder = -1; // behind everything — deep in darkness

        // Eyes — very subtle
        for (int i = 0; i < 2; i++)
        {
            var eye = new GameObject($"Eye_{i}");
            eye.transform.SetParent(go.transform, false);
            eye.transform.localPosition = new Vector3(i == 0 ? -0.14f : 0.14f, 0.07f, 0f);
            var esr = eye.AddComponent<SpriteRenderer>();
            esr.sprite       = CreateCircle(0.06f, new Color(0.85f, 0.72f, 0.55f));
            esr.color        = new Color(1f, 1f, 1f, 0.04f);
            esr.sortingOrder = 0;
            esr.name         = $"EyeRenderer_{i}";
        }

        return go;
    }

    // =========================================================================
    // UI BUILDING — Canvas overlay for settings panel
    // =========================================================================

    void BuildUI()
    {
        var canvasGO = new GameObject("MenuCanvas");
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        BuildSettingsPanel();
    }

    void BuildSettingsPanel()
    {
        // Full-screen overlay
        _settingsPanel = new GameObject("SettingsPanel");
        _settingsPanel.transform.SetParent(_canvas.transform, false);
        var rt = _settingsPanel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        _settingsPanel.AddComponent<Image>().color = new Color(0.02f, 0.02f, 0.01f, 0.92f);
        _settingsCG = _settingsPanel.AddComponent<CanvasGroup>();
        _settingsCG.alpha = 0f;
        _settingsPanel.SetActive(false);

        // Close on backdrop click
        var closeBtn = _settingsPanel.AddComponent<Button>();
        closeBtn.targetGraphic = _settingsPanel.GetComponent<Image>();
        closeBtn.onClick.AddListener(CloseSettings);

        // Inner card
        var card = new GameObject("Card");
        card.transform.SetParent(_settingsPanel.transform, false);
        var cRT = card.AddComponent<RectTransform>();
        cRT.anchorMin = cRT.anchorMax = cRT.pivot = new Vector2(0.5f, 0.5f);
        cRT.sizeDelta = new Vector2(560f, 420f);
        card.AddComponent<Image>().color = new Color(0.08f, 0.06f, 0.04f, 0.99f);

        // Block backdrop close through card
        card.AddComponent<Button>().targetGraphic = card.GetComponent<Image>();

        // Title
        AddLabel(card.transform, "P U R G A T O R Y", 28f,
            new Vector2(0f, 160f), new Vector2(500f, 40f),
            c_blood, FontStyles.Bold | FontStyles.SmallCaps);

        AddLabel(card.transform, "Settings", 14f,
            new Vector2(0f, 120f), new Vector2(500f, 24f),
            new Color(c_parch.r, c_parch.g, c_parch.b, 0.6f), FontStyles.Italic);

        // Note — settings are controlled by room objects
        AddLabel(card.transform,
            "Drag the record player to adjust volume.\nDrag the candle to adjust brightness.",
            12f, new Vector2(0f, 50f), new Vector2(480f, 50f),
            new Color(c_parch.r, c_parch.g, c_parch.b, 0.55f), FontStyles.Italic);

        // Close button
        var closeBtnGO = new GameObject("CloseBtn");
        closeBtnGO.transform.SetParent(card.transform, false);
        var cbRT = closeBtnGO.AddComponent<RectTransform>();
        cbRT.anchorMin = cbRT.anchorMax = cbRT.pivot = new Vector2(0.5f, 0.5f);
        cbRT.anchoredPosition = new Vector2(0f, -150f);
        cbRT.sizeDelta = new Vector2(200f, 48f);
        var cbImg = closeBtnGO.AddComponent<Image>();
        cbImg.color = new Color(0.25f, 0.07f, 0.05f, 0.95f);
        var cbBtn = closeBtnGO.AddComponent<Button>();
        cbBtn.targetGraphic = cbImg;
        cbBtn.onClick.AddListener(CloseSettings);
        AddLabel(closeBtnGO.transform, "CLOSE", 16f,
            Vector2.zero, new Vector2(200f, 48f), c_parch, FontStyles.Bold | FontStyles.SmallCaps);
    }

    void AddLabel(Transform parent, string text, float size, Vector2 pos, Vector2 sz,
        Color col, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = sz;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = col;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = style;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
    }

    // =========================================================================
    // INTERACTION — Update loop
    // =========================================================================

    void HandleHover()
    {
        Vector2 mouseWorld = mainCamera.ScreenToWorldPoint(
            Mouse.current.position.ReadValue());

        // Deck hover glow
        bool deckHit = _deck != null && HitObject(_deck, mouseWorld);
        if (deckHit && !_deckHovered)
        {
            _deckHovered = true;
            _deck.transform.DOScale(1.06f, 0.22f).SetEase(Ease.OutBack);
            PlaySFX(cardHoverClip, 0.35f);
        }
        else if (!deckHit && _deckHovered)
        {
            _deckHovered = false;
            _deck.transform.DOScale(1f, 0.18f);
        }

        // Mouse click handling
        if (Mouse.current.leftButton.wasPressedThisFrame)
            HandleClick(mouseWorld);
    }

    void HandleClick(Vector2 worldPos)
    {
        if (_deck != null && HitObject(_deck, worldPos))
        {
            OnDeckClicked();
            return;
        }
        if (_electricalBox != null && HitObject(_electricalBox, worldPos))
        {
            OnElecBoxClicked();
            return;
        }
        if (_recordPlayer != null && HitObject(_recordPlayer, worldPos))
        {
            _draggingRecord   = true;
            _recordDragStartX = worldPos.x;
            _recordArmStartAngle = GetArmAngle();
            PlaySFX(recordScratchClip, 0.4f);
            return;
        }
        if (_candle != null && HitObject(_candle, worldPos))
        {
            _draggingCandle   = true;
            _candleDragStartY = worldPos.y;
            PlaySFX(candleFlickerClip, 0.3f);
        }
    }

    void HandleRecordDrag()
    {
        if (_draggingRecord)
        {
            if (Mouse.current.leftButton.isPressed)
            {
                Vector2 mp = mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
                float delta = (mp.x - _recordDragStartX) * 0.3f;
                _masterVolume = Mathf.Clamp01(_masterVolume + delta * Time.deltaTime * 2f);
                AudioListener.volume = _masterVolume;

                // Rotate tonearm with volume
                var arm = _recordPlayer.transform.Find("Arm") ??
                          FindDeep(_recordPlayer.transform, "RecordArm");
                if (arm != null)
                    arm.localRotation = Quaternion.Euler(0f, 0f,
                        Mathf.Lerp(-55f, -15f, _masterVolume));
            }
            else
            {
                _draggingRecord = false;
            }
        }
    }

    void HandleCandleDrag()
    {
        if (_draggingCandle)
        {
            if (Mouse.current.leftButton.isPressed)
            {
                Vector2 mp = mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
                float delta = (mp.y - _candleDragStartY) * 0.5f;
                _brightness = Mathf.Clamp01(_brightness + delta * Time.deltaTime * 1.5f);
                ApplyBrightness(_brightness);
                _candleDragStartY = mp.y;
            }
            else
            {
                _draggingCandle = false;
            }
        }
    }

    void ApplyBrightness(float b)
    {
        // Save to PlayerPrefs
        PlayerPrefs.SetFloat("Brightness", b);

        // Adjust candle light intensity
        if (_candlePointLight != null)
            _candlePointLight.intensity = Mathf.Lerp(0.3f, 2.2f, b);

        // Adjust ambient
        RenderSettings.ambientLight = Color.Lerp(
            new Color(0.02f, 0.01f, 0.01f),
            new Color(0.12f, 0.09f, 0.06f), b);

        // Adjust candle flame brightness
        if (_candleFlame != null)
            _candleFlame.color = new Color(1f, Mathf.Lerp(0.5f, 0.85f, b),
                Mathf.Lerp(0.1f, 0.3f, b), 1f);
    }

    // =========================================================================
    // CLICK HANDLERS
    // =========================================================================

    void OnDeckClicked()
    {
        PlaySFX(cardClickClip, 0.7f);

        // Cards fan out briefly then fly up
        StartCoroutine(DeckPlayAnimation());
    }

    IEnumerator DeckPlayAnimation()
    {
        // Disable further input
        _deck.GetComponent<Collider2D>().enabled = false;

        // Fan cards
        var cards = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in _deck.transform)
            if (child.name.StartsWith("Card_")) cards.Add(child);

        for (int i = 0; i < cards.Count; i++)
        {
            float angle = Mathf.Lerp(-25f, 25f, (float)i / (cards.Count - 1));
            cards[i].DOLocalRotate(new Vector3(0f, 0f, angle), 0.35f).SetEase(Ease.OutBack);
            cards[i].DOLocalMoveX(Mathf.Lerp(-0.6f, 0.6f, (float)i / (cards.Count - 1)), 0.35f);
        }

        yield return new WaitForSeconds(0.5f);

        // All fly up and fade
        foreach (Transform card in cards)
        {
            card.DOMove(card.position + Vector3.up * 6f, 0.6f).SetEase(Ease.InCubic);
            card.GetComponent<SpriteRenderer>()?.DOFade(0f, 0.5f).SetDelay(0.1f);
        }

        // Fade to black
        yield return new WaitForSeconds(0.35f);
        DOTween.To(() => 0f, v => {
            mainCamera.backgroundColor = Color.Lerp(c_darkRoom, Color.black, v);
        }, 1f, 0.6f);

        yield return new WaitForSeconds(0.7f);
        SceneManager.LoadScene(gameSceneName);
    }

    void OnElecBoxClicked()
    {
        PlaySFX(electricalHumClip, 0.5f);
        if (!_settingsOpen) OpenSettings();
        else CloseSettings();
    }

    void OpenSettings()
    {
        _settingsOpen = true;
        _settingsPanel.SetActive(true);
        _settingsCG.alpha = 0f;
        _settingsCG.DOFade(1f, 0.22f);
        // Spark flash on the elec box
        if (_electricalBox != null)
            _electricalBox.transform.DOPunchScale(Vector3.one * 0.08f, 0.25f, 4, 0.5f);
    }

    void CloseSettings()
    {
        _settingsOpen = false;
        _settingsCG.DOFade(0f, 0.16f)
            .OnComplete(() => _settingsPanel.SetActive(false));
    }

    // =========================================================================
    // COROUTINES
    // =========================================================================

    IEnumerator IntroSequence()
    {
        // Start fully black, fade in over 2s
        mainCamera.backgroundColor = Color.black;
        yield return new WaitForSeconds(0.3f);

        DOTween.To(() => 0f, v =>
            mainCamera.backgroundColor = Color.Lerp(Color.black, c_darkRoom, v),
            1f, 2.2f).SetEase(Ease.OutCubic);

        // Candle light flickers in
        if (_candlePointLight != null)
        {
            _candlePointLight.intensity = 0f;
            DOTween.To(() => _candlePointLight.intensity,
                x => _candlePointLight.intensity = x,
                1.4f, 1.8f).SetDelay(0.6f);
        }

        // Objects fade in staggered
        float[] delays = { 0.8f, 1.1f, 1.4f, 1.6f, 1.9f };
        GameObject[] objects = { _table, _deck, _recordPlayer, _candle, _electricalBox };
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] == null) continue;
            foreach (var sr in objects[i].GetComponentsInChildren<SpriteRenderer>())
            {
                Color start = sr.color; start.a = 0f; sr.color = start;
                float delay = delays[i];
                sr.DOFade(sr.color.a == 0f ? 1f : sr.color.a, 0.8f).SetDelay(delay);
            }
        }

        // Face very slowly appears much later
        yield return new WaitForSeconds(3.5f);
        if (_face != null)
        {
            foreach (var sr in _face.GetComponentsInChildren<SpriteRenderer>())
                sr.DOFade(sr.color.a, 1.5f);
        }
    }

    IEnumerator DeckIdleFloat()
    {
        float t = Random.Range(0f, Mathf.PI * 2f);
        while (true)
        {
            t += Time.deltaTime * 0.7f;
            float yOff = Mathf.Sin(t) * 0.06f;
            if (!_deckHovered && _deck != null)
                _deck.transform.localPosition = new Vector3(0f, -1.2f + yOff, -0.1f);
            yield return null;
        }
    }

    IEnumerator RecordSpin()
    {
        var disc = FindDeep(_recordPlayer?.transform, "RecordDisc");
        if (disc == null) yield break;
        while (true)
        {
            float speed = Mathf.Lerp(0f, 120f, _masterVolume);
            disc.Rotate(0f, 0f, -speed * Time.deltaTime);
            yield return null;
        }
    }

    IEnumerator CandleFlicker()
    {
        while (true)
        {
            float target   = Random.Range(1.1f, 1.7f) * _brightness;
            float duration = Random.Range(0.06f, 0.22f);
            if (_candlePointLight != null)
                DOTween.To(() => _candlePointLight.intensity,
                    x => _candlePointLight.intensity = x, target, duration);
            yield return new WaitForSeconds(duration + Random.Range(0.05f, 0.3f));
        }
    }

    IEnumerator FaceIdleBreathe()
    {
        if (_face == null) yield break;
        var srs = _face.GetComponentsInChildren<SpriteRenderer>();

        while (true)
        {
            // Breathe — pulse alpha very subtly
            float wait = Random.Range(3f, 9f);
            yield return new WaitForSeconds(wait);

            float baseAlpha = 0.02f;
            foreach (var sr in srs)
            {
                float a = sr.color.a;
                DOTween.To(() => a, v => {
                    var c = sr.color; c.a = v; sr.color = c;
                }, a + 0.015f, 1.2f).SetLoops(2, LoopType.Yoyo);
            }

            // Occasional blink (eyes briefly brighten)
            if (Random.value > 0.6f)
            {
                yield return new WaitForSeconds(Random.Range(1f, 3f));
                var eyes = new SpriteRenderer[2];
                int found = 0;
                foreach (var sr in srs)
                    if (sr.name.StartsWith("EyeRenderer") && found < 2)
                        eyes[found++] = sr;

                foreach (var eye in eyes)
                {
                    if (eye == null) continue;
                    float ea = eye.color.a;
                    DOTween.To(() => ea, v => {
                        var c = eye.color; c.a = v; eye.color = c;
                    }, 0.12f, 0.08f)
                    .OnComplete(() => DOTween.To(() => 0.12f, v => {
                        var c = eye.color; c.a = v; eye.color = c;
                    }, ea, 0.4f));
                }
            }
        }
    }

    IEnumerator IndicatorBlink(SpriteRenderer sr)
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(1.5f, 4f));
            sr.DOFade(0.1f, 0.08f).OnComplete(() => sr.DOFade(0.9f, 0.08f));
        }
    }

    // =========================================================================
    // SPRITE FACTORIES
    // =========================================================================

    Sprite CreateRect(float w, float h, Color col)
    {
        int pw = Mathf.Max(1, Mathf.RoundToInt(w * 64));
        int ph = Mathf.Max(1, Mathf.RoundToInt(h * 64));
        var tex = new Texture2D(pw, ph, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var pixels = new Color[pw * ph];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = col;
        tex.SetPixels(pixels); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, pw, ph),
            new Vector2(0.5f, 0.5f), 64f);
    }

    Sprite CreateCircle(float radius, Color col)
    {
        int size = Mathf.Max(4, Mathf.RoundToInt(radius * 128));
        var tex  = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var pixels = new Color[size * size];
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - r + 0.5f, dy = y - r + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01((r - dist) / (r * 0.06f + 0.5f));
                pixels[y * size + x] = new Color(col.r, col.g, col.b, col.a * alpha);
            }
        tex.SetPixels(pixels); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), 64f);
    }

    // =========================================================================
    // UTILITIES
    // =========================================================================

    bool HitObject(GameObject obj, Vector2 worldPos)
    {
        var col = obj.GetComponent<Collider2D>();
        if (col == null) return false;
        return col.OverlapPoint(worldPos);
    }

    float GetArmAngle()
    {
        if (_recordPlayer == null) return -35f;
        var arm = FindDeep(_recordPlayer.transform, "Arm") ??
                  FindDeep(_recordPlayer.transform, "RecordArm");
        return arm != null ? arm.localEulerAngles.z : -35f;
    }

    Transform FindDeep(Transform parent, string name)
    {
        if (parent == null) return null;
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var result = FindDeep(child, name);
            if (result != null) return result;
        }
        return null;
    }

    void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip == null || _sfxSource == null) return;
        _sfxSource.PlayOneShot(clip, volume);
    }
}
