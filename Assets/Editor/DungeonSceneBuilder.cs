using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// DUNGEON SCENE BUILDER - Editor Tool
/// =====================================
/// HOW TO USE:
///   1. Place this file inside a folder called "Editor" in your Assets directory
///      e.g. Assets/Editor/DungeonSceneBuilder.cs
///   2. In Unity top menu → Tools → Dungeon Scene Builder
///   3. Assign your model prefabs in the inspector window that opens
///   4. Click "Build Dungeon Scene"
///
/// ROOM LAYOUT (top-down):
///   - Room is 10m wide x 10m deep x 4m tall
///   - Table + stool + bucket in center
///   - Cell door on right wall (East)
///   - Broken wall opening on North wall → leads to shop cell
///   - Wall lamps + candles on walls
///   - Metal grate in NW corner
///   - Pipes along ceiling/upper walls
///   - Cobwebs in corners and ceiling
///   - Dirt floor with random vertex coloring (see DirtFloorGenerator.cs)
/// </summary>
public class DungeonSceneBuilder : EditorWindow
{
    // ── Model Prefab Slots ──────────────────────────────────────────────
    public GameObject wallLampPrefab;
    public GameObject barrelPrefab;
    public GameObject candlestickPrefab;
    public GameObject pipesPrefab;
    public GameObject securityCameraPrefab;
    public GameObject dryBranchesPrefab;
    public GameObject boltCuttersPrefab;
    public GameObject stoolPrefab;
    public GameObject bucketPrefab;

    // ── Room Dimensions ─────────────────────────────────────────────────
    private const float ROOM_WIDTH  = 10f;
    private const float ROOM_DEPTH  = 10f;
    private const float ROOM_HEIGHT = 4f;
    private const float HALF_W      = ROOM_WIDTH  / 2f;
    private const float HALF_D      = ROOM_DEPTH  / 2f;

    // ── Wall thickness for positioning ──────────────────────────────────
    private const float WALL_INSET  = 0.15f;

    [MenuItem("Tools/Dungeon Scene Builder")]
    public static void ShowWindow()
    {
        GetWindow<DungeonSceneBuilder>("Dungeon Scene Builder");
    }

