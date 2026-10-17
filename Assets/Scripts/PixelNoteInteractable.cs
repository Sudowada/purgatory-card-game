using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>Clickable world note with a focused, editable reading overlay.</summary>
[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider))]
public class PixelNoteInteractable : WorldInteractable
{
    [Header("Note")]
    public string noteTitle = "A WORN NOTE";
    [TextArea(5, 20)] public string noteText = "Watch the room, not the switches.\n\nFollow the lights.\nRemember their order.";
    [Tooltip("Optional paper artwork. A crisp pixel paper is created if left empty.")]
    public Sprite paperSprite;
    public TMP_FontAsset pixelFont;
    public FirstPersonController playerController;

    private static PixelNoteInteractable _activeNote;
    private GameObject _view;
    private RectTransform _paperRect;
    private bool _clickPending;
    private float _clickTravel;
    private CursorLockMode _previousLock;
    private bool _previousCursorVisible;
    private bool _previousControllerEnabled;
    private GameStateManager _stateManager;

    protected override void Awake()
    {
        base.Awake();
        interactionType = InteractionType.Read;
        if (pixelFont == null)
            pixelFont = Resources.Load<TMP_FontAsset>("Fonts/PixelifySans-Regular SDF");
        var renderer = GetComponent<SpriteRenderer>();
        if (paperSprite == null) paperSprite = renderer.sprite;
        if (paperSprite == null) paperSprite = CreatePixelPaper();
        renderer.sprite = paperSprite;
        var box = GetComponent<BoxCollider>();
        // Keep a user-configured collider; fit the newly added default collider.
        if (box.size == Vector3.one && box.center == Vector3.zero)
        {
            box.size = new Vector3(paperSprite.bounds.size.x, paperSprite.bounds.size.y, 0.025f);
            box.center = paperSprite.bounds.center;
        }
    }

    protected override void Update()
    {
        // Retain the existing Read icon and E prompt through WorldInteractable.
        if (_activeNote == null) base.Update();
    }

    bool CanOpen()
    {
        if (_cam == null) _cam = Camera.main;
        if (playerController == null && _cam != null)
            playerController = _cam.GetComponentInParent<FirstPersonController>();
        return _activeNote == null && _cam != null && _cam.isActiveAndEnabled
            && playerController != null && playerController.isActiveAndEnabled
            && Cursor.lockState == CursorLockMode.Locked
            && GameStateManager.Instance != null
            && GameStateManager.Instance.CurrentState == GameState.Exploration;
    }

    bool IsAimedAt()
    {
        if (_cam == null) return false;
        Ray ray = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        return Physics.Raycast(ray, out RaycastHit hit, interactDistance)
            && (hit.transform == transform || hit.transform.IsChildOf(transform));
    }

