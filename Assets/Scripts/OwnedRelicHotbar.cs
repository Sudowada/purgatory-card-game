using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Shop-only owned relic bar with live 3D previews and an inspect/sell modal.</summary>
public class OwnedRelicHotbar : MonoBehaviour
{
    [Range(0, 31)] public int previewLayer = 31;
    [Range(64, 512)] public int previewResolution = 256;
    public float spinSpeed = 12f;
    public Color panelColor = new Color(0.08f, 0.10f, 0.13f, 1f);
    public static OwnedRelicHotbar Instance { get; private set; }
    public static bool Inspecting => Instance != null && Instance.ShopOpen && Instance.selected.HasValue;
    public static bool BlocksWorldInput
    {
        get
        {
            var ui = Instance;
            if (ui == null || !ui.ShopOpen || Mouse.current == null) return false;
            Vector2 mouse = Mouse.current.position.ReadValue();
            Vector2 point = new Vector2(mouse.x, Screen.height - mouse.y) / ui.Scale;
            return ui.selected.HasValue || ui.BarRect.Contains(point);
        }
    }
    private class Preview
    {
        public Relic relic;
        public GameObject root;
        public Transform pivot;
        public Camera camera;
        public RenderTexture texture;
    }
    private readonly List<Preview> previews = new List<Preview>();
    private string signature = "";
    private RelicType? selected;
    private ShopWindowInteractable window;
    private GUIStyle title, body, button;
    private int escapeFrame = -1;
    public static bool HandleEscape()
    {
        var ui = Instance;
        if (ui == null) return false;
        if (ui.escapeFrame == Time.frameCount) return true;
        if (!ui.ShopOpen || !ui.selected.HasValue) return false;
        ui.selected = null;
        ui.escapeFrame = Time.frameCount;
        return true;
    }
    private float Scale => Mathf.Max(0.1f, Mathf.Min(Screen.width / 1600f, Screen.height / 900f));
    private float Width => Screen.width / Scale;
    private float Height => Screen.height / Scale;
    private bool ShopOpen => window != null && window.IsOpen;
    private Rect BarRect => new Rect(Width - BarWidth - 20f, Height - 144f, BarWidth, 124f);
    private float BarWidth => Mathf.Max(270f, 24f + (RelicSystem.Instance != null ? RelicSystem.Instance.RelicCapacity : 5) * 90f);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }
    void Update()
    {
        if (window == null) window = FindFirstObjectByType<ShopWindowInteractable>();
        if (!ShopOpen)
        {
            selected = null;
            foreach (var preview in previews) if (preview.root != null) preview.root.SetActive(false);
            return;
        }
        var system = RelicSystem.Instance;
        if (system == null) return;
        string next = "";
        foreach (var relic in system.activeRelics) next += (int)relic.type + ",";
        if (next != signature) { signature = next; Rebuild(); }
        if (selected.HasValue && !system.HasRelic(selected.Value)) selected = null;
        foreach (var preview in previews)
        {
            if (preview.root == null) continue;
            preview.root.SetActive(true);
            preview.pivot.Rotate(Vector3.up, spinSpeed * Time.unscaledDeltaTime, Space.Self);
        }
        if (selected.HasValue && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            HandleEscape();
    }
    void Rebuild()
    {
        DisposePreviews();
        if (RelicSystem.Instance == null) return;
        int index = 0;
        signature = "";
        foreach (var relic in RelicSystem.Instance.activeRelics)
        {
            signature += (int)relic.type + ",";
            previews.Add(BuildPreview(relic, index++));
        }
    }
    Preview BuildPreview(Relic relic, int index)
    {
        // Separate render stages, outside the prison's playable area.
        var root = new GameObject("OwnedRelicPreview_" + relic.type);
        root.transform.position = new Vector3(10000f + index * 20f, -10000f, 10000f);
        var pivot = new GameObject("SpinPivot").transform;
        pivot.SetParent(root.transform, false);
        var visual = RelicPrefabFactory.Create(relic, pivot);
        var effects = visual.GetComponent<RelicObject3D>();
        if (effects != null) effects.enabled = false; // The preview pivot supplies its own unscaled rotation.
        foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (ParticleSystem particles in visual.GetComponentsInChildren<ParticleSystem>(true)) particles.gameObject.SetActive(false);
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
        bool haveBounds = false;
        Bounds bounds = new Bounds();
        foreach (Renderer renderer in renderers)
        {
            if (renderer is ParticleSystemRenderer) continue;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (!haveBounds) { bounds = renderer.bounds; haveBounds = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        if (haveBounds)
        {
            float diameter = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            float normalization = 1.35f / Mathf.Max(0.001f, diameter);
            Vector3 center = root.transform.InverseTransformPoint(bounds.center);
            visual.transform.localScale *= normalization;
            visual.transform.localPosition -= center * normalization;
        }
        SetLayer(root.transform, previewLayer);
        var cameraObject = new GameObject("PreviewCamera");
        cameraObject.transform.SetParent(root.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 0f, -4f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 1.1f;
        camera.nearClipPlane = 0.1f; camera.farClipPlane = 8f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = panelColor;
        camera.cullingMask = 1 << previewLayer;
        camera.allowHDR = false; camera.allowMSAA = false;
        camera.depth = -100f;
        var texture = new RenderTexture(previewResolution, previewResolution, 24);
        texture.name = "RelicPreview_" + relic.type;
        texture.filterMode = FilterMode.Point;
        texture.Create();
        camera.targetTexture = texture;
        var lamp = new GameObject("PreviewLight");
        lamp.transform.SetParent(root.transform, false);
        lamp.transform.localPosition = new Vector3(-1f, 2f, -2f);
        Light light = lamp.AddComponent<Light>();
        light.type = LightType.Point; light.range = 7f; light.intensity = 3f;
        light.cullingMask = 1 << previewLayer;
        return new Preview { relic = relic, root = root, pivot = pivot, camera = camera, texture = texture };
    }
    static void SetLayer(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root) SetLayer(child, layer);
    }
    public void CloseInspection() { selected = null; }
    void OnGUI()
    {
        if (!ShopOpen || ShopManager.Instance == null || RelicSystem.Instance == null) return;
        if (title == null)
        {
            title = new GUIStyle(GUI.skin.label) { fontSize = 23, wordWrap = true };
            body = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
            button = new GUIStyle(GUI.skin.button) { fontSize = 18 };
        }
        Matrix4x4 oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(Vector3.one * Scale);
        Rect bar = BarRect;
        GUI.Box(bar, "");
        GUI.Label(new Rect(bar.x + 12, bar.y + 6, bar.width - 24, 28),
            $"OWNED RELICS  {RelicSystem.Instance.activeRelics.Count}/{RelicSystem.Instance.RelicCapacity}", body);
        Relic hover = null;
        Vector2 pointer = Event.current.mousePosition;
        for (int i = 0; i < RelicSystem.Instance.RelicCapacity; i++)
        {
            Rect slot = new Rect(bar.x + 12 + i * 90f, bar.y + 36, 82, 76);
            GUI.Box(slot, "");
            if (i >= previews.Count) continue;
            Preview preview = previews[i];
            if (preview.texture != null) GUI.DrawTexture(slot, preview.texture, ScaleMode.ScaleToFit, false);
            if (!selected.HasValue && slot.Contains(pointer))
            {
                hover = preview.relic;
                if (window.CanInteract && Event.current.type == EventType.MouseDown)
                {
                    if (Event.current.button == 0) selected = preview.relic.type;
                    if (Event.current.button == 1) ShopRelicInterface.RequestJournal(preview.relic);
                    Event.current.Use();
                }
            }
        }
        if (previews.Count == 0)
            GUI.Label(new Rect(bar.x + 20, bar.y + 55, bar.width - 40, 45), "Buy a relic to start your build.", body);
        if (hover != null && !selected.HasValue)
        {
            Rect tooltip = new Rect(bar.x, bar.y - 130, bar.width, 118);
            GUI.Box(tooltip, "");
            GUI.Label(new Rect(tooltip.x + 12, tooltip.y + 8, tooltip.width - 24, 30), hover.displayName, title);
            GUI.Label(new Rect(tooltip.x + 12, tooltip.y + 43, tooltip.width - 24, 65), hover.description, body);
        }
        if (selected.HasValue) DrawInspection();
        GUI.matrix = oldMatrix;
    }
    void DrawInspection()
    {
        Preview preview = previews.Find(p => p.relic.type == selected.Value);
        if (preview == null) { selected = null; return; }
        Color previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(0, 0, Width, Height), Texture2D.whiteTexture);
        GUI.color = previous;
        Rect panel = new Rect((Width - 650) * 0.5f, (Height - 500) * 0.5f, 650, 500);
        GUI.Box(panel, "");
        GUI.Label(new Rect(panel.x + 24, panel.y + 18, 540, 42), preview.relic.displayName, title);
        if (GUI.Button(new Rect(panel.x + 590, panel.y + 16, 40, 35), "X", button)) selected = null;
        GUI.DrawTexture(new Rect(panel.x + 24, panel.y + 80, 290, 290), preview.texture, ScaleMode.ScaleToFit, false);
        GUI.Label(new Rect(panel.x + 335, panel.y + 95, 290, 250), preview.relic.description, body);
        GUI.Label(new Rect(panel.x + 24, panel.y + 385, 590, 30), "Selling removes this relic's effects and frees its slot.", body);
        bool enabled = GUI.enabled;
        GUI.enabled = window.CanInteract;
        if (GUI.Button(new Rect(panel.x + 130, panel.y + 432, 390, 44),
            $"Sell for {ShopManager.Instance.GetRelicSaleValue(preview.relic.type)} gold", button))
        {
            bool sold = ShopManager.Instance.TrySellRelic(preview.relic.type, out string reason);
            ShopRelicInterface.Message = reason;
            if (sold) { selected = null; signature = ""; Rebuild(); }
        }
        GUI.enabled = enabled;
    }
    void DisposePreviews()
    {
        foreach (var preview in previews)
        {
            if (preview.root != null) { preview.root.SetActive(false); Destroy(preview.root); }
            if (preview.camera != null) preview.camera.targetTexture = null;
            if (preview.texture != null) { preview.texture.Release(); Destroy(preview.texture); }
        }
        previews.Clear();
    }
    void OnDisable() { selected = null; DisposePreviews(); signature = ""; }
    void OnDestroy() { DisposePreviews(); if (Instance == this) Instance = null; }
}
