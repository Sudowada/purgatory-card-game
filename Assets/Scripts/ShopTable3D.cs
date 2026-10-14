using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Physical shop presentation; purchases stay owned by ShopManager.</summary>
[DisallowMultipleComponent]
public class ShopTable3D : MonoBehaviour
{
    [Header("Table Placement")]
    [Tooltip("Empty object on the desk. Local X runs across the desk; local Z points away from the player.")]
    [SerializeField] private Transform shopAnchor;
    [SerializeField] private Camera interactionCamera;
    [SerializeField, Min(0.1f)] private float displaySpacing = 0.27f;
    [SerializeField, Min(0.1f)] private float displayWidth = 0.24f;
    [SerializeField, Min(0.1f)] private float displayDepth = 0.32f;
    [SerializeField, Min(0f)] private float hoverLift = 0.015f;
    [SerializeField] private LayerMask interactionMask = ~0;
    [SerializeField] private Material displayMaterial;

    private class Display
    {
        public Transform root;
        public BoxCollider collider;
        public TextMeshPro label;
        public Renderer renderer;
        public Vector3 home;
        public int action;
        public float pressUntil;
        public Color color;
    }

    private readonly List<Display> displays = new List<Display>();
    private GameObject shopRoot;
    private TextMeshPro header;
    private TextMeshPro message;
    private Material ownedMaterial;
    private MaterialPropertyBlock properties;
    private Display hovered;
    private bool open;
    private string feedback;
    private float feedbackUntil;

    public void ShowShop(Transform fallbackAnchor)
    {
        if (shopRoot == null) Build(shopAnchor != null ? shopAnchor : fallbackAnchor);
        if (shopRoot == null) return;
        open = true;
        feedback = "Choose an item. Click to buy.";
        feedbackUntil = Time.unscaledTime + 4f;
        RefreshLabels();
        UpdateVisibility();
    }

    public void HideShop()
    {
        open = false;
        hovered = null;
        if (shopRoot != null) shopRoot.SetActive(false);
    }

    private bool IsSeated => GameStateManager.Instance == null ||
        GameStateManager.Instance.CurrentState == GameState.CardGame;

    private void UpdateVisibility()
    {
        if (shopRoot != null) shopRoot.SetActive(open && IsSeated);
    }

