using UnityEngine;
using System.Collections;
using DG.Tweening;

/// <summary>
/// Attach to any relic 3D prefab (or generated primitive).
/// Drives emission color by category and surface effect by relic type.
/// Call Initialize(relic) after instantiating.
/// </summary>
public class RelicObject3D : MonoBehaviour
{
    [Header("Auto-configured by Initialize()")]
    public RelicType     relicType;
    public RelicCategory relicCategory;

    [Header("Visual Settings")]
    public float rotationSpeed    = 22f;   // degrees per second Y axis
    public float bobAmplitude     = 0.012f;
    public float bobSpeed         = 1.1f;

    [Header("Emission")]
    public float baseEmissionIntensity  = 1.8f;
    public float pulseMinIntensity      = 0.8f;
    public float pulseMaxIntensity      = 3.2f;
    public float pulseSpeed             = 1.4f;

    [Header("Particles")]
    public ParticleSystem smokeParticles;
    public ParticleSystem sparkParticles;
    public ParticleSystem shimmerParticles;

    // ── Internal ─────────────────────────────────────────────────────────────
    private Renderer  _renderer;
    private Material  _mat;
    private Color     _categoryColor;
    private Vector3   _baseLocalPos;
    private Coroutine _effectCoroutine;

    // ── Public Init ───────────────────────────────────────────────────────────

    /// <summary>Call immediately after Instantiate to configure visuals.</summary>
    public void Initialize(Relic relic)
    {
        relicType     = relic.type;
        relicCategory = relic.category;
        _categoryColor = GetCategoryColor(relicCategory);
        _baseLocalPos  = transform.localPosition;

        SetupRenderer();
        SetupParticles();
        StartEffects();
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    void SetupRenderer()
    {
        _renderer = GetComponentInChildren<Renderer>();
        if (_renderer == null) return;

        // Create unique material instance so relics don't share
        _mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        _mat.SetFloat("_Smoothness",  0.3f);
        _mat.SetFloat("_Metallic",    0.1f);
        _mat.SetColor("_BaseColor",   _categoryColor * 0.7f);
        _mat.SetColor("_EmissionColor", _categoryColor * baseEmissionIntensity);
        _mat.EnableKeyword("_EMISSION");
        _renderer.material = _mat;
    }

    void SetupParticles()
    {
        // Smoke — decay/rot relics
        if (smokeParticles != null)
        {
            bool useSmoke = relicCategory == RelicCategory.DecayManipulation;
            smokeParticles.gameObject.SetActive(useSmoke);
            if (useSmoke)
            {
                var main = smokeParticles.main;
                main.startColor = new Color(0.15f, 0.35f, 0.05f, 0.5f);
                smokeParticles.Play();
            }
        }

        // Sparks — risk/challenge relics
        if (sparkParticles != null)
        {
            bool useSparks = relicCategory == RelicCategory.RiskChallenge
                          || relicCategory == RelicCategory.ComboEngine;
            sparkParticles.gameObject.SetActive(useSparks);
            if (useSparks)
            {
                var main = sparkParticles.main;
                main.startColor = _categoryColor;
                sparkParticles.Play();
            }
        }

        // Shimmer — card manipulation relics
        if (shimmerParticles != null)
        {
            bool useShimmer = relicCategory == RelicCategory.CardManipulation
                           || relicCategory == RelicCategory.RuleChanger;
            shimmerParticles.gameObject.SetActive(useShimmer);
            if (useShimmer)
            {
                var main = shimmerParticles.main;
                main.startColor = _categoryColor * 0.6f;
                shimmerParticles.Play();
            }
        }
    }

    // ── Effects ───────────────────────────────────────────────────────────────

    void StartEffects()
    {
        if (_effectCoroutine != null) StopCoroutine(_effectCoroutine);
        _effectCoroutine = StartCoroutine(EffectLoop());
    }

    IEnumerator EffectLoop()
    {
        float t = Random.Range(0f, Mathf.PI * 2f); // random phase so relics don't sync
        while (true)
        {
            t += Time.deltaTime;

            // ── Rotation ───────────────────────────────────────────────────
            transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.World);

            // ── Bob ────────────────────────────────────────────────────────
            float yOff = Mathf.Sin(t * bobSpeed) * bobAmplitude;
            transform.localPosition = _baseLocalPos + new Vector3(0f, yOff, 0f);

            // ── Emission effect by category ────────────────────────────────
            if (_mat != null)
            {
                float emissionMult = GetEmissionMult(t);
                _mat.SetColor("_EmissionColor", _categoryColor * emissionMult);
            }

            yield return null;
        }
    }

