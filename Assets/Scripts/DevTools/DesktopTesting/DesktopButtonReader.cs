#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

// EDITOR ONLY dev tool: a button XRI interactors read, set from code instead of a controller
public class DesktopButtonReader : IXRInputButtonReader
{
    bool isPerformed;
    int performedFrame = -1;
    int completedFrame = -1;

    // Call every frame (or whenever it changes) with the current pressed state
    public void Set(bool pressed)
    {
        if (pressed == isPerformed)
            return;

        isPerformed = pressed;
        if (pressed)
            performedFrame = Time.frameCount;
        else
            completedFrame = Time.frameCount;
    }

    public bool ReadIsPerformed() => isPerformed;

    public bool ReadWasPerformedThisFrame() => performedFrame == Time.frameCount;

    public bool ReadWasCompletedThisFrame() => completedFrame == Time.frameCount;

    public float ReadValue() => isPerformed ? 1f : 0f;

    public bool TryReadValue(out float value)
    {
        value = ReadValue();
        return isPerformed;
    }
}
#endif
