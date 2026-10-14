using UnityEngine;

/// <summary>
/// A small pixelated flame burst, meant to play at the altar the moment
/// cards are confirmed/sacrificed. Fully procedural — no sprite asset
/// required — so it works immediately, but every visual parameter is
/// exposed so it can be art-directed from the Inspector.
///
/// Usage: put this on (or near) the altar, assign it to CardOfferingController
/// if you want it to auto-play on confirm, or call PlayBurst() from anywhere.
/// </summary>
public class AltarFlameEffect : MonoBehaviour
{
    [Header("Placement")]
    [Tooltip("Where the flame spawns from. Defaults to this object's position if left empty.")]
    [SerializeField] private Transform spawnPoint;

    [Header("Pixelation")]
    [Tooltip("Resolution of the generated flame texture. Lower = chunkier pixels. " +
             "6-8 gives a strongly blocky retro look; higher values smooth it out.")]
    [SerializeField] private int pixelTextureSize = 6;

    [Tooltip("Number of distinct alpha bands in the generated sprite. Lower (3-4) " +
             "reads as genuine flat-color pixel art; higher approaches a smooth gradient.")]
    [SerializeField] private int pixelAlphaBands = 3;

    [Header("Burst Shape")]
    [SerializeField] private int particleCount = 20;
    [SerializeField] private float burstDuration = 0.6f;
    [SerializeField] private float startSize = 0.09f;
    [SerializeField] private float endSizeMultiplier = 0.15f;
    [SerializeField] private float startSpeed = 1.2f;
    [SerializeField] private float upwardBias = 1.5f;
    [SerializeField] private float spreadRadius = 0.08f;

    [Header("Color (bottom of flame -> tip)")]
    [SerializeField] private Color coreColor = new Color(1f, 0.95f, 0.6f);   // near-white/yellow
    [SerializeField] private Color midColor = new Color(1f, 0.55f, 0.1f);   // orange
    [SerializeField] private Color tipColor = new Color(0.6f, 0.1f, 0.05f, 0f); // dark red, fades out

    private ParticleSystem cachedSystem;
    private static Sprite cachedPixelSprite;
    private static int cachedSpriteSize = -1;
    private static int cachedSpriteBands = -1;

    private void Awake()
    {
        BuildParticleSystem();
    }

    /// <summary>
    /// Plays one flame burst. Safe to call repeatedly (e.g. once per confirmed offer).
    /// </summary>
    public void PlayBurst()
    {
        if (cachedSystem == null)
            BuildParticleSystem();

        Transform origin = spawnPoint != null ? spawnPoint : transform;
        cachedSystem.transform.position = origin.position;
        cachedSystem.transform.rotation = origin.rotation;

        cachedSystem.Clear(true);
        cachedSystem.Play(true);
    }

    private void BuildParticleSystem()
    {
        if (cachedSystem != null)
            return;

        GameObject go = new GameObject("AltarFlameParticles");
        go.transform.SetParent(transform, false);

        cachedSystem = go.AddComponent<ParticleSystem>();

        // AddComponent<ParticleSystem> starts playing immediately by default
        // (playOnAwake = true), which caused it to fire on scene load and
        // also broke reconfiguring `main.duration` below. Stop it completely
        // before touching any settings.
        cachedSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = BuildUnlitSpriteMaterial();

        var main = cachedSystem.main;
        main.duration = burstDuration;
        main.loop = false;
        main.startLifetime = burstDuration;
        main.startSpeed = startSpeed;
        main.startSize = startSize;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.05f; // very slight upward drift once launched
        main.playOnAwake = false;
        main.stopAction = ParticleSystemStopAction.None;

        var emission = cachedSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)particleCount)
        });

        var shape = cachedSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = spreadRadius;

        var velocityOverLifetime = cachedSystem.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        // All three axes must share the same MinMaxCurve mode (TwoConstants
        // here) — mixing a two-constant Y with default constant X/Z throws
        // "Particle Velocity curves must all be in the same mode".
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(upwardBias * 0.5f, upwardBias);
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var sizeOverLifetime = cachedSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.6f, 0.8f),
            new Keyframe(1f, endSizeMultiplier)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = cachedSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(coreColor, 0f),
                new GradientColorKey(midColor, 0.45f),
                new GradientColorKey(tipColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.85f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        var noise = cachedSystem.noise;
        noise.enabled = true;
        noise.strength = 0.15f;
        noise.frequency = 1.2f;
        noise.scrollSpeed = 1.5f;

        var rotationOverLifetime = cachedSystem.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-90f, 90f);
    }

    private Material BuildUnlitSpriteMaterial()
    {
        Sprite sprite = GetOrBuildPixelSprite();

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material material = new Material(shader);

        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", sprite.texture);
        else if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", sprite.texture);

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f); // Transparent

        material.EnableKeyword("_ALPHABLEND_ON");
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        return material;
    }

    /// <summary>
    /// Generates a small, blocky radial-gradient sprite at runtime so this
    /// effect needs zero imported art. Cached across instances since the
    /// pixel size rarely changes at runtime.
    /// </summary>
    private Sprite GetOrBuildPixelSprite()
    {
        if (cachedPixelSprite != null &&
            cachedSpriteSize == pixelTextureSize &&
            cachedSpriteBands == pixelAlphaBands)
        {
            return cachedPixelSprite;
        }

        int size = Mathf.Max(4, pixelTextureSize);
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point; // chunky pixels, no smoothing
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxDist = center.magnitude;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                float alpha = Mathf.Clamp01(1f - dist);
                alpha = alpha * alpha; // sharper falloff, reads as a blocky blob at low res

                // Quantize into discrete bands so it reads as flat-color
                // pixel art rather than a smooth gradient, even though each
                // texel technically supports a continuous alpha value.
                int bands = Mathf.Max(1, pixelAlphaBands);
                alpha = Mathf.Round(alpha * bands) / bands;

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, false);

        cachedPixelSprite = Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            size // pixels per unit == size keeps the sprite ~1 world unit
        );
        cachedSpriteSize = pixelTextureSize;
        cachedSpriteBands = pixelAlphaBands;

        return cachedPixelSprite;
    }
}