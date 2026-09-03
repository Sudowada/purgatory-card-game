using UnityEngine;

public class KnobVolumeDisplay : MonoBehaviour
{
    [Header("Knob Reference")]
    public KnobInteractable trackedKnob;
    public LanternInteractable trackedLantern;

    [Header("Position Offset")]
    public Vector3 localOffset    = Vector3.zero;
    public float   displayScale   = 1f;

    [Header("Rotation")]
    [Tooltip("Fixed world rotation for the display — does not follow the knob spin")]
    public Vector3 fixedWorldRotation = new Vector3(0f, 0f, 0f);

    [Header("Arc Settings")]
    [Range(0f, 360f)] public float arcSpanDegrees  = 180f;
    [Range(0f, 360f)] public float arcStartDegrees = 180f;
    public float outerRadius = 0.45f;
    public float innerRadius = 0.30f;
    public int   arcSegments = 64;

    [Header("Needle Settings")]
    public float needleLength = 0.28f;
    public float needleWidth  = 0.012f;

    [Header("Colors")]
    public Color trackColor    = new Color(0.12f, 0.08f, 0.02f);
    public Color fillColor     = new Color(1.0f,  0.55f, 0.0f);
    public Color fillColorHigh = new Color(1.0f,  0.88f, 0.15f);
    public Color needleColor   = new Color(1.0f,  0.92f, 0.6f);

    [Header("Emission")]
    public float emissionIntensity = 2.5f;

    private MeshFilter   _trackFilter,  _fillFilter,  _needleFilter;
    private MeshRenderer _trackRenderer, _fillRenderer, _needleRenderer;
    private Material     _trackMat,     _fillMat,     _needleMat;
    private Mesh         _trackMesh,    _fillMesh,    _needleMesh;

    private float _displayValue = 0.5f;
    private bool  _initialized  = false;

    void Start()
    {
        Initialize();
        SnapToSource();
        Rebuild(_displayValue);
    }

    void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("[KnobVolumeDisplay] Could not find URP/Lit shader!");
            return;
        }

        (_trackFilter, _trackRenderer, _trackMat) = MakeLayer("_Track", shader, trackColor);
        _trackMesh = new Mesh { name = "TrackMesh" };
        _trackFilter.sharedMesh = _trackMesh;

        (_fillFilter, _fillRenderer, _fillMat) = MakeLayer("_Fill", shader, fillColor);
        _fillMesh = new Mesh { name = "FillMesh" };
        _fillFilter.sharedMesh = _fillMesh;

        (_needleFilter, _needleRenderer, _needleMat) = MakeLayer("_Needle", shader, needleColor);
        _needleMesh = new Mesh { name = "NeedleMesh" };
        _needleFilter.sharedMesh = _needleMesh;
    }

    (MeshFilter, MeshRenderer, Material) MakeLayer(string layerName, Shader shader, Color color)
    {
        var go = new GameObject(layerName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale    = Vector3.one;

        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows    = false;

        var mat = new Material(shader);
        mat.SetFloat("_Surface", 0f);
        mat.SetFloat("_Smoothness", 0f);
        mat.SetFloat("_Metallic", 0f);
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_EmissionColor", color * emissionIntensity);
        mat.EnableKeyword("_EMISSION");
        mr.sharedMaterial = mat;

        return (mf, mr, mat);
    }

    void Update()
    {
        if (!_initialized) return;

        SnapToSource();

        float target = trackedKnob != null ? trackedKnob.NormalizedValue
                     : trackedLantern != null ? trackedLantern.NormalizedValue
                     : 0.5f;

        _displayValue = Mathf.Lerp(_displayValue, target, Time.deltaTime * 14f);
        Rebuild(_displayValue);
    }

    void SnapToSource()
    {
        Transform source = trackedKnob != null ? trackedKnob.transform
                         : trackedLantern != null ? trackedLantern.transform
                         : null;

        if (source == null) return;

        transform.position = source.TransformPoint(localOffset);
        transform.rotation = Quaternion.Euler(fixedWorldRotation);

        Vector3 s = source.lossyScale;
        transform.localScale = new Vector3(
            s.x * displayScale,
            s.y * displayScale,
            s.z * displayScale);
    }

    void Rebuild(float normalized)
    {
        if (!_initialized) return;

        BuildArcMesh(_trackMesh, arcStartDegrees, arcSpanDegrees,
                     innerRadius, outerRadius, 0f);

        float filledSpan = arcSpanDegrees * Mathf.Clamp01(normalized);
        if (filledSpan > 0.5f)
            BuildArcMesh(_fillMesh, arcStartDegrees, filledSpan,
                         innerRadius, outerRadius, 0.001f);
        else
            _fillMesh.Clear();

        Color c = Color.Lerp(fillColor, fillColorHigh, normalized);
        _fillMat.SetColor("_BaseColor",     c);
        _fillMat.SetColor("_EmissionColor", c * emissionIntensity);

        float needleAngle = arcStartDegrees + arcSpanDegrees * normalized;
        BuildNeedleMesh(_needleMesh, needleAngle, needleLength, needleWidth, 0.002f);
    }

    void BuildArcMesh(Mesh mesh, float startDeg, float spanDeg,
                      float inner, float outer, float zOff)
    {
        int vc = (arcSegments + 1) * 2;
        var v  = new Vector3[vc];
        var tr = new int[arcSegments * 6];

        for (int i = 0; i <= arcSegments; i++)
        {
            float t   = (float)i / arcSegments;
            float ang = (startDeg + spanDeg * t) * Mathf.Deg2Rad;
            float c   = Mathf.Cos(ang), s = Mathf.Sin(ang);
            v[i*2]   = new Vector3(c * inner, s * inner, zOff);
            v[i*2+1] = new Vector3(c * outer, s * outer, zOff);
        }
        for (int i = 0; i < arcSegments; i++)
        {
            int a = i*2, b=a+1, c=a+2, d=a+3, t=i*6;
            tr[t]=a; tr[t+1]=b; tr[t+2]=c;
            tr[t+3]=b; tr[t+4]=d; tr[t+5]=c;
        }

        mesh.Clear();
        mesh.vertices  = v;
        mesh.triangles = tr;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    void BuildNeedleMesh(Mesh mesh, float angleDeg,
                         float length, float width, float zOff)
    {
        float r  = angleDeg * Mathf.Deg2Rad;
        float c  = Mathf.Cos(r), s = Mathf.Sin(r);
        float px = -s * width * 0.5f, py = c * width * 0.5f;

        mesh.Clear();
        mesh.vertices  = new Vector3[]
        {
            new Vector3(px,          py,          zOff),
            new Vector3(-px,        -py,          zOff),
            new Vector3(c*length+px, s*length+py, zOff),
            new Vector3(c*length-px, s*length-py, zOff),
        };
        mesh.triangles = new int[] { 0,1,2, 1,3,2 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    void OnDrawGizmosSelected()
    {
        Transform source = trackedKnob != null ? trackedKnob.transform
                         : trackedLantern != null ? trackedLantern.transform
                         : null;
        if (source == null) return;
        Gizmos.color = new Color(1f, 0.55f, 0f, 0.8f);
        Vector3 pos = source.TransformPoint(localOffset);
        Gizmos.DrawWireSphere(pos, 0.03f);
        Gizmos.DrawLine(source.position, pos);
    }
}