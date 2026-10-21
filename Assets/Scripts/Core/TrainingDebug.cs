using System;
using UnityEngine;

// One switch for every debug aid: Tools > KuvanTarkka > Debug Overlays in the editor, hold left menu 2 s in the headset
public static class TrainingDebug
{
    static bool enabled = true;

    public static event Action<bool> Changed;

    public static bool Enabled
    {
        get => enabled;
        set
        {
            if (enabled == value)
                return;
            enabled = value;
            Changed?.Invoke(enabled);
        }
    }

    // Debug.Log that disappears together with the overlays - use for per-event chatter, not for real warnings
    public static void Log(string message, UnityEngine.Object context = null)
    {
        if (enabled)
            Debug.Log(message, context);
    }
}
