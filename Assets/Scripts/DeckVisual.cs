using UnityEngine;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;

/// <summary>
/// Builds 3D card stacks that correctly face the orthographic camera.
/// Cards are rendered as face-forward quads with painted textures,
/// stacked with a slight Y+X offset per layer to create a depth illusion.
/// Plinth base and engraved label sit below the stack.
/// </summary>
public class DeckVisual : MonoBehaviour
{
    public static DeckVisual Instance { get; private set; }

    [Header("Stack Appearance")]
    [SerializeField] private int   layerCount  = 9;     // visible card layers
    [SerializeField] private float cardW       = 1.10f; // world units wide
    [SerializeField] private float cardH       = 1.55f; // world units tall
    [SerializeField] private float cardDepth   = 0.02f; // cube Z thickness
    [SerializeField] private float stackStepY  = 0.016f; // vertical offset per layer
    [SerializeField] private float stackStepX  = 0.008f; // horizontal lean per layer

    // Screen-space positions (world Z=0, same plane as hand cards)
    // Ortho size=5, aspect 16:9 → X range ≈ ±8.9, Y range ±5
    // Place in bottom corners, slightly inset
    private static readonly Vector3 DeckPos    = new Vector3( 6.6f, -3.6f, 0f);
    private static readonly Vector3 DiscardPos = new Vector3(-6.6f, -3.6f, 0f);

    // Colours
    static readonly Color c_parch     = new Color(0.85f, 0.76f, 0.55f);
    static readonly Color c_cardBackD = new Color(0.16f, 0.08f, 0.24f); // deck: purple
    static readonly Color c_cardBackA = new Color(0.09f, 0.11f, 0.26f); // discard: blue
    static readonly Color c_gold      = new Color(0.78f, 0.60f, 0.12f);
    static readonly Color c_blood     = new Color(0.60f, 0.05f, 0.05f);
    static readonly Color c_stone     = new Color(0.28f, 0.26f, 0.23f);
    static readonly Color c_stoneHi   = new Color(0.42f, 0.39f, 0.34f);
    static readonly Color c_ironDark  = new Color(0.12f, 0.11f, 0.10f);

