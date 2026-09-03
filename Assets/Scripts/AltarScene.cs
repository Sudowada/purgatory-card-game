using UnityEngine;
using UnityEngine.Rendering.Universal;
using TMPro;
using DG.Tweening;
 
/// <summary>
/// Attach this to any GameObject in your dungeon scene.
/// Handles render settings, the enemy card display, and camera setup.
/// Does NOT touch AltarInteractable or any exploration logic.
///
/// NOTE: FixCamera() used to run unconditionally on Start(), forcing
/// Camera.main into orthographic 2D mode at a fixed position. That
/// fights with GameStateManager, which now owns camera placement for
/// both Exploration and CardGame modes. FixCamera() is now gated so
/// it only applies when the game actually starts/enters CardGame mode.
/// </summary>
public class AltarScene : MonoBehaviour
{
    [Header("Fonts")]
    [SerializeField] private TMP_FontAsset altarFont;
 
    [Header("Enemy Card")]
    [Tooltip("Drag a 3D card GameObject here. Leave empty for a simple built-in card.")]
    [SerializeField] private GameObject enemyCardObject;
 
    [Header("Rendering")]
    [SerializeField] private Color ambientColour = new Color(0.12f, 0.08f, 0.04f);
    [SerializeField] [Range(0f, 0.1f)] private float fogDensity = 0.015f;
    [SerializeField] private Color fogColour = new Color(0.04f, 0.025f, 0.01f);
 
    private TextMeshPro _enemyTMP;
    private GameObject  _enemyCard3D;
 
    void Start()
    {
        ApplyRenderSettings();
 
        // Only force the 2D orthographic card-game camera if we're actually
        // booting straight into card mode. If GameStateManager exists and
        // starts in Exploration, leave the camera alone — GameStateManager
        // owns it.
        bool startingInCardGame =
            GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState == GameState.CardGame;
 
        if (startingInCardGame)
            FixCamera();
 
        SetupEnemyCard();
    }
 
    // ── RENDER SETTINGS ───────────────────────────────────────────────────────
 
    void ApplyRenderSettings()
    {
        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColour;
        RenderSettings.fog          = true;
        RenderSettings.fogMode      = FogMode.Exponential;
        RenderSettings.fogDensity   = fogDensity;
        RenderSettings.fogColor     = fogColour;
    }
 
    /// <summary>
    /// Snaps Camera.main into the flat 2D card-game view. Call this
    /// explicitly when entering card mode (e.g. from GameStateManager or
    /// DeckReturnInteractable), NOT automatically on scene Start().
    /// </summary>
    public void FixCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
 
        cam.orthographic     = true;
        cam.orthographicSize = 5f;
        cam.nearClipPlane    = 0.1f;
        cam.farClipPlane     = 100f;
        cam.transform.SetParent(null);
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.transform.rotation = Quaternion.identity;
        cam.backgroundColor    = new Color(0.04f, 0.03f, 0.02f, 1f);
 
