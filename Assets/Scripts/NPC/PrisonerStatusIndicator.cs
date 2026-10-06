using UnityEngine;


// DEBUG label above the prisoner's head: his condition (calm, electrocuted, ...) and what he is doing (patrolling, chasing, and
// whether he currently sees the player). Hidden together with every other debug aid by one switch - Tools > KuvanTarkka >
// Debug Overlays in the editor, or holding the left menu button in the headset (see TrainingDebug).

[RequireComponent(typeof(PrisonerStatusSystem))]
public class PrisonerStatusIndicator : MonoBehaviour
{
    [Tooltip("Extra height above the top of the prisoner's collider, in meters.")]
    [SerializeField] float heightAboveHead = 0.35f;

    [Tooltip("Font size of the label. Bigger = easier to read from afar.")]
    [SerializeField] int fontSize = 64;

    PrisonerStatusSystem status;
    PrisonerAIMovement movement;
    PrisonerPerception perception;
    Collider bodyCollider;
    TextMesh label;
    string lastText;

    void Awake()
    {
        status = GetComponent<PrisonerStatusSystem>();
        movement = GetComponent<PrisonerAIMovement>();
        perception = GetComponent<PrisonerPerception>();
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

        label.transform.position = GetLabelPosition();

        if (PlayerRig.TryGetHead(out Transform head))
        {
            // Face the camera (TextMesh is readable when looking along +Z).
            label.transform.rotation = Quaternion.LookRotation(label.transform.position - head.position);
        }

        UpdateText();
    }

    void CreateLabel()
    {
        // A standalone object, not a child: the prisoner's transform scale
        // would distort the text otherwise.
        GameObject labelObject = new GameObject($"{name} StatusLabel");
        label = labelObject.AddComponent<TextMesh>();
        label.anchor = TextAnchor.LowerCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = fontSize;
        label.characterSize = 0.005f;
        label.richText = true;
        labelObject.transform.position = GetLabelPosition();
    }

    Vector3 GetLabelPosition()
    {
        float top = bodyCollider != null
            ? bodyCollider.bounds.max.y
            : transform.position.y;
        return new Vector3(transform.position.x, top + heightAboveHead, transform.position.z);
    }

    void UpdateText()
    {
        string text = BuildStatusLine();
        string movementLine = BuildMovementLine();
        if (movementLine != null)
            text += "\n" + movementLine;

        if (text == lastText)
            return;

        lastText = text;
        label.text = text;
    }

    string BuildStatusLine()
    {
        switch (status.CurrentConditional)
        {
            case PrisonerConditional.Electrocuted:
                return "<color=yellow>ELECTROCUTED</color>";

            case PrisonerConditional.InPain:
                return "<color=orange>IN PAIN</color>";

            case PrisonerConditional.Detained:
                return "<color=green>DETAINED</color>";
        }

        return status.CurrentEmotional == PrisonerEmotional.Scared
            ? "<color=cyan>SCARED</color>"
            : "<color=white>CALM</color>";
    }

    string BuildMovementLine()
    {
        // Frozen by an effect or detained: there is no movement to show.
        if (movement == null || !movement.enabled ||
            status.CurrentConditional != PrisonerConditional.Idle)
            return null;

        if (!movement.IsActive)
            return "<color=grey>IN CELL</color>";

        string line = movement.IsChasing
            ? "<color=red>CHASING</color>"
            : "<color=grey>PATROLLING</color>";
        if (perception != null && perception.CanSeePlayer)
            line += " <color=red>(sees you)</color>";
        return line;
    }
}
