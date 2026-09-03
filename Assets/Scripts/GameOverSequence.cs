using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;

/// <summary>
/// Drives the game-over cinematic:
///   1. Card UI fades out
///   2. Camera tilts down toward the hole beneath the altar
///   3. Vignette iris closes, player is "consumed"
///   4. Fade to black
///   5. RunStatsUI appears
///
/// Attach to any persistent GameObject (e.g. GameManager).
/// Call TriggerSequence() from GameManager.
/// </summary>
public class GameOverSequence : MonoBehaviour
{
    public static GameOverSequence Instance { get; private set; }

    [Header("Hole Position (world space)")]
    [Tooltip("Set this to the world position of the drain/hole under the altar")]
    [SerializeField] public Vector3 holeWorldPosition = new Vector3(0f, -1.1f, 0.5f);

    [Header("Timing")]
    [SerializeField] private float cardFadeTime      = 0.6f;
    [SerializeField] private float pullDuration      = 3.5f;
    [SerializeField] private float vignetteDuration  = 2.0f;
    [SerializeField] private float fadeToBlackTime   = 1.0f;
    [SerializeField] private float statsAppearDelay  = 0.8f;

    // Vignette overlay (built at runtime)
    private Image  vignetteImage;
    private Image  fadeImage;
    private Canvas overlayCanvas;
    private GraphicRaycaster overlayRaycaster;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(this);
    }

    void Start() => BuildOverlays();

    // ── Called by GameManager ─────────────────────────────────────────────────

    public void TriggerSequence()
    {
        Debug.Log("[GameOverSequence] TriggerSequence called from: " + System.Environment.StackTrace);
        StartCoroutine(RunSequence());
    }

    IEnumerator RunSequence()
    {
        // ✅ Only enable raycaster now that the sequence is actually running
        if (overlayRaycaster != null)
            overlayRaycaster.enabled = true;

        Camera cam = Camera.main;

        // 1 ── Fade out card UI
        var cardUI = FindFirstObjectByType<HUDBuilder>()?.GetComponent<CanvasGroup>();
        if (cardUI != null)
        {
            cardUI.DOFade(0f, cardFadeTime);
            yield return new WaitForSeconds(cardFadeTime + 0.1f);
        }

        // Disable card interaction
        if (cardUI != null) { cardUI.interactable = false; cardUI.blocksRaycasts = false; }

        // 2 ── Subtle rumble / screen shake before pull
        if (cam != null)
        {
            cam.transform.DOShakePosition(0.6f, 0.04f, 12, 90f, false, true);
            yield return new WaitForSeconds(0.7f);
        }

        // 3 ── Pull camera toward the hole
        if (cam != null)
        {
            Vector3 startPos = cam.transform.position;
            Quaternion startRot = cam.transform.rotation;

            Vector3    lookDir   = (holeWorldPosition - startPos).normalized;
            Quaternion targetRot = Quaternion.LookRotation(lookDir);

            Vector3 targetPos = Vector3.Lerp(startPos, holeWorldPosition, 0.55f)
                                + new Vector3(0f, -0.6f, 0f);

            cam.transform.DORotateQuaternion(targetRot, pullDuration * 0.35f)
                .SetEase(Ease.InCubic);

            cam.transform.DOMove(targetPos, pullDuration)
                .SetEase(Ease.InExpo);

            float startFOV = cam.fieldOfView;
            DOTween.To(() => cam.fieldOfView, x => cam.fieldOfView = x,
                startFOV * 0.3f, pullDuration).SetEase(Ease.InExpo);
        }

        // 4 ── Vignette iris closes during pull
        StartCoroutine(IrisClose(vignetteDuration, pullDuration * 0.4f));

        yield return new WaitForSeconds(pullDuration);

        // 5 ── Hard cut to black
        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            fadeImage.DOFade(1f, fadeToBlackTime);
        }

        yield return new WaitForSeconds(fadeToBlackTime + statsAppearDelay);

        // 6 ── Show stats screen
        RunStatsUI.Instance?.Show();

        // Reset FOV
        if (cam != null) cam.fieldOfView = 75f;
    }

    // ── Vignette iris close ───────────────────────────────────────────────────

    IEnumerator IrisClose(float duration, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (vignetteImage == null) yield break;

        vignetteImage.gameObject.SetActive(true);
        float elapsed = 0f;

        vignetteImage.color = new Color(0f, 0f, 0f, 0f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Pow(elapsed / duration, 2.5f);
            vignetteImage.color = new Color(0f, 0f, 0f, Mathf.Clamp01(t * 1.4f));
            yield return null;
        }

        vignetteImage.color = Color.black;
    }

    // ── Build screen overlays ─────────────────────────────────────────────────

    void BuildOverlays()
    {
        var go = new GameObject("GameOverOverlay");
        overlayCanvas = go.AddComponent<Canvas>();
        overlayCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 100;
        go.AddComponent<CanvasScaler>();

        // ✅ Add raycaster but disable it immediately — it must NOT block clicks at startup
        overlayRaycaster = go.AddComponent<GraphicRaycaster>();
        overlayRaycaster.enabled = false;

        // Vignette
        var vigGO = new GameObject("Vignette");
        vigGO.transform.SetParent(go.transform, false);
        vignetteImage = vigGO.AddComponent<Image>();
        var vigRT = vigGO.GetComponent<RectTransform>();
        vigRT.anchorMin = Vector2.zero;
        vigRT.anchorMax = Vector2.one;
        vigRT.offsetMin = vigRT.offsetMax = Vector2.zero;
        vignetteImage.color = new Color(0f, 0f, 0f, 0f);
        vignetteImage.raycastTarget = false;
        vigGO.SetActive(false);

        // Full black fade
        var fadeGO = new GameObject("FadeToBlack");
        fadeGO.transform.SetParent(go.transform, false);
        fadeImage = fadeGO.AddComponent<Image>();
        var fadeRT = fadeGO.GetComponent<RectTransform>();
        fadeRT.anchorMin = Vector2.zero;
        fadeRT.anchorMax = Vector2.one;
        fadeRT.offsetMin = fadeRT.offsetMax = Vector2.zero;
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        fadeImage.raycastTarget = false;
        fadeGO.SetActive(false);
    }
}