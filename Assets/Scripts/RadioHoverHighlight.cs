using UnityEngine;

/// <summary>
/// Attach to the Radio root GameObject.
/// Highlights all child Renderers on mouse hover using emission.
/// Requires materials to have emission enabled in their shader (Standard or URP Lit).
/// </summary>
public class RadioHoverHighlight : MonoBehaviour
{
    [Header("Highlight Settings")]
    public Color highlightColor = new Color(1f, 0.85f, 0.4f); // Warm golden glow
    [Range(0f, 2f)]
    public float highlightIntensity = 0.4f;
    public float fadeSpeed = 6f;

    private Renderer[] _renderers;
    private float _currentIntensity = 0f;
    private float _targetIntensity = 0f;
    private bool _isZoomedIn = false;

    // Store original emission colors per material
    private Color[] _originalEmissions;
    private Material[] _materials;

    void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>();

        // Collect all materials and cache original emissions
        var matList = new System.Collections.Generic.List<Material>();
        foreach (var r in _renderers)
            foreach (var m in r.materials)
                matList.Add(m);

        _materials = matList.ToArray();
        _originalEmissions = new Color[_materials.Length];

        for (int i = 0; i < _materials.Length; i++)
        {
            _materials[i].EnableKeyword("_EMISSION");
            _originalEmissions[i] = _materials[i].GetColor("_EmissionColor");
        }
    }

    void OnMouseEnter()
    {
        if (_isZoomedIn) return;
        _targetIntensity = highlightIntensity;
    }

    void OnMouseExit()
    {
        _targetIntensity = 0f;
    }

    void Update()
    {
        _currentIntensity = Mathf.Lerp(_currentIntensity, _targetIntensity, Time.deltaTime * fadeSpeed);

        for (int i = 0; i < _materials.Length; i++)
        {
            Color emission = _originalEmissions[i] + highlightColor * _currentIntensity;
            _materials[i].SetColor("_EmissionColor", emission);
        }
    }

    // Call this from RadioInteractable when zoomed in/out to disable highlight
    public void SetZoomedIn(bool zoomedIn)
    {
        _isZoomedIn = zoomedIn;
        _targetIntensity = 0f;
    }
}
