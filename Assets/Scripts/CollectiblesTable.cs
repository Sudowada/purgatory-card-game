using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Displays collectible items earned through secret combos.
/// The slot machine appears here when the player plays 3 fully-rotted 7s.
/// Collectibles persist across runs via ShopManager.collectibles.
/// </summary>
public class CollectiblesTable : MonoBehaviour
{
    public static CollectiblesTable Instance { get; private set; }

    [Header("UI (auto-built if not assigned)")]
    [SerializeField] private GameObject tablePanel;
    [SerializeField] private Transform  itemContainer;
    [SerializeField] private TMP_FontAsset tableFont;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (tablePanel == null) BuildTableUI();
        Refresh();
    }

    // ── PUBLIC ────────────────────────────────────────────────────────────────

    /// <summary>Refresh the table display after a new collectible is added.</summary>
    public void Refresh()
    {
        if (itemContainer == null) return;

        // Clear existing items
        foreach (Transform child in itemContainer)
            Destroy(child.gameObject);

        var shop = ShopManager.Instance;
        if (shop == null) return;

        foreach (var id in shop.collectibles)
            SpawnCollectibleItem(id);
    }

    // ── ITEM DEFINITIONS ──────────────────────────────────────────────────────

    void SpawnCollectibleItem(string id)
    {
        var go = new GameObject($"Collectible_{id}");
        go.transform.SetParent(itemContainer, false);

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(90f, 90f);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.10f, 0.08f, 0.05f, 0.9f);

        // Icon text
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(go.transform, false);
        var irt = iconGO.AddComponent<RectTransform>();
        irt.anchorMin = new Vector2(0.1f, 0.35f);
        irt.anchorMax = new Vector2(0.9f, 0.95f);
        irt.offsetMin = irt.offsetMax = Vector2.zero;
        var icon = iconGO.AddComponent<TextMeshProUGUI>();
        icon.fontSize  = 28f;
        icon.alignment = TextAlignmentOptions.Center;
        if (tableFont) icon.font = tableFont;

        // Label text
        var lblGO = new GameObject("Label");
        lblGO.transform.SetParent(go.transform, false);
        var lrt = lblGO.AddComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(1f, 0.38f);
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var lbl = lblGO.AddComponent<TextMeshProUGUI>();
        lbl.fontSize  = 9f;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.color     = new Color(0.65f, 0.55f, 0.35f);
        if (tableFont) lbl.font = tableFont;

        // Assign icon and label based on ID
        switch (id)
        {
            case "collectible_slot_machine":
                icon.text  = "🎰";
                lbl.text   = "Slot Machine";
                bg.color   = new Color(0.08f, 0.05f, 0.12f, 0.9f);
                icon.color = new Color(0.9f, 0.4f, 0.9f);
                // Give it a subtle spin animation
                go.transform.DORotate(new Vector3(0, 0, 3f), 1.2f)
                    .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
                // Tooltip on hover
                AddTooltip(go, "\"Three rotten sevens.\nMay it bring you worse luck than it brought me.\"\n\n— Unknown");
                break;

            // Add more collectibles here as you design them
            default:
                icon.text  = "?";
                lbl.text   = id;
                icon.color = new Color(0.5f, 0.5f, 0.5f);
                break;
        }

        // Entrance animation
        go.transform.localScale = Vector3.zero;
        go.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);
    }

    void AddTooltip(GameObject target, string text)
    {
        // Simple hover tooltip via EventTrigger
        var trigger = target.AddComponent<UnityEngine.EventSystems.EventTrigger>();

        var enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry
        {
            eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter
        };
        enterEntry.callback.AddListener(_ => ShowTooltip(text, target.transform.position));
        trigger.triggers.Add(enterEntry);

        var exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry
        {
            eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit
        };
        exitEntry.callback.AddListener(_ => HideTooltip());
        trigger.triggers.Add(exitEntry);
    }

    // ── TOOLTIP ───────────────────────────────────────────────────────────────

    private GameObject _tooltip;

    void ShowTooltip(string text, Vector3 worldPos)
    {
        HideTooltip();
        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        _tooltip = new GameObject("Tooltip");
        _tooltip.transform.SetParent(canvas.transform, false);
        var rt = _tooltip.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200f, 80f);
        rt.position  = worldPos + new Vector3(50, 50, 0);

        var bg = _tooltip.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.03f, 0.02f, 0.95f);

        var txt = new GameObject("Text");
        txt.transform.SetParent(_tooltip.transform, false);
        var trt = txt.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(6, 4); trt.offsetMax = new Vector2(-6, -4);
        var tmp = txt.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = 10f;
        tmp.color     = new Color(0.80f, 0.72f, 0.55f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Italic;
        if (tableFont) tmp.font = tableFont;
    }

    void HideTooltip()
    {
        if (_tooltip != null) Destroy(_tooltip);
    }

    // ── AUTO-BUILD UI ─────────────────────────────────────────────────────────

    void BuildTableUI()
    {
        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // Small panel in bottom-right corner — unobtrusive
        tablePanel = new GameObject("CollectiblesTable");
        tablePanel.transform.SetParent(canvas.transform, false);
        var rt = tablePanel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.78f, 0.01f);
        rt.anchorMax = new Vector2(0.99f, 0.22f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var bg = tablePanel.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.04f, 0.03f, 0.75f);

        // Header
        var hdr = new GameObject("Header");
        hdr.transform.SetParent(tablePanel.transform, false);
        var hrt = hdr.AddComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0f, 0.82f);
        hrt.anchorMax = Vector2.one;
        hrt.offsetMin = hrt.offsetMax = Vector2.zero;
        var htmp = hdr.AddComponent<TextMeshProUGUI>();
        htmp.text      = "Collectibles";
        htmp.fontSize  = 10f;
        htmp.alignment = TextAlignmentOptions.Center;
        htmp.color     = new Color(0.55f, 0.45f, 0.25f);
        htmp.fontStyle = FontStyles.SmallCaps;
        if (tableFont) htmp.font = tableFont;

        // Item container — horizontal layout
        var container = new GameObject("ItemContainer");
        container.transform.SetParent(tablePanel.transform, false);
        var crt = container.AddComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.02f, 0.02f);
        crt.anchorMax = new Vector2(0.98f, 0.80f);
        crt.offsetMin = crt.offsetMax = Vector2.zero;

        var layout = container.AddComponent<HorizontalLayoutGroup>();
        layout.spacing            = 4f;
        layout.childAlignment     = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth  = false;
        layout.childForceExpandHeight = false;

        itemContainer = container.transform;
    }
}
