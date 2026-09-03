using UnityEngine;

/// <summary>
/// Generates a dark worn wood texture procedurally, matching the
/// dark tavern table aesthetic from the reference image.
/// Call WoodTextureGenerator.Generate() to get a Texture2D.
/// </summary>
public static class WoodTextureGenerator
{
    public static Texture2D Generate(
        int   width      = 512,
        int   height     = 1024,
        int   seed       = 42,
        float grainScale = 0.015f,  // how fine the wood grain is
        float ringScale  = 0.008f,  // how many growth rings
        float warpAmount = 0.35f)   // how wavy the grain is
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Repeat;

        var rng   = new System.Random(seed);
        float offX = (float)(rng.NextDouble() * 100);
        float offY = (float)(rng.NextDouble() * 100);

        // Dark worn wood palette — matches reference image
        Color darkBase    = new Color(0.08f, 0.04f, 0.02f); // very dark brown
        Color midGrain    = new Color(0.18f, 0.09f, 0.04f); // medium brown grain
        Color lightGrain  = new Color(0.28f, 0.14f, 0.06f); // lighter grain highlight
        Color knot        = new Color(0.04f, 0.02f, 0.01f); // dark knot color
        Color wornLight   = new Color(0.32f, 0.18f, 0.08f); // worn/polished areas

        var pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float u = (float)x / width;
                float v = (float)y / height;

                // ── Wood grain warp ──────────────────────────────────────
                // Warp the coordinates to create wavy grain
                float warpX = Perlin(u * 2f + offX, v * 0.5f + offY) * warpAmount;
                float warpY = Perlin(u * 1.5f + offX + 5f, v * 0.3f + offY + 5f) * warpAmount * 0.5f;

                float wu = u + warpX * 0.15f;
                float wv = v + warpY * 0.10f;

                // ── Growth rings ─────────────────────────────────────────
                // Sinusoidal rings running along the grain direction
                float ring = Mathf.Sin((wu * ringScale * width + warpX * 8f) * Mathf.PI * 2f);
                ring = (ring + 1f) * 0.5f; // 0-1
                ring = Mathf.Pow(ring, 1.4f); // sharpen the rings

                // ── Fine grain lines ─────────────────────────────────────
                float grain1 = Perlin(wu * grainScale * width + offX,
                                      wv * grainScale * height * 0.1f + offY);
                float grain2 = Perlin(wu * grainScale * width * 2.5f + offX + 10f,
                                      wv * grainScale * height * 0.05f + offY + 10f);
                float grain  = grain1 * 0.65f + grain2 * 0.35f;

                // ── Worn/polished patches ────────────────────────────────
                float worn = Perlin(wu * 0.8f + offX + 20f, wv * 0.4f + offY + 20f);
                worn = Mathf.Pow(Mathf.Clamp01(worn - 0.55f) * 2.5f, 2f);

                // ── Knot placement ───────────────────────────────────────
                // A few dark oval knots scattered across the wood
                float knotVal = 0f;
                for (int k = 0; k < 3; k++)
                {
                    float kx  = (float)(rng.NextDouble()) * 0.8f + 0.1f;
                    float ky  = (float)(rng.NextDouble()) * 0.8f + 0.1f;
                    float kdx = (u - kx) * 2.5f;
                    float kdy = (v - ky) * 1.0f;
                    float kd  = kdx * kdx + kdy * kdy;
                    knotVal   = Mathf.Max(knotVal, Mathf.Exp(-kd * 80f));
                }

                // ── Combine ──────────────────────────────────────────────
                Color col = Color.Lerp(darkBase, midGrain,  grain * 0.7f + ring * 0.3f);
                col       = Color.Lerp(col,      lightGrain, grain2 * 0.3f);
                col       = Color.Lerp(col,      wornLight,  worn * 0.4f);
                col       = Color.Lerp(col,      knot,       knotVal * 0.8f);

                // ── Subtle vertical scratches ────────────────────────────
                float scratch = Perlin(u * 80f + offX, v * 0.5f + offY);
                if (scratch > 0.82f)
                    col = Color.Lerp(col, darkBase, (scratch - 0.82f) * 5f * 0.4f);

                // ── Edge darkening (worn darker at edges) ────────────────
                float edgeDist = Mathf.Min(
                    Mathf.Min(u, 1f - u) * 4f,
                    Mathf.Min(v, 1f - v) * 4f);
                edgeDist = Mathf.Clamp01(edgeDist);
                col = Color.Lerp(darkBase * 0.6f, col, edgeDist);

                col.a = 1f;
                pixels[y * width + x] = col;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // Simple Perlin noise wrapper
    static float Perlin(float x, float y)
        => Mathf.PerlinNoise(x, y);
}
