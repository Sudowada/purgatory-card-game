using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class LightPuzzlePlaytestScene
{
    private const string Props = "Assets/Models/modular_electric_cables_1k.blend/";
    private const string FontPath = "Assets/Resources/Fonts/IMFellEnglish-Regular SDF.asset";

    [MenuItem("Tools/Purgatory/Add Two-Rocker Puzzle at Selected Wall Anchor")]
    private static void AddToCurrentScene()
    {
        var anchor = Selection.activeTransform;
        if (anchor == null || !anchor.gameObject.scene.IsValid())
        {
            EditorUtility.DisplayDialog("Lighting Puzzle", "Select a wall anchor in the current scene.", "OK");
            return;
        }

        var controller = Object.FindObjectOfType<FirstPersonController>();
        var camera = controller != null && controller.cameraTransform != null
            ? controller.cameraTransform.GetComponent<Camera>()
            : Camera.main;
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (camera == null || font == null)
        {
            EditorUtility.DisplayDialog("Lighting Puzzle", "The current scene needs a first-person camera and the project's TMP font.", "OK");
            return;
        }

        var circuit = CreateRockers(anchor, Vector3.zero, font, camera, controller);
        var clue = CreateWallClue(anchor, new Vector3(0.8f, 0.1f, -0.1f), font, camera, controller);
        WireClue(clue, circuit);
        clue.puzzleId = "cell_wall_keyhole_" + GUID.Generate();
        Undo.RegisterCreatedObjectUndo(circuit.gameObject, "Add Two-Rocker Panel");
        Undo.RegisterCreatedObjectUndo(clue.gameObject, "Add Wall Lighting Clue");
        EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene);
        Selection.activeGameObject = clue.gameObject;
    }

    [MenuItem("Tools/Purgatory/Open Lighting Puzzle Playtest")]
    private static void Open()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null || font.material == null)
        {
            EditorUtility.DisplayDialog("Lighting Puzzle", "The existing IMFellEnglish TMP font asset could not be loaded.", "OK");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var room = new GameObject("Playtest Cell").transform;
        var wall = AssetDatabase.LoadAssetAtPath<Material>(Props + "CellWall.mat");
        var floor = AssetDatabase.LoadAssetAtPath<Material>(Props + "ConcreteFloor.mat");

        Shape("Floor", room, new Vector3(0f, -0.1f, 1.5f), new Vector3(6f, 0.2f, 7f), floor);
        Shape("Clue Wall", room, new Vector3(0f, 1.5f, 4.1f), new Vector3(6f, 3f, 0.2f), wall);
        Shape("Back Wall", room, new Vector3(0f, 1.5f, -2.1f), new Vector3(6f, 3f, 0.2f), wall);
        Shape("Left Wall", room, new Vector3(-3f, 1.5f, 1f), new Vector3(0.2f, 3f, 6f), wall);
        Shape("Right Wall", room, new Vector3(3f, 1.5f, 1f), new Vector3(0.2f, 3f, 6f), wall);
        var ambient = new GameObject("Room Fill").AddComponent<Light>();
        ambient.type = LightType.Directional;
        ambient.intensity = 0.65f;
        ambient.transform.rotation = Quaternion.Euler(30f, -25f, 0f);

        var player = new GameObject("Player");
        player.transform.position = new Vector3(-0.85f, 0.02f, 2.15f);
        var controller = player.AddComponent<FirstPersonController>();
        player.GetComponent<CharacterController>().center = Vector3.up;
        player.AddComponent<LightPuzzlePlaytestControls>();
        var camera = new GameObject("FP Camera").AddComponent<Camera>();
        camera.gameObject.AddComponent<AudioListener>();
        camera.tag = "MainCamera";
        camera.gameObject.AddComponent<UniversalAdditionalCameraData>().SetRenderer(2);
        camera.transform.SetParent(player.transform, false);
        camera.transform.localPosition = Vector3.up * 1.55f;
        controller.cameraTransform = camera.transform;

        var circuit = CreateRockers(room, new Vector3(-0.85f, 1.45f, 3.995f), font, camera, controller);
        circuit.roomLight.transform.position = new Vector3(0.4f, 1.9f, 3.2f);
        Text("Wall Hint", room, new Vector3(-0.85f, 1.74f, 3.97f), font, new Vector2(0.65f, 0.18f),
            "VIOLET INK\nTHE STONE REMEMBERS", new Color(0.7f, 0.64f, 0.67f));

        var clue = CreateWallClue(room, new Vector3(1.05f, 1.6f, 3.9f), font, camera, controller);
        clue.puzzleId = "two_rocker_playtest_wall_keyhole";
        WireClue(clue, circuit);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = clue.gameObject;
    }

    private static void WireClue(LightConditionClue clue, TwoRockerLightCircuit circuit)
    {
        clue.requiredLights = System.Array.Empty<Light>();
        clue.forbiddenLights = System.Array.Empty<Light>();
        clue.requiredColors = new[]
        {
            new LightConditionClue.LightColorCondition
            {
                light = circuit.roomLight, color = circuit.colorRocker.binaryDownColor
            }
        };
    }

    private static LightConditionClue CreateWallClue(Transform parent, Vector3 position,
        TMP_FontAsset font, Camera camera, FirstPersonController controller)
    {
        var metal = AssetDatabase.LoadAssetAtPath<Material>(Props + "ClockFrontRIm.mat");
        var dark = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_CardEdge.mat");
        var clueRoot = new GameObject("Lighting Clue");
        clueRoot.SetActive(false);
        clueRoot.transform.SetParent(parent, false);
        clueRoot.transform.localPosition = position;

        var writing = Text("Wall Writing", clueRoot.transform, Vector3.forward * 0.05f, font, new Vector2(1.6f, 0.4f),
            "WHAT IS HIDDEN\nMAY STILL BE OPENED", new Color(0.85f, 0.55f, 1f));
        var writingTarget = writing.gameObject.AddComponent<BoxCollider>();
        writingTarget.size = new Vector3(1.65f, 0.46f, 0.04f);
        var glow = new GameObject("Writing Glow").AddComponent<Light>();
        glow.transform.SetParent(writing.transform, false);
        glow.transform.localPosition = Vector3.back * 0.24f;
        glow.type = LightType.Point;
        glow.color = writing.color;
        glow.range = 0.8f;
        glow.intensity = 0.35f;
        writing.gameObject.SetActive(false);

        var keyhole = new GameObject("Wall Keyhole");
        keyhole.transform.SetParent(clueRoot.transform, false);
        keyhole.transform.localPosition = new Vector3(0f, -0.32f, 0.05f);
        var keyholeTarget = keyhole.AddComponent<BoxCollider>();
        keyholeTarget.size = new Vector3(0.31f, 0.39f, 0.12f);
        PlatePart("Metal Plate", keyhole.transform, Vector3.zero, new Vector3(0.27f, 0.34f, 0.055f), metal);
        var circle = PlatePart("Keyway Head", keyhole.transform, new Vector3(0f, 0.045f, -0.036f),
            new Vector3(0.10f, 0.10f, 0.015f), dark, PrimitiveType.Sphere);
        circle.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        PlatePart("Keyway Slot", keyhole.transform, new Vector3(0f, -0.028f, -0.036f),
            new Vector3(0.052f, 0.11f, 0.015f), dark);

        var feedback = Text("Locked Feedback", keyhole.transform, new Vector3(0f, -0.29f, -0.06f), font,
            new Vector2(0.9f, 0.15f), "A key is needed.", Color.white).gameObject;
        feedback.SetActive(false);
        keyhole.SetActive(false);

        var clue = clueRoot.AddComponent<LightConditionClue>();
        clue.playerCamera = camera;
        clue.playerController = controller;
        clue.requiredLights = new Light[2];
        clue.forbiddenLights = new Light[1];
        clue.writing = writing;
        clue.writingTarget = writingTarget;
        clue.writingGlow = glow;
        clue.keyholeTarget = keyholeTarget;
        UnityEventTools.AddBoolPersistentListener(clue.onKeyholeClicked, feedback.SetActive, true);
        clueRoot.SetActive(true);
        return clue;
    }

    private static TwoRockerLightCircuit CreateRockers(Transform parent, Vector3 position,
        TMP_FontAsset font, Camera camera, FirstPersonController controller)
    {
        var metal = AssetDatabase.LoadAssetAtPath<Material>(Props + "ClockFrontRIm.mat");
        var dark = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_CardEdge.mat");
        var stone = AssetDatabase.LoadAssetAtPath<Material>(Props + "CellWall.mat");
        var root = new GameObject("Two-Rocker Light Panel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = position;
        Shape("Ceramic Wall Plate", root.transform, Vector3.back * 0.01f,
            new Vector3(0.30f, 0.20f, 0.02f), stone);

        foreach (float x in new[] { -0.132f, 0.132f })
        foreach (float y in new[] { -0.078f, 0.078f })
            PlatePart("Fastener", root.transform, new Vector3(x, y, -0.021f),
                new Vector3(0.016f, 0.016f, 0.008f), metal, PrimitiveType.Sphere);

        var color = MakeRocker("Color Rocker", -0.065f, root.transform, dark);
        color.switchType = SwitchType.Binary;
        color.binaryUpColor = new Color(0.12f, 0.35f, 1f);
        color.binaryDownColor = new Color(0.67f, 0.20f, 1f);
        var power = MakeRocker("Power Rocker", 0.065f, root.transform, dark);
        color.clickClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/SoundFX/xomxomski-walkman-tape-click-sound-effect-429843.mp3");
        power.clickClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/SoundFX/freesound_community-breaker-switch-45684.mp3");
        Color stamp = new Color(0.86f, 0.79f, 0.68f);
        Text("Blue Mark", root.transform, new Vector3(-0.065f, 0.081f, -0.021f), font,
            new Vector2(0.10f, 0.023f), "BLUE", Color.Lerp(color.binaryUpColor, Color.white, 0.45f));
        Text("Violet Mark", root.transform, new Vector3(-0.065f, -0.081f, -0.021f), font,
            new Vector2(0.10f, 0.023f), "VIOLET", Color.Lerp(color.binaryDownColor, Color.white, 0.45f));
        Text("On Mark", root.transform, new Vector3(0.065f, 0.081f, -0.021f), font,
            new Vector2(0.10f, 0.023f), "I", stamp);
        Text("Off Mark", root.transform, new Vector3(0.065f, -0.081f, -0.021f), font,
            new Vector2(0.10f, 0.023f), "O", stamp);

        var panel = root.AddComponent<SwitchPanelInteract>();
        panel.playerCamera = camera;
        panel.playerController = controller;
        panel.switches = new[] { color, power };
        var lamp = new GameObject("Blue or Violet Room Light").AddComponent<Light>();
        lamp.transform.SetParent(root.transform, false);
        lamp.transform.localPosition = new Vector3(0.65f, 0.45f, -0.75f);
        lamp.type = LightType.Point;
        lamp.range = 4f;
        lamp.intensity = 2.5f;
        lamp.color = color.binaryUpColor;
        lamp.enabled = false;

        var circuit = root.AddComponent<TwoRockerLightCircuit>();
        circuit.colorRocker = color;
        circuit.powerRocker = power;
        circuit.roomLight = lamp;
        return circuit;
    }

    private static PuzzleSwitch MakeRocker(string label, float x, Transform parent, Material material)
    {
        var part = new GameObject(label);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = new Vector3(x, 0f, -0.038f);
        part.AddComponent<BoxCollider>().size = new Vector3(0.12f, 0.15f, 0.04f);
        var toggle = part.AddComponent<PuzzleSwitch>();
        toggle.clickOnly = true;
        toggle.interactDistance = 2.5f;
        toggle.leverUpAngle = 12f;
        toggle.leverDownAngle = -12f;
        toggle.snapDuration = 0.1f;
        toggle.overshootAmount = 1.5f;
        toggle.settleDuration = 0.055f;
        toggle.audioSource = part.AddComponent<AudioSource>();
        toggle.audioSource.playOnAwake = false;
        toggle.audioSource.spatialBlend = 1f;
        toggle.audioSource.volume = 0.55f;

        var bracket = new GameObject("Hinge Axis").transform;
        bracket.SetParent(part.transform, false);
        bracket.localRotation = Quaternion.Euler(0f, 0f, -90f);
        var handle = new GameObject("Lever Handle").transform;
        handle.SetParent(bracket, false);
        PlatePart("Bakelite Paddle", handle, Vector3.zero,
            new Vector3(0.13f, 0.10f, 0.016f), material);
        toggle.leverHandle = handle;
        return toggle;
    }

    private static GameObject Shape(string name, Transform parent, Vector3 position, Vector3 scale,
        Material material, PrimitiveType type = PrimitiveType.Cube)
    {
        var shape = GameObject.CreatePrimitive(type);
        shape.name = name;
        shape.transform.SetParent(parent, false);
        shape.transform.localPosition = position;
        shape.transform.localScale = scale;
        if (material != null) shape.GetComponent<Renderer>().sharedMaterial = material;
        return shape;
    }

    private static GameObject PlatePart(string name, Transform parent, Vector3 position, Vector3 scale,
        Material material, PrimitiveType type = PrimitiveType.Cube)
    {
        var shape = Shape(name, parent, position, scale, material, type);
        Object.DestroyImmediate(shape.GetComponent<Collider>());
        return shape;
    }

    private static TextMeshPro Text(string name, Transform parent, Vector3 position, TMP_FontAsset font,
        Vector2 size, string content, Color color)
    {
        var text = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshPro>();
        text.transform.SetParent(parent, false);
        text.transform.localPosition = position;
        text.rectTransform.sizeDelta = size;
        text.font = font;
        text.enableAutoSizing = true;
        text.fontSizeMin = 0.1f;
        text.fontSizeMax = 36f;
        text.enableWordWrapping = false;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.text = content;
        return text;
    }
}
