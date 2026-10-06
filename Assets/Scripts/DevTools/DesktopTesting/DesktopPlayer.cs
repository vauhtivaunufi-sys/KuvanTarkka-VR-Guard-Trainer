#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;

// DEV TOOL - EDITOR ONLY (see DesktopMode). THE GAME IS VR ONLY - this only lets people without a headset test the logic.
// Keyboard & mouse stand-in for the headset and controllers, first-person-shooter style.
// DesktopMode adds it to the XR Origin automatically in Play mode when there is no headset - never add it to the scene.
//
// It does NOT reimplement grabbing, the taser, the spray, the shield or doors. It drives the XR rig that already exists:
//   - the head (camera) is turned by the mouse instead of the headset;
//   - both controllers are held in front of the camera like FPS weapon hands and aimed at the crosshair;
//   - the controllers' Near-Far Interactors get keyboard/mouse "buttons" (DesktopButtonReader) instead of grip/trigger.
// So XRI does all the interaction exactly as in VR, and every new feature built on XRI or ControllerInput can be tested here.
// Feel, reach, aim and timing are NOT VR-accurate here - final checks of any mechanic are always done in the headset.
[DefaultExecutionOrder(-300)] // before XRI's interaction manager, so this frame's hand poses and buttons are used this frame
[DisallowMultipleComponent]
[RequireComponent(typeof(XROrigin))]
public class DesktopPlayer : MonoBehaviour
{
    [Header("Body")]
    [Tooltip("Eye height above the floor, in meters.")]
    [SerializeField] float eyeHeight = 1.7f;

    [Tooltip("Walking speed in m/s. A calm adult walk is about 1.4.")]
    [SerializeField] float walkSpeed = 1.6f;

    [Tooltip("Speed multiplier while Left Shift is held.")]
    [SerializeField] float sprintMultiplier = 2f;

    [Tooltip("Used only if the rig has no active XRI Gravity Provider.")]
    [SerializeField] float gravity = -9.81f;

    [Header("Look")]
    [Tooltip("Degrees of turn per pixel of mouse movement.")]
    [SerializeField] float mouseSensitivity = 0.1f;

    [Tooltip("How far up/down the player can look, in degrees.")]
    [SerializeField] float maxPitch = 85f;

    [Header("Hands")]
    [Tooltip("Right controller position relative to the camera (right, up, forward), in meters.")]
    [SerializeField] Vector3 rightHandOffset = new Vector3(0.2f, -0.22f, 0.4f);

    [Tooltip("Left controller position relative to the camera (right, up, forward), in meters.")]
    [SerializeField] Vector3 leftHandOffset = new Vector3(-0.22f, -0.22f, 0.4f);

    [Tooltip("Hands aim at whatever is under the crosshair, up to this distance.")]
    [SerializeField] float aimDistance = 15f;

    [Header("Diagnostics")]
    [Tooltip("Write the input state to the Console every 2 seconds. Only for debugging \"nothing reacts\" problems.")]
    [SerializeField] bool logInputToConsole;

    class HandRig
    {
        public Hand hand;
        public Key gripKey;
        public Vector3 offset;
        public Transform root;               // "Left Controller" / "Right Controller" - the object the headset tracking used to move
        public NearFarInteractor interactor;
        public readonly DesktopButtonReader select = new DesktopButtonReader();
        public readonly DesktopButtonReader activate = new DesktopButtonReader();
        public readonly DesktopButtonReader uiPress = new DesktopButtonReader();
        public bool holdWanted;
        public int holdRequestFrame;
    }

    readonly List<HandRig> hands = new List<HandRig>();
    readonly DesktopControls controls = new DesktopControls();
    readonly RaycastHit[] aimHits = new RaycastHit[16];

    XROrigin origin;
    Transform head;
    Transform cameraOffset;
    CharacterController body;
    GravityProvider gravityProvider;

