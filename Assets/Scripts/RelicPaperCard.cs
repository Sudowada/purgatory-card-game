using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;

/// <summary>
/// A living paper card for one relic in the shop window.
/// Procedurally generates its visuals to match the playing card aesthetic.
/// Includes idle float, mouse-tracked tilt, and hover animations.
/// </summary>
public class RelicPaperCard : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    // ── Palette ───────────────────────────────────────────────────────────────
    private static readonly Color parchmentBase = new Color(0.82f, 0.72f, 0.52f);
    private static readonly Color parchmentDark = new Color(0.62f, 0.50f, 0.32f);
    private static readonly Color burnedEdge    = new Color(0.12f, 0.08f, 0.04f);
    private static readonly Color inkBlack      = new Color(0.08f, 0.05f, 0.03f);
    private static readonly Color bloodRed      = new Color(0.65f, 0.06f, 0.06f);
    private static readonly Color ageSpot       = new Color(0.50f, 0.35f, 0.15f);
    private static readonly Color boneWhite     = new Color(0.90f, 0.85f, 0.72f);

    // ── Card dimensions ───────────────────────────────────────────────────────
    private const int   CARD_W         = 150;
    private const int   CARD_H         = 200;
    // The real-world scale of a fully expanded card
    private static readonly Vector3 FULL_SCALE   = new Vector3(0.001f, 0.001f, 0.001f);
    // Scale when squeezed through the window slot
    private static readonly Vector3 HIDDEN_SCALE = new Vector3(0.001f, 0.001f, 0.001f) * 0.3f;

    // ── Runtime data ──────────────────────────────────────────────────────────
    private Relic  _relic;
    private int    _cost;
    private bool   _purchased;
    private int    _index;
    private System.Action<int> _onBuy;

    private Vector3 _hiddenLocalPos;
    private Vector3 _visibleLocalPos;

    // ── References ────────────────────────────────────────────────────────────
    private Canvas          _canvas;
    private RectTransform   _canvasRT;
    private GameObject      _tiltParent;
    private RectTransform   _tiltRT;
    private Image           _background;
    private TextMeshProUGUI _nameText;
    private TextMeshProUGUI _categoryText;
    private TextMeshProUGUI _descText;
    private TextMeshProUGUI _flavorText;
    private TextMeshProUGUI _costText;
    private Button          _buyButton;
    private TextMeshProUGUI _buyButtonText;

    // ── Animation state ───────────────────────────────────────────────────────
    private bool  _isHovering = false;
    private bool  _hasArrived = false;  // only bob after sliding out
    private float _idleTime   = 0f;
    private float _idlePhase;
    private const float IDLE_AMPLITUDE = 0.004f;
    private const float IDLE_SPEED     = 0.9f;
    private const float TILT_AMOUNT    = 12f;
    private const float TILT_SPEED     = 8f;
    private Vector3 _currentTilt = Vector3.zero;
    private int     _cardSeed;

    // ── Public Init ───────────────────────────────────────────────────────────

    public void Initialize(
        Relic relic, int cost, bool purchased, int index,
        Vector3 hiddenLocalPos, Vector3 visibleLocalPos,
        System.Action<int> onBuyCallback)
    {
        _relic           = relic;
        _cost            = cost;
        _purchased       = purchased;
        _index           = index;
        _hiddenLocalPos  = hiddenLocalPos;
        _visibleLocalPos = visibleLocalPos;
        _onBuy           = onBuyCallback;
        _idlePhase       = Random.Range(0f, Mathf.PI * 2f);
        _cardSeed        = index * 7919 + (int)relic.type * 1234;

        // Start at hidden position and squeezed scale
        transform.localPosition = hiddenLocalPos;
        transform.localScale    = HIDDEN_SCALE;

        BuildCard();
    }

    // ── Card Construction ─────────────────────────────────────────────────────

    void BuildCard()
    {
        _canvas = gameObject.GetComponent<Canvas>();
        if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;

        var scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        var raycaster = gameObject.GetComponent<GraphicRaycaster>();
        if (raycaster == null) gameObject.AddComponent<GraphicRaycaster>();

        _canvasRT           = GetComponent<RectTransform>();
        _canvasRT.sizeDelta = new Vector2(CARD_W, CARD_H);

        // NOTE: do NOT set transform.localScale here — it's set by Initialize()
        // and animated by SlideOut/SlideIn

        // TiltParent
        _tiltParent = new GameObject("TiltParent");
        _tiltParent.transform.SetParent(transform, false);
        _tiltRT           = _tiltParent.AddComponent<RectTransform>();
        _tiltRT.sizeDelta = new Vector2(CARD_W, CARD_H);
        _tiltRT.anchorMin = Vector2.zero;
        _tiltRT.anchorMax = Vector2.one;
        _tiltRT.offsetMin = Vector2.zero;
        _tiltRT.offsetMax = Vector2.zero;

        // Background
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(_tiltParent.transform, false);
        _background         = bgGO.AddComponent<Image>();
        _background.sprite  = CreateParchmentSprite(CARD_W, CARD_H, _cardSeed);
        _background.type    = Image.Type.Simple;
        _background.color   = Color.white;
        _background.raycastTarget = true;
        var bgRT            = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin      = Vector2.zero;
        bgRT.anchorMax      = Vector2.one;
        bgRT.offsetMin      = Vector2.zero;
        bgRT.offsetMax      = Vector2.zero;

        // Weathering overlay
        var wGO  = new GameObject("Weathering");
        wGO.transform.SetParent(_tiltParent.transform, false);
        var wImg = wGO.AddComponent<Image>();
        wImg.sprite         = CreateWeatheringSprite(CARD_W, CARD_H, _cardSeed);
        wImg.color          = Color.white;
        wImg.raycastTarget  = false;
        var wRT  = wGO.GetComponent<RectTransform>();
        wRT.anchorMin = Vector2.zero;
        wRT.anchorMax = Vector2.one;
        wRT.offsetMin = Vector2.zero;
        wRT.offsetMax = Vector2.zero;

        // Category color band
        Color catColor = RelicObject3D.GetCategoryColor(_relic.category);
        AddColorBand(catColor);

        // Age spots
        AddAgeSpots();

        // Text
        _nameText     = AddLabel("NameText",     new Vector2(0f,  68f),  new Vector2(130f, 28f), 13f,  TextAlignmentOptions.Center, FontStyles.Bold);
        _nameText.color = inkBlack;

        _categoryText = AddLabel("CategoryText", new Vector2(0f,  52f),  new Vector2(130f, 16f), 7f,   TextAlignmentOptions.Center, FontStyles.Italic);
        _categoryText.color = new Color(catColor.r * 0.6f, catColor.g * 0.6f, catColor.b * 0.6f);

        _descText     = AddLabel("DescText",     new Vector2(0f,  10f),  new Vector2(128f, 70f), 7.5f, TextAlignmentOptions.Center, FontStyles.Normal);
        _descText.color = inkBlack;

        _flavorText   = AddLabel("FlavorText",   new Vector2(0f, -38f),  new Vector2(128f, 28f), 6.5f, TextAlignmentOptions.Center, FontStyles.Italic);
        _flavorText.color = new Color(inkBlack.r, inkBlack.g, inkBlack.b, 0.65f);

        _costText     = AddLabel("CostText",     new Vector2(38f, -72f), new Vector2(50f,  18f), 9f,   TextAlignmentOptions.Right,  FontStyles.Bold);
        _costText.color = bloodRed;

        BuildBuyButton();
        PopulateText();
    }

    void AddColorBand(Color catColor)
    {
        var go  = new GameObject("CategoryBand");
        go.transform.SetParent(_tiltParent.transform, false);
        var img = go.AddComponent<Image>();
        img.color         = new Color(catColor.r, catColor.g, catColor.b, 0.35f);
        img.raycastTarget = false;
        var rt  = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(0f, 22f);
    }

    void AddAgeSpots()
    {
        System.Random rng = new System.Random(_cardSeed + 3);
        int count = rng.Next(3, 7);
        for (int i = 0; i < count; i++)
        {
            float px    = (float)(rng.NextDouble() * 100f - 50f);
            float py    = (float)(rng.NextDouble() * 140f - 70f);
            float sz    = (float)(rng.NextDouble() * 18f  + 6f);
            float alpha = (float)(rng.NextDouble() * 0.10f + 0.03f);

            var go  = new GameObject($"AgeSpot{i}");
            go.transform.SetParent(_tiltParent.transform, false);
            var img = go.AddComponent<Image>();
            img.color         = new Color(ageSpot.r, ageSpot.g, ageSpot.b, alpha);
            img.raycastTarget = false;
            var rt  = go.GetComponent<RectTransform>();
            rt.sizeDelta        = new Vector2(sz, sz * (float)(rng.NextDouble() * 0.5f + 0.6f));
            rt.anchoredPosition = new Vector2(px, py);
            rt.localEulerAngles = new Vector3(0f, 0f, (float)(rng.NextDouble() * 360f));
        }
    }

    void BuildBuyButton()
    {
        var btnGO  = new GameObject("BuyButton");
        btnGO.transform.SetParent(_tiltParent.transform, false);
        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = _purchased
            ? new Color(0.35f, 0.28f, 0.18f)
            : new Color(0.15f, 0.38f, 0.09f);
        _buyButton = btnGO.AddComponent<Button>();
        _buyButton.targetGraphic = btnImg;
        _buyButton.onClick.AddListener(OnBuyClicked);
        var btnRT  = btnGO.GetComponent<RectTransform>();
        btnRT.anchoredPosition = new Vector2(0f, -74f);
        btnRT.sizeDelta        = new Vector2(100f, 20f);

        var lblGO  = new GameObject("BuyText");
        lblGO.transform.SetParent(btnGO.transform, false);
        _buyButtonText              = lblGO.AddComponent<TextMeshProUGUI>();
        _buyButtonText.fontSize     = 9f;
        _buyButtonText.alignment    = TextAlignmentOptions.Center;
        _buyButtonText.color        = boneWhite;
        _buyButtonText.raycastTarget     = false;
        _buyButtonText.textWrappingMode  = TextWrappingModes.NoWrap;
        var lblRT  = lblGO.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero;
        lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = Vector2.zero;
        lblRT.offsetMax = Vector2.zero;
    }

    TextMeshProUGUI AddLabel(string name, Vector2 pos, Vector2 size, float fontSize,
        TextAlignmentOptions alignment, FontStyles style)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(_tiltParent.transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize         = fontSize;
        tmp.alignment        = alignment;
        tmp.fontStyle        = style;
        tmp.color            = boneWhite;
        tmp.raycastTarget    = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        var rt  = go.GetComponent<RectTransform>();
        rt.sizeDelta        = size;
        rt.anchoredPosition = pos;
        return tmp;
    }

    void PopulateText()
    {
        if (_nameText)      _nameText.text      = _relic.displayName.ToUpper();
        if (_categoryText)  _categoryText.text  = _relic.category.ToString();
        if (_descText)      _descText.text      = _relic.description;
        if (_flavorText)    _flavorText.text    = $"\"{_relic.flavorText}\"";
        if (_costText)      _costText.text      = _purchased ? "SOLD" : $"{_cost}g";
        if (_buyButtonText) _buyButtonText.text = _purchased ? "Purchased" : $"Buy  {_cost}g";
        UpdateBuyButton();
    }

    void UpdateBuyButton()
    {
        if (_buyButton == null) return;
        int  gold      = ShopManager.Instance?.playerGold ?? 0;
        bool canAfford = !_purchased && gold >= _cost;
        _buyButton.interactable = canAfford;
        var img = _buyButton.GetComponent<Image>();
        if (img != null)
            img.color = _purchased
                ? new Color(0.35f, 0.28f, 0.18f)
                : canAfford
                    ? new Color(0.15f, 0.38f, 0.09f)
                    : new Color(0.45f, 0.20f, 0.10f);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (_tiltParent == null || !_hasArrived) return;
        _idleTime += Time.deltaTime;

        // Idle bob on Y
        float yBob = Mathf.Sin(_idleTime * IDLE_SPEED + _idlePhase) * IDLE_AMPLITUDE;
        transform.localPosition = new Vector3(
            transform.localPosition.x,
            _visibleLocalPos.y + yBob,
            transform.localPosition.z);

        // Mouse tilt
        if (_isHovering)
        {
            Vector2 mousePos = Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : new Vector2(Screen.width / 2f, Screen.height / 2f);
            Vector2 center   = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Vector2 delta    = (mousePos - center) / center;
            Vector3 target   = new Vector3(-delta.y * TILT_AMOUNT, delta.x * TILT_AMOUNT, 0f);
            _currentTilt     = Vector3.Lerp(_currentTilt, target, Time.deltaTime * TILT_SPEED);
        }
        else
        {
            _currentTilt = Vector3.Lerp(_currentTilt, Vector3.zero, Time.deltaTime * TILT_SPEED);
        }

        if (_tiltRT != null)
            _tiltRT.localEulerAngles = _currentTilt;
    }

    // ── Pointer Events ────────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovering = true;
        transform.DOScale(FULL_SCALE * 1.06f, 0.18f).SetEase(Ease.OutBack);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovering = false;
        transform.DOScale(FULL_SCALE, 0.15f);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        transform.DOPunchScale(FULL_SCALE * 0.05f, 0.2f, 5, 0.5f);
    }

    // ── Slide Animation ───────────────────────────────────────────────────────

    /// <summary>
    /// Slides card out from window (small, squeezed) to shelf (full size).
    /// Position and scale animate simultaneously.
    /// </summary>
    public IEnumerator SlideOut(float duration = 0.45f)
    {
        _hasArrived = false;
        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOLocalMove(_visibleLocalPos, duration).SetEase(Ease.OutCubic));
        seq.Join(transform.DOScale(FULL_SCALE, duration).SetEase(Ease.OutBack));
        yield return seq.WaitForCompletion();
        _hasArrived = true; // start idle bob only after arriving
    }

    /// <summary>
    /// Slides card back into window, shrinking as it goes.
    /// </summary>
    public IEnumerator SlideIn(float duration = 0.28f)
    {
        _hasArrived = false;
        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOLocalMove(_hiddenLocalPos, duration).SetEase(Ease.InCubic));
        seq.Join(transform.DOScale(HIDDEN_SCALE, duration).SetEase(Ease.InCubic));
        yield return seq.WaitForCompletion();
    }

    // ── Buy ───────────────────────────────────────────────────────────────────

    void OnBuyClicked()
    {
        if (_purchased) return;
        int gold = ShopManager.Instance?.playerGold ?? 0;
        if (gold < _cost) return;
        _onBuy?.Invoke(_index);
    }

    public void MarkPurchased()
    {
        _purchased = true;
        if (_costText)      _costText.text      = "SOLD";
        if (_buyButtonText) _buyButtonText.text = "Purchased";
        UpdateBuyButton();
        transform.DOPunchScale(FULL_SCALE * 0.08f, 0.3f, 5, 0.4f);
    }

    public void RefreshGold() => UpdateBuyButton();

    // ── Sprite Generation ─────────────────────────────────────────────────────

    Sprite CreateParchmentSprite(int w, int h, int seed)
    {
        System.Random rng = new System.Random(seed);
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px  = new Color[w * h];
        float offX  = (float)(rng.NextDouble() * 500 + 10);
        float offY  = (float)(rng.NextDouble() * 500 + 10);
        int cornerR = 8;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (!InsideRoundedRect(x, y, w, h, cornerR))
                { px[y * w + x] = new Color(0,0,0,0); continue; }

                float g1 = Mathf.PerlinNoise((x + offX) * 0.15f, (y + offY) * 0.15f);
                float g2 = Mathf.PerlinNoise((x + offX) * 0.04f, (y + offY) * 0.04f);
                Color c  = Color.Lerp(parchmentDark, parchmentBase, g1 * 0.6f + g2 * 0.4f);

                float age = Mathf.PerlinNoise((x + offX) * 0.02f, (y + offY) * 0.02f);
                c = Color.Lerp(c, ageSpot, age * 0.10f);

                float cx     = (x - w * 0.5f) / (w * 0.5f);
                float cy     = (y - h * 0.5f) / (h * 0.5f);
                float dist   = Mathf.Sqrt(cx * cx + cy * cy);
                float bright = Mathf.Pow(Mathf.Clamp01(1f - dist * 0.55f), 1.1f);
                c = Color.Lerp(c, new Color(0.99f, 0.97f, 0.92f), bright * 0.75f);

                px[y * w + x] = c;
            }
        }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(cornerR, cornerR, cornerR, cornerR));
    }

    Sprite CreateWeatheringSprite(int w, int h, int seed)
    {
        System.Random rng = new System.Random(seed + 99);
        var tex   = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px    = new Color[w * h];
        var clear = new Color(0, 0, 0, 0);
        float bX  = (float)(rng.NextDouble() * 200 + 10);
        float bY  = (float)(rng.NextDouble() * 200 + 10);
        int cornerR = 8;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (!InsideRoundedRect(x, y, w, h, cornerR))
                { px[y * w + x] = clear; continue; }

                float edge = EdgeDistance(x, y, w, h);
                if (edge > 30f) { px[y * w + x] = clear; continue; }

                Color ov   = clear;
                float rimN = Mathf.PerlinNoise((x + bX) * 0.10f + 55f, (y + bY) * 0.10f + 55f);
                if (edge < 22f)
                {
                    float rf = Mathf.Pow(Mathf.Clamp01(1f - edge / 22f), 0.7f)
                             * Mathf.Lerp(0.5f, 1f, rimN);
                    ov = new Color(0.80f, 0.70f, 0.52f, rf * 0.50f);
                }

                float burnN = Mathf.PerlinNoise((x + bX) * 0.09f, (y + bY) * 0.09f);
                float burnF = Mathf.Pow(Mathf.Clamp01(1f - edge / 14f), 1.5f) * burnN;
                if (burnF > 0.01f)
                    ov = new Color(
                        Mathf.Lerp(ov.r, burnedEdge.r, burnF),
                        Mathf.Lerp(ov.g, burnedEdge.g, burnF),
                        Mathf.Lerp(ov.b, burnedEdge.b, burnF),
                        Mathf.Max(ov.a, burnF * 0.92f));

                float cDist = CornerDist(x, y, w, h);
                float cN    = Mathf.PerlinNoise((x + bX) * 0.05f, (y + bY) * 0.05f);
                float cBurn = Mathf.Pow(Mathf.Clamp01(1f - cDist / 40f), 2f)
                            * Mathf.Lerp(0.3f, 1f, cN);
                if (cBurn > 0.01f)
                    ov = new Color(
                        Mathf.Lerp(ov.r, burnedEdge.r, cBurn),
                        Mathf.Lerp(ov.g, burnedEdge.g, cBurn),
                        Mathf.Lerp(ov.b, burnedEdge.b, cBurn),
                        Mathf.Max(ov.a, cBurn * 0.88f));

                px[y * w + x] = ov;
            }
        }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(cornerR, cornerR, cornerR, cornerR));
    }

    bool InsideRoundedRect(int x, int y, int w, int h, int r)
    {
        int cx = Mathf.Min(x, w - 1 - x);
        int cy = Mathf.Min(y, h - 1 - y);
        if (cx < r && cy < r)
        {
            float dx = r - cx, dy = r - cy;
            return dx * dx + dy * dy <= r * r;
        }
        return true;
    }

    float EdgeDistance(int x, int y, int w, int h)
        => Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));

    float CornerDist(int x, int y, int w, int h)
    {
        float cx = Mathf.Min(x, w - 1 - x);
        float cy = Mathf.Min(y, h - 1 - y);
        return Mathf.Sqrt(cx * cx + cy * cy);
    }
}