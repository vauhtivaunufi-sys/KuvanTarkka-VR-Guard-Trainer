using UnityEditor;

// Tools > KuvanTarkka > Desktop Testing - whether pressing Play in the editor uses the headset or the keyboard & mouse
// test tool (DevTools/DesktopTesting). The game is VR only; this is for testing logic without a headset.
// Saved per computer, so everyone picks their own and nothing changes in the project or in git.
//   Auto                - headset if one is connected and running (Quest Link), otherwise keyboard & mouse.
//   Keyboard and Mouse  - always the test tool, even with a headset plugged in.
//   Headset Only        - never the test tool.
// Has no effect on builds: the test tool is not compiled into them.
static class KeyboardMouseModeMenu
{
    const string Root = "Tools/KuvanTarkka/Desktop Testing (editor only)/";
    const string AutoPath = Root + "Auto (headset if connected)";
    const string DesktopPath = Root + "Keyboard and Mouse";
    const string HeadsetPath = Root + "Headset Only";

    [MenuItem(AutoPath, priority = 1)]
    static void SetAuto() => Set(DesktopMode.Setting.Auto);

    [MenuItem(DesktopPath, priority = 2)]
    static void SetDesktop() => Set(DesktopMode.Setting.Desktop);

    [MenuItem(HeadsetPath, priority = 3)]
    static void SetHeadset() => Set(DesktopMode.Setting.Headset);

    // Validate functions run when the menu opens - used here only to draw the checkmark next to the current choice.
    [MenuItem(AutoPath, true)]
    static bool ValidateAuto() => Check(AutoPath, DesktopMode.Setting.Auto);

    [MenuItem(DesktopPath, true)]
    static bool ValidateDesktop() => Check(DesktopPath, DesktopMode.Setting.Desktop);

    [MenuItem(HeadsetPath, true)]
    static bool ValidateHeadset() => Check(HeadsetPath, DesktopMode.Setting.Headset);

    static void Set(DesktopMode.Setting setting)
    {
        EditorPrefs.SetInt(DesktopMode.EditorPrefsKey, (int)setting);
        UnityEngine.Debug.Log($"Desktop Testing: {setting}. " +
                              (EditorApplication.isPlaying ? "Takes effect the next time you press Play." : "Press Play."));
    }

    static bool Check(string path, DesktopMode.Setting setting)
    {
        Menu.SetChecked(path, DesktopMode.CurrentSetting == setting);
        return true;
    }
}