    float GetEmissionMult(float t)
    {
        switch (relicCategory)
        {
            // Scaling: smooth breathe pulse
            case RelicCategory.ScalingMultiplier:
                return Mathf.Lerp(pulseMinIntensity, pulseMaxIntensity,
                    (Mathf.Sin(t * pulseSpeed) + 1f) * 0.5f);

            // Risk: fast unstable flicker
            case RelicCategory.RiskChallenge:
                float flicker = Mathf.PerlinNoise(t * 6f, 0f);
                return Mathf.Lerp(pulseMinIntensity, pulseMaxIntensity, flicker);

            // Combo: medium pulse
            case RelicCategory.ComboEngine:
                return Mathf.Lerp(baseEmissionIntensity * 0.7f, pulseMaxIntensity,
                    (Mathf.Sin(t * pulseSpeed * 1.5f) + 1f) * 0.5f);

            // Decay: slow sickly throb
            case RelicCategory.DecayManipulation:
                return Mathf.Lerp(pulseMinIntensity * 0.6f, baseEmissionIntensity,
                    (Mathf.Sin(t * pulseSpeed * 0.5f) + 1f) * 0.5f);

            // Card manipulation: gentle shimmer
            case RelicCategory.CardManipulation:
                float shimmer = Mathf.PerlinNoise(t * 2f, 99f);
                return Mathf.Lerp(baseEmissionIntensity * 0.8f,
                    baseEmissionIntensity * 1.4f, shimmer);

            // Rule changers: solid steady with slow breathe
            default:
                return Mathf.Lerp(baseEmissionIntensity * 0.9f, baseEmissionIntensity * 1.2f,
                    (Mathf.Sin(t * 0.7f) + 1f) * 0.5f);
        }
    }

    // ── Category Colors ───────────────────────────────────────────────────────

    public static Color GetCategoryColor(RelicCategory cat) => cat switch
    {
        RelicCategory.RuleChanger       => new Color(1.0f, 0.78f, 0.10f),  // gold
        RelicCategory.DecayManipulation => new Color(0.18f, 0.72f, 0.22f), // sickly green
        RelicCategory.ComboEngine       => new Color(0.15f, 0.45f, 1.00f), // blue
        RelicCategory.ScalingMultiplier => new Color(0.65f, 0.15f, 1.00f), // purple
        RelicCategory.CardManipulation  => new Color(1.00f, 0.50f, 0.10f), // amber
        RelicCategory.RiskChallenge     => new Color(0.90f, 0.08f, 0.08f), // deep red
        _                               => Color.white
    };

    // ── Shape Helpers (called by RelicPrefabFactory) ──────────────────────────

    /// <summary>Returns the primitive type that best represents this relic category.</summary>
    public static PrimitiveType GetCategoryShape(RelicCategory cat) => cat switch
    {
        RelicCategory.RuleChanger       => PrimitiveType.Cube,
        RelicCategory.DecayManipulation => PrimitiveType.Sphere,
        RelicCategory.ComboEngine       => PrimitiveType.Cylinder,
        RelicCategory.ScalingMultiplier => PrimitiveType.Sphere,
        RelicCategory.CardManipulation  => PrimitiveType.Cube,
        RelicCategory.RiskChallenge     => PrimitiveType.Capsule,
        _                               => PrimitiveType.Cube
    };

    void OnDestroy()
    {
        if (_mat != null) Destroy(_mat);
    }
}
