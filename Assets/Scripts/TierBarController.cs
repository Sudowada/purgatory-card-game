using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;

/// <summary>
/// Drives the tier bar in the HUD.
/// No custom shader — uses plain Image color + RectTransform fill animation.
/// Tier-up: particle burst colored to match tier + white flash.
/// Tier-down: blue debuff pulses.
/// </summary>
public class TierBarController : MonoBehaviour
{
    public static TierBarController Instance { get; private set; }

    [Header("References — auto-found if null")]
    public Image            barImage;
    public TextMeshProUGUI  tierDotsText;
    public TextMeshProUGUI  nextHintText;
    public ParticleSystem   sparkParticles;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip   tierUpClip;
    public AudioClip   tierDownClip;

    [Header("Tuning")]
    public float fillAnimDuration = 0.35f;
    public float flashDuration    = 0.25f;
    public float debuffDuration   = 0.55f;

    // ── Tier color palette ────────────────────────────────────────────────────
    private static readonly Color[] tierColors = new Color[]
    {
        new Color(0.55f, 0.06f, 0.06f, 1f),  // 0 — penalty    dark red
        new Color(0.85f, 0.35f, 0.05f, 1f),  // 1 — tier 1     burnt orange
        new Color(0.90f, 0.58f, 0.05f, 1f),  // 2 — tier 2     amber
        new Color(0.88f, 0.82f, 0.08f, 1f),  // 3 — tier 3     yellow
        new Color(0.45f, 0.82f, 0.12f, 1f),  // 4 — tier 4     lime
        new Color(0.10f, 0.78f, 0.62f, 1f),  // 5 — tier 5     teal
        new Color(0.10f, 0.72f, 0.90f, 1f),  // 6 — tier 6     cyan
        new Color(0.20f, 0.40f, 0.95f, 1f),  // 7 — tier 7     blue
        new Color(0.55f, 0.15f, 0.95f, 1f),  // 8 — tier 8     deep violet
    };

    private static readonly string[] tierDotStrings = new string[]
    {
        "X", "▪", "▪▪", "▪▪▪", "▪▪▪▪",
        "▪▪▪▪▪", "▪▪▪▪▪▪", "▪▪▪▪▪▪▪", "▪▪▪▪▪▪▪▪",
    };

