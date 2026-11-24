using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PurgatoryMixedLightSetup : EditorWindow
{
    private readonly Transform[] models = new Transform[4];
    private readonly Light[] lights = new Light[4];
    private Camera playerCamera;
    private PuzzleDoor door;
    private Transform clueAnchor;

    private static readonly string[] Labels = { "1 Red", "2 Blue", "3 Green / Yellow", "4 Magenta / Orange" };
    private static readonly Color[] Primary =
    {
        new Color(0.86f, 0.078f, 0.235f), new Color(0.12f, 0.35f, 1f),
        new Color(0.50f, 1f, 0f), new Color(0.88f, 0.04f, 0.62f)
    };
    private static readonly Color[] Secondary =
    {
        Color.white, Color.white, new Color(1f, 0.92f, 0.016f), new Color(1f, 0.55f, 0f)
    };

    [MenuItem("Tools/Purgatory/Wire Lighting Puzzle")]
    private static void Open() => GetWindow<PurgatoryMixedLightSetup>("Lighting Puzzle");

    private void OnEnable()
    {
        var scene = SceneManager.GetActiveScene();
        playerCamera = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
            .FirstOrDefault(item => item.gameObject.scene == scene && item.name == "FPCamera");
        var doors = Object.FindObjectsByType<PuzzleDoor>(FindObjectsSortMode.None)
            .Where(item => item.gameObject.scene == scene).ToArray();
        if (doors.Length == 1 && (doors[0].doorTransform != null || doors[0].hiddenReward != null))
            door = doors[0];
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Assign the four switches already placed in the cell and one room Light for each fixture. The setup uses Light_Color as the selector and OnOff as power on double rockers.", MessageType.Info);
        playerCamera = (Camera)EditorGUILayout.ObjectField("Player Camera", playerCamera, typeof(Camera), true);
        door = (PuzzleDoor)EditorGUILayout.ObjectField("Reward Door (optional)", door, typeof(PuzzleDoor), true);
        clueAnchor = (Transform)EditorGUILayout.ObjectField("Wall Clue Anchor", clueAnchor, typeof(Transform), true);
        var existing = FindObjectsByType<LightSequencePanel>(FindObjectsSortMode.None)
            .FirstOrDefault(panel => panel.gameObject.scene == SceneManager.GetActiveScene());
        if (existing != null)
        {
            EditorGUILayout.HelpBox("The active scene already has a light sequence. You can add the red-and-blue clue to it.", MessageType.Info);
            if (GUILayout.Button("Apply First Puzzle Answer")) ApplyFirstAnswer(existing);
            if (GUILayout.Button("Add Puzzle Sounds")) AddPuzzleSounds(existing);
            if (GUILayout.Button("Add Wall Clue")) PurgatoryWallClueSetup.Add(existing, clueAnchor);
            return;
        }
        for (int i = 0; i < models.Length; i++)
        {
            EditorGUILayout.Space();
            models[i] = (Transform)EditorGUILayout.ObjectField(Labels[i], models[i], typeof(Transform), true);
            lights[i] = (Light)EditorGUILayout.ObjectField("Room Light", lights[i], typeof(Light), true);
        }
        if (GUILayout.Button("Wire Switches and Wall Clue")) Wire();
    }

    private void Wire()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!PurgatoryWallClueSetup.ValidateAnchor(clueAnchor)) return;
        if (FindObjectsByType<LightSequencePanel>(FindObjectsSortMode.None)
            .Any(panel => panel.gameObject.scene == scene))
        {
            EditorUtility.DisplayDialog("Light Sequence", "This scene already has a Light Sequence Panel. Edit that component in the Inspector.", "OK");
            return;
        }
        if (playerCamera == null || playerCamera.gameObject.scene != scene ||
            models.Any(model => model == null || model.gameObject.scene != scene) ||
            lights.Any(light => light == null || light.gameObject.scene != scene || !light.gameObject.activeInHierarchy) ||
            models.Distinct().Count() != 4 || lights.Distinct().Count() != 4 ||
            (door != null && door.gameObject.scene != scene))
        {
            EditorUtility.DisplayDialog("Light Sequence", "Assign the player camera, four different models, and four different active room Light objects from this scene.", "OK");
            return;
        }

        var power = new Transform[4];
        var selectors = new Transform[4];
        for (int i = 0; i < models.Length; i++)
        {
            power[i] = FindPaddle(models[i], "OnOff");
            var color = FindPaddle(models[i], "Light_Color");
            if (i < 2) power[i] ??= color;
            else selectors[i] = color;
            if (power[i] == null || (i >= 2 && selectors[i] == null))
            {
                EditorUtility.DisplayDialog("Light Sequence", $"{Labels[i]} is missing its paddle mesh. Choose the imported model root with its OnOff and/or Light_Color children.", "OK");
                return;
            }
        }
        if (power.Concat(selectors).Where(paddle => paddle != null).Distinct().Count() != 6)
        {
            EditorUtility.DisplayDialog("Light Sequence", "The models must have six separate paddle meshes.", "OK");
            return;
        }
        if (models.Any(model => model.GetComponentInChildren<PuzzleSwitch>(true) != null))
        {
            EditorUtility.DisplayDialog("Light Sequence", "One of the selected models already has a PuzzleSwitch component. Choose the model without legacy switch input so a click is only handled once.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Wire Mixed Light Sequence");
        var root = new GameObject("Light Activation Sequence");
        Undo.RegisterCreatedObjectUndo(root, "Create Light Activation Sequence");
        var panel = Undo.AddComponent<LightSequencePanel>(root);
        panel.playerCamera = playerCamera;
        panel.playerController = playerCamera.GetComponentInParent<FirstPersonController>();
        if (panel.playerController == null)
            panel.playerController = FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None)
                .FirstOrDefault(controller => controller.gameObject.scene == scene &&
                                              controller.cameraTransform == playerCamera.transform);

        panel.fixtures = new LightSequencePanel.Fixture[4];
        for (int i = 0; i < 4; i++)
        {
            panel.fixtures[i] = new LightSequencePanel.Fixture
            {
                power = Bind(power[i], models[i]),
                selector = Bind(selectors[i], models[i]),
                roomLight = lights[i],
                primaryColor = Primary[i],
                secondaryColor = Secondary[i],
                intensity = lights[i].intensity > 0f ? lights[i].intensity : 1.5f
            };
        }
        panel.sequence = FirstAnswer();
        panel.onWrongInput = new UnityEngine.Events.UnityEvent();
        panel.onSequenceSolved = new UnityEngine.Events.UnityEvent();
        if (door != null) UnityEventTools.AddPersistentListener(panel.onSequenceSolved, door.OnPuzzleSolved);
        AddPuzzleSounds(panel);
        PurgatoryWallClueSetup.Add(panel, clueAnchor);
        EditorUtility.SetDirty(panel);
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undo);
        Selection.activeGameObject = root;
    }

    private static void AddPuzzleSounds(LightSequencePanel panel)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        Undo.RecordObject(panel, "Add Lighting Puzzle Sounds");
        if (panel.feedbackSource == null)
        {
            var source = Undo.AddComponent<AudioSource>(panel.gameObject);
            source.playOnAwake = false;
            source.volume = 0.45f;
            panel.feedbackSource = source;
        }

        if (panel.powerRockerClick == null)
            panel.powerRockerClick = LoadClip("7f21770079d9c3843a76c7734c06987e");
        if (panel.colorSelectorClick == null)
            panel.colorSelectorClick = LoadClip("338107c820f50f24d90e6f0a8e7d0c35");
        panel.correctStepClip ??= LoadClip("b9c0e3c832ab39a49b4b03e9c9b6b5d2");
        panel.wrongInputClip ??= LoadClip("ae705af900de39e4ca0b540302a4d513");
        panel.solvedClip ??= LoadClip("45a3975eea2b2134ca95290ca7d40aa7");
        PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
        EditorUtility.SetDirty(panel);
        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
    }

    private static AudioClip LoadClip(string guid) =>
        AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));

    private static Transform FindPaddle(Transform model, string name) =>
        model.GetComponentsInChildren<MeshFilter>(true)
            .FirstOrDefault(item => item.sharedMesh != null &&
                (item.name == name || item.name.StartsWith(name + ".", System.StringComparison.Ordinal)))?.transform;

    private static LightSequencePanel.Rocker Bind(Transform paddle, Transform model)
    {
        if (paddle == null) return new LightSequencePanel.Rocker();
        var target = paddle.GetComponents<Collider>().FirstOrDefault(collider => collider.enabled && !collider.isTrigger);
        if (target == null)
        {
            var bounds = paddle.GetComponent<MeshFilter>().sharedMesh.bounds;
            var box = Undo.AddComponent<BoxCollider>(paddle.gameObject);
            box.center = bounds.center;
            box.size = bounds.size;
            target = box;
        }
        return new LightSequencePanel.Rocker { paddle = paddle, target = target, axisReference = model };
    }

    private static LightSequencePanel.Step[] FirstAnswer() =>
        new[] { Step(1), Step(3, secondary: true), Step(4), Step(2) };

    private static void ApplyFirstAnswer(LightSequencePanel panel)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (panel.fixtures == null || panel.fixtures.Length != 4 ||
            panel.fixtures[2]?.selector?.paddle == null || panel.fixtures[3]?.selector?.paddle == null)
        {
            EditorUtility.DisplayDialog("Lighting Puzzle", "The first answer needs four fixtures with color selectors on fixtures 3 and 4.", "OK");
            return;
        }

        Undo.RecordObject(panel, "Apply First Light Puzzle Answer");
        panel.fixtures[2].secondaryColor = Secondary[2];
        panel.fixtures[3].primaryColor = Primary[3];
        panel.sequence = FirstAnswer();
        PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
        EditorUtility.SetDirty(panel);
        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
        Selection.activeGameObject = panel.gameObject;
    }

    private static LightSequencePanel.Step Step(int fixture, bool secondary = false) =>
        new LightSequencePanel.Step { fixtureNumber = fixture, secondary = secondary };
}
