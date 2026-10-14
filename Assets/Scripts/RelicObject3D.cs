using UnityEngine;

/// <summary>Visual effects only. Shop input and colliders live on the parent slot.</summary>
public class RelicObject3D : MonoBehaviour
{
    public RelicType relicType;
    public RelicCategory relicCategory;
    [Header("Visual Settings")]
    public float rotationSpeed = 22f;
    public float bobAmplitude = 0.012f;
    public float bobSpeed = 1.1f;
    [Header("Emission")]
    public float baseEmissionIntensity = 1.8f;
    public float pulseMinIntensity = 0.8f;
    public float pulseMaxIntensity = 3.2f;
    public float pulseSpeed = 1.4f;
    [Header("Particles")]
    public ParticleSystem smokeParticles, sparkParticles, shimmerParticles;
    private Material material;
    private Color categoryColor;
    private Vector3 restPosition;
    private float phase;
    private bool initialized, sold;

    public void Initialize(Relic relic)
    {
        if (relic == null) return;
        relicType = relic.type;
        relicCategory = relic.category;
        categoryColor = GetCategoryColor(relicCategory);
        phase = Random.Range(0f, Mathf.PI * 2f);
        CaptureRestPose();
        if (material != null) Destroy(material);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader != null)
        {
            material = new Material(shader);
            material.SetFloat("_Smoothness", 0.3f);
            material.SetFloat("_Metallic", 0.1f);
            material.EnableKeyword("_EMISSION");
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer) continue;
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
            }
        }
        SetParticles(smokeParticles, relicCategory == RelicCategory.DecayManipulation,
            new Color(0.15f, 0.35f, 0.05f, 0.5f));
        SetParticles(sparkParticles, relicCategory == RelicCategory.RiskChallenge ||
            relicCategory == RelicCategory.ComboEngine, categoryColor);
        SetParticles(shimmerParticles, relicCategory == RelicCategory.CardManipulation ||
            relicCategory == RelicCategory.RuleChanger, categoryColor * 0.6f);
        initialized = true;
        SetSold(false);
    }
    // Start runs after the factory's caller finishes positioning the object.
    void Start() { CaptureRestPose(); }
    public void CaptureRestPose() { restPosition = transform.localPosition; }
    public void SetSold(bool value)
    {
        sold = value;
        if (material == null) return;
        Color surface = value ? Color.gray : categoryColor * 0.7f;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", surface);
        if (material.HasProperty("_Color")) material.SetColor("_Color", surface);
        material.SetColor("_EmissionColor", value ? Color.black : categoryColor * baseEmissionIntensity);
    }
    void Update()
    {
        if (!initialized) return;
        phase += Time.deltaTime;
        if (!sold) transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.Self);
        transform.localPosition = restPosition + Vector3.up * (sold ? 0f : Mathf.Sin(phase * bobSpeed) * bobAmplitude);
        if (material != null && !sold)
            material.SetColor("_EmissionColor", categoryColor * GetEmissionMult(phase));
    }
    void OnDisable() { if (initialized) transform.localPosition = restPosition; }
    void OnDestroy() { if (material != null) Destroy(material); }
    static void SetParticles(ParticleSystem particles, bool enabled, Color color)
    {
        if (particles == null) return;
        particles.gameObject.SetActive(enabled);
        if (enabled) { var main = particles.main; main.startColor = color; particles.Play(); }
    }
    float GetEmissionMult(float t)
    {
        switch (relicCategory)
        {
            case RelicCategory.ScalingMultiplier:
                return Mathf.Lerp(pulseMinIntensity, pulseMaxIntensity, (Mathf.Sin(t * pulseSpeed) + 1f) * 0.5f);
            case RelicCategory.RiskChallenge:
                return Mathf.Lerp(pulseMinIntensity, pulseMaxIntensity, Mathf.PerlinNoise(t * 6f, 0f));
            case RelicCategory.ComboEngine:
                return Mathf.Lerp(baseEmissionIntensity * 0.7f, pulseMaxIntensity, (Mathf.Sin(t * pulseSpeed * 1.5f) + 1f) * 0.5f);
            case RelicCategory.DecayManipulation:
                return Mathf.Lerp(pulseMinIntensity * 0.6f, baseEmissionIntensity, (Mathf.Sin(t * pulseSpeed * 0.5f) + 1f) * 0.5f);
            case RelicCategory.CardManipulation:
                return Mathf.Lerp(baseEmissionIntensity * 0.8f, baseEmissionIntensity * 1.4f, Mathf.PerlinNoise(t * 2f, 99f));
            default:
                return Mathf.Lerp(baseEmissionIntensity * 0.9f, baseEmissionIntensity * 1.2f, (Mathf.Sin(t * 0.7f) + 1f) * 0.5f);
        }
    }
    public static Color GetCategoryColor(RelicCategory category) => category switch
    {
        RelicCategory.RuleChanger => new Color(1f, 0.78f, 0.1f),
        RelicCategory.DecayManipulation => new Color(0.18f, 0.72f, 0.22f),
        RelicCategory.ComboEngine => new Color(0.15f, 0.45f, 1f),
        RelicCategory.ScalingMultiplier => new Color(0.65f, 0.15f, 1f),
        RelicCategory.CardManipulation => new Color(1f, 0.5f, 0.1f),
        RelicCategory.RiskChallenge => new Color(0.9f, 0.08f, 0.08f),
        _ => Color.white
    };
    public static PrimitiveType GetCategoryShape(RelicCategory category) => category switch
    {
        RelicCategory.RuleChanger => PrimitiveType.Cube,
        RelicCategory.DecayManipulation => PrimitiveType.Sphere,
        RelicCategory.ComboEngine => PrimitiveType.Cylinder,
        RelicCategory.ScalingMultiplier => PrimitiveType.Sphere,
        RelicCategory.CardManipulation => PrimitiveType.Cube,
        RelicCategory.RiskChallenge => PrimitiveType.Capsule,
        _ => PrimitiveType.Cube
    };
}
