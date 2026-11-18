using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Runtime score display. Added automatically by GameManager.</summary>
[DisallowMultipleComponent]
public class ScoreHUD : MonoBehaviour
{
    [SerializeField] private Vector2 position = new Vector2(24f, -24f);
    [SerializeField] private bool showDuringExploration = false;

    [Header("Number Feedback")]
    [SerializeField, Min(0f)] private float idleMovement = 0.45f;
    [SerializeField, Min(0f)] private float scoreShake = 2.5f;
    [SerializeField, Range(0f, 0.3f)] private float scoreBounce = 0.12f;
    [SerializeField, Min(0.05f)] private float feedbackDuration = 0.55f;
    [SerializeField, Min(0.05f)] private float countUpDuration = 0.45f;

    [Header("Difference Tier Animation")]
    [SerializeField, Range(0f, 0.3f)] private float tierPunchStrength = 0.12f;
    [SerializeField, Range(0.05f, 1f)] private float tierPunchDuration = 0.30f;
    [SerializeField, Range(0.05f, 1f)] private float tierFlashDuration = 0.25f;
    [SerializeField, Range(1f, 20f)] private float tierFillSpeed = 8f;
    [SerializeField, Range(0, 24)] private int maxTierSparks = 12;
    [SerializeField] private AudioSource tierAudioSource;
    [SerializeField] private AudioClip tierUpSound;

    private static readonly Color[] TierColors =
    {
        new Color(0.55f, 0.06f, 0.06f), // penalty
        new Color(0.85f, 0.35f, 0.05f), // 0-2
        new Color(0.90f, 0.58f, 0.05f), // 3-5
        new Color(0.88f, 0.82f, 0.08f), // 6-9
        new Color(0.45f, 0.82f, 0.12f), // 10-14
        new Color(0.10f, 0.78f, 0.62f), // 15-19
        new Color(0.10f, 0.72f, 0.90f), // 20-24
        new Color(0.20f, 0.40f, 0.95f), // 25-30
        new Color(0.55f, 0.15f, 0.95f)  // 31+
    };
    private static readonly int[] TierStarts = { 0, 0, 3, 6, 10, 15, 20, 25, 31 };

    private TextMeshProUGUI pointsValue;
    private TextMeshProUGUI additiveMultValue;
    private TextMeshProUGUI multiplicativeMultValue;
    private TextMeshProUGUI tierMultValue;
    private TextMeshProUGUI differenceValue;
    private TextMeshProUGUI nextTierHint;
    private TextMeshProUGUI discardStatus;
    private RectTransform tierTrack;
    private Image tierFill;
    private float tierFillDisplayed;
    private float tierFillTarget;
    private float tierFlashRemaining;
    private int lastPreviewTier = -1;
    private Color activeTierColor = TierColors[1];

    private float displayedScore;
    private float countFrom;
    private int targetScore;
    private int observedRound = -1;
    private float countElapsed;
    private float impactElapsed = 100f;
    private float impactStrength;
    private bool wasVisible;
    private bool reachedTarget;
    private int animatedDigits;

    private GameObject canvasObject;
    private CanvasGroup group;
    private TextMeshProUGUI heading;
    private TextMeshProUGUI score;
    private TextMeshProUGUI details;
    private Image progress;
    private string lastHeading;
    private string lastScore;
    private string lastDetails;

    private void Start()
    {
        BuildUI();
        Refresh();
    }

    private void LateUpdate()
    {
        Refresh();
        if (group != null && group.alpha > 0f) AnimateScoreDigits();
    }

    private void OnDisable()
    {
        if (group != null) group.alpha = 0f;
    }

    private void OnDestroy()
    {
        if (tierMultValue != null) tierMultValue.rectTransform.DOKill();
        if (tierTrack != null) tierTrack.DOKill();
        if (canvasObject != null) Destroy(canvasObject);
    }

