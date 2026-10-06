using System;
using UnityEngine;

// One switch for every debug aid in the demo: floating prisoner status labels, AI perception gizmos and chatty console logs.
// Toggle it with one click:
//   - Editor: Tools > KuvanTarkka > Debug Overlays (checkmark), or the checkbox on the TrainingDebugSettings component.
//   - Headset: hold the left controller's menu button for 2 seconds.
// Everything that is debug-only reads TrainingDebug.Enabled or listens to Changed, so nothing needs to be hidden by hand.
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

    // Debug.Log that disappears together with the overlays - use for per-event chatter, not for real warnings.
    public static void Log(string message, UnityEngine.Object context = null)
    {
        if (enabled)
            Debug.Log(message, context);
    }
}