    private int       _currentTier = -1;
    private float     _currentFill = 0f;
    private Coroutine _animCoroutine;
    private Image     _overlayImage;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }
    }

    void Start()
    {
        // Auto-create bar fill image if not assigned
        if (barImage == null)
        {
            var barGO    = new GameObject("TierFill");
            barGO.transform.SetParent(transform, false);
            var rt       = barGO.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            barImage     = barGO.AddComponent<Image>();
            barImage.color = tierColors[1];
        }

        // Flash/debuff overlay
        var overlayGO     = new GameObject("TierOverlay");
        overlayGO.transform.SetParent(transform, false);
        var ort           = overlayGO.AddComponent<RectTransform>();
        ort.anchorMin     = Vector2.zero;
        ort.anchorMax     = Vector2.one;
        ort.offsetMin     = ort.offsetMax = Vector2.zero;
        _overlayImage     = overlayGO.AddComponent<Image>();
        _overlayImage.color         = new Color(0, 0, 0, 0);
        _overlayImage.raycastTarget = false;

        // Auto-find text refs
        if (tierDotsText == null)
            tierDotsText = GameObject.Find("PreviewTier")?.GetComponent<TextMeshProUGUI>();
        if (nextHintText == null)
            nextHintText = GameObject.Find("PreviewNextHint")?.GetComponent<TextMeshProUGUI>();

        // Sparks handled via SpawnUIBurst() — no particle system needed

        SetTier(0, 0f, "");
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SetTier(int tierIndex, float fillFraction, string nextHint)
    {
        tierIndex = Mathf.Clamp(tierIndex, 0, 8);
        bool tierChanged = tierIndex != _currentTier;
        bool wentDown    = tierChanged && _currentTier >= 0 && tierIndex < _currentTier;
        bool wentUp      = tierChanged && _currentTier >= 0 && tierIndex > _currentTier;

        _currentTier = tierIndex;
        Color tierColor = tierColors[tierIndex];

        if (tierDotsText != null)
        {
            tierDotsText.text  = tierDotStrings[tierIndex];
            tierDotsText.color = tierColor;
        }

        if (nextHintText != null)
            nextHintText.text = nextHint;

        float targetFill = tierIndex == 0
            ? 0.05f
            : Mathf.Clamp01((tierIndex / 8f) * 0.85f + fillFraction * (1f / 8f));

        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(AnimateBar(targetFill, tierColor, wentUp, wentDown));
    }

    // ── Animation ─────────────────────────────────────────────────────────────

    IEnumerator AnimateBar(float targetFill, Color tierColor, bool wentUp, bool wentDown)
    {
        if (barImage == null) yield break;

        barImage.DOColor(tierColor, fillAnimDuration).SetEase(Ease.OutCubic);

        var rt = barImage.GetComponent<RectTransform>();
        if (rt != null)
        {
            float startFill = rt.anchorMax.x;
            DOTween.To(
                () => startFill,
                x  => { startFill = x; rt.anchorMax = new Vector2(x, rt.anchorMax.y); },
                targetFill, fillAnimDuration).SetEase(Ease.OutCubic);
        }

        yield return new WaitForSeconds(fillAnimDuration);
        _currentFill = targetFill;

        if (wentUp)
        {
            PlaySound(tierUpClip);

            // UI dot burst colored to this tier — localized around the bar
            SpawnUIBurst(tierColor);

            yield return StartCoroutine(FlashOverlay(new Color(1f, 1f, 1f, 0.55f), flashDuration));
        }

        if (wentDown)
        {
            PlaySound(tierDownClip);
            yield return StartCoroutine(DebuffOverlay(debuffDuration));
        }
    }

    IEnumerator FlashOverlay(Color flashCol, float duration)
    {
        if (_overlayImage == null) yield break;
        _overlayImage.color = new Color(flashCol.r, flashCol.g, flashCol.b, 0f);
        _overlayImage.DOFade(flashCol.a, duration * 0.3f)
            .OnComplete(() => _overlayImage.DOFade(0f, duration * 0.7f));
        yield return new WaitForSeconds(duration);
    }

    IEnumerator DebuffOverlay(float duration)
    {
        if (_overlayImage == null) yield break;
        Color debuffCol = new Color(0.15f, 0.30f, 1.00f, 1f);
        int   pulses    = 3;
        float pulseDur  = duration / pulses;

        for (int i = 0; i < pulses; i++)
        {
            float alpha = Mathf.Lerp(0.5f, 0.2f, i / (float)(pulses - 1));
            _overlayImage.color = new Color(debuffCol.r, debuffCol.g, debuffCol.b, 0f);
            _overlayImage.DOFade(alpha, pulseDur * 0.4f)
                .OnComplete(() => _overlayImage.DOFade(0f, pulseDur * 0.6f));
            yield return new WaitForSeconds(pulseDur);
        }
        _overlayImage.color = new Color(0, 0, 0, 0);
    }

    // ── Spark Particles ───────────────────────────────────────────────────────

    ParticleSystem BuildSparkParticles()
    {
        return null; // Using UI dot burst instead — particles don't scale correctly with UI
    }

    /// <summary>
    /// Spawns small colored UI dots bursting from the tier bar top edge.
    /// Stays localized around the bar — no world-space particles.
    /// </summary>
    void SpawnUIBurst(Color tierColor)
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null) return;

        // Find the overlay canvas or any screen-space canvas
        Canvas canvas = null;
        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var c in canvases)
            if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.sortingOrder >= 40)
            { canvas = c; break; }
        if (canvas == null)
            foreach (var c in canvases)
                if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                { canvas = c; break; }
        if (canvas == null) return;

        // Get screen position of bar top-center
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        // corners: 0=BL, 1=TL, 2=TR, 3=BR
        Vector3 topCenter = (corners[1] + corners[2]) * 0.5f;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.GetComponent<RectTransform>(),
            RectTransformUtility.WorldToScreenPoint(null, topCenter),
            null, out Vector2 origin);

        // Spawn 12 small dots
        for (int i = 0; i < 12; i++)
        {
            var dotGO  = new GameObject("TierSpark");
            dotGO.transform.SetParent(canvas.transform, false);
            var dotRT  = dotGO.AddComponent<RectTransform>();
            dotRT.anchorMin        = new Vector2(0.5f, 0.5f);
            dotRT.anchorMax        = new Vector2(0.5f, 0.5f);
            dotRT.sizeDelta        = new Vector2(Random.Range(4f, 8f), Random.Range(4f, 8f));
            dotRT.anchoredPosition = origin;

            var img           = dotGO.AddComponent<Image>();
            img.color         = Color.Lerp(tierColor, Color.white, Random.Range(0f, 0.4f));
            img.raycastTarget = false;

            // Burst in a cone upward from bar top
            float angle = Random.Range(30f, 150f) * Mathf.Deg2Rad; // upward arc
            float dist  = Random.Range(15f, 50f);
            Vector2 dest = origin + new Vector2(
                Mathf.Cos(angle) * dist,
                Mathf.Sin(angle) * dist);

            float dur = Random.Range(0.25f, 0.45f);
            var seq   = DOTween.Sequence();
            seq.Append(dotRT.DOAnchorPos(dest, dur).SetEase(Ease.OutCubic));
            seq.Join(img.DOFade(0f, dur).SetEase(Ease.InCubic));
            seq.Join(dotGO.transform.DOScale(0.1f, dur).SetEase(Ease.InCubic));
            seq.OnComplete(() => { if (dotGO != null) Destroy(dotGO); });
        }
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
        else if (clip != null)
            AudioSource.PlayClipAtPoint(clip, Vector3.zero);
    }
}