    private void Refresh()
    {
        if (group == null) return;
        GameManager manager = GameManager.Instance;
        bool seated = GameStateManager.Instance == null ||
            GameStateManager.Instance.CurrentState == GameState.CardGame;
        bool visible = manager != null && !manager.IsShopOpen &&
            (seated || showDuringExploration);
        group.alpha = visible ? 1f : 0f;
        if (!visible) { wasVisible = false; return; }

        bool newRound = observedRound != manager.CurrentRoundNumber;
        if (!wasVisible || newRound || manager.RoundScore < targetScore)
        {
            observedRound = manager.CurrentRoundNumber;
            targetScore = manager.RoundScore;
            displayedScore = targetScore;
            countFrom = displayedScore;
            countElapsed = countUpDuration;
            impactElapsed = feedbackDuration;
            reachedTarget = manager.RoundTarget > 0 && targetScore >= manager.RoundTarget;
        }
        else if (manager.RoundScore != targetScore)
        {
            int gained = manager.RoundScore - targetScore;
            countFrom = displayedScore;
            targetScore = manager.RoundScore;
            countElapsed = 0f;
            impactElapsed = 0f;
            impactStrength = Mathf.Lerp(0.65f, 1.25f,
                Mathf.Clamp01((float)gained / Mathf.Max(1, manager.RoundTarget)));
            bool nowReached = manager.RoundTarget > 0 && targetScore >= manager.RoundTarget;
            if (nowReached && !reachedTarget) impactStrength = 1.6f;
            reachedTarget = nowReached;
        }
        wasVisible = true;
        countElapsed += Time.unscaledDeltaTime;
        float countT = Mathf.Clamp01(countElapsed / Mathf.Max(0.05f, countUpDuration));
        float eased = 1f - Mathf.Pow(1f - countT, 3f);
        displayedScore = Mathf.Lerp(countFrom, targetScore, eased);
        if (countT >= 1f) displayedScore = targetScore;
        impactElapsed += Time.unscaledDeltaTime;

        string newHeading = manager.CurrentRoundNumber > 0
            ? $"ROUND {manager.CurrentRoundNumber}" : "READY TO PLAY";
        string digits = Mathf.RoundToInt(displayedScore).ToString("N0");
        animatedDigits = digits.Length;
        string newScore = $"{digits} <size=23><color=#B8B2A6>/ {manager.RoundTarget:N0}</color></size>";
        string newDetails = $"HANDS  {manager.HandsRemaining} / {manager.MaximumHands}       ENEMY  {manager.EnemyCardValue}\n" +
            $"DECK  {manager.CardsRemainingInDrawPile}       RUN SCORE  {manager.TotalRunScore:N0}";
        if (newHeading != lastHeading) { heading.text = newHeading; lastHeading = newHeading; }
        if (newScore != lastScore) { score.text = newScore; lastScore = newScore; }
        if (newDetails != lastDetails) { details.text = newDetails; lastDetails = newDetails; }
        float fraction = manager.RoundTarget > 0
            ? Mathf.Clamp01(displayedScore / manager.RoundTarget) : 0f;
        progress.rectTransform.sizeDelta = new Vector2(404f * fraction, 7f);
        progress.color = manager.RoundTarget > 0 && manager.RoundScore >= manager.RoundTarget
            ? new Color(0.45f, 0.8f, 0.55f) : new Color(0.85f, 0.66f, 0.32f);
        RefreshScoringIngredients(manager);
        if (discardStatus != null) discardStatus.text = manager.DiscardHudStatus;
    }

    private void RefreshScoringIngredients(GameManager manager)
    {
        if (!manager.TryGetLiveScorePreview(out ComboResult result))
        {
            pointsValue.text = "—";
            additiveMultValue.text = "—";
            multiplicativeMultValue.text = "—";
            tierMultValue.text = "—";
            tierMultValue.fontSize = 25f;
            differenceValue.text = "DIFF  —";
            nextTierHint.text = "STAGE CARDS TO SEE DIFFERENCE TIERS";
            activeTierColor = TierColors[1];
            tierFillTarget = 0f;
            lastPreviewTier = 0;
        }
        else
        {
            int tier = result.isPenalty ? 0 : Mathf.Clamp(result.diffDots, 1, 8);
            pointsValue.text = result.points.ToString("N0");
            additiveMultValue.text = $"+{result.additiveMult:0.##}";
            multiplicativeMultValue.text = $"x{result.multiplicativeMult:0.##}";
            // Negative difference scores a fixed penalty; ×0.25 is not applied.
            tierMultValue.text = result.isPenalty ? "PENALTY" : $"x{result.tierMult:0.##}";
            tierMultValue.fontSize = result.isPenalty ? 18f : 25f;
            differenceValue.text = result.difference >= 0
                ? $"DIFF  +{result.difference}" : $"DIFF  {result.difference}";

            nextTierHint.text = result.isPenalty
                ? $"+{Mathf.Abs(result.difference)} TO ESCAPE PENALTY"
                : result.nextTierAt > 0
                    ? $"+{result.nextTierAt} DIFFERENCE TO x{result.nextTierMult:0.##}"
                    : "MAX DIFFERENCE TIER";

            activeTierColor = TierColors[tier];
            if (tier == 0) tierFillTarget = 0f;
            else if (result.nextTierAt < 0) tierFillTarget = 1f;
            else
            {
                int start = TierStarts[tier];
                int next = result.difference + result.nextTierAt;
                tierFillTarget = Mathf.Clamp01(
                    (float)(result.difference - start) / Mathf.Max(1, next - start));
            }

            if (lastPreviewTier >= 0 && tier > lastPreviewTier)
                CelebrateTier(tier);
            lastPreviewTier = tier;
        }

        float blend = 1f - Mathf.Exp(-tierFillSpeed * Time.unscaledDeltaTime);
        tierFillDisplayed = Mathf.Lerp(tierFillDisplayed, tierFillTarget, blend);
        if (Mathf.Abs(tierFillDisplayed - tierFillTarget) < 0.002f)
            tierFillDisplayed = tierFillTarget;
        tierFill.rectTransform.sizeDelta = new Vector2(404f * tierFillDisplayed, 12f);

        float flash = tierFlashDuration > 0f
            ? Mathf.Clamp01(tierFlashRemaining / tierFlashDuration) : 0f;
        tierFill.color = Color.Lerp(activeTierColor, Color.white, flash * 0.75f);
        tierMultValue.color = Color.Lerp(activeTierColor, Color.white, flash * 0.65f);
        tierFlashRemaining = Mathf.Max(0f, tierFlashRemaining - Time.unscaledDeltaTime);
    }