    float pitch;
    float verticalVelocity;
    bool swallowClick;
    float swallowUntil;
    bool showHelp = true;
    Vector2 lastMouseDelta;
    Vector2 lastMoveInput;
    Vector2 mouseSum;
    float nextLogTime;
    InputSettings.EditorInputBehaviorInPlayMode previousInputBehavior;

    void Awake()
    {
        origin = GetComponent<XROrigin>();
        head = origin.Camera != null ? origin.Camera.transform : Camera.main.transform;
        cameraOffset = origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : head.parent;
        body = GetComponent<CharacterController>();
        gravityProvider = GetComponentInChildren<GravityProvider>(true);

        TakeOverFromTracking();
        SetUpHands();
        LockCursor();

        // In the editor, keyboard input normally reaches the game only while the Game view itself has focus. A floating
        // Console / Input Debugger window or a click elsewhere silently sends it to the editor instead. For the test
        // tool, send all input to the game while playing; restored when Play stops.
        previousInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    }

    void OnDestroy()
    {
        if (InputSystem.settings != null)
            InputSystem.settings.editorInputBehaviorInPlayMode = previousInputBehavior;
    }

    void OnEnable()
    {
        // From now on ControllerInput answers from the keyboard and mouse instead of the (missing) controllers.
        ControllerInput.Override = controls;
    }

    void OnDisable()
    {
        if (ControllerInput.Override == controls)
            ControllerInput.Override = null;
        controls.InputEnabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Stops everything that would fight us for the camera and controllers when no headset is there.
    void TakeOverFromTracking()
    {
        // The pose drivers copy headset/controller tracking onto the transforms every frame.
        foreach (TrackedPoseDriver poseDriver in GetComponentsInChildren<TrackedPoseDriver>(true))
            poseDriver.enabled = false;

        // The modality manager hides controllers it can't detect - with no headset that is both of them.
        XRInputModalityManager modality = GetComponentInChildren<XRInputModalityManager>(true);
        if (modality != null)
        {
            modality.enabled = false;
            if (modality.leftHand != null) modality.leftHand.SetActive(false);
            if (modality.rightHand != null) modality.rightHand.SetActive(false);
            if (modality.leftController != null) modality.leftController.SetActive(true);
            if (modality.rightController != null) modality.rightController.SetActive(true);
        }
    }

    void SetUpHands()
    {
        foreach (NearFarInteractor interactor in GetComponentsInChildren<NearFarInteractor>(true))
        {
            bool isLeft = interactor.handedness == InteractorHandedness.Left ||
                          (interactor.handedness == InteractorHandedness.None && interactor.name.Contains("Left"));

            Transform root = ControllerRootOf(interactor.transform);
            root.gameObject.SetActive(true);
            interactor.gameObject.SetActive(true);

            var rig = new HandRig
            {
                hand = isLeft ? Hand.Left : Hand.Right,
                gripKey = isLeft ? Key.Q : Key.E,
                offset = isLeft ? leftHandOffset : rightHandOffset,
                root = root,
                interactor = interactor
            };

            // From now on XRI reads these instead of the controller's grip / trigger.
            interactor.selectInput.bypass = rig.select;
            interactor.activateInput.bypass = rig.activate;
            interactor.uiPressInput.bypass = rig.uiPress;
            // "Pressed" must mean pressed, whatever the prefab says (Toggle / Sticky would break the E/Q logic below).
            interactor.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.StateChange;
            // Point-and-press needs the long ray, and a picked-up item should fly into the hand, not float where it was.
            interactor.enableFarCasting = true;
            interactor.farAttachMode = InteractorFarAttachMode.Near;

            hands.Add(rig);
        }

        if (hands.Count == 0)
            Debug.LogWarning("[DesktopMode] No Near-Far Interactor found under the XR Origin - you can walk and look, but not grab.", this);
    }

    // The controller object is the child of Camera Offset that contains the interactor.
    Transform ControllerRootOf(Transform t)
    {
        while (t.parent != null && t.parent != cameraOffset)
            t = t.parent;
        return t;
    }

    void Update()
    {
        UpdateCursor();
        Look();
        Move();
        PoseHands();
        UpdateHandButtons();
        UpdateShortcuts();
        LogInputState();
    }

    // Every 2 seconds: what each input system sees. The first thing to read in the Console when "nothing reacts".
    void LogInputState()
    {
        if (!logInputToConsole)
            return;

        Keyboard kb = Keyboard.current;
        mouseSum += MouseDelta();
        if (Time.unscaledTime < nextLogTime)
            return;

        nextLogTime = Time.unscaledTime + 2f;
        Debug.Log($"[Desktop Testing] frame {Time.frameCount} t {Time.time:F1} ts {Time.timeScale} | focus {Application.isFocused} lock {Cursor.lockState} input {controls.InputEnabled} swallow {swallowClick} | " +
                  $"InputSystem: W {(kb != null && kb.wKey.isPressed)} any {(kb != null && kb.anyKey.isPressed)} | " +
                  $"Legacy: W {Input.GetKey(KeyCode.W)} any {Input.anyKey} | move {lastMoveInput} | mouse(2s) {mouseSum} | " +
                  $"pos {transform.position:F2} yaw {transform.eulerAngles.y:F0}");
        mouseSum = Vector2.zero;
    }

    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void UpdateCursor()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        bool escape = DesktopControls.KeyDown(kb?.escapeKey, KeyCode.Escape);
        bool clickDown = (mouse != null && mouse.leftButton.wasPressedThisFrame) || Input.GetMouseButtonDown(0);
        bool clickHeld = (mouse != null && mouse.leftButton.isPressed) || Input.GetMouseButton(0);

        if (Cursor.lockState == CursorLockMode.Locked && escape)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (Cursor.lockState != CursorLockMode.Locked && clickDown)
        {
            // This click only grabs the mouse back - it must not also fire the taser.
            LockCursor();
            swallowClick = true;
            swallowUntil = Time.unscaledTime + 0.5f;
        }

        // Release the swallowed click when the button goes up - or after half a second at the latest, in case the
        // release never reaches the game (it can be eaten by the editor when focus changes mid-click).
        if (swallowClick && (!clickHeld || Time.unscaledTime > swallowUntil))
            swallowClick = false;

        controls.InputEnabled = Cursor.lockState == CursorLockMode.Locked && !swallowClick;
    }

