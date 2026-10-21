using UnityEngine;
using UnityEngine.InputSystem;
using XRInputDevices = UnityEngine.XR.InputDevices;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRNode = UnityEngine.XR.XRNode;

public enum Hand
{
    Left,
    Right
}

public enum ControllerButton
{
    Primary,    // A / X
    Secondary,  // B / Y
    Menu,       // left controller menu button
    Stick,      // thumbstick click
    Trigger,
    Grip
}

public enum ControllerAxis
{
    Trigger,
    Grip
}

// Stand-in for the real controllers, set only by editor test tools, always null in builds
public interface IControllerInputSource
{
    bool GetButton(Hand hand, ControllerButton button);
    bool GetButtonDown(Hand hand, ControllerButton button);
    bool GetButtonUp(Hand hand, ControllerButton button);
    float GetAxis(Hand hand, ControllerAxis axis);
    Vector2 GetStick(Hand hand);
}

// One place to read controller buttons, game code reads them only here so the desktop test tool can stand in
public static class ControllerInput
{
    static readonly string[] ButtonUsages =
    {
        "PrimaryButton",
        "SecondaryButton",
        "MenuButton",
        "Primary2DAxisClick",
        "TriggerButton",
        "GripButton"
    };

    static readonly string[] AxisUsages = { "Trigger", "Grip" };

    static InputAction[,] buttons;
    static InputAction[,] axes;
    static InputAction[] sticks;

    // Test-only stand-in for the controllers. null (always, in builds) = read the real controllers
    public static IControllerInputSource Override { get; set; }

    // Statics survive between Play sessions when domain reload is off - never start a session with a stale override
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOverride()
    {
        Override = null;
    }

    public static bool GetButton(Hand hand, ControllerButton button) =>
        Override != null ? Override.GetButton(hand, button) : Button(hand, button).IsPressed();

    public static bool GetButtonDown(Hand hand, ControllerButton button) =>
        Override != null ? Override.GetButtonDown(hand, button) : Button(hand, button).WasPressedThisFrame();

    public static bool GetButtonUp(Hand hand, ControllerButton button) =>
        Override != null ? Override.GetButtonUp(hand, button) : Button(hand, button).WasReleasedThisFrame();

    // Analog trigger / grip, 0..1
    public static float GetAxis(Hand hand, ControllerAxis axis)
    {
        if (Override != null)
            return Override.GetAxis(hand, axis);

        EnsureActions();
        return axes[(int)hand, (int)axis].ReadValue<float>();
    }

    public static Vector2 GetStick(Hand hand)
    {
        if (Override != null)
            return Override.GetStick(hand);

        EnsureActions();
        return sticks[(int)hand].ReadValue<Vector2>();
    }

    // Short vibration on one controller. amplitude 0..1, duration in seconds. Does nothing without a real controller
    public static void Haptic(Hand hand, float amplitude, float duration)
    {
        XRInputDevice device = XRInputDevices.GetDeviceAtXRNode(hand == Hand.Left ? XRNode.LeftHand : XRNode.RightHand);
        if (device.isValid && device.TryGetHapticCapabilities(out UnityEngine.XR.HapticCapabilities caps) && caps.supportsImpulse)
            device.SendHapticImpulse(0, Mathf.Clamp01(amplitude), duration);
    }

    static InputAction Button(Hand hand, ControllerButton button)
    {
        EnsureActions();
        return buttons[(int)hand, (int)button];
    }

    static void EnsureActions()
    {
        if (buttons != null)
            return;

        buttons = new InputAction[2, ButtonUsages.Length];
        axes = new InputAction[2, AxisUsages.Length];
        sticks = new InputAction[2];

        for (int h = 0; h < 2; h++)
        {
            string side = h == (int)Hand.Left ? "{LeftHand}" : "{RightHand}";

            for (int b = 0; b < ButtonUsages.Length; b++)
            {
                var action = new InputAction($"{(Hand)h} {(ControllerButton)b}", InputActionType.Button,
                    $"<XRController>{side}/{{{ButtonUsages[b]}}}");
                action.Enable();
                buttons[h, b] = action;
            }

            for (int a = 0; a < AxisUsages.Length; a++)
            {
                var action = new InputAction($"{(Hand)h} {(ControllerAxis)a}", InputActionType.Value,
                    $"<XRController>{side}/{{{AxisUsages[a]}}}", expectedControlType: "Axis");
                action.Enable();
                axes[h, a] = action;
            }

            var stick = new InputAction($"{(Hand)h} Stick", InputActionType.Value,
                $"<XRController>{side}/{{Primary2DAxis}}", expectedControlType: "Vector2");
            stick.Enable();
            sticks[h] = stick;
        }
    }
}
