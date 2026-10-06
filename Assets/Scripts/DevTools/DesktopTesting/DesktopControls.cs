#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// DEV TOOL - EDITOR ONLY (see DesktopMode). The keyboard & mouse layout for desktop testing, all in one place.
// DesktopPlayer plugs an instance into ControllerInput.Override, so every game feature that reads a controller button
// through ControllerInput (taser safety, commands, debug toggle...) can be tested without a headset - with no
// desktop-specific code in the feature itself.
//
//   Right hand: trigger = Left mouse,  grip (hold item) = E,  A = R,  B = T
//   Left hand:  trigger = Right mouse, grip (hold item) = Q,  X = F,  Y = G,  menu = Tab
//   Left stick = WASD,  right stick click = Middle mouse
public class DesktopControls : IControllerInputSource
{
    // Grip in VR is "squeeze to hold". On a keyboard that would mean keeping E pressed all the time, so E/Q toggle a
    // held state instead. DesktopPlayer owns that state and reports it here.
    readonly bool[] gripHeld = new bool[2];
    readonly int[] gripChangedFrame = { -1, -1 };

    // False while the mouse cursor is free 
    public bool InputEnabled { get; set; }

    public void SetGripHeld(Hand hand, bool held)
    {
        int h = (int)hand;
        if (gripHeld[h] == held)
            return;
        gripHeld[h] = held;
        gripChangedFrame[h] = Time.frameCount;
    }

    public bool GetButton(Hand hand, ControllerButton button)
    {
        if (button == ControllerButton.Grip)
            return gripHeld[(int)hand];

        ButtonControl control = Control(hand, button);
        return (control != null && control.isPressed) || (InputEnabled && Input.GetKey(Legacy(hand, button)));
    }

    public bool GetButtonDown(Hand hand, ControllerButton button)
    {
        if (button == ControllerButton.Grip)
            return gripHeld[(int)hand] && gripChangedFrame[(int)hand] == Time.frameCount;

        ButtonControl control = Control(hand, button);
        return (control != null && control.wasPressedThisFrame) || (InputEnabled && Input.GetKeyDown(Legacy(hand, button)));
    }

    public bool GetButtonUp(Hand hand, ControllerButton button)
    {
        if (button == ControllerButton.Grip)
            return !gripHeld[(int)hand] && gripChangedFrame[(int)hand] == Time.frameCount;

        ButtonControl control = Control(hand, button);
        return (control != null && control.wasReleasedThisFrame) || (InputEnabled && Input.GetKeyUp(Legacy(hand, button)));
    }

    public float GetAxis(Hand hand, ControllerAxis axis)
    {
        ControllerButton button = axis == ControllerAxis.Trigger ? ControllerButton.Trigger : ControllerButton.Grip;
        return GetButton(hand, button) ? 1f : 0f;
    }

    public Vector2 GetStick(Hand hand)
    {
        if (!InputEnabled || hand != Hand.Left)
            return Vector2.zero;

        Vector2 move = Vector2.zero;
        if (Key(Keyboard.current?.wKey, KeyCode.W)) move.y += 1f;
        if (Key(Keyboard.current?.sKey, KeyCode.S)) move.y -= 1f;
        if (Key(Keyboard.current?.dKey, KeyCode.D)) move.x += 1f;
        if (Key(Keyboard.current?.aKey, KeyCode.A)) move.x -= 1f;
        return Vector2.ClampMagnitude(move, 1f);
    }

    // A key is down if either input system sees it. Both are read because on some setups the Input System never
    // updates keyboard/mouse in the editor's Play mode, while the old UnityEngine.Input (Active Input Handling = Both) does.
    public static bool Key(KeyControl inputSystemKey, KeyCode legacyKey) =>
        (inputSystemKey != null && inputSystemKey.isPressed) || Input.GetKey(legacyKey);

    public static bool KeyDown(KeyControl inputSystemKey, KeyCode legacyKey) =>
        (inputSystemKey != null && inputSystemKey.wasPressedThisFrame) || Input.GetKeyDown(legacyKey);

    // Same layout as Control(), for the old input manager.
    static KeyCode Legacy(Hand hand, ControllerButton button)
    {
        if (hand == Hand.Right)
        {
            switch (button)
            {
                case ControllerButton.Trigger: return KeyCode.Mouse0;
                case ControllerButton.Primary: return KeyCode.R;
                case ControllerButton.Secondary: return KeyCode.T;
                case ControllerButton.Stick: return KeyCode.Mouse2;
            }
        }
        else
        {
            switch (button)
            {
                case ControllerButton.Trigger: return KeyCode.Mouse1;
                case ControllerButton.Primary: return KeyCode.F;
                case ControllerButton.Secondary: return KeyCode.G;
                case ControllerButton.Menu: return KeyCode.Tab;
            }
        }
        return KeyCode.None;
    }

    // The physical key or mouse button behind each controller button
    ButtonControl Control(Hand hand, ControllerButton button)
    {
        if (!InputEnabled)
            return null;

        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (hand == Hand.Right)
        {
            switch (button)
            {
                case ControllerButton.Trigger: return mouse?.leftButton;
                case ControllerButton.Primary: return kb?.rKey;
                case ControllerButton.Secondary: return kb?.tKey;
                case ControllerButton.Stick: return mouse?.middleButton;
            }
        }
        else
        {
            switch (button)
            {
                case ControllerButton.Trigger: return mouse?.rightButton;
                case ControllerButton.Primary: return kb?.fKey;
                case ControllerButton.Secondary: return kb?.gKey;
                case ControllerButton.Menu: return kb?.tabKey;
            }
        }
        return null;
    }
}
#endif