    // ---------- Head ----------

    void Look()
    {
        if (controls.InputEnabled)
        {
            Vector2 delta = MouseDelta() * mouseSensitivity;
            lastMouseDelta = delta;

            // Yaw turns the whole rig (so walking follows the view), pitch tilts only the head.
            transform.Rotate(0f, delta.x, 0f, Space.World);
            pitch = Mathf.Clamp(pitch - delta.y, -maxPitch, maxPitch);
        }

        // Head straight above the rig's origin at eye height - what the headset tracking would normally provide.
        head.localPosition = cameraOffset.InverseTransformPoint(transform.position + transform.up * eyeHeight);
        head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    // Mouse movement in pixels this frame, from whichever input system delivers it.
    Vector2 MouseDelta()
    {
        Vector2 delta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        if (delta == Vector2.zero)
        {
            // Old input manager: "Mouse X/Y" are pixel deltas scaled by the axis sensitivity (0.1 by default).
            delta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
        }
        return delta;
    }

    // ---------- Body ----------

    void Move()
    {
        Vector2 input = controls.GetStick(Hand.Left);
        lastMoveInput = input;
        float speed = walkSpeed;

        // TODO(Ivan) - sprint: while Left Shift is held, multiply speed by sprintMultiplier.
        // Read the key the same way UpdateShortcuts() reads F1 (Keyboard.current, check it for null).
        // Only when controls.InputEnabled - with a free mouse cursor nothing should react to the keyboard.

        Vector3 velocity = (transform.forward * input.y + transform.right * input.x) * speed;

        bool xriHandlesGravity = gravityProvider != null && gravityProvider.isActiveAndEnabled && gravityProvider.useGravity;
        if (!xriHandlesGravity && body != null && body.enabled)
        {
            // Keep a small downward push while grounded so isGrounded stays true on slopes and steps.
            if (body.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -1f;
            verticalVelocity += gravity * Time.deltaTime;
            velocity.y = verticalVelocity;
        }

        if (body != null && body.enabled)
            body.Move(velocity * Time.deltaTime); // collides with walls
        else
            transform.position += velocity * Time.deltaTime;
    }

    // ---------- Hands ----------

    void PoseHands()
    {
        Vector3 aimPoint = AimPoint();
        foreach (HandRig rig in hands)
        {
            Vector3 position = head.TransformPoint(rig.offset);
            // Each hand points at the crosshair target, so the taser beam / spray cone goes where the player looks.
            rig.root.SetPositionAndRotation(position, Quaternion.LookRotation(aimPoint - position, head.up));
        }
    }

    // What the crosshair is on: the first thing hit along the view ray, ignoring the player's own rig and held items.
    Vector3 AimPoint()
    {
        Vector3 from = head.position;
        Vector3 dir = head.forward;
        float nearest = aimDistance;

        int count = Physics.RaycastNonAlloc(from, dir, aimHits, aimDistance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = aimHits[i];
            if (hit.distance >= nearest || hit.transform.IsChildOf(transform))
                continue;

            XRGrabInteractable grabbable = hit.collider.GetComponentInParent<XRGrabInteractable>();
            if (grabbable != null && grabbable.isSelected)
                continue;

            nearest = hit.distance;
        }

        return from + dir * Mathf.Max(nearest, 0.3f);
    }

    void UpdateHandButtons()
    {
        Keyboard kb = Keyboard.current;

        foreach (HandRig rig in hands)
        {
            NearFarInteractor interactor = rig.interactor;

            // E / Q: pick up if empty, drop if holding.
            KeyCode legacyGrip = rig.hand == Hand.Right ? KeyCode.E : KeyCode.Q;
            if (controls.InputEnabled && DesktopControls.KeyDown(kb?[rig.gripKey], legacyGrip))
            {
                if (interactor.hasSelection)
                {
                    rig.holdWanted = false;
                }
                else
                {
                    rig.holdWanted = true;
                    rig.holdRequestFrame = Time.frameCount;
                }
            }

            if (rig.holdWanted && interactor.hasSelection)
            {
                // Grabbables stay in the hand until the next press; a door is a click - release right after it reacted.
                if (!(interactor.firstInteractableSelected is XRGrabInteractable))
                    rig.holdWanted = false;
            }
            else if (rig.holdWanted && Time.frameCount - rig.holdRequestFrame > 3)
            {
                // Nothing was in reach (or the item was taken out of the hand) - stop "squeezing".
                rig.holdWanted = false;
            }

            rig.select.Set(rig.holdWanted);
            controls.SetGripHeld(rig.hand, rig.holdWanted);

            bool trigger = controls.GetButton(rig.hand, ControllerButton.Trigger);
            rig.activate.Set(trigger);
            rig.uiPress.Set(trigger);
        }
    }

    // ---------- Shortcuts ----------

    void UpdateShortcuts()
    {
        Keyboard kb = Keyboard.current;

        if (DesktopControls.KeyDown(kb?.f1Key, KeyCode.F1))
            showHelp = !showHelp;

        if (DesktopControls.KeyDown(kb?.f3Key, KeyCode.F3))
        {
            TrainingDebugSettings settings = FindFirstObjectByType<TrainingDebugSettings>();
            if (settings != null)
                settings.ShowDebugOverlays = !TrainingDebug.Enabled; // keeps the scene checkbox in sync
            else
                TrainingDebug.Enabled = !TrainingDebug.Enabled;
        }
    }

    // ---------- On-screen help ----------

    const string HelpText =
        "DESKTOP TEST MODE - editor only, the game is VR   F1 - hide\n" +
        "Mouse - look    WASD - walk    Left Shift - walk faster\n" +
        "E - right hand: pick up / drop, open doors\n" +
        "Q - left hand: pick up / drop\n" +
        "Left mouse - use item in right hand (taser, spray)\n" +
        "Right mouse - use item in left hand (shield up / down)\n" +
        "R / T - right A / B     F / G - left X / Y     Tab - left menu\n" +
        "F3 - debug overlays     Esc - free the mouse, click to return";

    GUIStyle helpStyle;
    GUIStyle promptStyle;
    GUIStyle watermarkStyle;

    void OnGUI()
    {
        if (helpStyle == null)
        {
            helpStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14, padding = new RectOffset(10, 10, 8, 8) };
            promptStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
            promptStyle.normal.textColor = Color.white;
            watermarkStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight, fontSize = 13, fontStyle = FontStyle.Bold };
            watermarkStyle.normal.textColor = new Color(1f, 0.75f, 0.2f);
        }

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            GUI.Label(new Rect(0, Screen.height * 0.5f - 20, Screen.width, 40), "Click to play", promptStyle);
            return;
        }

