using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Handles play/discard/draw animations, particles, and combo screen effects.
///
/// UPDATED FOR 3D: the old version animated a separate cardVisual UI object
/// per card. That's gone now — Card3DInteractable/Card3DView own all
/// per-card visual behavior. This version keeps the same public API
/// (PlayCards/DrawCards) so GameManager's existing calls don't need to
/// change, but the lift/fly-to-discard motion now happens on the real
/// Card's own transform, and sparks spawn at its real 3D position.
/// </summary>
public class CardAnimator : MonoBehaviour
{
    public static CardAnimator Instance { get; private set; }

    [Header("Positions")]
    public Vector3 discardPilePos;
    public Vector3 deckPos;

    [Header("Timing")]
    [SerializeField] private float cardFlyDuration       = 0.38f;
    [SerializeField] private float drawFlyDuration       = 0.32f;
    [SerializeField] private float staggerDelay          = 0.07f;

    [Header("Combo Flash Thresholds")]
    [SerializeField] private float bigComboMultiplier     = 1.5f;
    [SerializeField] private float massiveComboMultiplier = 2.0f;

    private Canvas     fxCanvas;
    private CanvasGroup flashOverlay;

    private List<TextMeshProUGUI> scorePopupPool = new List<TextMeshProUGUI>();
    private const int POOL_SIZE = 8;

    // Shared particle material — assigned once, reused everywhere
    private Material _particleMat;