    void LateUpdate()
    {
        var mouse = Mouse.current;
        var kb = Keyboard.current;
        if (_activeNote == this)
        {
            _clickPending = false;
            if ((kb != null && kb.escapeKey.wasPressedThisFrame)
                || (mouse != null && mouse.rightButton.wasPressedThisFrame)
                || (mouse != null && mouse.leftButton.wasPressedThisFrame
                    && !RectTransformUtility.RectangleContainsScreenPoint(
                        _paperRect, mouse.position.ReadValue())))
                Close();
            return;
        }
        if (!CanOpen() || mouse == null || mouse.rightButton.isPressed
            || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
        {
            _clickPending = false;
            return;
        }
        if (mouse.leftButton.wasPressedThisFrame)
        {
            _clickPending = IsAimedAt();
            _clickTravel = 0f;
            return;
        }
        if (!_clickPending) return;
        _clickTravel += mouse.delta.ReadValue().magnitude;
        if (_clickTravel > 6f) { _clickPending = false; return; }
        if (mouse.leftButton.wasReleasedThisFrame)
        {
            _clickPending = false;
            if (IsAimedAt()) OnInteract();
        }
        else if (!mouse.leftButton.isPressed) _clickPending = false;
    }

    protected override void OnInteract()
    {
        if (!CanOpen()) return;
        _activeNote = this;
        _clickPending = false;
        _previousLock = Cursor.lockState;
        _previousCursorVisible = Cursor.visible;
        _previousControllerEnabled = playerController.enabled;
        playerController.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        ScreenReticle.Instance?.SetVisible(false);
        BuildView();
        _stateManager = GameStateManager.Instance;
        _stateManager.OnStateChanged += OnStateChanged;
    }

    void OnStateChanged(GameState state)
    {
        // GameStateManager has already applied the new controller/cursor state.
        if (state != GameState.Exploration) Close(false);
    }

    public void Close() => Close(true);

    void Close(bool restoreExploration)
    {
        _clickPending = false;
        if (_activeNote != this) return;
        _activeNote = null;
        if (_stateManager != null) _stateManager.OnStateChanged -= OnStateChanged;
        if (_view != null) { _view.SetActive(false); Destroy(_view); }
        _view = null;
        bool exploring = GameStateManager.Instance != null
            && GameStateManager.Instance.CurrentState == GameState.Exploration;
        if (restoreExploration && exploring)
        {
            if (playerController != null) playerController.enabled = _previousControllerEnabled;
            Cursor.lockState = _previousLock;
            Cursor.visible = _previousCursorVisible;
        }
        ScreenReticle.Instance?.SetVisible(exploring);
        // Camera transform is never modified: closing returns to the same view.
    }

    void OnDisable() => Close();
    void OnDestroy() => Close();
    void OnApplicationFocus(bool focused)
    {
        _clickPending = false;
        if (!focused) Close();
    }

    void BuildView()
    {
        _view = new GameObject("FocusedNote", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = _view.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        var scaler = _view.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;

        var shade = Rect("Backdrop", _view.transform, Vector2.zero, Vector2.one);
        shade.gameObject.AddComponent<Image>().color = new Color(0.015f, 0.012f, 0.01f, 0.88f);
        var holder = Rect("PaperArea", shade, new Vector2(0.08f, 0.13f), new Vector2(0.92f, 0.94f));
        _paperRect = Rect("Paper", holder, Vector2.zero, Vector2.one);
        var paper = _paperRect.gameObject.AddComponent<Image>();
        paper.sprite = paperSprite;
        var aspect = _paperRect.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspect.aspectRatio = paperSprite.rect.width / paperSprite.rect.height;

        Label("Title", _paperRect, new Vector2(0.14f, 0.79f), new Vector2(0.86f, 0.9f),
            noteTitle, 28, TextAlignmentOptions.Center);
        // Scrollable body keeps long clues available without shrinking them to illegibility.
        var viewport = Rect("TextViewport", _paperRect, new Vector2(0.14f, 0.13f), new Vector2(0.86f, 0.76f));
        viewport.gameObject.AddComponent<RectMask2D>();
        var body = Label("Body", viewport, new Vector2(0, 1), Vector2.one,
            noteText, 22, TextAlignmentOptions.TopLeft);
        body.rectTransform.pivot = new Vector2(0.5f, 1);
        var fit = body.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = body.rectTransform;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;
        Label("CloseHint", shade, new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.1f),
            "ESC / RIGHT CLICK / CLICK OUTSIDE TO CLOSE", 22, TextAlignmentOptions.Center,
            new Color(0.87f, 0.81f, 0.65f));
    }

    static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    TextMeshProUGUI Label(string name, Transform parent, Vector2 min, Vector2 max,
        string text, float size, TextAlignmentOptions alignment, Color? color = null)
    {
        var label = Rect(name, parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
        if (pixelFont != null) label.font = pixelFont;
        label.text = text;
        label.fontSize = size;
        label.alignment = alignment;
        label.color = color ?? new Color(0.18f, 0.12f, 0.075f);
        label.richText = false;
        return label;
    }

    // Built-in fallback keeps the component usable before optional artwork is imported.
    private static Sprite _fallbackPaper;
    static Sprite CreatePixelPaper()
    {
        if (_fallbackPaper != null) return _fallbackPaper;
        const int w = 96, h = 128;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int edge = 2 + ((y / 7 * 13) % 3);
                bool outside = x < edge || x >= w - edge
                    || y < 2 + ((x / 8) % 3) || y >= h - 2 - ((x / 9) % 3);
                int border = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                Color c = outside ? Color.clear
                    : border < 7 ? new Color(0.60f, 0.43f, 0.23f)
                    : border < 11 ? new Color(0.76f, 0.61f, 0.38f)
                    : new Color(0.88f, 0.79f, 0.60f);
                if (!outside && border >= 11 && (y == 42 || x == 47))
                    c = new Color(0.83f, 0.72f, 0.51f);
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        _fallbackPaper = Sprite.Create(tex, new Rect(0, 0, w, h),
            new Vector2(0.5f, 0.5f), 100f);
        return _fallbackPaper;
    }
}