        DrawCrosshair();

        // Always visible, so nobody mistakes a desktop screen recording for the actual (VR) game.
        GUI.Label(new Rect(Screen.width - 330, 10, 320, 24), "DESKTOP TEST MODE - not the game (VR only)", watermarkStyle);

        if (showHelp)
        {
            GUI.Box(new Rect(10, 10, 470, 170), HelpText, helpStyle);
            GUI.Box(new Rect(10, 184, 560, 64), StatusText(), helpStyle);
        }

        string prompt = HandPrompt();
        if (prompt.Length > 0)
            GUI.Label(new Rect(0, Screen.height * 0.5f + 30, Screen.width, 60), prompt, promptStyle);
    }

    // Live input state - first thing to look at when "nothing reacts".
    string StatusText()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        return $"frame {Time.frameCount}  time {Time.time:F1}  unscaled {Time.unscaledTime:F1}  timeScale {Time.timeScale}  dt {Time.deltaTime:F3}  paused {UnityEditor.EditorApplication.isPaused}\n" +
               $"focus {Application.isFocused}  lock {Cursor.lockState}  input {controls.InputEnabled}  swallow {swallowClick}  hands {hands.Count}\n" +
               $"W: InputSystem {(kb != null && kb.wKey.isPressed)} / Legacy {Input.GetKey(KeyCode.W)}  LMB {(mouse != null && mouse.leftButton.isPressed) || Input.GetMouseButton(0)}  " +
               $"move {lastMoveInput}  mouse {lastMouseDelta}  pos {transform.position:F2}";
    }

    void DrawCrosshair()
    {
        const float size = 10f;
        const float thickness = 2f;
        float x = Screen.width * 0.5f;
        float y = Screen.height * 0.5f;

        GUI.color = new Color(1f, 1f, 1f, 0.85f);
        GUI.DrawTexture(new Rect(x - size, y - thickness * 0.5f, size * 2f, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - thickness * 0.5f, y - size, thickness, size * 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    // "E - pick up Taser" etc. under the crosshair, so nobody has to remember the key map.
    string HandPrompt()
    {
        string text = "";
        foreach (HandRig rig in hands)
        {
            string key = rig.hand == Hand.Right ? "E" : "Q";
            string use = rig.hand == Hand.Right ? "Left mouse" : "Right mouse";
            NearFarInteractor interactor = rig.interactor;

            string line;
            if (interactor.hasSelection)
                line = $"{use} - use {interactor.firstInteractableSelected.transform.name}    {key} - drop";
            else if (interactor.hasHover)
                line = $"{key} - {interactor.interactablesHovered[0].transform.name}";
            else
                continue;

            text += (text.Length > 0 ? "\n" : "") + line;
        }
        return text;
    }
}
#endif
