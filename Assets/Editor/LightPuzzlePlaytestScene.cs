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

    [MenuItem("Tools/Purgatory/Add Lighting Clue at Selected Wall Anchor")]
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

        var clue = CreateWallClue(anchor, Vector3.zero, font, camera, controller);
        clue.puzzleId = "cell_wall_keyhole_" + GUID.Generate();
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
        var metal = AssetDatabase.LoadAssetAtPath<Material>(Props + "ClockFrontRIm.mat");
        var dark = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_CardEdge.mat");

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
        player.transform.position = new Vector3(-1.4f, 0.02f, 1.5f);
        var controller = player.AddComponent<FirstPersonController>();
        player.GetComponent<CharacterController>().center = Vector3.up;
        player.AddComponent<LightPuzzlePlaytestControls>();
        var camera = new GameObject("FP Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.gameObject.AddComponent<UniversalAdditionalCameraData>().SetRenderer(2);
        camera.transform.SetParent(player.transform, false);
        camera.transform.localPosition = Vector3.up * 1.55f;
        controller.cameraTransform = camera.transform;

        var panel = new GameObject("Electrical Switch Box").AddComponent<SwitchPanelInteract>();
        panel.transform.position = new Vector3(-1.4f, 1.55f, 3.9f);
        Shape("Housing", panel.transform, Vector3.zero, new Vector3(1.3f, 0.55f, 0.15f), metal);
        var zoom = new GameObject("Zoom Target").transform;
        zoom.SetParent(panel.transform, false);
        zoom.localPosition = new Vector3(0f, 0f, -1.35f);
        panel.zoomTarget = zoom;
        panel.playerCamera = camera;
        panel.playerController = controller;
        panel.useWorldInteraction = true;

        var red = MakeSwitch("Red", new Color(1f, 0.12f, 0.18f), -0.42f, panel, font, dark);
        var blue = MakeSwitch("Blue", new Color(0.15f, 0.35f, 1f), 0f, panel, font, dark);
        var green = MakeSwitch("Green", new Color(0.18f, 1f, 0.25f), 0.42f, panel, font, dark);
        panel.switches = new[] { red, blue, green };

        Text("Instructions", room, new Vector3(-1.4f, 2.04f, 3.97f), font, new Vector2(1.6f, 0.32f),
            "Aim at a switch. Click to flip or drag up/down.\nRed and blue together. Leave green dark.", Color.white);

        var clue = CreateWallClue(room, new Vector3(1.05f, 1.6f, 3.9f), font, camera, controller);
        clue.puzzleId = "light_playtest_wall_keyhole";
        clue.requiredLights = new[] { red.leverLight, blue.leverLight };
        clue.forbiddenLights = new[] { green.leverLight };
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = clue.gameObject;
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

    private static PuzzleSwitch MakeSwitch(string label, Color color, float x,
        SwitchPanelInteract panel, TMP_FontAsset font, Material metal)
    {
        var part = new GameObject(label + " Switch");
        part.transform.SetParent(panel.transform, false);
        part.transform.localPosition = new Vector3(x, 0f, -0.10f);
        Shape("Base", part.transform, Vector3.zero, new Vector3(0.25f, 0.21f, 0.12f), metal);
        var toggle = part.AddComponent<PuzzleSwitch>();
        toggle.playerCamera = panel.playerCamera;
        toggle.leverOnColor = color;
        toggle.leverUpAngle = 315f;
        toggle.leverDownAngle = 405f;

        var bracket = new GameObject("Hinge Axis").transform;
        bracket.SetParent(part.transform, false);
        bracket.localPosition = new Vector3(0f, 0f, -0.08f);
        bracket.localRotation = Quaternion.Euler(0f, 0f, 90f);
        var handle = new GameObject("Lever Handle").transform;
        handle.SetParent(bracket, false);
        Shape("Lever", handle, new Vector3(0f, 0f, -0.065f),
            new Vector3(0.06f, 0.085f, 0.15f), metal);
        toggle.leverHandle = handle;

        var lamp = new GameObject(label + " Circuit").AddComponent<Light>();
        lamp.transform.SetParent(panel.transform, false);
        lamp.transform.position = new Vector3(1.05f + x, 1.8f, 3.2f);
        lamp.type = LightType.Point;
        lamp.color = color;
        lamp.range = 3f;
        lamp.intensity = 0f;
        toggle.leverLight = lamp;

        Text(label + " Label", panel.transform, new Vector3(x, -0.18f, -0.09f),
            font, new Vector2(0.32f, 0.12f), label, color);
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
