#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor tool that bakes the runtime HUD into permanent scene GameObjects.
/// After baking, you can edit everything visually in the editor.
/// 
/// HOW TO USE:
/// 1. Put this file in Assets/Editor/
/// 2. In the menu bar: Tools → Bake HUD to Scene
/// 3. The HUD will be built and saved as real scene objects
/// 4. You can then delete HUDBuilder component and edit everything visually
/// 5. Add a UIReferenceLinker component to Canvas to re-wire references at runtime
/// </summary>
public class HUDBaker : EditorWindow
{
    [MenuItem("Tools/Bake HUD to Scene")]
    public static void BakeHUD()
    {
        // Find the Canvas with HUDBuilder
        var hudBuilder = Object.FindFirstObjectByType<HUDBuilder>();
        if (hudBuilder == null)
        {
            EditorUtility.DisplayDialog("HUD Baker",
                "No HUDBuilder found in scene.\nMake sure HUDBuilder is on your Canvas.", "OK");
            return;
        }

        // Check if already baked
        var existing = hudBuilder.transform.Find("LeftCol");
        if (existing != null)
        {
            bool replace = EditorUtility.DisplayDialog("HUD Baker",
                "HUD already exists in scene. Replace it?",
                "Yes, replace", "Cancel");
            if (!replace) return;

            // Destroy existing baked objects
            var toDestroy = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in hudBuilder.transform)
                toDestroy.Add(child.gameObject);
            foreach (var go in toDestroy)
                Object.DestroyImmediate(go);
        }

        // Run HUDBuilder in editor context
        // We need to call BuildHUD via reflection since it's private
        var buildMethod = typeof(HUDBuilder).GetMethod("BuildHUD",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        if (buildMethod == null)
        {
            // Try Start() which calls BuildHUD
            var startMethod = typeof(HUDBuilder).GetMethod("Start",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (startMethod != null)
            {
                startMethod.Invoke(hudBuilder, null);
            }
            else
            {
                EditorUtility.DisplayDialog("HUD Baker",
                    "Could not find BuildHUD or Start method on HUDBuilder.\n" +
                    "Try entering Play mode once first, then use this tool.", "OK");
                return;
            }
        }
        else
        {
            buildMethod.Invoke(hudBuilder, null);
        }

        // Mark scene dirty so it saves
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("HUD Baker",
            "HUD baked successfully!\n\n" +
            "Next steps:\n" +
            "1. Save the scene (Ctrl+S)\n" +
            "2. Edit elements visually in the Hierarchy\n" +
            "3. Add UIReferenceLinker to Canvas\n" +
            "4. You can now disable or delete HUDBuilder", "OK");

        Debug.Log("[HUDBaker] HUD baked to scene. You can now edit it visually.");
    }

    [MenuItem("Tools/Clear Baked HUD")]
    public static void ClearBakedHUD()
    {
        var hudBuilder = Object.FindFirstObjectByType<HUDBuilder>();
        if (hudBuilder == null) return;

        bool confirm = EditorUtility.DisplayDialog("Clear Baked HUD",
            "This will delete all baked HUD objects. Are you sure?",
            "Yes, clear", "Cancel");
        if (!confirm) return;

        var toDestroy = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in hudBuilder.transform)
            toDestroy.Add(child.gameObject);
        foreach (var go in toDestroy)
            Object.DestroyImmediate(go);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[HUDBaker] Baked HUD cleared.");
    }
}
#endif
