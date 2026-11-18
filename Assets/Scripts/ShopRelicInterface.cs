using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class ShopRelicTarget : MonoBehaviour
{
    public ShopWindowInteractable window;
    public Relic relic;
    public int index;
    public int cost;
    public bool owned;
    public Transform pressTransform;
}

// One input path for merchandise, price buttons, and the owned table row.
public class ShopRelicInterface : MonoBehaviour
{
    [Header("Owned relics: place this anchor along the back of the table")]
    public Transform ownedRelicAnchor;
    public float ownedSpacing = 0.13f;
    [Tooltip("Optional second anchor visible from the shop camera. Owned relics return to the table when the shop closes.")]
    public Transform shopOwnedRelicAnchor;
    [Header("Optional visual prefab: mesh only, without Offer scripts")]
    public GameObject priceButtonPrefab;
    public Vector3 priceButtonOffset = new Vector3(0f, -0.04f, -0.08f);
    [Header("Relic Hover Panel")]
    [Range(0.3f, 0.7f)] public float hoverWidthFraction = 0.5f;
    [Min(100f)] public float hoverMinimumHeight = 210f;
    [Min(12)] public int hoverTitleFontSize = 32;
    [Min(12)] public int hoverEffectFontSize = 24;
    private GUIStyle hoverTitleStyle, hoverEffectStyle;
    private float hoverContentHeight;
    private float HoverScale => Mathf.Clamp(Screen.height / 1080f, 0.75f, 1.6f);
    private Rect HoverRect()
    {
        float width = Mathf.Min(Screen.width - 32f,
            Mathf.Min(Screen.width * hoverWidthFraction, 960f * HoverScale));
        float height = Mathf.Max(hoverMinimumHeight * HoverScale, hoverContentHeight);
        return new Rect((Screen.width - width) * 0.5f, 24f * HoverScale, width, height);
    }

    public static string Message = "";
    public static event System.Action<RelicType> JournalInspectionRequested;
    public static void RequestJournal(Relic relic)
    {
        if (relic == null) return;
        JournalInspectionRequested?.Invoke(relic.type);
        Message = "Journal inspection reserved for " + relic.displayName + ".";
    }
    private readonly List<GameObject> ownedObjects = new List<GameObject>();
    private ShopRelicTarget hovered;
    private RelicType? selected;
    private string ownershipSignature = "";
    private float displayedGold;
    private int lastGoldValue = int.MinValue;
    private int goldFlashDelta;
    private float goldFlashUntil;
    private const float GoldFlashDuration = 0.65f;
    private float ShopScale => Mathf.Clamp(Screen.height / 1080f, 0.72f, 1.25f);
    private Rect ShopPanelRect()
    {
        float scale = ShopScale;
        return new Rect(16f * scale, 20f * scale, 310f * scale, 424f * scale);
    }
    private GUIStyle titleStyle, bodyStyle, buttonStyle;
    private GUIStyle sectionStyle, numberStyle, valueStyle, flashStyle, messageStyle;
    private static ShopRelicInterface active;

