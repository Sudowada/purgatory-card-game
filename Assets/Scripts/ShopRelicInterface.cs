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
    private GUIStyle titleStyle, bodyStyle, buttonStyle;
    private static ShopRelicInterface active;

    void Awake()
    {
        if (active != null && active != this) { Destroy(this); return; }
        active = this;
        if (FindFirstObjectByType<OwnedRelicHotbar>() == null) gameObject.AddComponent<OwnedRelicHotbar>();
        displayedGold = ShopManager.Instance != null ? ShopManager.Instance.playerGold : 0;
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
            displayedGold = Mathf.MoveTowards(displayedGold, ShopManager.Instance.playerGold, Time.unscaledDeltaTime * 30f);
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
        return ((hovered != null || selected.HasValue) && HoverRect().Contains(gui)) || (selected.HasValue && SellRect().Contains(gui)) || (shop && (ContinueRect().Contains(gui) || RerollRect().Contains(gui) || new Rect(15, 20, 230, 335).Contains(gui)));
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
    Rect RerollRect() => new Rect(28, 240, 205, 32);
    Rect ContinueRect() => new Rect(28, 282, 205, 40);
    void OnGUI()
    {
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, wordWrap = true };
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 18 };
        }
        var shop = ShopManager.Instance; var system = RelicSystem.Instance;
        if (shop == null || system == null) return;
        var window = FindFirstObjectByType<ShopWindowInteractable>();
        bool open = window != null && window.IsOpen;
        bool seated = GameStateManager.Instance == null || GameStateManager.Instance.CurrentState == GameState.CardGame;
        if ((!open && !seated) || OwnedRelicHotbar.Inspecting) return;
        if (open)
        {
            GUI.Box(new Rect(15, 20, 230, 335), "");
            GUI.Label(new Rect(28, 30, 210, 35), "GOLD  " + Mathf.RoundToInt(displayedGold), titleStyle);
            GUI.Label(new Rect(28, 75, 205, 150), $"Round earnings\nBase: +{shop.LastBaseGold}\nScore bonus: +{shop.LastScoreGold}\nInterest: +{shop.LastInterestGold}\nTotal earned: +{shop.LastAwardedGold}\nRelics: {system.activeRelics.Count}/{system.RelicCapacity}", bodyStyle);
            bool before = GUI.enabled;
            GUI.enabled = window.CanInteract;
            if (GUI.Button(ContinueRect(), "Continue", buttonStyle)) window.ContinueRun();
            GUI.enabled = window.CanInteract && shop.playerGold >= window.RerollCost;
            if (GUI.Button(RerollRect(), $"Reroll: {window.RerollCost}g", buttonStyle)) window.RerollDisplayedRelics();
            GUI.enabled = before;
        }
        Relic detail = hovered != null ? hovered.relic : null;
        if (selected.HasValue) detail = system.activeRelics.Find(r => r.type == selected.Value);
        if (detail != null)
        {
            if (hoverTitleStyle == null)
            {
                hoverTitleStyle = new GUIStyle(GUI.skin.label)
                    { wordWrap = true, fontStyle = FontStyle.Bold };
                hoverEffectStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
            }
            float scale = HoverScale;
            hoverTitleStyle.fontSize = Mathf.RoundToInt(hoverTitleFontSize * scale);
            hoverEffectStyle.fontSize = Mathf.RoundToInt(hoverEffectFontSize * scale);
            float padding = 20f * scale;
            Rect panel = HoverRect();
            float textWidth = panel.width - padding * 2f;
            float titleHeight = hoverTitleStyle.CalcHeight(new GUIContent(detail.displayName), textWidth);
            float effectHeight = hoverEffectStyle.CalcHeight(new GUIContent(detail.description), textWidth);
            hoverContentHeight = padding * 2f + titleHeight + 12f * scale + effectHeight;
            panel = HoverRect();
            GUI.Box(panel, "");
            GUI.Label(new Rect(panel.x + padding, panel.y + padding, textWidth, titleHeight),
                detail.displayName, hoverTitleStyle);
            GUI.Label(new Rect(panel.x + padding, panel.y + padding + titleHeight + 12f * scale,
                textWidth, effectHeight), detail.description, hoverEffectStyle);
        }
        if (selected.HasValue && system.HasRelic(selected.Value))
        {
            var gm = GameManager.Instance;
            var offering = FindFirstObjectByType<CardOfferingController>();
            bool safe = (window == null || !window.IsBusy) && (gm == null || ((gm.IsShopOpen || (open && window.CanInteract) || (!open && gm.CanPlayOffering)) && (offering == null || offering.OfferedCount == 0)));
            bool before = GUI.enabled; GUI.enabled = safe;
            if (GUI.Button(SellRect(), $"Sell selected relic for {shop.GetRelicSaleValue(selected.Value)} gold", buttonStyle))
            { bool sold = shop.TrySellRelic(selected.Value, out string reason); Message = reason; if (sold) selected = null; }
            GUI.enabled = before;
        }
        GUI.Label(new Rect(Screen.width * 0.28f, Screen.height - 40, Screen.width * 0.44f, 35), Message, bodyStyle);
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
