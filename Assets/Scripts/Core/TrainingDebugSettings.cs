using UnityEngine;

// Scene-side home of the debug switch (see TrainingDebug). The checkbox is the source of truth when the scene loads,
// so a build made with it unchecked starts clean; it can still be flipped live in the headset.
[DisallowMultipleComponent]
public class TrainingDebugSettings : MonoBehaviour
{
    [Tooltip("Show floating prisoner labels, perception gizmos and debug logs. Uncheck before a customer demo.")]
    [SerializeField] bool showDebugOverlays = true;

    [Tooltip("Allow toggling the overlays in the headset by holding the left menu button.")]
    [SerializeField] bool allowHeadsetToggle = true;

    [Tooltip("How long the left menu button must be held to toggle, in seconds.")]
    [SerializeField] float holdToToggle = 2f;

    float heldTime;
    bool toggledThisHold;

    public bool ShowDebugOverlays
    {
        get => showDebugOverlays;
        set
        {
            showDebugOverlays = value;
            TrainingDebug.Enabled = value;
        }
    }

    void Awake()
    {
        TrainingDebug.Enabled = showDebugOverlays;
    }

    void OnValidate()
    {
        // Makes the inspector checkbox work live in Play mode too.
        if (Application.isPlaying)
            TrainingDebug.Enabled = showDebugOverlays;
    }

    void Update()
    {
        if (!allowHeadsetToggle)
            return;

        if (ControllerInput.GetButton(Hand.Left, ControllerButton.Menu))
        {
            heldTime += Time.unscaledDeltaTime;
            if (!toggledThisHold && heldTime >= holdToToggle)
            {
                toggledThisHold = true;
                ShowDebugOverlays = !showDebugOverlays;
                ControllerInput.Haptic(Hand.Left, 0.5f, 0.15f);
            }
        }
        else
        {
            heldTime = 0f;
            toggledThisHold = false;
        }
    }
}