    private void OnGUI()
    {
        GUILayout.Label("── Assign Your Model Prefabs ──", EditorStyles.boldLabel);
        GUILayout.Space(6);

        wallLampPrefab      = (GameObject)EditorGUILayout.ObjectField("Industrial Wall Lamp",  wallLampPrefab,      typeof(GameObject), false);
        barrelPrefab        = (GameObject)EditorGUILayout.ObjectField("Wooden Barrel",         barrelPrefab,        typeof(GameObject), false);
        candlestickPrefab   = (GameObject)EditorGUILayout.ObjectField("Wooden Candlestick",    candlestickPrefab,   typeof(GameObject), false);
        pipesPrefab         = (GameObject)EditorGUILayout.ObjectField("Modular Pipes",         pipesPrefab,         typeof(GameObject), false);
        securityCameraPrefab= (GameObject)EditorGUILayout.ObjectField("Security Camera",       securityCameraPrefab,typeof(GameObject), false);
        dryBranchesPrefab   = (GameObject)EditorGUILayout.ObjectField("Dry Branches (cobweb)", dryBranchesPrefab,   typeof(GameObject), false);
        boltCuttersPrefab   = (GameObject)EditorGUILayout.ObjectField("Bolt Cutters",          boltCuttersPrefab,   typeof(GameObject), false);
        stoolPrefab         = (GameObject)EditorGUILayout.ObjectField("Wooden Stool",          stoolPrefab,         typeof(GameObject), false);
        bucketPrefab        = (GameObject)EditorGUILayout.ObjectField("Wooden Bucket",         bucketPrefab,        typeof(GameObject), false);

        GUILayout.Space(12);
        EditorGUILayout.HelpBox(
            "Builds room geometry + places all props.\n" +
            "A parent GameObject 'DungeonRoom' will be created.\n" +
            "Run again to rebuild (old root is destroyed first).",
            MessageType.Info);

        GUILayout.Space(6);

        if (GUILayout.Button("🏗  Build Dungeon Scene", GUILayout.Height(36)))
        {
            BuildScene();
        }

        if (GUILayout.Button("🗑  Clear Scene", GUILayout.Height(28)))
        {
            ClearScene();
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  MAIN BUILD
    // ════════════════════════════════════════════════════════════════════
    private void BuildScene()
    {
        ClearScene();

        GameObject root = new GameObject("DungeonRoom");
        Undo.RegisterCreatedObjectUndo(root, "Build Dungeon Scene");

        BuildGeometry(root);
        PlaceProps(root);
        PlaceLighting(root);

        Selection.activeGameObject = root;
        SceneView.lastActiveSceneView?.FrameSelected();

        Debug.Log("[DungeonSceneBuilder] Scene built successfully!");
    }

    private void ClearScene()
    {
        GameObject old = GameObject.Find("DungeonRoom");
        if (old != null)
        {
            Undo.DestroyObjectImmediate(old);
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  GEOMETRY  (floor, ceiling, 4 walls, cell door opening, broken wall)
    // ════════════════════════════════════════════════════════════════════
    private void BuildGeometry(GameObject root)
    {
        // ── Floor ──────────────────────────────────────────────────────
        // The DirtFloorGenerator will handle vertex-colored floor mesh.
        // We create a placeholder here so the scene isn't empty if that
        // script isn't present.
        GameObject floor = CreateQuad("Floor", root,
            new Vector3(0, 0, 0),
            new Vector3(90, 0, 0),
            new Vector3(ROOM_WIDTH, ROOM_DEPTH, 1));
        SetMaterialColor(floor, new Color(0.28f, 0.22f, 0.14f)); // dirt brown

        // ── Ceiling ────────────────────────────────────────────────────
        GameObject ceiling = CreateQuad("Ceiling", root,
            new Vector3(0, ROOM_HEIGHT, 0),
            new Vector3(-90, 0, 0),
            new Vector3(ROOM_WIDTH, ROOM_DEPTH, 1));
        SetMaterialColor(ceiling, new Color(0.15f, 0.13f, 0.11f));

        // ── South Wall (behind player start) ──────────────────────────
        CreateWall("Wall_South", root,
            new Vector3(0, ROOM_HEIGHT / 2f, -HALF_D),
            new Vector3(0, 0, 0),
            new Vector3(ROOM_WIDTH, ROOM_HEIGHT, 0.3f));

        // ── North Wall (has broken opening → shop cell) ────────────────
        // Two panels flanking the opening (opening is 2m wide, centred at x=1)
        float openingX   = 1f;
        float openingW   = 2f;
        float leftW      = (HALF_W + openingX - openingW / 2f);
        float rightW     = (HALF_W - openingX - openingW / 2f);

        CreateWall("Wall_North_Left", root,
            new Vector3(-HALF_W + leftW / 2f, ROOM_HEIGHT / 2f, HALF_D),
            new Vector3(0, 180, 0),
            new Vector3(leftW, ROOM_HEIGHT, 0.3f));

        CreateWall("Wall_North_Right", root,
            new Vector3(openingX + openingW / 2f + rightW / 2f, ROOM_HEIGHT / 2f, HALF_D),
            new Vector3(0, 180, 0),
            new Vector3(rightW, ROOM_HEIGHT, 0.3f));

        // Rubble marker above the opening
        CreateWall("Wall_North_Top", root,
            new Vector3(openingX, ROOM_HEIGHT - 0.6f, HALF_D),
            new Vector3(0, 180, 0),
            new Vector3(openingW, 1.2f, 0.3f));

        // ── West Wall ──────────────────────────────────────────────────
        CreateWall("Wall_West", root,
            new Vector3(-HALF_W, ROOM_HEIGHT / 2f, 0),
            new Vector3(0, 90, 0),
            new Vector3(ROOM_DEPTH, ROOM_HEIGHT, 0.3f));

        // ── East Wall (has cell door opening) ──────────────────────────
        // Door opening 1.2m wide, 2.4m tall, flush right-centre
        float doorW = 1.2f;
        float doorH = 2.4f;
        float doorX = 0.5f; // offset from centre

        // Bottom-left panel
        CreateWall("Wall_East_Left", root,
            new Vector3(HALF_W, ROOM_HEIGHT / 2f, -doorX - doorW / 2f - (HALF_D - doorX - doorW / 2f) / 2f),
            new Vector3(0, -90, 0),
            new Vector3(HALF_D - doorX - doorW / 2f, ROOM_HEIGHT, 0.3f));

        // Bottom-right panel
        CreateWall("Wall_East_Right", root,
            new Vector3(HALF_W, ROOM_HEIGHT / 2f, doorX + doorW / 2f + (HALF_D - doorX - doorW / 2f) / 2f),
            new Vector3(0, -90, 0),
            new Vector3(HALF_D - doorX - doorW / 2f, ROOM_HEIGHT, 0.3f));

        // Panel above door
        CreateWall("Wall_East_Top", root,
            new Vector3(HALF_W, doorH + (ROOM_HEIGHT - doorH) / 2f, doorX),
            new Vector3(0, -90, 0),
            new Vector3(doorW, ROOM_HEIGHT - doorH, 0.3f));
    }

    // ════════════════════════════════════════════════════════════════════
    //  PROPS
    // ════════════════════════════════════════════════════════════════════
    private void PlaceProps(GameObject root)
    {
        // ── Centre table (primitive cube as stand-in if no table prefab) ─
        GameObject tableGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tableGO.name = "Table_Centre";
        tableGO.transform.SetParent(root.transform);
        tableGO.transform.localPosition = new Vector3(0, 0.45f, 0);
        tableGO.transform.localScale    = new Vector3(1.6f, 0.9f, 0.8f);
        SetMaterialColor(tableGO, new Color(0.25f, 0.18f, 0.12f));
        Undo.RegisterCreatedObjectUndo(tableGO, "Create Table");

        // ── Stool (left of table) ──────────────────────────────────────
        SpawnProp(stoolPrefab, "Stool", root,
            new Vector3(-1.2f, 0, 0.3f),
            Quaternion.Euler(0, 35f, 0));

        // ── Bucket (under table) ───────────────────────────────────────
        SpawnProp(bucketPrefab, "Bucket", root,
            new Vector3(0.5f, 0, 0.2f),
            Quaternion.Euler(0, 20f, 0));

        // ── Bolt cutters (on table surface) ───────────────────────────
        SpawnProp(boltCuttersPrefab, "BoltCutters_OnTable", root,
            new Vector3(-0.3f, 0.92f, 0.1f),
            Quaternion.Euler(0, -15f, 0));

        // ── Barrels (NW corner) ────────────────────────────────────────
        SpawnProp(barrelPrefab, "Barrel_NW_1", root,
            new Vector3(-HALF_W + 0.7f, 0, HALF_D - 0.7f),
            Quaternion.Euler(0, 0, 0));
        SpawnProp(barrelPrefab, "Barrel_NW_2", root,
            new Vector3(-HALF_W + 1.4f, 0, HALF_D - 0.5f),
            Quaternion.Euler(0, 40f, 0));

        // ── Metal grate (NW floor corner) ─────────────────────────────
        // Represented as a flat dark quad until you assign a grate prefab
        GameObject grate = CreateQuad("MetalGrate_NW", root,
            new Vector3(-HALF_W + 0.8f, 0.01f, HALF_D - 1.0f),
            new Vector3(90, 0, 0),
            new Vector3(1f, 1f, 1f));
        SetMaterialColor(grate, new Color(0.2f, 0.2f, 0.2f));

        // ── Wall lamps (South wall + West wall) ───────────────────────
        SpawnProp(wallLampPrefab, "WallLamp_South_L", root,
            new Vector3(-2.5f, 2.2f, -HALF_D + WALL_INSET),
            Quaternion.Euler(0, 0, 0));
        SpawnProp(wallLampPrefab, "WallLamp_South_R", root,
            new Vector3(2.5f, 2.2f, -HALF_D + WALL_INSET),
            Quaternion.Euler(0, 0, 0));
        SpawnProp(wallLampPrefab, "WallLamp_West", root,
            new Vector3(-HALF_W + WALL_INSET, 2.2f, -1.5f),
            Quaternion.Euler(0, 90, 0));

        // ── Candlesticks ───────────────────────────────────────────────
        SpawnProp(candlestickPrefab, "Candle_Table_1", root,
            new Vector3(0.4f, 0.92f, 0f),
            Quaternion.identity);
        SpawnProp(candlestickPrefab, "Candle_Table_2", root,
            new Vector3(-0.4f, 0.92f, -0.1f),
            Quaternion.identity);
        SpawnProp(candlestickPrefab, "Candle_Floor_SW", root,
            new Vector3(-HALF_W + 1f, 0f, -HALF_D + 1f),
            Quaternion.identity);
        SpawnProp(candlestickPrefab, "Candle_Floor_East", root,
            new Vector3(HALF_W - 1.5f, 0f, 1.0f),
            Quaternion.Euler(0, -30f, 0));

        // ── Modular pipes (upper West wall, run horizontal) ────────────
        SpawnProp(pipesPrefab, "Pipes_West_High", root,
            new Vector3(-HALF_W + WALL_INSET, 3.2f, 0),
            Quaternion.Euler(0, 90, 0));
        SpawnProp(pipesPrefab, "Pipes_South_High", root,
            new Vector3(0, 3.2f, -HALF_D + WALL_INSET),
            Quaternion.Euler(0, 0, 0));

        // ── Security camera (South wall, high up looking inward) ───────
        SpawnProp(securityCameraPrefab, "SecurityCam_South", root,
            new Vector3(3.5f, 3.4f, -HALF_D + WALL_INSET),
            Quaternion.Euler(30, 180, 0));

        // ── Dry branches as cobweb stand-ins ──────────────────────────
        // Four corners near ceiling
        SpawnProp(dryBranchesPrefab, "Cobweb_NW", root,
            new Vector3(-HALF_W + 0.4f, ROOM_HEIGHT - 0.3f, HALF_D - 0.4f),
            Quaternion.Euler(0, 45, 0));
        SpawnProp(dryBranchesPrefab, "Cobweb_NE", root,
            new Vector3(HALF_W - 0.4f, ROOM_HEIGHT - 0.3f, HALF_D - 0.4f),
            Quaternion.Euler(0, -45, 0));
        SpawnProp(dryBranchesPrefab, "Cobweb_SW", root,
            new Vector3(-HALF_W + 0.4f, ROOM_HEIGHT - 0.3f, -HALF_D + 0.4f),
            Quaternion.Euler(0, 135, 0));
        SpawnProp(dryBranchesPrefab, "Cobweb_Ceiling_Mid", root,
            new Vector3(1.5f, ROOM_HEIGHT - 0.1f, 1f),
            Quaternion.Euler(180, 0, 0));

        // ── Cell door frame on East wall ───────────────────────────────
        // Three thin cubes forming a door frame
        CreateDoorFrame(root, new Vector3(HALF_W, 0, 0.5f));
    }

    // ════════════════════════════════════════════════════════════════════
    //  LIGHTING
    // ════════════════════════════════════════════════════════════════════
    private void PlaceLighting(GameObject root)
    {
        // Ambient point light – dim orange-yellow, centre of room
        CreatePointLight("Light_Centre_Ambient", root,
            new Vector3(0, 2.5f, 0),
            new Color(1f, 0.6f, 0.2f), intensity: 1.2f, range: 12f);

        // Lamp lights on south wall
        CreatePointLight("Light_WallLamp_South_L", root,
            new Vector3(-2.5f, 2.0f, -HALF_D + 0.5f),
            new Color(1f, 0.55f, 0.15f), intensity: 0.8f, range: 5f);
        CreatePointLight("Light_WallLamp_South_R", root,
            new Vector3(2.5f, 2.0f, -HALF_D + 0.5f),
            new Color(1f, 0.55f, 0.15f), intensity: 0.8f, range: 5f);

        // Candle flickery lights (just point lights — hook up a flicker script later)
        CreatePointLight("Light_Candle_Table", root,
            new Vector3(0, 1.1f, 0),
            new Color(1f, 0.75f, 0.3f), intensity: 0.6f, range: 3f);
        CreatePointLight("Light_Candle_Floor", root,
            new Vector3(-HALF_W + 1f, 0.3f, -HALF_D + 1f),
            new Color(1f, 0.65f, 0.25f), intensity: 0.4f, range: 2f);
    }

    // ════════════════════════════════════════════════════════════════════
    //  HELPERS
    // ════════════════════════════════════════════════════════════════════

    /// Instantiates a prefab if assigned, otherwise logs a warning.
    private void SpawnProp(GameObject prefab, string name, GameObject parent,
                           Vector3 localPos, Quaternion localRot)
    {
        GameObject go;
        if (prefab != null)
        {
            go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
            go.name = name;
        }
        else
        {
            // Placeholder cube so scene isn't empty
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name + "_PLACEHOLDER";
            go.transform.SetParent(parent.transform);
            go.transform.localScale = Vector3.one * 0.4f;
            SetMaterialColor(go, new Color(0.8f, 0.3f, 0.8f)); // magenta = missing prefab
            Debug.LogWarning($"[DungeonSceneBuilder] No prefab assigned for '{name}'. Placed magenta placeholder.");
        }

        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        Undo.RegisterCreatedObjectUndo(go, "Spawn " + name);
    }

    private GameObject CreateWall(string name, GameObject parent,
                                  Vector3 localPos, Vector3 eulerRot, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent.transform);
        wall.transform.localPosition = localPos;
        wall.transform.localRotation = Quaternion.Euler(eulerRot);
        wall.transform.localScale    = scale;
        SetMaterialColor(wall, new Color(0.22f, 0.19f, 0.16f)); // dark stone
        Undo.RegisterCreatedObjectUndo(wall, "Create " + name);
        return wall;
    }

    private GameObject CreateQuad(string name, GameObject parent,
                                  Vector3 localPos, Vector3 eulerRot, Vector3 scale)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        quad.transform.SetParent(parent.transform);
        quad.transform.localPosition = localPos;
        quad.transform.localRotation = Quaternion.Euler(eulerRot);
        quad.transform.localScale    = scale;
        Undo.RegisterCreatedObjectUndo(quad, "Create " + name);
        return quad;
    }

    /// Builds a simple door frame from 3 cubes (left post, right post, top lintel).
    private void CreateDoorFrame(GameObject parent, Vector3 origin)
    {
        float doorW  = 1.2f;
        float doorH  = 2.4f;
        float postW  = 0.12f;
        float depth  = 0.3f;
        Color stone  = new Color(0.22f, 0.19f, 0.16f);

        // Left post
        var lp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lp.name = "CellDoor_Frame_Left";
        lp.transform.SetParent(parent.transform);
        lp.transform.localPosition = origin + new Vector3(0, doorH / 2f, -doorW / 2f - postW / 2f);
        lp.transform.localScale    = new Vector3(depth, doorH, postW);
        SetMaterialColor(lp, stone);
        Undo.RegisterCreatedObjectUndo(lp, "Door Frame");

        // Right post
        var rp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rp.name = "CellDoor_Frame_Right";
        rp.transform.SetParent(parent.transform);
        rp.transform.localPosition = origin + new Vector3(0, doorH / 2f, doorW / 2f + postW / 2f);
        rp.transform.localScale    = new Vector3(depth, doorH, postW);
        SetMaterialColor(rp, stone);
        Undo.RegisterCreatedObjectUndo(rp, "Door Frame");

        // Lintel (top bar)
        var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lintel.name = "CellDoor_Frame_Lintel";
        lintel.transform.SetParent(parent.transform);
        lintel.transform.localPosition = origin + new Vector3(0, doorH + postW / 2f, 0);
        lintel.transform.localScale    = new Vector3(depth, postW, doorW + postW * 2f);
        SetMaterialColor(lintel, stone);
        Undo.RegisterCreatedObjectUndo(lintel, "Door Frame");

        // Iron bar door (5 vertical bars) — replace with your cell door prefab
        for (int i = 0; i < 5; i++)
        {
            float z = -doorW / 2f + (doorW / 4f) * i;
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bar.name = $"CellDoor_Bar_{i}";
            bar.transform.SetParent(parent.transform);
            bar.transform.localPosition = origin + new Vector3(-0.02f, doorH / 2f, z);
            bar.transform.localScale    = new Vector3(0.04f, doorH / 2f, 0.04f);
            SetMaterialColor(bar, new Color(0.15f, 0.15f, 0.15f));
            Undo.RegisterCreatedObjectUndo(bar, "Cell Bar");
        }
    }

    private void CreatePointLight(string name, GameObject parent,
                                  Vector3 localPos, Color color,
                                  float intensity, float range)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform);
        go.transform.localPosition = localPos;
        Light l = go.AddComponent<Light>();
        l.type      = LightType.Point;
        l.color     = color;
        l.intensity = intensity;
        l.range     = range;
        l.shadows   = LightShadows.Soft;
        Undo.RegisterCreatedObjectUndo(go, "Create Light " + name);
    }

    private void SetMaterialColor(GameObject go, Color color)
    {
        Renderer r = go.GetComponent<Renderer>();
        if (r == null) return;
        // Create a new material instance so we don't modify shared defaults
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        r.sharedMaterial = mat;
    }
}