    static readonly Color c_spark       = new Color(1.0f, 0.75f, 0.15f);
    static readonly Color c_sparkBlood  = new Color(0.8f, 0.15f, 0.05f);
    static readonly Color c_flashBig    = new Color(0.9f, 0.7f, 0.2f, 0f);
    static readonly Color c_flashMassive= new Color(0.7f, 0.05f, 0.05f, 0f);

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }
    }

    void Start()
    {
        // Create shared particle material once — fixes pink/magenta squares
        _particleMat = new Material(Shader.Find("Sprites/Default"));

        BuildFXCanvas();
        SetDefaultPositions();
    }

    void SetDefaultPositions()
    {
        var deckGO    = GameObject.Find("DeckStack");
        var discardGO = GameObject.Find("DiscardStack");

        if (deckGO != null)
            deckPos = deckGO.transform.position;
        else
        {
            var cam = Camera.main;
            deckPos = cam != null
                ? cam.ViewportToWorldPoint(new Vector3(0.88f, 0.12f, 10f))
                : new Vector3(6.6f, -3.6f, 0f);
        }

        if (discardGO != null)
            discardPilePos = discardGO.transform.position;
        else
        {
            var cam = Camera.main;
            discardPilePos = cam != null
                ? cam.ViewportToWorldPoint(new Vector3(0.12f, 0.12f, 10f))
                : new Vector3(-6.6f, -3.6f, 0f);
        }
    }

    // ── PUBLIC API ────────────────────────────────────────────────────────────

    public IEnumerator PlayCards(List<Card> cards, ComboResult result)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            Card card = cards[i];
            if (card == null) continue;
            int idx = i;
            DOVirtual.DelayedCall(idx * staggerDelay, () => LiftCard(card));
        }

        yield return new WaitForSeconds(cards.Count * staggerDelay + 0.15f);

        foreach (Card card in cards)
        {
            if (card == null) continue;
            SpawnSparks(card.transform.position, result.multiplier >= bigComboMultiplier);
        }

        for (int i = 0; i < cards.Count; i++)
        {
            Card card = cards[i];
            if (card == null) continue;
            int idx = i;
            DOVirtual.DelayedCall(idx * 0.04f, () => FlyToDiscard(card));
        }

        yield return new WaitForSeconds(cardFlyDuration + 0.1f);

        SpawnScorePopup(result.finalScore, result.multiplier);

        if (result.multiplier >= massiveComboMultiplier)
            StartCoroutine(ComboFlash(c_flashMassive, true));
        else if (result.multiplier >= bigComboMultiplier)
            StartCoroutine(ComboFlash(c_flashBig, false));

        yield return new WaitForSeconds(0.25f);
    }

    public IEnumerator DrawCards(List<Card> cards)
    {
        yield return new WaitForEndOfFrame();

        for (int i = 0; i < cards.Count; i++)
        {
            Card card = cards[i];
            if (card == null) continue;

            Transform cardTransform = card.transform;
            int   idx        = i;
            float delay      = idx * staggerDelay;

            DOVirtual.DelayedCall(delay, () =>
            {
                if (card == null) return;

                Vector3 destination = cardTransform.position;

                cardTransform.DOKill();
                cardTransform.position   = deckPos;
                cardTransform.localScale = cardTransform.localScale * 0.3f;

                Sequence seq = DOTween.Sequence();
                seq.Append(cardTransform.DOMove(destination, drawFlyDuration).SetEase(Ease.OutCubic));
                seq.Join(cardTransform.DOScale(cardTransform.localScale / 0.3f, drawFlyDuration).SetEase(Ease.OutBack));
                seq.OnComplete(() => SpawnSparks(destination, false, 5));
            });
        }

        yield return new WaitForSeconds(cards.Count * staggerDelay + drawFlyDuration + 0.15f);
    }

    // ── CARD ANIMATIONS ───────────────────────────────────────────────────────

    void LiftCard(Card card)
    {
        if (card == null) return;
        Transform t = card.transform;
        t.DOKill();
        t.DOPunchScale(Vector3.one * 0.18f, 0.2f, 5, 0.5f);
        t.DOMove(t.position + Vector3.up * 0.05f, 0.12f).SetEase(Ease.OutQuad);
    }

    void FlyToDiscard(Card card)
    {
        if (card == null) return;
        Transform t = card.transform;

        Vector3 start = t.position;
        Vector3 end   = discardPilePos;
        Vector3 mid   = (start + end) * 0.5f + Vector3.up * 0.25f;

        t.DOKill();
        Sequence seq = DOTween.Sequence();
        seq.Append(t.DOPath(new[] { start, mid, end }, cardFlyDuration, PathType.CatmullRom)
            .SetEase(Ease.InQuad));
        seq.Join(t.DORotate(new Vector3(0f, Random.Range(-200f, 200f), 0f),
            cardFlyDuration, RotateMode.FastBeyond360).SetEase(Ease.InQuad));
    }

    // ── PARTICLES ─────────────────────────────────────────────────────────────

    void SpawnSparks(Vector3 worldPos, bool bloody, int count = 12)
    {
        var go = new GameObject("Sparks");
        go.transform.position = worldPos;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(2.5f, 6f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
            bloody ? c_sparkBlood : c_spark,
            bloody ? new Color(0.5f, 0.05f, 0.05f) : new Color(1f, 0.95f, 0.6f));
        main.maxParticles    = count * 2;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = new ParticleSystem.MinMaxCurve(1.2f);
        main.loop            = false;

        var emission = ps.emission;
        emission.SetBursts(new[] {
            new ParticleSystem.Burst(0f, (short)count, (short)(count * 2), 1, 0.01f)
        });
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.15f;

        var col  = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(bloody ? c_sparkBlood : c_spark, 0f),
                new GradientColorKey(new Color(0.2f, 0.1f, 0.05f), 1f)
            },
            new[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var size  = ps.sizeOverLifetime;
        size.enabled = true;
        var curve = new AnimationCurve();
        curve.AddKey(0f, 1f); curve.AddKey(1f, 0f);
        size.size = new ParticleSystem.MinMaxCurve(1f, curve);

        // ── Assign material — fixes pink/magenta squares ──────────────────
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        if (renderer != null && _particleMat != null)
            renderer.material = _particleMat;

        ps.Play();
        Destroy(go, 2f);
    }

    // ── SCREEN EFFECTS ────────────────────────────────────────────────────────

    IEnumerator ComboFlash(Color flashColor, bool withZoom)
    {
        if (flashOverlay != null)
        {
            var img   = flashOverlay.GetComponent<Image>();
            img.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
            flashOverlay.gameObject.SetActive(true);
            img.DOFade(withZoom ? 0.35f : 0.22f, 0.06f)
               .OnComplete(() => img.DOFade(0f, withZoom ? 0.45f : 0.3f));
        }

        if (withZoom)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.transform.DOKill();
                cam.transform.DOShakePosition(0.3f, 0.06f, 14, 90f, false, true);
            }
        }

        yield return null;
    }

    void SpawnScorePopup(int score, float multiplier)
    {
        TextMeshProUGUI popup = GetPooledPopup();
        if (popup == null) return;

        RectTransform rt = popup.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(0f, -80f);

        bool isBig     = multiplier >= bigComboMultiplier;
        bool isMassive = multiplier >= massiveComboMultiplier;

        popup.text      = $"+{score}";
        popup.fontSize  = isMassive ? 72f : isBig ? 58f : 44f;
        popup.color     = isMassive
            ? new Color(0.9f, 0.2f, 0.1f, 1f)
            : isBig
                ? new Color(0.95f, 0.75f, 0.2f, 1f)
                : new Color(0.88f, 0.82f, 0.65f, 1f);

        popup.gameObject.SetActive(true);
        popup.transform.localScale = Vector3.one * 0.3f;

        Sequence seq = DOTween.Sequence();
        seq.Append(popup.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack));
        seq.Join(rt.DOAnchorPosY(
            rt.anchoredPosition.y + (isMassive ? 220f : 160f), 0.9f)
            .SetEase(Ease.OutCubic));
        seq.AppendInterval(0.15f);
        seq.Append(popup.DOFade(0f, 0.3f));
        seq.OnComplete(() => popup.gameObject.SetActive(false));
    }

    TextMeshProUGUI GetPooledPopup()
    {
        foreach (var p in scorePopupPool)
            if (p != null && !p.gameObject.activeSelf) return p;

        if (scorePopupPool.Count >= POOL_SIZE) return null;

        var go = new GameObject("ScorePopup");
        go.transform.SetParent(fxCanvas.transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(300f, 100f);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.alignment        = TextAlignmentOptions.Center;
        tmp.fontStyle        = FontStyles.Bold;
        tmp.raycastTarget    = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;

        go.SetActive(false);
        scorePopupPool.Add(tmp);
        return tmp;
    }

    // ── CANVAS SETUP ──────────────────────────────────────────────────────────

    void BuildFXCanvas()
    {
        var go = new GameObject("CardFXCanvas");
        fxCanvas = go.AddComponent<Canvas>();
        fxCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        fxCanvas.sortingOrder = 50;
        go.AddComponent<CanvasScaler>();

        var flashGO = new GameObject("FlashOverlay");
        flashGO.transform.SetParent(go.transform, false);
        var flashImg = flashGO.AddComponent<Image>();
        var flashRT  = flashGO.GetComponent<RectTransform>();
        flashRT.anchorMin = Vector2.zero;
        flashRT.anchorMax = Vector2.one;
        flashRT.offsetMin = flashRT.offsetMax = Vector2.zero;
        flashImg.color         = new Color(1f, 0.8f, 0.2f, 0f);
        flashImg.raycastTarget = false;
        flashOverlay           = flashGO.AddComponent<CanvasGroup>();
        flashOverlay.alpha     = 1f;
        flashOverlay.interactable    = false;
        flashOverlay.blocksRaycasts  = false;
        flashGO.SetActive(false);
    }
}