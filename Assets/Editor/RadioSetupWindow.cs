using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class RadioSetupWindow : EditorWindow
{
    GameObject root;
    Transform tuning, volume, bandSwitch, closeUp;
    Camera camera;
    FirstPersonController player;
    AudioSource audio, staticAudio;
    Vector3 axis = Vector3.forward;
    float dragSensitivity = 0.0015f;
    AudioSource clicks;
    AudioClip clickClip;

    [MenuItem("Tools/Purgatory/Wire Radio")]
    public static void Open()
    {
        var window = GetWindow<RadioSetupWindow>("Wire Radio");
        window.root = Selection.activeGameObject;
    }
    void OnGUI()
    {
        EditorGUILayout.HelpBox("Assign your imported mesh parts. This adds the new radio controls and disables legacy KnobInteractable components on this radio. Undo is supported.", MessageType.Info);
        root = (GameObject)EditorGUILayout.ObjectField("Radio root", root, typeof(GameObject), true);
        tuning = Field("Bottom knob: tuning", tuning);
        volume = Field("Top-left knob: volume", volume);
        bandSwitch = Field("Top-right: AM/FM switch", bandSwitch);
        closeUp = Field("Close-up camera target", closeUp);
        camera = (Camera)EditorGUILayout.ObjectField("Player camera", camera, typeof(Camera), true);
        player = (FirstPersonController)EditorGUILayout.ObjectField("Player controller", player, typeof(FirstPersonController), true);
        audio = (AudioSource)EditorGUILayout.ObjectField("Radio audio", audio, typeof(AudioSource), true);
        staticAudio = (AudioSource)EditorGUILayout.ObjectField("Static audio (optional)", staticAudio, typeof(AudioSource), true);
        axis = EditorGUILayout.Vector3Field("Knob local rotation axis", axis);
        dragSensitivity = EditorGUILayout.FloatField("Drag sensitivity", dragSensitivity);
        clicks = (AudioSource)EditorGUILayout.ObjectField("Click audio (optional)", clicks, typeof(AudioSource), true);
        clickClip = (AudioClip)EditorGUILayout.ObjectField("Click clip (optional)", clickClip, typeof(AudioClip), false);
        EditorGUILayout.HelpBox("Space confirms. Up/Down select a control, Left/Right adjust it. Click the switch or press F for AM/FM. Each control needs a collider; this tool adds a box when none exists. Adjust collider size and axes afterward.", MessageType.None);
        bool valid = root != null && root.scene.IsValid() && tuning != null && volume != null && tuning != volume &&
            tuning != root.transform && volume != root.transform && tuning.IsChildOf(root.transform) && volume.IsChildOf(root.transform) &&
            !tuning.IsChildOf(volume) && !volume.IsChildOf(tuning) &&
            (bandSwitch == null || (bandSwitch != tuning && bandSwitch != volume && bandSwitch != root.transform && bandSwitch.IsChildOf(root.transform) &&
                !bandSwitch.IsChildOf(tuning) && !bandSwitch.IsChildOf(volume) && !tuning.IsChildOf(bandSwitch) && !volume.IsChildOf(bandSwitch))) &&
            closeUp != null && camera != null && player != null;
        using (new EditorGUI.DisabledScope(!valid || EditorApplication.isPlaying))
            if (GUILayout.Button("Wire radio controls")) Wire();
    }
    Transform Field(string name, Transform value) => (Transform)EditorGUILayout.ObjectField(name, value, typeof(Transform), true);
    T Component<T>(GameObject target) where T : Component
    {
        var component = target.GetComponent<T>();
        if (component == null) component = Undo.AddComponent<T>(target);
        Undo.RecordObject(component, "Wire radio");
        return component;
    }
    void Wire()
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Wire radio");
        var interaction = Component<RadioInteractable>(root);
        interaction.zoomTarget = closeUp;
        interaction.playerCamera = camera;
        interaction.playerController = player;
        var tuner = Component<RadioTuner>(root);
        tuner.radioAudio = audio;
        tuner.staticAudio = staticAudio;
        foreach (var oldKnob in root.GetComponentsInChildren<KnobInteractable>(true))
        { Undo.RecordObject(oldKnob, "Disable old knob"); oldKnob.enabled = false; EditorUtility.SetDirty(oldKnob); }
        foreach (var oldConfirm in root.GetComponentsInChildren<RadioConfirmButton>(true))
        { Undo.RecordObject(oldConfirm, "Use Space to confirm"); oldConfirm.enabled = false; EditorUtility.SetDirty(oldConfirm); }
        WireKnob(tuning, RadioKnob.Function.Tuning, tuner);
        WireKnob(volume, RadioKnob.Function.Volume, tuner);
        if (bandSwitch != null)
        {
            var oldKnob = bandSwitch.GetComponent<RadioKnob>();
            if (oldKnob != null) { Undo.RecordObject(oldKnob, "Replace knob with switch"); oldKnob.enabled = false; }
            var toggle = Component<RadioBandSwitch>(bandSwitch.gameObject);
            toggle.radio = tuner;
            toggle.visualPivot = bandSwitch;
            toggle.clickSource = clicks;
            toggle.clickClip = clickClip;
            EditorUtility.SetDirty(toggle);
            AddCollider(bandSwitch);
        }
        // Child colliders are enough for the root radio's interaction raycast.
        EditorUtility.SetDirty(interaction);
        EditorUtility.SetDirty(tuner);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = root;
    }
    void WireKnob(Transform part, RadioKnob.Function function, RadioTuner tuner)
    {
        var knob = Component<RadioKnob>(part.gameObject);
        knob.radio = tuner;
        knob.function = function;
        knob.visualPivot = part;
        knob.localAxis = axis;
        knob.dragSensitivity = Mathf.Max(0.0001f, dragSensitivity);
        knob.clickSource = clicks;
        knob.clickClip = clickClip;
        AddCollider(part);
        EditorUtility.SetDirty(knob);
    }
    void AddCollider(Transform part)
    {
        if (part.GetComponentInChildren<Collider>() != null) return;
        var box = Undo.AddComponent<BoxCollider>(part.gameObject);
        var renderers = part.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { box.size = Vector3.one * 0.05f; return; }
        bool first = true;
        Bounds bounds = new Bounds();
        foreach (var renderer in renderers)
        {
            var world = renderer.bounds;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        var point = part.InverseTransformPoint(world.center + Vector3.Scale(world.extents, new Vector3(x, y, z)));
                        if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                        else bounds.Encapsulate(point);
                    }
        }
        box.center = bounds.center;
        box.size = Vector3.Max(bounds.size, Vector3.one * 0.001f);
    }
}