    private Transform deckRoot;
    private Transform discardRoot;
    private TextMeshPro deckCountTMP;
    private TextMeshPro discardCountTMP;
    private List<Transform> deckLayers    = new List<Transform>();
    private List<Transform> discardLayers = new List<Transform>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }
    }

    void Start()
    {
        // Destroy leftover stacks from previous scene load
        if (deckRoot    != null) { Destroy(deckRoot.gameObject);    deckRoot    = null; }
        if (discardRoot != null) { Destroy(discardRoot.gameObject); discardRoot = null; }
        deckLayers.Clear(); discardLayers.Clear();

        DOVirtual.DelayedCall(0.08f, () => BuildNow());
    }

    void TryBuildWhenReady()
    {
        BuildNow();
    }

    void BuildNow()
    {
            deckRoot    = BuildStack("DeckStack",    DeckPos,    isDeck: true);
            discardRoot = BuildStack("DiscardStack", DiscardPos, isDeck: false);
            UpdateCounts(52, 0);
            if (CardAnimator.Instance != null)
            {
                CardAnimator.Instance.deckPos        = DeckPos;
                CardAnimator.Instance.discardPilePos = DiscardPos;
            }
        
    }

    // ── PUBLIC API ────────────────────────────────────────────────────────────

    public void UpdateCounts(int deck, int discard)
    {
        if (deckCountTMP)    deckCountTMP.text    = deck.ToString();
        if (discardCountTMP) discardCountTMP.text = discard.ToString();
        RefreshLayers(deckLayers,    deck,    52);
        RefreshLayers(discardLayers, discard, 52);
    }

    public void OnCardDrawn()
    {
        if (deckLayers.Count == 0) return;
        // Find topmost visible layer and briefly lift it
        for (int i = deckLayers.Count - 1; i >= 0; i--)
        {
            if (deckLayers[i] != null && deckLayers[i].gameObject.activeSelf)
            {
                deckLayers[i].DOKill();
                deckLayers[i].DOLocalMoveY(deckLayers[i].localPosition.y + 0.05f, 0.07f)
                    .OnComplete(() => deckLayers[i]?.DOLocalMoveY(
                        deckLayers[i].localPosition.y - 0.05f, 0.06f));
                break;
            }
        }
    }

    public void OnCardDiscarded()
    {
        if (discardRoot != null)
            discardRoot.DOPunchScale(Vector3.one * 0.04f, 0.12f, 3, 0.4f);
    }

    // ── STACK BUILDER ─────────────────────────────────────────────────────────

    Transform BuildStack(string name, Vector3 worldPos, bool isDeck)
    {
        var root = new GameObject(name).transform;
        root.SetParent(null); // scene root — destroyed on reload
        root.position = worldPos;

        var layers = isDeck ? deckLayers : discardLayers;
        layers.Clear();

        // ── Card layers — stacked with Y+X offset for depth illusion ──────────
        for (int i = 0; i < layerCount; i++)
        {
            bool isTop = (i == layerCount - 1);
            var layer  = BuildCardLayer(root, i, isDeck, isTop && !isDeck);
            layers.Add(layer);
        }

        // ── Plinth base ────────────────────────────────────────────────────────
        BuildPlinth(root, isDeck);

        // ── Iron border frame around top card ─────────────────────────────────
        BuildFrame(root, isDeck);

        // ── Count label below plinth ───────────────────────────────────────────
        BuildCountLabel(root, isDeck,
            out var cTMP);
        if (isDeck) deckCountTMP    = cTMP;
        else        discardCountTMP = cTMP;

        return root;
    }

    Transform BuildCardLayer(Transform parent, int i, bool isDeck, bool isParchFace)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = $"Layer_{i}";
        go.transform.SetParent(parent);

        // Each layer steps up and slightly to one side (lean toward the pile)
        float xOff = stackStepX * i * (isDeck ? -1f : 1f); // lean inward
        float yOff = stackStepY * i;
        float rotZ = Mathf.Sin(i * 0.6f) * 0.4f; // tiny random tilt

        go.transform.localPosition = new Vector3(xOff, yOff, -i * 0.001f); // tiny Z for sorting
        go.transform.localScale    = new Vector3(cardW, cardH, cardDepth);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);

        var mat = new Material(GetLitShader());
        mat.color = Color.white;
        mat.mainTexture = isParchFace
            ? MakeParchTex(128, 180)
            : MakeCardBackTex(128, 180, isDeck);
        mat.mainTexture.filterMode = FilterMode.Bilinear;

        var rend = go.GetComponent<Renderer>();
        rend.material = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows    = false;
        Destroy(go.GetComponent<Collider>());

        return go.transform;
    }

    void BuildPlinth(Transform parent, bool isDeck)
    {
        // Stone rectangle sitting just below the card stack
        float plinthH = 0.28f;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Plinth";
        go.transform.SetParent(parent);
        go.transform.localPosition = new Vector3(0f, -cardH * 0.5f - plinthH * 0.5f - 0.01f, 0.005f);
        go.transform.localScale    = new Vector3(cardW + 0.22f, plinthH, cardDepth + 0.005f);

        var mat = new Material(GetLitShader());
        mat.mainTexture = MakeStoneTex(64, 32, isDeck);
        mat.mainTexture.filterMode = FilterMode.Bilinear;
        mat.color = c_stone;
        go.GetComponent<Renderer>().material = mat;
        go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Destroy(go.GetComponent<Collider>());

        // Highlight lip at top of plinth
        var lip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lip.name = "PlinthLip";
        lip.transform.SetParent(parent);
        lip.transform.localPosition = new Vector3(0f, -cardH * 0.5f - 0.02f, -0.001f);
        lip.transform.localScale    = new Vector3(cardW + 0.22f, 0.04f, cardDepth + 0.008f);
        var lipMat = new Material(GetLitShader());
        lipMat.color = c_stoneHi;
        lip.GetComponent<Renderer>().material = lipMat;
        lip.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Destroy(lip.GetComponent<Collider>());

        // Blood/rune groove on plinth face
        var groove = GameObject.CreatePrimitive(PrimitiveType.Cube);
        groove.name = "Groove";
        groove.transform.SetParent(parent);
        groove.transform.localPosition = new Vector3(0f, -cardH * 0.5f - plinthH * 0.5f, -0.002f);
        groove.transform.localScale    = new Vector3(cardW * 0.45f, 0.025f, cardDepth + 0.01f);
        var gMat = new Material(GetUnlitShader());
        gMat.color = isDeck ? new Color(c_blood.r,c_blood.g,c_blood.b,0.8f)
                            : new Color(c_gold.r, c_gold.g, c_gold.b, 0.7f);
        groove.GetComponent<Renderer>().material = gMat;
        groove.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Destroy(groove.GetComponent<Collider>());
    }

    void BuildFrame(Transform parent, bool isDeck)
    {
        // Thin iron border around the top card for definition
        float fw = cardW + 0.055f, fh = cardH + 0.055f, fd = cardDepth + 0.004f;
        float borderW = 0.028f;
        Color frameCol = isDeck
            ? new Color(c_blood.r * 0.6f, c_blood.g * 0.6f, c_blood.b * 0.6f)
            : new Color(c_gold.r  * 0.5f, c_gold.g  * 0.5f, c_gold.b  * 0.5f);

        float topY    = stackStepY * (layerCount - 1);
        float topXOff = stackStepX * (layerCount - 1) * (isDeck ? -1f : 1f);

        // Four border strips: top, bottom, left, right
        (Vector3 pos, Vector3 scale)[] strips =
        {
            (new Vector3(topXOff, topY + fh*0.5f - borderW*0.5f, -(layerCount-1)*0.001f - 0.001f),
             new Vector3(fw, borderW, fd)),
            (new Vector3(topXOff, topY - fh*0.5f + borderW*0.5f, -(layerCount-1)*0.001f - 0.001f),
             new Vector3(fw, borderW, fd)),
            (new Vector3(topXOff - fw*0.5f + borderW*0.5f, topY, -(layerCount-1)*0.001f - 0.001f),
             new Vector3(borderW, fh - borderW*2f, fd)),
            (new Vector3(topXOff + fw*0.5f - borderW*0.5f, topY, -(layerCount-1)*0.001f - 0.001f),
             new Vector3(borderW, fh - borderW*2f, fd)),
        };

        foreach (var (pos, scale) in strips)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Cube);
            s.name = "Frame";
            s.transform.SetParent(parent);
            s.transform.localPosition = pos;
            s.transform.localScale    = scale;
            var sMat = new Material(GetLitShader());
            sMat.color = frameCol;
            s.GetComponent<Renderer>().material = sMat;
            s.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Destroy(s.GetComponent<Collider>());
        }
    }

    void BuildCountLabel(Transform parent, bool isDeck, out TextMeshPro countTMP)
    {
        float plinthH = 0.28f;
        float labelY  = -cardH * 0.5f - plinthH - 0.22f;

        // Label background — dark stone tablet
        var bg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bg.name = "LabelBg";
        bg.transform.SetParent(parent);
        bg.transform.localPosition = new Vector3(0f, labelY, 0.006f);
        bg.transform.localScale    = new Vector3(cardW + 0.22f, 0.30f, cardDepth);
        var bgMat = new Material(GetLitShader());
        bgMat.mainTexture = MakeStoneTex(64, 16, isDeck);
        bgMat.mainTexture.filterMode = FilterMode.Bilinear;
        bgMat.color = c_ironDark;
        bg.GetComponent<Renderer>().material = bgMat;
        bg.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Destroy(bg.GetComponent<Collider>());

        // Count TMP — in world space, same Z as cards
        var countGO = new GameObject("CountLabel");
        countGO.transform.SetParent(parent);
        countGO.transform.localPosition = new Vector3(0f, labelY + 0.06f, -0.012f);
        countGO.transform.localScale    = Vector3.one * 0.18f;
        countGO.transform.localRotation = Quaternion.identity;

        countTMP = countGO.AddComponent<TextMeshPro>();
        countTMP.text      = "—";
        countTMP.fontSize  = 2.4f;
        countTMP.alignment = TextAlignmentOptions.Center;
        countTMP.color     = isDeck ? c_parch : new Color(c_gold.r, c_gold.g, c_gold.b, 0.95f);
        countTMP.fontStyle = FontStyles.Bold;

        // Word label (DECK / DISCARD)
        var wordGO = new GameObject("WordLabel");
        wordGO.transform.SetParent(parent);
        wordGO.transform.localPosition = new Vector3(0f, labelY - 0.06f, -0.012f);
        wordGO.transform.localScale    = Vector3.one * 0.12f;
        wordGO.transform.localRotation = Quaternion.identity;

        var wordTMP = wordGO.AddComponent<TextMeshPro>();
        wordTMP.text      = isDeck ? "DECK" : "DISCARD";
        wordTMP.fontSize  = 1.8f;
        wordTMP.alignment = TextAlignmentOptions.Center;
        wordTMP.color     = new Color(0.55f, 0.50f, 0.40f, 0.75f);
        wordTMP.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
    }

    // ── LAYER VISIBILITY ──────────────────────────────────────────────────────

    void RefreshLayers(List<Transform> layers, int count, int max)
    {
        if (layers == null) return;
        float t = max > 0 ? Mathf.Clamp01((float)count / max) : 0f;
        int show = count <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(t * layers.Count));
        for (int i = 0; i < layers.Count; i++)
            if (layers[i] != null)
                layers[i].gameObject.SetActive(i < show);
    }

    // ── TEXTURE GENERATORS ────────────────────────────────────────────────────

    Texture2D MakeParchTex(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float n  = Mathf.PerlinNoise(x * 0.12f, y * 0.12f);
            float n2 = Mathf.PerlinNoise(x * 0.04f + 5f, y * 0.04f + 3f);
            Color c  = Color.Lerp(new Color(0.66f,0.53f,0.31f),
                                  new Color(0.90f,0.82f,0.60f), n*.7f+n2*.3f);
            float ex = Mathf.Min(x,w-1-x)/(float)w;
            float ey = Mathf.Min(y,h-1-y)/(float)h;
            c = Color.Lerp(c, new Color(0.18f,0.09f,0.03f),
                (1f - Mathf.Clamp01(Mathf.Pow(Mathf.Min(ex,ey)*10f,.7f))) * .72f);
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    Texture2D MakeCardBackTex(int w, int h, bool isDeck)
    {
        Color bg   = isDeck ? new Color(0.13f,0.06f,0.20f) : new Color(0.08f,0.10f,0.24f);
        Color line = isDeck ? new Color(0.55f,0.05f,0.05f) : new Color(0.20f,0.30f,0.65f);
        Color gold = new Color(0.60f, 0.46f, 0.08f);
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float n  = Mathf.PerlinNoise(x*.09f,y*.09f);
            Color c  = Color.Lerp(bg, bg*1.35f, n*.3f);
            // Diamond lattice
            int period = 14;
            float u = (x+y) % period / (float)period;
            float v = (x-y+h*2) % period / (float)period;
            if (u < 0.08f || u > 0.92f || v < 0.08f || v > 0.92f)
                c = Color.Lerp(c, line, 0.6f);
            // Gold border
            int bw = 6;
            if (x<bw||x>=w-bw||y<bw||y>=h-bw)
                c = Color.Lerp(c, gold, 0.55f);
            // Center diamond
            float dcx = Mathf.Abs(x - w/2f) + Mathf.Abs(y - h/2f);
            if (dcx < 9f)
                c = Color.Lerp(c, gold, (1f - dcx/9f) * 0.7f);
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    Texture2D MakeStoneTex(int w, int h, bool bloodAccent)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float n = Mathf.PerlinNoise(x*.14f,y*.14f);
            Color c = Color.Lerp(new Color(0.14f,0.13f,0.11f), c_stone, n*.6f);
            if (bloodAccent)
            {
                float r = Mathf.PerlinNoise(x*.07f+30f,y*.18f+10f);
                if (r > .70f) c = Color.Lerp(c, c_blood, (r-.70f)*.5f);
            }
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    // ── UNITY SHADERS ─────────────────────────────────────────────────────────

    Shader GetLitShader()   => Shader.Find("Universal Render Pipeline/Lit")   ?? Shader.Find("Standard");
    Shader GetUnlitShader() => Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
}

/// <summary>Makes deck labels always face the camera.</summary>
public class DeckLabelBillboard : MonoBehaviour
{
    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam != null) transform.forward = cam.transform.forward;
    }
}