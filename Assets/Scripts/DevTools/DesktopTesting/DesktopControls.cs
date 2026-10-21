#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// EDITOR ONLY dev tool: keyboard and mouse layout for desktop testing, plugged into ControllerInput.Override
public class DesktopControls : IControllerInputSource
{
    // E/Q toggle holding instead of squeezing, DesktopPlayer owns that state and reports it here
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

    // Reads both input systems, in some editor setups only the old Input sees the keyboard
    public static bool Key(KeyControl inputSystemKey, KeyCode legacyKey) =>
        (inputSystemKey != null && inputSystemKey.isPressed) || Input.GetKey(legacyKey);

    public static bool KeyDown(KeyControl inputSystemKey, KeyCode legacyKey) =>
        (inputSystemKey != null && inputSystemKey.wasPressedThisFrame) || Input.GetKeyDown(legacyKey);

    // Same layout as Control(), for the old input manager
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