    void Awake()
    {
        if (active != null && active != this) { Destroy(this); return; }
        active = this;
        if (FindFirstObjectByType<OwnedRelicHotbar>() == null) gameObject.AddComponent<OwnedRelicHotbar>();
        displayedGold = ShopManager.Instance != null ? ShopManager.Instance.playerGold : 0;
        lastGoldValue = Mathf.RoundToInt(displayedGold);
    }
    void OnDestroy()
    {
        if (active == this) active = null;
        foreach (var obj in ownedObjects) if (obj != null) Destroy(obj);
    }
    // Kept for older callers; merchandise now uses a price-only paper slip.
    public static void CreatePriceButton(ShopRelicTarget item, Transform parent, Vector3 position) { }
    public static GameObject CreateRelicSlot(Relic relic, Transform anchor, Vector3 localPosition)
    {
        var slot = new GameObject("RelicSlot_" + relic.type);
        slot.transform.SetParent(anchor, false);
        slot.transform.localPosition = localPosition;
        var target = slot.AddComponent<ShopRelicTarget>();
        target.relic = relic;
        var visual = RelicPrefabFactory.Create(relic, slot.transform);
        visual.transform.localPosition = Vector3.zero;
        foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        // The factory destroys primitive colliders at the end of the frame.
        // This separate parent collider survives that destruction.
        var box = slot.AddComponent<BoxCollider>();
        var renderers = visual.GetComponentsInChildren<Renderer>();
        Bounds localBounds = new Bounds(Vector3.zero, Vector3.one * 0.05f);
        foreach (Renderer renderer in renderers)
        {
            if (renderer is ParticleSystemRenderer) continue;
            Bounds world = renderer.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = world.center + Vector3.Scale(world.extents,
                    new Vector3((corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                localBounds.Encapsulate(slot.transform.InverseTransformPoint(point));
            }
        }
        box.center = localBounds.center;
        // Include the rotating visual and bob without covering neighboring slots.
        float width = Mathf.Max(localBounds.size.x, localBounds.size.z);
        box.size = new Vector3(width + 0.01f, localBounds.size.y + 0.035f, width + 0.01f);
        return slot;
    }
    void Update()
    {
        var system = RelicSystem.Instance;
        if (system == null) return;
        string signature = "";
        foreach (var r in system.activeRelics) signature += (int)r.type + ",";
        if (signature != ownershipSignature || (ownedObjects.Count == 0 && system.activeRelics.Count > 0 && ownedRelicAnchor != null))
        { ownershipSignature = signature; RebuildOwned(); }
        bool seated = GameStateManager.Instance == null || GameStateManager.Instance.CurrentState == GameState.CardGame;
        var window = FindFirstObjectByType<ShopWindowInteractable>();
        bool windowOpen = window != null && window.IsOpen;
        if (selected.HasValue && !system.HasRelic(selected.Value)) selected = null;
        // The table display remains during combat; shopping uses the screen hotbar.
        foreach (var obj in ownedObjects) if (obj != null) obj.SetActive(seated && !windowOpen);
        if (windowOpen) selected = null;
        if (ShopManager.Instance != null)
        {
            int currentGold = ShopManager.Instance.playerGold;
            if (lastGoldValue == int.MinValue)
            {
                lastGoldValue = currentGold;
                displayedGold = currentGold;
            }
            else if (currentGold != lastGoldValue)
            {
                goldFlashDelta = currentGold - lastGoldValue;
                goldFlashUntil = Time.unscaledTime + GoldFlashDuration;
                lastGoldValue = currentGold;
            }
            float speed = Mathf.Max(40f, Mathf.Abs(currentGold - displayedGold) * 9f);
            displayedGold = Mathf.MoveTowards(displayedGold, currentGold, speed * Time.unscaledDeltaTime);
        }
        ShopRelicTarget previousHovered = hovered;
        hovered = null;
        Camera camera = window != null && window.playerCamera != null ? window.playerCamera : Camera.main;
        if ((!seated && !windowOpen) || Mouse.current == null || camera == null) return;
        if ((window != null && window.IsBusy) || OwnedRelicHotbar.BlocksWorldInput) return;
        Vector2 mouse = Mouse.current.position.ReadValue();
        Vector2 guiMouse = new Vector2(mouse.x, Screen.height - mouse.y);
        if (previousHovered != null && HoverRect().Contains(guiMouse))
        { hovered = previousHovered; return; }
        if (OverControl(mouse, windowOpen)) return;
        if (Physics.Raycast(camera.ScreenPointToRay(mouse), out var hit, 100f))
            hovered = hit.collider.GetComponentInParent<ShopRelicTarget>();
        if (hovered == null || hovered.relic == null) return;
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            RequestJournal(hovered.relic);
        }
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (hovered.owned) selected = selected == hovered.relic.type ? null : (RelicType?)hovered.relic.type;
            else if (hovered.window != null && hovered.window.CanInteract)
            {
                selected = null;
                if (hovered.pressTransform != null) StartCoroutine(Press(hovered.pressTransform));
                hovered.window.BuyDisplayedRelic(hovered.index);
            }
        }
    }
    private System.Collections.IEnumerator Press(Transform button)
    {
        Vector3 initial = button.localPosition;
        button.localPosition = initial - Vector3.up * 0.007f;
        yield return new WaitForSecondsRealtime(0.08f);
        if (button != null) button.localPosition = initial;
    }
    private bool OverControl(Vector2 mouse, bool shop)
    {
        Vector2 gui = new Vector2(mouse.x, Screen.height - mouse.y);
        return ((hovered != null || selected.HasValue) && HoverRect().Contains(gui)) || (selected.HasValue && SellRect().Contains(gui)) || (shop && (ContinueRect().Contains(gui) || RerollRect().Contains(gui) || ShopPanelRect().Contains(gui)));
    }
    void RebuildOwned()
    {
        foreach (var obj in ownedObjects) if (obj != null) Destroy(obj);
        ownedObjects.Clear();
        if (ownedRelicAnchor == null || RelicSystem.Instance == null) return;
        var relics = RelicSystem.Instance.activeRelics;
        for (int i = 0; i < relics.Count; i++)
        {
            var obj = CreateRelicSlot(relics[i], ownedRelicAnchor,
                Vector3.right * ((i - (relics.Count - 1) * 0.5f) * ownedSpacing));
            var target = obj.GetComponent<ShopRelicTarget>();
            target.owned = true;
            ownedObjects.Add(obj);
        }
    }
    Rect SellRect()
    {
        Rect panel = HoverRect();
        return new Rect(panel.x + 20f * HoverScale, panel.yMax + 8f * HoverScale,
            panel.width - 40f * HoverScale, 36f * HoverScale);
    }
    Rect RerollRect()
    {
        Rect panel = ShopPanelRect();
        float sc = ShopScale;
        return new Rect(panel.x + 14f * sc, panel.y + 330f * sc, panel.width - 28f * sc, 38f * sc);
    }
    Rect ContinueRect()
    {
        Rect panel = ShopPanelRect();
        float sc = ShopScale;
        return new Rect(panel.x + 14f * sc, panel.y + 374f * sc, panel.width - 28f * sc, 38f * sc);
    }
    private void EnsureStyles()
    {
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
                { fontStyle = FontStyle.Bold, wordWrap = true };
            bodyStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
            sectionStyle = new GUIStyle(GUI.skin.label)
                { fontStyle = FontStyle.Bold };
            numberStyle = new GUIStyle(GUI.skin.label)
                { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            valueStyle = new GUIStyle(GUI.skin.label)
                { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            flashStyle = new GUIStyle(GUI.skin.label)
                { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            messageStyle = new GUIStyle(GUI.skin.label)
                { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            // A label-style button keeps the painted button backing visible while
            // retaining Unity IMGUI's native click/keyboard behavior.
            buttonStyle = new GUIStyle(GUI.skin.label)
                { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }
        float sc = ShopScale;
        titleStyle.fontSize = Mathf.RoundToInt(18f * sc);
        bodyStyle.fontSize = Mathf.RoundToInt(16f * sc);
        sectionStyle.fontSize = Mathf.RoundToInt(14f * sc);
        numberStyle.fontSize = Mathf.RoundToInt(37f * sc);
        valueStyle.fontSize = Mathf.RoundToInt(16f * sc);
        flashStyle.fontSize = Mathf.RoundToInt(14f * sc);
        messageStyle.fontSize = Mathf.RoundToInt(17f * sc);
        buttonStyle.fontSize = Mathf.RoundToInt(17f * sc);
        titleStyle.normal.textColor = ShopUITheme.Brass;
        bodyStyle.normal.textColor = ShopUITheme.Ivory;
        sectionStyle.normal.textColor = ShopUITheme.Muted;
        numberStyle.normal.textColor = ShopUITheme.Gold;
        valueStyle.normal.textColor = ShopUITheme.Ivory;
        messageStyle.normal.textColor = ShopUITheme.Ivory;
        buttonStyle.normal.textColor = ShopUITheme.Ivory;
        buttonStyle.hover.textColor = Color.white;
        buttonStyle.active.textColor = ShopUITheme.Gold;
        buttonStyle.focused.textColor = ShopUITheme.Ivory;
    }

    private bool DrawShopButton(Rect area, string content, bool enabled, bool primary)
    {
        ShopUITheme.ButtonBackground(area, enabled, area.Contains(Event.current.mousePosition), primary);
        bool old = GUI.enabled;
        GUI.enabled = enabled;
        bool clicked = GUI.Button(area, content, buttonStyle);
        GUI.enabled = old;
        return clicked;
    }

    private void DrawGoldCard(Rect area, float scale)
    {
        ShopUITheme.Cell(area);
        float flash = Mathf.Clamp01((goldFlashUntil - Time.unscaledTime) / GoldFlashDuration);
        if (flash > 0f)
            ShopUITheme.Fill(new Rect(area.x + 2f, area.y + 2f, area.width - 4f, area.height - 4f),
                new Color(0.95f, 0.77f, 0.36f, flash * 0.18f));

        sectionStyle.normal.textColor = ShopUITheme.Ivory;
        GUI.Label(new Rect(area.x + 12f * scale, area.y + 7f * scale, area.width - 24f * scale, 19f * scale),
            "GOLD", sectionStyle);
        sectionStyle.normal.textColor = ShopUITheme.Muted;

        string shownGold = Mathf.RoundToInt(displayedGold).ToString("N0");
        // Small, crisp punch on a changed balance, matching ScoreHUD number feedback.
        numberStyle.fontSize = Mathf.RoundToInt((shownGold.Length > 9 ? 25f :
            shownGold.Length > 7 ? 30f : 37f) * scale * (1f + flash * 0.08f));
        numberStyle.normal.textColor = Color.Lerp(ShopUITheme.Gold, Color.white, flash * 0.55f);
        GUI.Label(new Rect(area.x + 12f * scale, area.y + 27f * scale, area.width - 24f * scale, 42f * scale),
            shownGold + " G", numberStyle);
        if (flash > 0f && goldFlashDelta != 0)
        {
            flashStyle.normal.textColor = goldFlashDelta > 0
                ? ShopUITheme.Gold : new Color(0.98f, 0.55f, 0.38f);
            GUI.Label(new Rect(area.x + 100f * scale, area.y + 5f * scale,
                area.width - 115f * scale, 20f * scale),
                (goldFlashDelta > 0 ? "+" : "") + goldFlashDelta + " G", flashStyle);
        }
    }

    private void DrawEarningsRow(Rect rect, string title, int amount, bool highlight = false)
    {
        bodyStyle.normal.textColor = highlight ? ShopUITheme.Ivory : ShopUITheme.Muted;
        GUI.Label(rect, title, bodyStyle);
        valueStyle.normal.textColor = highlight ? ShopUITheme.Gold : ShopUITheme.Ivory;
        GUI.Label(rect, "+" + amount.ToString("N0") + " G", valueStyle);
        bodyStyle.normal.textColor = ShopUITheme.Ivory;
    }

    private void DrawShopSummary(ShopManager shop, RelicSystem system, ShopWindowInteractable window)
    {
        Rect panel = ShopPanelRect();
        float sc = ShopScale;
        ShopUITheme.Frame(panel);
        GUI.Label(new Rect(panel.x + 14f * sc, panel.y + 10f * sc,
            panel.width - 28f * sc, 30f * sc), "TRADER'S WINDOW", titleStyle);

        DrawGoldCard(new Rect(panel.x + 14f * sc, panel.y + 48f * sc,
            panel.width - 28f * sc, 79f * sc), sc);

        Rect earnings = new Rect(panel.x + 14f * sc, panel.y + 140f * sc,
            panel.width - 28f * sc, 153f * sc);
        ShopUITheme.Cell(earnings);
        GUI.Label(new Rect(earnings.x + 12f * sc, earnings.y + 7f * sc,
            earnings.width - 24f * sc, 21f * sc), "ROUND EARNINGS", sectionStyle);
        float rowX = earnings.x + 12f * sc;
        float rowW = earnings.width - 24f * sc;
        DrawEarningsRow(new Rect(rowX, earnings.y + 32f * sc, rowW, 23f * sc),
            "Base", shop.LastBaseGold);
        DrawEarningsRow(new Rect(rowX, earnings.y + 55f * sc, rowW, 23f * sc),
            "Over target", shop.LastScoreGold);
        DrawEarningsRow(new Rect(rowX, earnings.y + 78f * sc, rowW, 23f * sc),
            "Interest", shop.LastInterestGold);
        ShopUITheme.Fill(new Rect(rowX, earnings.y + 108f * sc, rowW, 1f * sc), ShopUITheme.Outline);
        DrawEarningsRow(new Rect(rowX, earnings.y + 113f * sc, rowW, 28f * sc),
            "TOTAL EARNED", shop.LastAwardedGold, true);

        bodyStyle.normal.textColor = ShopUITheme.Muted;
        GUI.Label(new Rect(panel.x + 16f * sc, panel.y + 299f * sc,
            panel.width - 32f * sc, 24f * sc),
            "RELICS  " + system.activeRelics.Count + " / " + system.RelicCapacity, bodyStyle);
        bodyStyle.normal.textColor = ShopUITheme.Ivory;

        bool canReroll = window.CanInteract && shop.playerGold >= window.RerollCost;
        string rerollText = window.RerollCost == 0 ? "REROLL  ·  FREE"
            : "REROLL  ·  " + window.RerollCost + " G";
        if (DrawShopButton(RerollRect(), rerollText, canReroll, false))
            window.RerollDisplayedRelics();
        if (DrawShopButton(ContinueRect(), "CONTINUE", window.CanInteract, true))
            window.ContinueRun();
    }

    private void DrawGameplayGold()
    {
        // The score panel occupies top-left; use top-right for a compact balance.
        float sc = ShopScale;
        Rect badge = new Rect(Screen.width - 231f * sc, 20f * sc, 215f * sc, 80f * sc);
        ShopUITheme.Frame(badge);
        DrawGoldCard(new Rect(badge.x + 5f * sc, badge.y + 5f * sc,
            badge.width - 10f * sc, badge.height - 10f * sc), sc);
    }

    void OnGUI()
    {
        var shop = ShopManager.Instance;
        var system = RelicSystem.Instance;
        if (shop == null || system == null) return;
        var window = FindFirstObjectByType<ShopWindowInteractable>();
        bool open = window != null && window.IsOpen;
        bool seated = GameStateManager.Instance == null ||
            GameStateManager.Instance.CurrentState == GameState.CardGame;
        if ((!open && !seated) || OwnedRelicHotbar.Inspecting) return;

        EnsureStyles();
        if (open) DrawShopSummary(shop, system, window);
        else DrawGameplayGold();

        Relic detail = hovered != null ? hovered.relic : null;
        if (selected.HasValue)
            detail = system.activeRelics.Find(r => r.type == selected.Value);
        if (detail != null)
        {
            if (hoverTitleStyle == null)
            {
                hoverTitleStyle = new GUIStyle(GUI.skin.label)
                    { wordWrap = true, fontStyle = FontStyle.Bold };
                hoverEffectStyle = new GUIStyle(GUI.skin.label)
                    { wordWrap = true };
            }
            float scale = HoverScale;
            hoverTitleStyle.fontSize = Mathf.RoundToInt(hoverTitleFontSize * scale);
            hoverEffectStyle.fontSize = Mathf.RoundToInt(hoverEffectFontSize * scale);
            hoverTitleStyle.normal.textColor = ShopUITheme.Brass;
            hoverEffectStyle.normal.textColor = ShopUITheme.Ivory;
            float padding = 20f * scale;
            Rect panel = HoverRect();
            float textWidth = panel.width - padding * 2f;
            float titleHeight = hoverTitleStyle.CalcHeight(new GUIContent(detail.displayName), textWidth);
            float effectHeight = hoverEffectStyle.CalcHeight(new GUIContent(detail.description), textWidth);
            hoverContentHeight = padding * 2f + titleHeight + 12f * scale + effectHeight;
            panel = HoverRect();
            ShopUITheme.Frame(panel);
            GUI.Label(new Rect(panel.x + padding, panel.y + padding, textWidth, titleHeight),
                detail.displayName, hoverTitleStyle);
            GUI.Label(new Rect(panel.x + padding, panel.y + padding + titleHeight + 12f * scale,
                textWidth, effectHeight), detail.description, hoverEffectStyle);
        }
        if (selected.HasValue && system.HasRelic(selected.Value))
        {
            var gm = GameManager.Instance;
            var offering = FindFirstObjectByType<CardOfferingController>();
            bool safe = (window == null || !window.IsBusy) &&
                (gm == null || ((gm.IsShopOpen || (open && window.CanInteract) ||
                (!open && gm.CanPlayOffering)) &&
                (offering == null || offering.OfferedCount == 0)));
            if (DrawShopButton(SellRect(),
                "SELL RELIC  ·  +" + shop.GetRelicSaleValue(selected.Value) + " G", safe, false))
            {
                bool sold = shop.TrySellRelic(selected.Value, out string reason);
                Message = reason;
                if (sold) selected = null;
            }
        }

        if (!string.IsNullOrEmpty(Message))
        {
            Rect messageArea = new Rect(Screen.width * 0.28f,
                Screen.height - 50f * ShopScale, Screen.width * 0.44f, 38f * ShopScale);
            ShopUITheme.Frame(messageArea);
            GUI.Label(messageArea, Message, messageStyle);
        }
    }

}

public class ShopPriceLabelOwner : MonoBehaviour
{
    public GameObject label;
    void OnDestroy() { if (label != null) Destroy(label); }
}
public class ShopPriceLabel : MonoBehaviour
{
    public ShopRelicTarget target;
    void LateUpdate()
    {
        if (Camera.main != null) transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
        if (target != null && target.window != null && target.window.IsSold(target.index)) GetComponent<TextMeshPro>().text = "SOLD";
    }
}