    private void CelebrateTier(int tier)
    {
        float intensity = Mathf.Clamp01((tier - 1f) / 7f);
        tierFlashRemaining = tierFlashDuration;
        RectTransform value = tierMultValue.rectTransform;
        value.DOKill(false);
        value.localScale = Vector3.one;
        value.DOPunchScale(Vector3.one * tierPunchStrength * (1f + intensity * 1.8f),
            tierPunchDuration, 7 + tier, 0.45f).SetUpdate(true);
        if (tierUpSound != null)
        {
            if (tierAudioSource != null) tierAudioSource.PlayOneShot(tierUpSound, 0.4f + 0.6f * intensity);
            else AudioSource.PlayClipAtPoint(tierUpSound, Vector3.zero, 0.4f + 0.6f * intensity);
        }
        int sparkCount = Mathf.RoundToInt(maxTierSparks * (0.3f + 0.7f * intensity));
        for (int i = 0; i < sparkCount; i++)
            SpawnTierSpark(intensity);
    }

    private void SpawnTierSpark(float intensity)
    {
        var spark = new GameObject("Tier Pixel Spark", typeof(RectTransform), typeof(Image));
        var rt = spark.GetComponent<RectTransform>();
        rt.SetParent(tierTrack, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        float size = Random.Range(3f, 6f);
        rt.sizeDelta = new Vector2(size, size);
        Vector2 origin = new Vector2(404f * tierFillTarget, -6f);
        rt.anchoredPosition = origin;
        Image image = spark.GetComponent<Image>();
        image.raycastTarget = false;
        image.color = Color.Lerp(activeTierColor, Color.white, 0.45f);
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float radius = Random.Range(12f, 30f + 30f * intensity);
        var seq = DOTween.Sequence().SetUpdate(true);
        seq.Join(rt.DOAnchorPos(origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
            0.24f + 0.12f * intensity));
        seq.Join(image.DOFade(0f, 0.30f + 0.12f * intensity));
        seq.OnComplete(() => { if (spark != null) Destroy(spark); });
    }

    private void AnimateScoreDigits()
    {
        // Rebuild pristine vertices first: offsets never accumulate across frames.
        score.ForceMeshUpdate();
        TMP_TextInfo info = score.textInfo;
        float phase = Time.unscaledTime;
        float t = Mathf.Clamp01(impactElapsed / Mathf.Max(0.05f, feedbackDuration));
        float envelope = (1f - t) * (1f - t) * impactStrength;
        for (int i = 0; i < Mathf.Min(animatedDigits, info.characterCount); i++)
        {
            TMP_CharacterInfo character = info.characterInfo[i];
            if (!character.isVisible) continue;
            Vector3[] vertices = info.meshInfo[character.materialReferenceIndex].vertices;
            int vertex = character.vertexIndex;
            Vector3 center = (vertices[vertex] + vertices[vertex + 2]) * 0.5f;
            float digitPhase = i * 0.7f;
            float idle = Mathf.Sin(phase * 2.8f + digitPhase) * idleMovement;
            float x = Mathf.Sin(impactElapsed * 57f + digitPhase) * scoreShake * envelope;
            float y = idle + Mathf.Sin(impactElapsed * 34f + digitPhase) * scoreShake * envelope;
            float bounce = 1f + Mathf.Cos(impactElapsed * 22f - digitPhase * 0.3f) * scoreBounce * envelope;
            float angle = Mathf.Sin(impactElapsed * 29f + digitPhase) * 2f * envelope;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
            for (int corner = 0; corner < 4; corner++)
                vertices[vertex + corner] = center + rotation * ((vertices[vertex + corner] - center) * bounce) + new Vector3(x, y, 0f);
        }
        score.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        score.color = Color.Lerp(Color.white,
            reachedTarget ? new Color(0.55f, 1f, 0.65f) : new Color(1f, 0.8f, 0.4f),
            Mathf.Clamp01(envelope));
    }

    private void BuildUI()
    {
        canvasObject = new GameObject("Runtime Score HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        group = canvasObject.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        RectTransform panel = CreateRect("Score Panel", canvasObject.transform, position, new Vector2(440f, 444f));
        Image background = panel.gameObject.AddComponent<Image>();
        background.color = new Color(0.055f, 0.06f, 0.075f, 0.92f);
        background.raycastTarget = false;
        heading = CreateText("Round", panel, new Vector2(18f, -14f), new Vector2(404f, 25f), 19f, new Color(0.85f, 0.66f, 0.32f));
        score = CreateText("Score", panel, new Vector2(18f, -43f), new Vector2(404f, 48f), 38f, Color.white);
        RectTransform track = CreateRect("Progress Track", panel, new Vector2(18f, -101f), new Vector2(404f, 7f));
        Image trackImage = track.gameObject.AddComponent<Image>();
        trackImage.color = new Color(1f, 1f, 1f, 0.12f);
        trackImage.raycastTarget = false;
        RectTransform fill = CreateRect("Progress Fill", track, Vector2.zero, new Vector2(404f, 7f));
        progress = fill.gameObject.AddComponent<Image>();
        progress.raycastTarget = false;
        details = CreateText("Details", panel, new Vector2(18f, -123f), new Vector2(404f, 53f), 16f, new Color(0.76f, 0.75f, 0.71f));
        CreateText("Offering Subtitle", panel, new Vector2(18f, -192f),
            new Vector2(404f, 20f), 16f, new Color(0.85f, 0.66f, 0.32f)).text = "OFFER SCORE";

        // Four separate ingredients, so +Mult does not hide rare xMult and
        // the guaranteed difference xMult remains a readable reward.
        pointsValue = CreateIngredient(panel, "POINTS", 18f);
        additiveMultValue = CreateIngredient(panel, "+MULT", 121f);
        multiplicativeMultValue = CreateIngredient(panel, "xMULT", 224f);
        tierMultValue = CreateIngredient(panel, "DIFF xMULT", 327f);

        differenceValue = CreateText("Difference", panel, new Vector2(18f, -308f),
            new Vector2(404f, 30f), 22f, Color.white);
        tierTrack = CreateRect("Difference Tier Track", panel,
            new Vector2(18f, -345f), new Vector2(404f, 12f));
        var tierTrackImage = tierTrack.gameObject.AddComponent<Image>();
        tierTrackImage.color = new Color(1f, 1f, 1f, 0.14f);
        tierTrackImage.raycastTarget = false;
        RectTransform tierFillRect = CreateRect("Difference Tier Fill", tierTrack, Vector2.zero, new Vector2(0f, 12f));
        tierFill = tierFillRect.gameObject.AddComponent<Image>();
        tierFill.color = TierColors[1];
        tierFill.raycastTarget = false;
        nextTierHint = CreateText("Next Difference Tier", panel, new Vector2(18f, -366f),
            new Vector2(404f, 24f), 16f, new Color(0.77f, 0.75f, 0.68f));
        RectTransform discardTile = CreateRect("Discard Tile", panel,
            new Vector2(18f, -401f), new Vector2(404f, 31f));
        Image discardBackground = discardTile.gameObject.AddComponent<Image>();
        discardBackground.color = new Color(0.14f, 0.15f, 0.16f);
        discardBackground.raycastTarget = false;
        discardStatus = CreateText("Discard Status", discardTile,
            new Vector2(8f, -6f), new Vector2(388f, 23f), 14f,
            new Color(0.90f, 0.73f, 0.43f));
    }

    private static TextMeshProUGUI CreateIngredient(Transform parent, string name, float x)
    {
        RectTransform panel = CreateRect(name + " Panel", parent, new Vector2(x, -221f),
            new Vector2(99f, 72f));
        Image background = panel.gameObject.AddComponent<Image>();
        background.color = new Color(0.14f, 0.15f, 0.16f, 1f);
        background.raycastTarget = false;
        CreateText(name + " Label", panel, new Vector2(8f, -8f),
            new Vector2(85f, 18f), name == "DIFF xMULT" ? 12f : 14f,
            new Color(0.68f, 0.66f, 0.62f)).text = name;
        var value = CreateText(name + " Value", panel, new Vector2(8f, -30f),
            new Vector2(85f, 34f), 23f, Color.white);
        value.enableAutoSizing = true;
        value.fontSizeMin = 13f;
        value.fontSizeMax = 23f;
        return value;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 offset, Vector2 size)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = offset;
        rect.sizeDelta = size;
        return rect;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, Vector2 offset, Vector2 size, float fontSize, Color color)
    {
        TextMeshProUGUI label = CreateRect(name, parent, offset, size).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.raycastTarget = false;
        return label;
    }
}