        // Remove teal background from Mix & Jam template
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas.transform.parent != null) continue;
            var img = canvas.GetComponent<UnityEngine.UI.Image>();
            if (img != null) img.color = Color.clear;
        }
    }
 
    // ── ENEMY CARD ────────────────────────────────────────────────────────────
 
    void SetupEnemyCard()
    {
        if (enemyCardObject != null)
        {
            _enemyCard3D = enemyCardObject;
            _enemyTMP    = enemyCardObject.GetComponentInChildren<TextMeshPro>();
        }
        else
        {
            _enemyCard3D = BuildFallbackEnemyCard();
        }
 
        // Start hidden — shown by GameManager via ShowEnemyCard()
        if (_enemyCard3D != null)
            _enemyCard3D.SetActive(false);
    }
 
    public void SetEnemyValue(int value)
    {
        if (_enemyTMP == null) return;
        _enemyTMP.text = value.ToString();
        _enemyTMP.transform.DOKill();
        _enemyTMP.transform.localScale = Vector3.one * 1.3f;
        _enemyTMP.transform.DOScale(Vector3.one, 0.45f).SetEase(Ease.OutBack);
    }
 
    public void ShowEnemyCard(bool visible)
    {
        if (_enemyCard3D != null)
            _enemyCard3D.SetActive(visible);
    }
 
    // ── FALLBACK ENEMY CARD ───────────────────────────────────────────────────
 
    GameObject BuildFallbackEnemyCard()
    {
        var root = new GameObject("EnemyCard3D");
        root.transform.SetParent(transform);
        root.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        root.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
 
        Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
 
        // Backing
        var card = GameObject.CreatePrimitive(PrimitiveType.Cube);
        card.name = "CardBacking";
        card.transform.SetParent(root.transform);
        card.transform.localPosition = Vector3.zero;
        card.transform.localScale    = new Vector3(0.95f, 1.30f, 0.012f);
        var cardMat = new Material(lit);
        cardMat.color = new Color(0.82f, 0.72f, 0.52f);
        card.GetComponent<Renderer>().material = cardMat;
        Destroy(card.GetComponent<Collider>());
 
        // Border
        var border = GameObject.CreatePrimitive(PrimitiveType.Cube);
        border.transform.SetParent(root.transform);
        border.transform.localPosition = new Vector3(0f, 0f, 0.007f);
        border.transform.localScale    = new Vector3(0.88f, 1.22f, 0.002f);
        var bMat = new Material(lit);
        bMat.color = new Color(0.14f, 0.10f, 0.06f);
        border.GetComponent<Renderer>().material = bMat;
        border.GetComponent<Renderer>().shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        Destroy(border.GetComponent<Collider>());
 
        // "ENEMY" label
        var labelGO = new GameObject("EnemyLabel");
        labelGO.transform.SetParent(root.transform);
        labelGO.transform.localPosition = new Vector3(0f, 0.46f, -0.012f);
        labelGO.transform.localScale    = Vector3.one * 0.012f;
        var lbl = labelGO.AddComponent<TextMeshPro>();
        lbl.text      = "ENEMY";
        lbl.fontSize  = 10;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.color     = new Color(0.45f, 0.10f, 0.06f, 0.85f);
        lbl.fontStyle = FontStyles.Bold;
        if (altarFont) lbl.font = altarFont;
 
        // Value (driven by SetEnemyValue)
        var valGO = new GameObject("EnemyValue");
        valGO.transform.SetParent(root.transform);
        valGO.transform.localPosition = new Vector3(0f, 0.02f, -0.012f);
        valGO.transform.localScale    = Vector3.one * 0.020f;
        _enemyTMP           = valGO.AddComponent<TextMeshPro>();
        _enemyTMP.text      = "?";
        _enemyTMP.fontSize  = 52;
        _enemyTMP.alignment = TextAlignmentOptions.Center;
        _enemyTMP.color     = new Color(0.58f, 0.05f, 0.05f, 0.92f);
        _enemyTMP.fontStyle = FontStyles.Bold;
        if (altarFont) _enemyTMP.font = altarFont;
 
        // Skull watermark
        var skullGO = new GameObject("Skull");
        skullGO.transform.SetParent(root.transform);
        skullGO.transform.localPosition = new Vector3(0f, 0.02f, -0.011f);
        skullGO.transform.localScale    = Vector3.one * 0.022f;
        var sk = skullGO.AddComponent<TextMeshPro>();
        sk.text      = "☠";
        sk.fontSize  = 58;
        sk.alignment = TextAlignmentOptions.Center;
        sk.color     = new Color(0.18f, 0.10f, 0.06f, 0.10f);
 
        // Float animation
        root.transform
            .DOLocalMoveY(root.transform.localPosition.y + 0.06f, 2.2f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
        root.transform
            .DOLocalRotate(new Vector3(10f, 1.5f, -1f), 3.1f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
 
        return root;
    }
}