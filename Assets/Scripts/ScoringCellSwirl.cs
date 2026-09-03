using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Animated swirling background for scoring cells.
/// Apply to a UI Image — creates a material instance with the swirl shader.
/// </summary>
[RequireComponent(typeof(Image))]
public class ScoringCellSwirl : MonoBehaviour
{
    [Header("Colors")]
    public Color baseColor  = new Color(0.08f, 0.18f, 0.35f, 1f);
    public Color swirlColor = new Color(0.15f, 0.28f, 0.55f, 1f);

    [Header("Animation")]
    [Range(0f, 2f)]   public float speed     = 0.35f;
    [Range(1f, 10f)]  public float scale     = 4.5f;
    [Range(0f, 0.5f)] public float intensity = 0.12f;

    private Material _mat;
    private Image    _img;
    private bool     _ready;

    void Awake()
    {
        Apply();
    }

    void Apply()
    {
        _img = GetComponent<Image>();
        if (_img == null) return;

        var shader = Shader.Find("Custom/ScoringCellSwirl");
        if (shader == null)
        {
            Debug.LogWarning("[ScoringCellSwirl] Shader 'Custom/ScoringCellSwirl' not found. " +
                "Make sure the shader is in Assets/Shaders/");
            return;
        }

        if (_mat != null) Destroy(_mat);
        _mat = new Material(shader);
        _mat.SetColor("_Color",     baseColor);
        _mat.SetColor("_Color2",    swirlColor);
        _mat.SetFloat("_Speed",     speed);
        _mat.SetFloat("_Scale",     scale);
        _mat.SetFloat("_Intensity", intensity);

        _img.material = _mat;
        _ready = true;
    }

    // Called from HUDBuilder after setting colors
    public void SetColors(Color base_, Color swirl)
    {
        baseColor  = base_;
        swirlColor = swirl;
        if (!_ready) Apply();
        if (_mat != null)
        {
            _mat.SetColor("_Color",  base_);
            _mat.SetColor("_Color2", swirl);
        }
    }

    void OnDestroy()
    {
        if (_mat != null) Destroy(_mat);
    }
}