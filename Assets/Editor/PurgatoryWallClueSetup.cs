using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class PurgatoryWallClueSetup
{
    private const string ClueName = "Red and Blue Wall Clue";

    public static bool ValidateAnchor(Transform anchor)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
        if (anchor != null && anchor.gameObject.scene == SceneManager.GetActiveScene() &&
            anchor.gameObject.activeInHierarchy && anchor.Find(ClueName) == null)
            return true;

        EditorUtility.DisplayDialog("Wall Clue",
            "Assign an active wall anchor in this scene that does not already contain the red and blue clue.", "OK");
        return false;
    }

    public static void Add(LightSequencePanel panel, Transform anchor)
    {
        if (!ValidateAnchor(anchor)) return;
        var lights = panel.fixtures.Select(fixture => fixture?.roomLight).ToArray();
        if (lights.Length != 4 || lights.Any(light => light == null) || lights.Distinct().Count() != 4 ||
            panel.playerCamera == null)
        {
            EditorUtility.DisplayDialog("Wall Clue", "The sequence needs a camera and four distinct circuit lights.", "OK");
            return;
        }

        var root = new GameObject(ClueName);
        root.transform.SetParent(anchor, false);
        var writing = Text(root.transform, "Wall Writing", "WHAT IS HIDDEN\nMAY STILL BE OPENED",
            new Vector3(0, 0, -0.045f), new Vector2(1.5f, 0.38f));
        var writingTarget = writing.gameObject.AddComponent<BoxCollider>();
        writingTarget.size = new Vector3(1.52f, 0.40f, 0.035f);
        var glow = new GameObject("Writing Glow").AddComponent<Light>();
        glow.transform.SetParent(writing.transform, false);
        glow.transform.localPosition = new Vector3(0, 0, -0.20f);
        glow.type = LightType.Point;
        glow.color = new Color(0.85f, 0.55f, 1f);
        glow.range = 0.7f;
        glow.intensity = 0.28f;

        var keyhole = new GameObject("Wall Keyhole");
        keyhole.transform.SetParent(root.transform, false);
        keyhole.transform.localPosition = new Vector3(0, -0.40f, -0.07f);
        var target = keyhole.AddComponent<BoxCollider>();
        target.size = new Vector3(0.29f, 0.34f, 0.08f);
        Part(keyhole.transform, "Faceplate", PrimitiveType.Cube, Vector3.zero,
            new Vector3(0.25f, 0.30f, 0.04f), MaterialNamed("ClockFrontRim"));
        var dark = MaterialNamed("M_CardEdge");
        Part(keyhole.transform, "Keyway Head", PrimitiveType.Sphere, new Vector3(0, 0.045f, -0.025f),
            new Vector3(0.09f, 0.09f, 0.014f), dark);
        Part(keyhole.transform, "Keyway Slot", PrimitiveType.Cube, new Vector3(0, -0.023f, -0.025f),
            new Vector3(0.041f, 0.093f, 0.013f), dark);
        var locked = Text(keyhole.transform, "Locked Message", "A key is needed.",
            new Vector3(0, -0.24f, -0.06f), new Vector2(0.9f, 0.13f));
        locked.gameObject.SetActive(false);
        writing.gameObject.SetActive(false);
        keyhole.SetActive(false);

        var clue = root.AddComponent<LightConditionClue>();
        clue.puzzleId = "cell_red_blue_" + GUID.Generate();
        clue.playerCamera = panel.playerCamera;
        clue.playerController = panel.playerController;
        clue.requiredLights = lights.Take(2).ToArray();
        clue.forbiddenLights = lights.Skip(2).ToArray();
        clue.writing = writing;
        clue.writingTarget = writingTarget;
        clue.writingGlow = glow;
        clue.keyholeTarget = target;
        UnityEventTools.AddBoolPersistentListener(clue.onKeyholeClicked, locked.gameObject.SetActive, true);
        Undo.RegisterCreatedObjectUndo(root, "Add Red and Blue Wall Clue");
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
    }

    private static TextMeshPro Text(Transform parent, string name, string value, Vector3 position, Vector2 size)
    {
        var text = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshPro>();
        text.transform.SetParent(parent, false);
        text.transform.localPosition = position;
        text.rectTransform.sizeDelta = size;
        var fontPath = AssetDatabase.FindAssets("IMFellEnglish-Regular t:TMP_FontAsset").FirstOrDefault();
        if (fontPath != null)
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(fontPath));
        text.text = value;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 0.1f;
        text.fontSizeMax = 36f;
        return text;
    }

    private static Material MaterialNamed(string name) =>
        AssetDatabase.FindAssets(name + " t:Material")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => System.IO.Path.GetFileNameWithoutExtension(path).Equals(name,
                System.StringComparison.OrdinalIgnoreCase))
            .Select(AssetDatabase.LoadAssetAtPath<Material>)
            .FirstOrDefault();

    private static void Part(Transform parent, string name, PrimitiveType type, Vector3 position,
        Vector3 scale, Material material)
    {
        var part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        Object.DestroyImmediate(part.GetComponent<Collider>());
        if (material != null) part.GetComponent<Renderer>().sharedMaterial = material;
    }
}
