using TMPro;
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
        progress.rectTransform.sizeDelta = new Vector2(324f * fraction, 7f);
        progress.color = manager.RoundTarget > 0 && manager.RoundScore >= manager.RoundTarget
            ? new Color(0.45f, 0.8f, 0.55f) : new Color(0.85f, 0.66f, 0.32f);
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

        RectTransform panel = CreateRect("Score Panel", canvasObject.transform, position, new Vector2(360f, 190f));
        Image background = panel.gameObject.AddComponent<Image>();
        background.color = new Color(0.055f, 0.06f, 0.075f, 0.92f);
        background.raycastTarget = false;
        heading = CreateText("Round", panel, new Vector2(18f, -14f), new Vector2(324f, 25f), 19f, new Color(0.85f, 0.66f, 0.32f));
        score = CreateText("Score", panel, new Vector2(18f, -43f), new Vector2(324f, 48f), 38f, Color.white);
        RectTransform track = CreateRect("Progress Track", panel, new Vector2(18f, -101f), new Vector2(324f, 7f));
        Image trackImage = track.gameObject.AddComponent<Image>();
        trackImage.color = new Color(1f, 1f, 1f, 0.12f);
        trackImage.raycastTarget = false;
        RectTransform fill = CreateRect("Progress Fill", track, Vector2.zero, new Vector2(324f, 7f));
        progress = fill.gameObject.AddComponent<Image>();
        progress.raycastTarget = false;
        details = CreateText("Details", panel, new Vector2(18f, -123f), new Vector2(324f, 53f), 17f, new Color(0.76f, 0.75f, 0.71f));
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
