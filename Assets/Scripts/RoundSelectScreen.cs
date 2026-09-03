using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class RoundSelectScreen : MonoBehaviour
{
    public static RoundSelectScreen Instance { get; private set; }

    private static readonly BossDef[] Bosses = new BossDef[]
    {
        new BossDef { round=5,  name="The Warden",     accentColor=new Color(0.55f,0.12f,0.05f) },
        new BossDef { round=10, name="The Gravedigger", accentColor=new Color(0.30f,0.18f,0.06f) },
        new BossDef { round=15, name="The Merchant",    accentColor=new Color(0.65f,0.50f,0.10f) },
        new BossDef { round=20, name="The Architect",   accentColor=new Color(0.20f,0.35f,0.55f) },
        new BossDef { round=25, name="The Rot",         accentColor=new Color(0.08f,0.06f,0.04f) },
    };

    static Color Blood    => new Color(0.68f, 0.06f, 0.05f);
    static Color Parch    => new Color(0.82f, 0.73f, 0.52f);
    static Color ParchDim => new Color(0.52f, 0.42f, 0.26f);

    private GameObject  _root;
    private CanvasGroup _rootCG;
    private System.Action _onContinue;
    private Canvas _ownCanvas;
    private GraphicRaycaster _ownRaycaster;
    private TextMeshProUGUI _titleText;

    [Header("Fonts")]
    public TMP_FontAsset headerFont;
    public TMP_FontAsset bodyFont;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        BuildScreen();
        if (_root != null) _root.SetActive(false);
        if (_ownRaycaster != null) _ownRaycaster.enabled = false;
    }

    public void Show(int completedRound, System.Action onContinue)
    {
        _onContinue = onContinue;

        if (_titleText != null)
            _titleText.text = $"ROUND {completedRound} COMPLETE";

        if (_ownRaycaster != null) _ownRaycaster.enabled = true;
        if (_root == null) { Debug.LogError("[RoundSelectScreen] _root is null!"); return; }

        _root.SetActive(true);
        _rootCG.alpha = 0f;
        _root.transform.localScale = Vector3.one * 0.97f;
        _rootCG.DOFade(1f, 0.25f);
        _root.transform.DOScale(1f, 0.28f).SetEase(Ease.OutBack);

        GameManager.Instance?.SetInputBlocked(true);
    }

    void Close()
    {
        _rootCG.DOFade(0f, 0.18f).OnComplete(() =>
        {
            _root.SetActive(false);
            if (_ownRaycaster != null) _ownRaycaster.enabled = false;
            GameManager.Instance?.SetInputBlocked(false);
            _onContinue?.Invoke();
        });
    }

    void BuildScreen()
    {
        // ── Own canvas ────────────────────────────────────────────────────────
        var canvasGO = new GameObject("RoundSelectCanvas");
        _ownCanvas = canvasGO.AddComponent<Canvas>();
        _ownCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _ownCanvas.sortingOrder = 60;
        canvasGO.AddComponent<CanvasScaler>();
        _ownRaycaster = canvasGO.AddComponent<GraphicRaycaster>();
        _ownRaycaster.enabled = false;

        // ── Root panel ────────────────────────────────────────────────────────
        _root = MakeRect("RoundSelectRoot", canvasGO.transform);
        FillParent(_root);
        _root.AddComponent<Image>().color = new Color(0.03f, 0.02f, 0.01f, 0.95f);
        _rootCG = _root.AddComponent<CanvasGroup>();

        // ── Title ─────────────────────────────────────────────────────────────
        var titleGO = MakeRect("Title", _root.transform);
        SetAnchored(titleGO, new Vector2(0.5f,1f), new Vector2(0.5f,1f),
                    new Vector2(0f,-60f), new Vector2(600f,52f));
        _titleText = titleGO.AddComponent<TextMeshProUGUI>();
        _titleText.text      = "ROUND COMPLETE";
        _titleText.fontSize  = 38f;
        _titleText.alignment = TextAlignmentOptions.Center;
        _titleText.color     = Blood;
        _titleText.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        if (headerFont) _titleText.font = headerFont;

        // ── Subtitle ──────────────────────────────────────────────────────────
        var subGO = MakeRect("Subtitle", _root.transform);
        SetAnchored(subGO, new Vector2(0.5f,1f), new Vector2(0.5f,1f),
                    new Vector2(0f,-120f), new Vector2(500f,24f));
        var subTMP = subGO.AddComponent<TextMeshProUGUI>();
        subTMP.text      = "The cards remember what you played.";
        subTMP.fontSize  = 15f;
        subTMP.alignment = TextAlignmentOptions.Center;
        subTMP.color     = ParchDim;
        subTMP.fontStyle = FontStyles.Italic;
        if (bodyFont) subTMP.font = bodyFont;

        // ── Continue button ───────────────────────────────────────────────────
        var btnGO = MakeRect("ContinueBtn", _root.transform);
        SetAnchored(btnGO, new Vector2(0.5f,0f), new Vector2(0.5f,0f),
                    new Vector2(0f,60f), new Vector2(240f,52f));
        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.12f, 0.35f, 0.08f);
        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.18f, 0.50f, 0.12f);
        colors.pressedColor     = new Color(0.08f, 0.22f, 0.05f);
        btn.colors = colors;
        btn.onClick.AddListener(() => Close());

        var btnLabelGO = MakeRect("BtnLabel", btnGO.transform);
        FillParent(btnLabelGO);
        var btnTMP = btnLabelGO.AddComponent<TextMeshProUGUI>();
        btnTMP.text      = "CONTINUE";
        btnTMP.fontSize  = 20f;
        btnTMP.alignment = TextAlignmentOptions.Center;
        btnTMP.color     = Parch;
        btnTMP.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        if (headerFont) btnTMP.font = headerFont;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Creates a GameObject with a RectTransform already attached
    static GameObject MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>(); // ✅ always add RectTransform explicitly
        return go;
    }

    static void FillParent(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void SetAnchored(GameObject go, Vector2 anchorMin, Vector2 anchorMax,
                             Vector2 anchoredPos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = size;
    }
}

[System.Serializable]
public class BossDef
{
    public int    round;
    public string name;
    public string subtitle;
    public string flavour;
    public string passive;
    public string[] modifiers;
    public Color  accentColor;
}