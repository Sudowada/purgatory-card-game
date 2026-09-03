using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

// ─────────────────────────────────────────────────────────────────────────────
// DOCUMENT SYSTEM
// Players discover documents by playing hidden combos.
// Each document is a small piece of lore, a puzzle hint, or a world reaction.
// Documents never explain the combo system — they let players infer it.
// ─────────────────────────────────────────────────────────────────────────────

public class DocumentSystem : MonoBehaviour
{
    public static DocumentSystem Instance { get; private set; }

    // ── State ─────────────────────────────────────────────────────────────────
    private Dictionary<string, string> _unlocked = new Dictionary<string, string>();
    private Queue<(string id, string text)> _pending = new Queue<(string, string)>();
    private bool _showing = false;

    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Document UI (optional — auto-built if not assigned)")]
    [SerializeField] private GameObject      documentPanel;
    [SerializeField] private TextMeshProUGUI documentText;
    [SerializeField] private Button          closeButton;
    [SerializeField] private TMP_FontAsset   documentFont;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (documentPanel == null)
            BuildDocumentUI();

        if (documentPanel != null)
            documentPanel.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PUBLIC API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Unlock a document. If the player hasn't seen it, it queues for display.
    /// </summary>
    public void Unlock(string docID, string text)
    {
        if (_unlocked.ContainsKey(docID)) return;
        _unlocked[docID] = text;
        _pending.Enqueue((docID, text));
        if (!_showing) ShowNext();
    }

    /// <summary>Has this document already been unlocked?</summary>
    public bool IsUnlocked(string docID) => _unlocked.ContainsKey(docID);

    /// <summary>Total documents found this run.</summary>
    public int UnlockedCount => _unlocked.Count;

    /// <summary>All unlocked document texts (for a journal UI).</summary>
    public IReadOnlyDictionary<string, string> AllUnlocked => _unlocked;

    // ─────────────────────────────────────────────────────────────────────────
    // DISPLAY
    // ─────────────────────────────────────────────────────────────────────────

    void ShowNext()
    {
        if (_pending.Count == 0) { _showing = false; return; }
        _showing = true;
        var (_, text) = _pending.Dequeue();
        Show(text);
    }

    void Show(string text)
    {
        if (documentPanel == null) return;
        if (documentText != null) documentText.text = text;

        // ✅ Enable raycaster so the dismiss button is clickable
        var raycaster = documentPanel.GetComponentInParent<GraphicRaycaster>();
        if (raycaster != null) raycaster.enabled = true;

        documentPanel.SetActive(true);

        var cg = documentPanel.GetComponent<CanvasGroup>();
        if (cg == null) cg = documentPanel.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.DOFade(1f, 0.5f);

        GameManager.Instance?.SetInputBlocked(true);
    }

    public void CloseDocument()
    {
        if (documentPanel == null) return;

        var cg = documentPanel.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.DOFade(0f, 0.3f).OnComplete(() =>
            {
                documentPanel.SetActive(false);
                // ✅ Disable raycaster so it never blocks clicks while hidden
                var raycaster = documentPanel.GetComponentInParent<GraphicRaycaster>();
                if (raycaster != null) raycaster.enabled = false;
                GameManager.Instance?.SetInputBlocked(false);
                ShowNext();
            });
        }
        else
        {
            documentPanel.SetActive(false);
            var raycaster = documentPanel.GetComponentInParent<GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = false;
            GameManager.Instance?.SetInputBlocked(false);
            ShowNext();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AUTO-BUILD UI
    // ─────────────────────────────────────────────────────────────────────────

    void BuildDocumentUI()
    {
        // ✅ Create a dedicated canvas — never steal from another system
        var canvasGO = new GameObject("DocumentCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50; // below GameOverOverlay (100) but above game UI
        canvasGO.AddComponent<CanvasScaler>();

        // ✅ Raycaster starts disabled — only enabled when a document is showing
        var raycaster = canvasGO.AddComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        // Outer panel — dark parchment feel
        var panel = new GameObject("DocumentPanel");
        panel.transform.SetParent(canvasGO.transform, false);
        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.2f, 0.15f);
        rt.anchorMax = new Vector2(0.8f, 0.85f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.05f, 0.03f, 0.96f);

        var cg = panel.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        // Border
        var border = new GameObject("Border");
        border.transform.SetParent(panel.transform, false);
        var brt = border.AddComponent<RectTransform>();
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(6, 6);
        brt.offsetMax = new Vector2(-6, -6);
        border.AddComponent<Image>().color = new Color(0.45f, 0.35f, 0.18f, 0.6f);

        // Text
        var textGO = new GameObject("DocText");
        textGO.transform.SetParent(panel.transform, false);
        var trt = textGO.AddComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.05f, 0.15f);
        trt.anchorMax = new Vector2(0.95f, 0.92f);
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.fontSize  = 18f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = new Color(0.88f, 0.80f, 0.62f);
        tmp.fontStyle = FontStyles.Italic;
        if (documentFont) tmp.font = documentFont;
        documentText = tmp;

        // "[ dismiss ]" button
        var btnGO = new GameObject("CloseBtn");
        btnGO.transform.SetParent(panel.transform, false);
        var brt2 = btnGO.AddComponent<RectTransform>();
        brt2.anchorMin = new Vector2(0.3f, 0.04f);
        brt2.anchorMax = new Vector2(0.7f, 0.13f);
        brt2.offsetMin = brt2.offsetMax = Vector2.zero;

        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.18f, 0.14f, 0.08f, 0.9f);

        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(CloseDocument);

        var btnTxt = new GameObject("BtnLabel");
        btnTxt.transform.SetParent(btnGO.transform, false);
        var btrt = btnTxt.AddComponent<RectTransform>();
        btrt.anchorMin = Vector2.zero;
        btrt.anchorMax = Vector2.one;
        btrt.offsetMin = btrt.offsetMax = Vector2.zero;
        var btmp = btnTxt.AddComponent<TextMeshProUGUI>();
        btmp.text      = "[ dismiss ]";
        btmp.fontSize  = 14f;
        btmp.alignment = TextAlignmentOptions.Center;
        btmp.color     = new Color(0.82f, 0.73f, 0.52f);
        if (documentFont) btmp.font = documentFont;

        closeButton   = btn;
        documentPanel = panel;

        // ✅ Start fully hidden
        panel.SetActive(false);
    }
}