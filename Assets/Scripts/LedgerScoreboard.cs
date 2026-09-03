using UnityEngine;
using TMPro;
using DG.Tweening;
using System.Collections;

/// <summary>
/// The diegetic scoreboard — a physical ledger book on the table.
/// Numbers are world-space TextMeshPro (not UI), and an "ink pool" quad
/// fills up like liquid toward the round threshold, changing color and
/// flickering like flame based on how the last hand scored.
///
/// INTEGRATION: GameManager currently writes to UI TextMeshProUGUI fields
/// directly (scoreText.text = ..., etc.) via UpdateHUD()/AnimateScorePreview().
/// In the Phase 4 pass, route those same values into this component's
/// public methods instead — SetScore(), ShowHandResult(), SetThresholdInfo().
///
/// SETUP:
///   1. Create an empty GameObject on the table, name it "Ledger".
///   2. Add this component. It builds its own placeholder visuals on Start()
///      if you don't assign a bookPrefab.
///   3. Once you're happy with placement/scale, swap in real book/ink
///      textures via the Inspector fields.
/// </summary>
public class LedgerScoreboard : MonoBehaviour
{
    [Header("Book Placeholder (leave null to auto-build)")]
    [SerializeField] private GameObject bookPrefab;
    [SerializeField] private Vector2 pageSize = new Vector2(0.55f, 0.4f);
    [SerializeField] private TMP_FontAsset ledgerFont;

    [Header("Ink Pool")]
    [Tooltip("Colors the ink cycles toward based on hand quality.")]
    [SerializeField] private Color inkColorNormal  = new Color(0.85f, 0.55f, 0.10f); // amber/flame
    [SerializeField] private Color inkColorGood    = new Color(0.95f, 0.35f, 0.05f); // hot orange
    [SerializeField] private Color inkColorPenalty = new Color(0.55f, 0.05f, 0.05f); // blood red
    [SerializeField] private Color inkColorIdle    = new Color(0.35f, 0.15f, 0.05f); // dim ember

    [Header("Motion")]
    [SerializeField] private float fillLerpSpeed = 2.5f;
    [SerializeField] private float flickerSpeed  = 6f;
    [SerializeField] private float flickerAmount = 0.06f;

    // Built visuals
    private TextMeshPro _scoreTMP;
    private TextMeshPro _thresholdTMP;
    private TextMeshPro _roundTMP;
    private TextMeshPro _comboTMP;
    private Transform    _inkPool;
    private MeshRenderer _inkRenderer;
    private Material     _inkMat;

    private float _targetFill  = 0f;
    private float _currentFill = 0f;
    private Color _targetInkColor;
    private Color _baseInkColor;

    void Start()
    {
        if (bookPrefab != null)
        {
            var inst = Instantiate(bookPrefab, transform);
            // Expect the prefab to have child objects named appropriately;
            // wire these up manually in the Inspector if using a real prefab.
        }
        else
        {
            BuildPlaceholderLedger();
        }

        _baseInkColor = inkColorIdle;
        _targetInkColor = inkColorIdle;
        StartCoroutine(FlickerLoop());
    }

    void Update()
    {
        if (_currentFill != _targetFill)
        {
            _currentFill = Mathf.Lerp(_currentFill, _targetFill, Time.deltaTime * fillLerpSpeed);
            if (_inkPool != null)
                _inkPool.localScale = new Vector3(_inkPool.localScale.x,
                    Mathf.Max(0.001f, _currentFill), _inkPool.localScale.z);
        }
    }

    // ── Public API — call these from GameManager ────────────────────────────

    /// <summary>Update the running round score, total, and current threshold.</summary>
    public void SetScore(int roundScore, int totalScore, int currentThreshold)
    {
        if (_scoreTMP != null)
        {
            int from = 0;
            int.TryParse(_scoreTMP.text.Replace(",", ""), out from);
            StartCoroutine(CountUpText(_scoreTMP, from, roundScore, 0.5f));
        }
        if (_thresholdTMP != null)
            _thresholdTMP.text = $"of {currentThreshold}";

        _targetFill = currentThreshold > 0
            ? Mathf.Clamp01((float)roundScore / currentThreshold)
            : 0f;
    }

    public void SetRound(int roundNumber, int stageNumber)
    {
        if (_roundTMP != null)
            _roundTMP.text = $"Round {roundNumber} · Stage {stageNumber}";
    }

    /// <summary>
    /// Call this right when a hand is scored — drives the ink's color/flicker
    /// reaction. isPenalty = red flare, containsDecayedCard = dim orange,
    /// otherwise = hot amber/gold flare that settles back to idle.
    /// </summary>
    public void ShowHandResult(string comboName, int finalScore, bool isPenalty, bool containsDecayedCard)
    {
        if (_comboTMP != null)
        {
            _comboTMP.text = isPenalty
                ? $"{comboName} — PENALTY {finalScore}"
                : $"{comboName}  +{finalScore}";
            _comboTMP.transform.DOKill();
            _comboTMP.transform.localScale = Vector3.one * 1.25f;
            _comboTMP.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
        }

        Color flare = isPenalty ? inkColorPenalty
            : containsDecayedCard ? inkColorNormal
            : inkColorGood;

        _targetInkColor = flare;
        if (_inkMat != null)
            _inkMat.DOColor(flare, 0.15f).OnComplete(() =>
            {
                _inkMat.DOColor(inkColorNormal, 1.2f).SetDelay(0.6f);
            });

        // Punch the ink pool like a droplet landing
        if (_inkPool != null)
        {
            _inkPool.DOKill();
            _inkPool.DOPunchScale(new Vector3(0.08f, 0f, 0.08f), 0.35f, 6, 0.6f);
        }
    }

