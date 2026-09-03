using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Attach to each Card's visual GameObject.
/// Handles the cyan glow + rise effect on selection.
/// Call SetSelected(true/false) from your card selection logic,
/// or it auto-hooks into Card.CardSelectedEvent if a Card component is found.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class CardGlowEffect : MonoBehaviour
{
    [Header("Glow Settings")]
    public Color  glowColor     = new Color(0.00f, 0.90f, 0.85f, 1f); // cyan/teal
    public float  glowSize      = 18f;   // px spread of glow layers
    public float  glowIntensity = 0.85f; // max alpha of outermost glow
    public int    glowLayers    = 4;     // number of concentric glow rings

    [Header("Rise Settings")]
    public float riseAmount   = 18f;   // px to rise on selection
    public float riseDuration = 0.18f;
    public float riseScale    = 1.06f; // slight scale up

    [Header("Pulse")]
    public bool  doPulse       = true;
    public float pulseSpeed    = 1.4f;
    public float pulseMin      = 0.65f;
    public float pulseMax      = 1.00f;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private RectTransform   _rt;
    private Vector2         _basePos;
    private bool            _selected;
    private GameObject[]    _glowLayers;
    private CanvasGroup[]   _glowGroups;
    private Tween           _pulseTween;
    private Card            _card;

    void Awake()
    {
        _rt       = GetComponent<RectTransform>();
        _basePos  = _rt.anchoredPosition;
        _card     = GetComponentInParent<Card>();

        BuildGlowLayers();

        if (_card != null)
        {
            _card.CardSelectedEvent.AddListener((card, selected) => SetSelected(selected));
        }
    }

    void OnDestroy()
    {
        _pulseTween?.Kill();
        if (_card != null)
            _card.CardSelectedEvent.RemoveAllListeners();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SetSelected(bool selected)
    {
        if (_selected == selected) return;
        _selected = selected;

        if (selected)
        {
            // Rise up
            _rt.DOAnchorPosY(_basePos.y + riseAmount, riseDuration)
               .SetEase(Ease.OutBack);
            _rt.DOScale(riseScale, riseDuration)
               .SetEase(Ease.OutBack);

            // Show glow
            ShowGlow(true);

            // Pulse
            if (doPulse) StartPulse();
        }
        else
        {
            // Return to base
            _rt.DOAnchorPosY(_basePos.y, riseDuration * 0.8f)
               .SetEase(Ease.OutCubic);
            _rt.DOScale(1f, riseDuration * 0.8f)
               .SetEase(Ease.OutCubic);

            // Hide glow
            ShowGlow(false);
            StopPulse();
        }
    }

    // Call this when the card position changes (e.g. hand reorder)
    public void UpdateBasePosition()
    {
        if (!_selected)
            _basePos = _rt.anchoredPosition;
    }

    // ── Glow Construction ─────────────────────────────────────────────────────

    void BuildGlowLayers()
    {
        _glowLayers = new GameObject[glowLayers];
        _glowGroups = new CanvasGroup[glowLayers];

        // Find or create a canvas for glow layers
        var canvas = GetComponentInParent<Canvas>();

        for (int i = 0; i < glowLayers; i++)
        {
            float spread  = glowSize * (i + 1) / glowLayers;
            float alpha   = glowIntensity * (1f - (float)i / glowLayers);

            var go  = new GameObject($"Glow_{i}");
            go.transform.SetParent(transform, false);
            go.transform.SetSiblingIndex(0); // behind card

            var rt  = go.AddComponent<RectTransform>();
            rt.anchorMin        = Vector2.zero;
            rt.anchorMax        = Vector2.one;
            rt.offsetMin        = new Vector2(-spread, -spread);
            rt.offsetMax        = new Vector2( spread,  spread);

            var img = go.AddComponent<Image>();
            img.color         = new Color(glowColor.r, glowColor.g, glowColor.b, alpha);
            img.raycastTarget = false;

            // Use a soft rounded sprite for the glow
            img.sprite = BuildGlowSprite(32, spread);
            img.type   = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 100f;

            var cg  = go.AddComponent<CanvasGroup>();
            cg.alpha            = 0f;
            cg.interactable     = false;
            cg.blocksRaycasts   = false;

            _glowLayers[i] = go;
            _glowGroups[i] = cg;
        }
    }

    void ShowGlow(bool show)
    {
        if (_glowGroups == null) return;
        for (int i = 0; i < _glowGroups.Length; i++)
        {
            if (_glowGroups[i] == null) continue;
            float targetAlpha = show ? 1f : 0f;
            float delay       = show ? i * 0.04f : 0f;
            _glowGroups[i].DOFade(targetAlpha, 0.2f).SetDelay(delay);
        }
    }

    void StartPulse()
    {
        _pulseTween?.Kill();
        if (_glowGroups == null || _glowGroups.Length == 0) return;

        // Pulse the outermost layer
        var cg = _glowGroups[_glowGroups.Length - 1];
        if (cg == null) return;

        _pulseTween = DOTween.To(
            () => cg.alpha,
            a  => cg.alpha = a,
            pulseMax, 1f / pulseSpeed)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .From(pulseMin);
    }

    void StopPulse()
    {
        _pulseTween?.Kill();
        _pulseTween = null;
    }

    // ── Sprite Generation ─────────────────────────────────────────────────────

    static Sprite BuildGlowSprite(int size, float spread)
    {
        // Soft rounded rect — corners match card corners
        int   r   = Mathf.Clamp(Mathf.RoundToInt(spread * 0.5f) + 4, 4, size / 2);
        var   tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;

        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float alpha = IsInsideRounded(x, y, size, size, r) ? 255 : 0;
                px[y * size + x] = new Color32(255, 255, 255, (byte)alpha);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();

        var border = new Vector4(r, r, r, r);
        return Sprite.Create(tex, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect, border);
    }

    static bool IsInsideRounded(int x, int y, int w, int h, int r)
    {
        int cx = Mathf.Min(x, w - 1 - x);
        int cy = Mathf.Min(y, h - 1 - y);
        if (cx < r && cy < r)
        {
            float dx = r - cx - 0.5f, dy = r - cy - 0.5f;
            return dx * dx + dy * dy <= (float)(r * r);
        }
        return true;
    }
}
