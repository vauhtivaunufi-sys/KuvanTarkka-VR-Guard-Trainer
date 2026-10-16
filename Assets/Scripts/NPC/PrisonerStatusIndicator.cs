using TMPro;
using UnityEngine;

// Debug label above the prisoner, hidden by the debug switch (TrainingDebug)
[RequireComponent(typeof(PrisonerStatusSystem))]
public class PrisonerStatusIndicator : MonoBehaviour
{
    [Tooltip("Gap above the top of the collider, m")]
    [SerializeField] float heightAboveHead = 0.35f;

    [Tooltip("Line height up close, m")]
    [SerializeField] float textHeight = 0.06f;

    [Tooltip("From this distance the label grows to keep the same size on screen, m")]
    [SerializeField] float constantSizeFrom = 2.5f;

    [Tooltip("Largest growth factor at long range")]
    [SerializeField] float maxScale = 6f;

    PrisonerStatusSystem status;
    PrisonerAIMovement movement;
    PrisonerPerception perception;
    PrisonerCompliance compliance;
    Collider bodyCollider;
    TextMeshPro label;
    string lastText;

    void Awake()
    {
        status = GetComponent<PrisonerStatusSystem>();
        movement = GetComponent<PrisonerAIMovement>();
        perception = PrisonerPerception.GetOrAdd(gameObject);
        compliance = PrisonerCompliance.GetOrAdd(gameObject);
        bodyCollider = GetComponent<Collider>();
        CreateLabel();
    }

    void OnEnable()
    {
        TrainingDebug.Changed += HandleDebugChanged;
        HandleDebugChanged(TrainingDebug.Enabled);
    }

    void OnDisable()
    {
        TrainingDebug.Changed -= HandleDebugChanged;
    }

    void HandleDebugChanged(bool show)
    {
        if (label != null)
            label.gameObject.SetActive(show);
    }

    void OnDestroy()
    {
        if (label != null)
            Destroy(label.gameObject);
    }

    void LateUpdate()
    {
        if (label == null || !TrainingDebug.Enabled)
            return;

        Transform t = label.transform;
        t.position = GetLabelPosition();

        if (PlayerRig.TryGetHead(out Transform head))
        {
            Vector3 fromHead = t.position - head.position;
            float distance = fromHead.magnitude;

            // Face the eyes, stay upright
            if (distance > 0.01f)
                t.rotation = Quaternion.LookRotation(fromHead, Vector3.up);

            // Grows with distance so far labels stay readable
            t.localScale = Vector3.one * Mathf.Clamp(distance / constantSizeFrom, 1f, maxScale);
        }

        UpdateText();
    }

    void CreateLabel()
    {
        // Standalone object, so the prisoner's scale doesn't distort the text
        var labelObject = new GameObject($"{name} StatusLabel", typeof(RectTransform));
        label = labelObject.AddComponent<TextMeshPro>();
        label.rectTransform.pivot = new Vector2(0.5f, 0f);
        label.rectTransform.sizeDelta = new Vector2(2f, 0.5f);
        label.alignment = TextAlignmentOptions.Bottom;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        // 3D TextMeshPro: font size 1 is 0.1 m
        label.fontSize = textHeight * 10f;
        label.fontStyle = FontStyles.Bold;
        label.outlineWidth = 0.25f;
        label.outlineColor = new Color32(0, 0, 0, 255);
        label.richText = true;
        labelObject.transform.position = GetLabelPosition();
    }

    Vector3 GetLabelPosition()
    {
        float top = bodyCollider != null ? bodyCollider.bounds.max.y : transform.position.y;
        return new Vector3(transform.position.x, top + heightAboveHead, transform.position.z);
    }

    void UpdateText()
    {
        string text = BuildStatusLine();
        string movementLine = BuildMovementLine();
        if (movementLine != null)
            text += "\n" + movementLine;
        string moodLine = BuildMoodLine();
        if (moodLine != null)
            text += "\n" + moodLine;

        if (text == lastText)
            return;

        lastText = text;
        // Dark backing behind the letters for contrast on bright walls
        label.text = $"<mark=#000000A0>{text}</mark>";
    }

    // Hex colors: TextMeshPro knows only a few color names
    string BuildStatusLine()
    {
        switch (status.CurrentConditional)
        {
            case PrisonerConditional.Electrocuted:
                return "<color=#FFE24D>ELECTROCUTED</color>";

            case PrisonerConditional.InPain:
                return "<color=#FF9A2E>IN PAIN</color>";

            case PrisonerConditional.Detained:
                return "<color=#4CE06A>DETAINED</color>";
        }

        switch (status.CurrentRestraint)
        {
            case PrisonerRestraint.Submitting:
                return "<color=#4DD8FF>SUBMITTING</color>";

            case PrisonerRestraint.Cuffed:
                return "<color=#4CE06A>CUFFED</color>";
        }

        return status.CurrentEmotional == PrisonerEmotional.Scared
            ? "<color=#4DD8FF>SCARED</color>"
            : "<color=#FFFFFF>FREE</color>";
    }

    // What the AI is doing: patrolling, chasing, confronting
    string BuildMovementLine()
    {
        if (movement == null)
            return null;

        if (!movement.IsActive)
            return "<color=#C8C8C8>IN CELL</color>";

        string line = $"<color=#C8C8C8>{movement.CurrentMode.ToString().ToUpperInvariant()}</color>";
        if (perception != null && perception.CanSeePlayer)
            line += " <color=#FF4D4D>(sees you)</color>";
        return line;
    }

    string BuildMoodLine()
    {
        if (compliance == null)
            return null;

        string mood = compliance.IsHostile ? "<color=#FF4D4D>HOSTILE</color>"
                    : compliance.IsAgitated ? "<color=#FF9A2E>AGITATED</color>"
                    : "<color=#FFFFFF>CALM</color>";
        return $"{mood} {compliance.Aggression:0.00}";
    }
}
