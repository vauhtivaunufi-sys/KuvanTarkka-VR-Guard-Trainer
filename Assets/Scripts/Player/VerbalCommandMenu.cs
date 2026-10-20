using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Radial menu of verbal commands: press left Y to open, tilt the left stick to pick, let the stick go to say it
// Y and the stick are both under the thumb, so nothing has to be held while tilting
// Desktop test mode: press G, pick with WASD, let the keys go
[RequireComponent(typeof(PlayerRig))]
public class VerbalCommandMenu : MonoBehaviour
{
    [Tooltip("Stick tilt 0..1 needed to select a command")]
    [SerializeField] float selectDeadzone = 0.5f;

    [Tooltip("A command counts once the stick rests on it this long, so sweeping past others says nothing, s")]
    [SerializeField] float holdToPick = 0.12f;

    [Tooltip("The menu closes by itself after this long without a pick, s")]
    [SerializeField] float autoCloseAfter = 8f;

    [Tooltip("Menu centre from the left controller (right, up, forward), m")]
    [SerializeField] Vector3 menuOffset = new Vector3(0f, 0.17f, 0.06f);

    [Tooltip("Distance from the centre to each command, m")]
    [SerializeField] float radius = 0.115f;

    [Tooltip("Optional voice lines in VerbalCommand order, empty means subtitles only")]
    [SerializeField] AudioClip[] commandClips;

    [SerializeField] Color itemColor = new Color(0.08f, 0.1f, 0.14f, 0.85f);
    [SerializeField] Color selectedColor = new Color(0.95f, 0.55f, 0.1f, 0.95f);

    // Canvas is laid out in units and scaled down to meters
    const float UnitsPerMeter = 2000f;
    static readonly VerbalCommand[] Commands = (VerbalCommand[])System.Enum.GetValues(typeof(VerbalCommand));

    PlayerRig rig;
    PlayerHealth health;
    PlayerFeedbackOverlay overlay;
    AudioSource voice;
    Transform head;
    Transform anchor;

    RectTransform menuRoot;
    Image[] itemBackgrounds;
    TextMeshProUGUI centerText;

    bool isOpen;
    int selected = -1;
    int picked = -1;
    float selectedSince;
    float openedAt;
    bool waitForCentre;

    public bool IsOpen => isOpen;

    void Awake()
    {
        rig = GetComponent<PlayerRig>();
    }

    void Start()
    {
        if (!PlayerRig.TryGetHead(out head))
        {
            Debug.LogWarning("VerbalCommandMenu: no head camera, commands disabled", this);
            enabled = false;
            return;
        }

        health = GetComponent<PlayerHealth>();
        overlay = GetComponent<PlayerFeedbackOverlay>();
        anchor = rig.GetController(Hand.Left);
        if (anchor == null)
            anchor = head;

        if (commandClips != null && commandClips.Length > 0)
        {
            voice = head.gameObject.AddComponent<AudioSource>();
            voice.spatialBlend = 0f;
            voice.playOnAwake = false;
        }

        Build();
        menuRoot.gameObject.SetActive(false);
    }

    void OnDisable()
    {
        if (isOpen)
            Close();
    }

    void Update()
    {
        // Paused game: nobody is listening
        bool down = (health != null && health.IsIncapacitated) || Time.timeScale <= 0f;

        if (!isOpen)
        {
            if (!down && ControllerInput.GetButtonDown(Hand.Left, ControllerButton.Secondary) && !LeftHandItemUsesY())
                Open();
            return;
        }

        if (down)
        {
            Close();
            return;
        }

        // Y again: changed his mind
        if (ControllerInput.GetButtonDown(Hand.Left, ControllerButton.Secondary))
        {
            Close();
            return;
        }

        UpdateSelection();

        if (picked < 0 && Time.unscaledTime - openedAt > autoCloseAfter)
            Close();
    }

    void LateUpdate()
    {
        if (!isOpen)
            return;

        // Above the left hand, upright and facing the eyes however the wrist turns
        menuRoot.position = anchor == head ? head.TransformPoint(0f, -0.1f, 0.4f) : anchor.TransformPoint(menuOffset);
        Vector3 toMenu = menuRoot.position - head.position;
        if (toMenu.sqrMagnitude > 0.0001f)
            menuRoot.rotation = Quaternion.LookRotation(toMenu, Vector3.up);
    }

    // A held item that needs left Y itself wins over the menu
    bool LeftHandItemUsesY()
    {
        foreach (GrabbableWeapon item in FindObjectsByType<GrabbableWeapon>(FindObjectsSortMode.None))
        {
            if (item.HoldingHand == Hand.Left && item.UsesSecondaryButton)
                return true;
        }
        return false;
    }

    void Open()
    {
        isOpen = true;
        selected = -1;
        picked = -1;
        openedAt = Time.unscaledTime;
        // Opened while walking: the stick has to come back to the centre first, or he'd shout by accident
        waitForCentre = ControllerInput.GetStick(Hand.Left).magnitude >= selectDeadzone;
        // The stick picks commands now, so walking pauses
        LocomotionLock.Acquire();
        menuRoot.gameObject.SetActive(true);
        RefreshHighlight();
        LateUpdate();
    }

