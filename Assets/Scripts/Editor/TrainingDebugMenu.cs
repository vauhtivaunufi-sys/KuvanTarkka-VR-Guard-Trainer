using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > KuvanTarkka > Debug Overlays - the one-click switch for every debug aid (see TrainingDebug).
// Flips the checkbox on the scene's TrainingDebugSettings (creating that object the first time), so the choice is saved with the
// scene and a build made with it off starts clean. Works in Play mode too.
static class TrainingDebugMenu
{
    const string MenuPath = "Tools/KuvanTarkka/Debug Overlays";

    [MenuItem(MenuPath, priority = 0)]
    static void Toggle()
    {
        TrainingDebugSettings settings = Object.FindFirstObjectByType<TrainingDebugSettings>();
        if (settings == null)
        {
            var go = new GameObject("TrainingDebugSettings");
            Undo.RegisterCreatedObjectUndo(go, "Create Training Debug Settings");
            settings = go.AddComponent<TrainingDebugSettings>();
        }

        Undo.RecordObject(settings, "Toggle Debug Overlays");
        settings.ShowDebugOverlays = !settings.ShowDebugOverlays;
        EditorUtility.SetDirty(settings);
        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(settings.gameObject.scene);
    }

    // Unity calls this before drawing the menu - keeps the checkmark in sync with the scene.
    [MenuItem(MenuPath, true)]
    static bool ToggleValidate()
    {
        TrainingDebugSettings settings = Object.FindFirstObjectByType<TrainingDebugSettings>();
        Menu.SetChecked(MenuPath, settings != null ? settings.ShowDebugOverlays : TrainingDebug.Enabled);
        return true;
    }
}
