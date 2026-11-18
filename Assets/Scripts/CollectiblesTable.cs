using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Displays collectible items earned through secret combos.
/// Any three sevens unlock the clickable slot machine once. Fully rotten
/// sevens additionally unlock the collectible Poker Chip.
/// Collectibles persist across runs via ShopManager.collectibles.
/// </summary>
public class CollectiblesTable : MonoBehaviour
{
    public static CollectiblesTable Instance { get; private set; }

    [Header("UI (auto-built if not assigned)")]
    [SerializeField] private GameObject tablePanel;
    [SerializeField] private Transform  itemContainer;
    [SerializeField] private TMP_FontAsset tableFont;

    private GameObject wagerPanel;
    private TextMeshProUGUI wagerBalance;
    private TextMeshProUGUI wagerFeedback;
    private readonly System.Collections.Generic.List<(Button button, int amount)> wagerButtons =
        new System.Collections.Generic.List<(Button, int)>();
    private Button allInButton;

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

    void Update()
    {
        if (wagerPanel == null || !wagerPanel.activeSelf) return;
        ShopManager shop = ShopManager.Instance;
        if (shop == null || !shop.HasCollectible(ShopManager.SlotMachineCollectible))
        {
            CloseSlotMachine();
            return;
        }
        if (wagerBalance != null)
            wagerBalance.text = "GOLD  " + shop.playerGold.ToString("N0") + "     COIN  50% / 50%";
        foreach (var entry in wagerButtons)
            if (entry.button != null)
                entry.button.interactable = shop.playerGold >= entry.amount;
        if (allInButton != null) allInButton.interactable = shop.playerGold > 0;
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
                // Collectible now has an actual interactive use.
                AddTooltip(go, "SLOT MACHINE\nFound by offering 7-7-7.\nClick to wager gold on a fair coin flip: double your wager or lose it.");
                var slotButton = go.AddComponent<Button>();
                slotButton.transition = Selectable.Transition.ColorTint;
                slotButton.onClick.AddListener(OpenSlotMachine);
                break;

            case "collectible_poker_chip":
                icon.text  = "♠";
                lbl.text   = "Poker Chip";
                bg.color   = new Color(0.19f, 0.05f, 0.07f, 0.96f);
                icon.color = new Color(0.98f, 0.72f, 0.47f);
                AddTooltip(go, "Gamblers always quit before hitting it big\nUnlocks the rare Poker Chip relic in the shop.");
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

    // ── SLOT MACHINE: COIN FLIP WAGER ─────────────────────────────────────────
    // A compact UGUI dialog opened by clicking the collectible's table icon.
    // Betting is independent of the Poker Chip's luck-adjusted score flip.
    private TextMeshProUGUI AddSlotText(Transform parent, string name,
        Vector2 position, Vector2 size, string text, int fontSize, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var label = go.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = true;
        label.raycastTarget = false;
        if (tableFont != null) label.font = tableFont;
        return label;
    }

    private Button AddWagerButton(Transform parent, string label,
        Vector2 position, System.Action onClick)
    {
        var go = new GameObject("Bet " + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(96f, 40f);
        var image = go.GetComponent<Image>();
        image.color = new Color(0.31f, 0.25f, 0.16f, 1f);
        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(() => onClick());
        // Child text sits inside the button.
        AddSlotText(go.transform, "Button Label", new Vector2(0f, -7f),
            new Vector2(90f, 29f), label, 17, new Color(0.97f, 0.86f, 0.63f));
        return btn;
    }

    private void OpenSlotMachine()
    {
        ShopManager shop = ShopManager.Instance;
        if (shop == null || !shop.HasCollectible(ShopManager.SlotMachineCollectible)) return;
        if (wagerPanel == null) CreateWagerPanel();
        if (wagerPanel == null) return;
        wagerPanel.SetActive(true);
        wagerPanel.transform.SetAsLastSibling();
        if (wagerFeedback != null)
            wagerFeedback.text = "HEADS: win the amount wagered. TAILS: lose the wager.";
    }

    private void CloseSlotMachine()
    {
        if (wagerPanel != null) wagerPanel.SetActive(false);
    }

    private void WagerGold(int requested)
    {
        ShopManager shop = ShopManager.Instance;
        if (shop == null || wagerFeedback == null) return;
        int wager = requested < 0 ? shop.playerGold : requested;
        shop.TryGambleGold(wager, out _, out string outcome);
        wagerFeedback.text = outcome;
    }

    private void CreateWagerPanel()
    {
        var canvas = tablePanel != null ? tablePanel.GetComponentInParent<Canvas>() : null;
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;
        wagerPanel = new GameObject("Slot Machine Gold Wager",
            typeof(RectTransform), typeof(Image));
        wagerPanel.transform.SetParent(canvas.transform, false);
        var rect = wagerPanel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(450f, 255f);
        var bg = wagerPanel.GetComponent<Image>();
        bg.color = new Color(0.055f, 0.060f, 0.075f, 0.97f);

        AddSlotText(rect, "Header", new Vector2(0f, -16f),
            new Vector2(415f, 30f), "SLOT MACHINE  ·  DOUBLE OR NOTHING",
            20, new Color(0.95f, 0.77f, 0.36f));
        wagerBalance = AddSlotText(rect, "Gold Balance", new Vector2(0f, -61f),
            new Vector2(415f, 25f), "", 18, Color.white);
        wagerFeedback = AddSlotText(rect, "Outcome", new Vector2(0f, -103f),
            new Vector2(415f, 39f), "", 16, new Color(0.90f, 0.84f, 0.71f));

        wagerButtons.Clear();
        for (int i = 0; i < 3; i++)
        {
            int stake = i == 0 ? 1 : i == 1 ? 5 : 10;
            var button = AddWagerButton(rect, stake + " G",
                new Vector2(-159f + 104f * i, -157f), () => WagerGold(stake));
            wagerButtons.Add((button, stake));
        }
        allInButton = AddWagerButton(rect, "ALL IN",
            new Vector2(153f, -157f), () => WagerGold(-1));
        var close = AddWagerButton(rect, "CLOSE", new Vector2(0f, -207f),
            CloseSlotMachine);
        close.GetComponent<RectTransform>().sizeDelta = new Vector2(130f, 34f);
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