    // ── Visual construction ──────────────────────────────────────────────────

    void BuildPlaceholderLedger()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // Book cover base
        var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cover.name = "LedgerCover";
        cover.transform.SetParent(transform, false);
        cover.transform.localPosition = Vector3.zero;
        cover.transform.localScale = new Vector3(pageSize.x * 2.1f, 0.03f, pageSize.y * 1.1f);
        var coverMat = new Material(lit);
        coverMat.color = new Color(0.20f, 0.10f, 0.06f);
        cover.GetComponent<MeshRenderer>().material = coverMat;
        Destroy(cover.GetComponent<Collider>());

        // Open page (right side, where numbers live)
        var page = GameObject.CreatePrimitive(PrimitiveType.Cube);
        page.name = "Page";
        page.transform.SetParent(transform, false);
        page.transform.localPosition = new Vector3(pageSize.x * 0.52f, 0.018f, 0f);
        page.transform.localScale = new Vector3(pageSize.x, 0.005f, pageSize.y * 0.94f);
        var pageMat = new Material(lit);
        pageMat.color = new Color(0.82f, 0.74f, 0.56f);
        page.GetComponent<MeshRenderer>().material = pageMat;
        Destroy(page.GetComponent<Collider>());

        // Ink pool — a quad that scales up on the page like liquid filling in
        var poolGO = new GameObject("InkPool");
        poolGO.transform.SetParent(page.transform, false);
        poolGO.transform.localPosition = new Vector3(0f, 1.05f, -0.3f);
        poolGO.transform.localScale = new Vector3(0.7f, 0.02f, 0.7f);
        var poolMesh = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        poolMesh.transform.SetParent(poolGO.transform, false);
        poolMesh.transform.localRotation = Quaternion.identity;
        poolMesh.transform.localScale = Vector3.one;
        Destroy(poolMesh.GetComponent<Collider>());
        _inkMat = new Material(lit);
        _inkMat.color = inkColorIdle;
        _inkMat.EnableKeyword("_EMISSION");
        _inkMat.SetColor("_EmissionColor", inkColorIdle * 0.6f);
        poolMesh.GetComponent<MeshRenderer>().material = _inkMat;
        _inkPool = poolGO.transform;

        // Score number (large, top of page)
        _scoreTMP = CreatePageText(page.transform, "ScoreText",
            new Vector3(0f, 0.9f, 0.55f), 40f, new Color(0.10f, 0.08f, 0.06f));
        _scoreTMP.text = "0";

        // "of N" threshold label under score
        _thresholdTMP = CreatePageText(page.transform, "ThresholdText",
            new Vector3(0f, 0.85f, 0.35f), 16f, new Color(0.30f, 0.24f, 0.16f));
        _thresholdTMP.text = "of 250";

        // Round/stage label
        _roundTMP = CreatePageText(page.transform, "RoundText",
            new Vector3(0f, 0.95f, -0.75f), 14f, new Color(0.30f, 0.24f, 0.16f));
        _roundTMP.text = "Round 1 · Stage 1";
        _roundTMP.fontStyle = FontStyles.Italic;

        // Combo/result readout (bottom of page)
        _comboTMP = CreatePageText(page.transform, "ComboText",
            new Vector3(0f, 0.75f, -0.55f), 15f, new Color(0.35f, 0.10f, 0.08f));
        _comboTMP.text = "";
    }

    TextMeshPro CreatePageText(Transform parent, string goName, Vector3 localPos, float fontSize, Color color)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale    = Vector3.one * 0.035f;

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.fontSize  = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = color;
        tmp.fontStyle = FontStyles.Bold;
        if (ledgerFont) tmp.font = ledgerFont;
        return tmp;
    }

    // ── Animation loops ───────────────────────────────────────────────────────

    IEnumerator CountUpText(TextMeshPro tmp, int from, int to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            tmp.text = Mathf.RoundToInt(Mathf.Lerp(from, to, t / duration)).ToString();
            yield return null;
        }
        tmp.text = to.ToString();
    }

    IEnumerator FlickerLoop()
    {
        while (true)
        {
            if (_inkMat != null)
            {
                float flicker = 1f + Random.Range(-flickerAmount, flickerAmount);
                Color emit = _inkMat.color * flicker;
                _inkMat.SetColor("_EmissionColor", emit * 0.7f);
            }
            yield return new WaitForSeconds(1f / flickerSpeed);
        }
    }
}