    private void Update()
    {
        if (!open) return;
        if (GameManager.Instance == null || !GameManager.Instance.IsShopOpen)
        {
            HideShop();
            return;
        }
        UpdateVisibility();
        if (!IsSeated || shopRoot == null) return;
        RefreshLabels();
        hovered = null;
        Camera camera = interactionCamera != null ? interactionCamera : Camera.main;
        Mouse mouse = Mouse.current;
        if (camera != null && mouse != null && Physics.Raycast(
            camera.ScreenPointToRay(mouse.position.ReadValue()), out RaycastHit hit,
            100f, interactionMask, QueryTriggerInteraction.Ignore))
        {
            foreach (Display display in displays)
                if (hit.collider == display.collider) { hovered = display; break; }
        }
        foreach (Display display in displays)
        {
            bool active = display == hovered;
            float press = Time.unscaledTime < display.pressUntil ? -0.008f : 0f;
            Vector3 target = display.home + Vector3.up * (press != 0f ? press : active ? hoverLift : 0f);
            display.root.localPosition = Vector3.Lerp(display.root.localPosition, target,
                1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            properties.Clear();
            Color tint = active ? Color.Lerp(display.color, Color.white, 0.22f) : display.color;
            properties.SetColor("_BaseColor", tint);
            properties.SetColor("_Color", tint);
            display.renderer.SetPropertyBlock(properties);
        }
        if (hovered != null && mouse != null && mouse.leftButton.wasPressedThisFrame)
            Purchase(hovered);
    }

    private void Purchase(Display display)
    {
        // One input path; collider clicks do not also run OnMouseDown.
        display.pressUntil = Time.unscaledTime + 0.1f;
        GameManager gm = GameManager.Instance;
        ShopManager shop = ShopManager.Instance;
        if (gm == null || !gm.IsShopOpen || shop == null) return;
        if (display.action == 3) { gm.LeaveTableShop(); return; }
        bool sold = display.action == 0 ? shop.offeredRelic == null :
            display.action == 1 ? shop.offeredCard == null : shop.offeredConsumable == null;
        if (sold) { SetFeedback("Already sold."); return; }
        int cost = display.action == 0 ? shop.offeredRelicCost :
            display.action == 1 ? shop.offeredCard.cost : shop.offeredConsumable.cost;
        if (shop.playerGold < cost) { SetFeedback("Not enough gold."); return; }
        bool bought = display.action == 0 ? shop.BuyRelic() :
            display.action == 1 ? shop.BuyCard() : shop.BuyConsumable();
        SetFeedback(bought ? display.action == 0 ? "Relic acquired." :
            display.action == 1 ? "Card added to your deck." : "Consumable purchased." :
            "Purchase unavailable. Check capacity or requirements.");
        RefreshLabels();
    }

    private void SetFeedback(string text)
    {
        feedback = text;
        feedbackUntil = Time.unscaledTime + 3f;
    }

    private static string Safe(string text)
    {
        return string.IsNullOrEmpty(text) ? "" : text.Replace("<", "").Replace(">", "");
    }

    private void RefreshLabels()
    {
        ShopManager shop = ShopManager.Instance;
        if (shop == null || header == null) return;
        header.text = $"SHOP    •    {shop.playerGold} GOLD";
        for (int i = 0; i < 3; i++)
        {
            bool sold = i == 0 ? shop.offeredRelic == null :
                i == 1 ? shop.offeredCard == null : shop.offeredConsumable == null;
            string category = i == 0 ? "RELIC" : i == 1 ? "CARD" : "CONSUMABLE";
            if (sold) { displays[i].label.text = $"{category}\n\nSOLD"; continue; }
            string name = i == 0 ? shop.offeredRelic.displayName :
                i == 1 ? shop.offeredCard.displayName : shop.offeredConsumable.displayName;
            string desc = i == 0 ? shop.offeredRelic.description :
                i == 1 ? shop.offeredCard.rarity.ToString() : shop.offeredConsumable.description;
            int cost = i == 0 ? shop.offeredRelicCost : i == 1 ? shop.offeredCard.cost : shop.offeredConsumable.cost;
            string priceColor = shop.playerGold >= cost ? "E8BF73" : "DD7777";
            displays[i].label.text = $"<size=75%>{category}</size>\n<b>{Safe(name)}</b>\n<size=70%>{Safe(desc)}</size>\n<color=#{priceColor}>{cost} GOLD — BUY</color>";
        }
        message.text = Time.unscaledTime < feedbackUntil ? feedback : "Click an item to buy. Continue when ready.";
    }

    private void Build(Transform anchor)
    {
        if (anchor == null)
        {
            Debug.LogError("[ShopTable3D] Assign a Shop Anchor on the desk.");
            return;
        }
        properties = new MaterialPropertyBlock();
        Material material = displayMaterial;
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) { Debug.LogError("[ShopTable3D] Assign a Display Material."); return; }
            ownedMaterial = new Material(shader);
            material = ownedMaterial;
        }
        shopRoot = new GameObject("3D Table Shop");
        shopRoot.transform.SetParent(anchor, false);
        for (int i = 0; i < 4; i++)
        {
            Vector3 position = i < 3 ? new Vector3((i - 1) * displaySpacing, 0.018f, 0.04f) : new Vector3(0f, 0.018f, -0.22f);
            Vector3 size = i < 3 ? new Vector3(displayWidth, 0.018f, displayDepth) : new Vector3(0.3f, 0.018f, 0.07f);
            Transform root = new GameObject(i == 3 ? "Continue" : "Shop Item " + i).transform;
            root.SetParent(shopRoot.transform, false);
            root.localPosition = position;
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "Clickable Display";
            block.transform.SetParent(root, false);
            block.transform.localScale = size;
            Renderer renderer = block.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            Color color = i == 0 ? new Color(0.22f, 0.16f, 0.1f) : i == 1 ? new Color(0.12f, 0.2f, 0.22f) :
                i == 2 ? new Color(0.2f, 0.12f, 0.18f) : new Color(0.12f, 0.23f, 0.15f);
            TextMeshPro label = CreateLabel(root, Vector3.up * 0.011f, new Vector2(size.x - 0.018f, size.z - 0.012f), i == 3 ? 2.5f : 2.2f);
            label.text = i == 3 ? "CONTINUE →" : "Loading…";
            displays.Add(new Display { root = root, home = position, collider = block.GetComponent<BoxCollider>(), renderer = renderer, label = label, action = i, color = color });
        }
        header = CreateLabel(shopRoot.transform, new Vector3(0f, 0.025f, 0.26f), new Vector2(0.85f, 0.07f), 3.5f);
        message = CreateLabel(shopRoot.transform, new Vector3(0f, 0.025f, -0.32f), new Vector2(0.85f, 0.07f), 2.3f);
        shopRoot.SetActive(false);
    }

    private static TextMeshPro CreateLabel(Transform parent, Vector3 position, Vector2 size, float fontSize)
    {
        GameObject go = new GameObject("Printed Label");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        TextMeshPro label = go.AddComponent<TextMeshPro>();
        label.font = TMP_Settings.defaultFontAsset;
        label.rectTransform.sizeDelta = size;
        label.fontSize = fontSize;
        label.enableAutoSizing = true;
        label.fontSizeMin = fontSize * 0.55f;
        label.fontSizeMax = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.94f, 0.9f, 0.8f);
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    private void OnDisable()
    {
        if (shopRoot != null) shopRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (shopRoot != null) Destroy(shopRoot);
        if (ownedMaterial != null) Destroy(ownedMaterial);
    }
}
