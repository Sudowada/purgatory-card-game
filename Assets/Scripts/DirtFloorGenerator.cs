using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// DIRT FLOOR GENERATOR
/// =====================
/// Attach this to a GameObject in your DungeonRoom, or call
/// DirtFloorGenerator.GenerateFloor() from DungeonSceneBuilder.
///
/// Creates a subdivided plane mesh with:
///   - Random vertex height displacement (bumpy, uneven ground)
///   - Per-vertex color variation (dark dirt, dry patches, damp spots)
///
/// HOW TO USE:
///   Option A (Runtime):  Add this component to any GameObject. It runs on Awake.
///   Option B (Editor):   Select the GameObject → Inspector → "Generate Floor" button.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class DirtFloorGenerator : MonoBehaviour
{
    [Header("Grid Resolution")]
    [Tooltip("More divisions = more detail but higher poly count. 20–40 is ideal.")]
    public int subdivisions = 30;

    [Header("Room Size")]
    public float width = 10f;
    public float depth = 10f;

    [Header("Height Noise")]
    [Tooltip("Maximum vertex vertical displacement in metres.")]
    [Range(0f, 0.15f)]
    public float maxBump = 0.06f;
    [Tooltip("Scale of the Perlin noise. Higher = more frequent bumps.")]
    public float noiseScale = 3.5f;
    public int   randomSeed = 42;

    [Header("Vertex Colors")]
    public Color baseDirt    = new Color(0.28f, 0.21f, 0.13f);
    public Color darkDamp    = new Color(0.15f, 0.11f, 0.08f);
    public Color dryPatch    = new Color(0.42f, 0.33f, 0.22f);
    [Range(0f, 1f)]
    public float colorNoise  = 0.55f;

    // ── Called at runtime ───────────────────────────────────────────────
    private void Awake()
    {
        GenerateFloor();
    }

    // ════════════════════════════════════════════════════════════════════
    //  CORE GENERATION
    // ════════════════════════════════════════════════════════════════════
    public void GenerateFloor()
    {
        Random.InitState(randomSeed);

        int vertsX = subdivisions + 1;
        int vertsZ = subdivisions + 1;
        int totalVerts = vertsX * vertsZ;

        Vector3[] vertices  = new Vector3[totalVerts];
        Vector2[] uvs       = new Vector2[totalVerts];
        Color[]   colors    = new Color[totalVerts];
        int[]     triangles = new int[subdivisions * subdivisions * 6];

        float stepX = width  / subdivisions;
        float stepZ = depth  / subdivisions;

        // Unique noise offset so each generation looks different
        float offsetX = Random.Range(0f, 1000f);
        float offsetZ = Random.Range(0f, 1000f);

        // ── Vertices, UVs, Colors ───────────────────────────────────────
        for (int z = 0; z < vertsZ; z++)
        {
            for (int x = 0; x < vertsX; x++)
            {
                int idx = z * vertsX + x;

                float wx = (x * stepX) - width  / 2f;
                float wz = (z * stepZ) - depth  / 2f;

                // Height displacement via layered Perlin noise
                float nx = (x / (float)subdivisions) * noiseScale + offsetX;
                float nz = (z / (float)subdivisions) * noiseScale + offsetZ;
                float h  = Mathf.PerlinNoise(nx, nz)
                         + 0.4f * Mathf.PerlinNoise(nx * 2.1f, nz * 2.1f)
                         + 0.2f * Mathf.PerlinNoise(nx * 4.3f, nz * 4.3f);
                h /= 1.6f; // normalise back to ~0-1
                float y  = (h - 0.5f) * maxBump * 2f;

                vertices[idx] = new Vector3(wx, y, wz);
                uvs[idx]      = new Vector2(x / (float)subdivisions,
                                            z / (float)subdivisions);

                // Vertex color — blend between base, dark damp, and dry patches
                float cn = Mathf.PerlinNoise(
                    nx * colorNoise + 500f,
                    nz * colorNoise + 500f);

                Color c;
                if (cn < 0.35f)
                    c = Color.Lerp(baseDirt, darkDamp, (0.35f - cn) / 0.35f);
                else if (cn > 0.70f)
                    c = Color.Lerp(baseDirt, dryPatch, (cn - 0.70f) / 0.30f);
                else
                    c = baseDirt;

                // Extra random micro-variation per vertex
                float jitter = Random.Range(-0.05f, 0.05f);
                c = new Color(
                    Mathf.Clamp01(c.r + jitter),
                    Mathf.Clamp01(c.g + jitter * 0.8f),
                    Mathf.Clamp01(c.b + jitter * 0.6f));

                colors[idx] = c;
            }
        }

        // ── Triangles ───────────────────────────────────────────────────
        int triIdx = 0;
        for (int z = 0; z < subdivisions; z++)
        {
            for (int x = 0; x < subdivisions; x++)
            {
                int bl = z  * vertsX + x;
                int br = bl + 1;
                int tl = bl + vertsX;
                int tr = tl + 1;

                // Triangle 1
                triangles[triIdx++] = bl;
                triangles[triIdx++] = tl;
                triangles[triIdx++] = br;

                // Triangle 2
                triangles[triIdx++] = br;
                triangles[triIdx++] = tl;
                triangles[triIdx++] = tr;
            }
        }

        // ── Build mesh ──────────────────────────────────────────────────
        Mesh mesh = new Mesh();
        mesh.name = "DirtFloor";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // supports >65k verts

        mesh.vertices  = vertices;
        mesh.uv        = uvs;
        mesh.colors    = colors;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = mesh;

        // ── Material ────────────────────────────────────────────────────
        // Uses vertex colors — assign a material with a vertex-color shader.
        // The included "Particles/Standard Unlit" or a custom shader works.
        // For URP:  use "Universal Render Pipeline/Particles/Unlit"
        // For HDRP: use "HDRP/Unlit" with vertex color mode enabled
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr.sharedMaterial == null || mr.sharedMaterial.name == "Default-Material")
        {
            // Try to find a vertex color compatible shader automatically
            Shader vcShader = Shader.Find("Particles/Standard Unlit")
                           ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                           ?? Shader.Find("Standard");

            if (vcShader != null)
            {
                Material mat = new Material(vcShader);
                mat.name = "DirtFloor_VertexColor";
                mr.sharedMaterial = mat;
            }
            else
            {
                Debug.LogWarning("[DirtFloorGenerator] Could not auto-find a vertex color shader. " +
                                 "Please assign a material that reads vertex colors in the MeshRenderer.");
            }
        }

        Debug.Log($"[DirtFloorGenerator] Generated {totalVerts} vertices, " +
                  $"{triangles.Length / 3} triangles.");
    }

#if UNITY_EDITOR
    // ── Editor button in Inspector ──────────────────────────────────────
    [CustomEditor(typeof(DirtFloorGenerator))]
    public class DirtFloorGeneratorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            GUILayout.Space(8);
            if (GUILayout.Button("🌱  Generate Floor", GUILayout.Height(30)))
            {
                ((DirtFloorGenerator)target).GenerateFloor();
                EditorUtility.SetDirty(target);
            }
        }
    }
#endif
}