    void Close()
    {
        isOpen = false;
        selected = -1;
        picked = -1;
        LocomotionLock.Release();
        if (menuRoot != null)
            menuRoot.gameObject.SetActive(false);
    }

    void UpdateSelection()
    {
        Vector2 stick = ControllerInput.GetStick(Hand.Left);
        if (stick.magnitude < selectDeadzone)
        {
            waitForCentre = false;
            // Stick let go after resting on a command: say it
            if (picked >= 0)
            {
                VerbalCommand command = Commands[picked];
                Close();
                Say(command);
            }
            else if (selected >= 0)
            {
                // Only swept past, nothing picked
                selected = -1;
                RefreshHighlight();
            }
            return;
        }

        if (waitForCentre)
            return;

        // 0 degrees straight up, growing clockwise, same order the items are laid out in
        float angle = Mathf.Atan2(stick.x, stick.y) * Mathf.Rad2Deg;
        if (angle < 0f)
            angle += 360f;
        float sectorSize = 360f / Commands.Length;
        int index = Mathf.RoundToInt(angle / sectorSize) % Commands.Length;

        if (index != selected)
        {
            selected = index;
            selectedSince = Time.unscaledTime;
            picked = -1;
            RefreshHighlight();
            Haptics.MenuTick(Hand.Left);
        }
        else if (picked < 0 && Time.unscaledTime - selectedSince >= holdToPick)
        {
            picked = selected;
        }
    }

    void Say(VerbalCommand command)
    {
        string line = VerbalCommands.Spoken(command);

        if (overlay != null)
            overlay.ShowSubtitle($"\"{line}\"", 2.5f);
        if (voice != null && (int)command < commandClips.Length && commandClips[(int)command] != null)
            voice.PlayOneShot(commandClips[(int)command]);

        TrainingLog.Record(TrainingEventType.VerbalCommand, $"Officer: \"{line}\"");

        // A shout reaches everyone in earshot, not just the one you look at
        Vector3 mouth = head.position;
        foreach (PrisonerPerception prisoner in PrisonerPerception.All)
        {
            if (!prisoner.CanHear(mouth, VerbalCommands.ShoutRadius))
                continue;

            // Shouting also gives away where the officer is
            prisoner.Hear(mouth, fromPlayer: true);

            PrisonerCompliance compliance = PrisonerCompliance.GetOrAdd(prisoner.gameObject);
            compliance.ReceiveCommand(command, Vector3.Distance(prisoner.EyePosition, mouth));
        }
    }

    // UI, built at runtime

    void Build()
    {
        var canvasGo = new GameObject("VerbalCommandMenu", typeof(Canvas));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        menuRoot = canvasGo.GetComponent<RectTransform>();
        float size = (radius * 2f + 0.11f) * UnitsPerMeter;
        menuRoot.sizeDelta = new Vector2(size, size);
        menuRoot.localScale = Vector3.one / UnitsPerMeter;

        Vector2 itemSize = new Vector2(0.105f, 0.036f) * UnitsPerMeter;
        itemBackgrounds = new Image[Commands.Length];

        for (int i = 0; i < Commands.Length; i++)
        {
            // Clockwise from the top
            float a = i * Mathf.PI * 2f / Commands.Length;
            Vector2 pos = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * radius * UnitsPerMeter;

            var itemGo = new GameObject(Commands[i].ToString(), typeof(RectTransform));
            itemGo.transform.SetParent(menuRoot, false);
            var bg = itemGo.AddComponent<Image>();
            bg.sprite = VrUi.RoundedSprite();
            bg.type = Image.Type.Sliced;
            bg.color = itemColor;
            bg.raycastTarget = false;
            bg.rectTransform.sizeDelta = itemSize;
            bg.rectTransform.anchoredPosition = pos;
            itemBackgrounds[i] = bg;

            TextMeshProUGUI text = CreateText(itemGo.transform, VerbalCommands.MenuLabel(Commands[i]), itemSize.y * 0.42f);
            text.rectTransform.sizeDelta = itemSize * 0.94f;
        }

        centerText = CreateText(menuRoot, string.Empty, 0.011f * UnitsPerMeter);
        centerText.rectTransform.sizeDelta = new Vector2(radius * 1.5f, 0.05f) * UnitsPerMeter;
        centerText.color = new Color(1f, 1f, 0.85f);
    }

    static TextMeshProUGUI CreateText(Transform parent, string content, float fontSize)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = content;
        // Long labels shrink to fit instead of spilling out of the box
        text.enableAutoSizing = true;
        text.fontSizeMax = fontSize;
        text.fontSizeMin = fontSize * 0.5f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    void RefreshHighlight()
    {
        for (int i = 0; i < itemBackgrounds.Length; i++)
            itemBackgrounds[i].color = i == selected ? selectedColor : itemColor;

        centerText.text = selected >= 0 ? VerbalCommands.Spoken(Commands[selected]) : "Tilt the stick, let go to say it\nY again to cancel";
    }
}
