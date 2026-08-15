using UnityEngine;


// Floating two-line label above the prisoner's head so the trainee can see at a glance both the NPC's condition (calm, electrocuted, ...) and
// what it is doing (patrolling or chasing). Creates its own TextMesh at  runtime and always faces the player camera. Can later be replaced wit proper UI — read the same PrisonerStatusSystem / PrisonerAIMovement state.

[RequireComponent(typeof(PrisonerStatusSystem))]
public class PrisonerStatusIndicator : MonoBehaviour
{
    [Tooltip("Extra height above the top of the prisoner's collider, in meters.")]
    [SerializeField] float heightAboveHead = 0.35f;

    [Tooltip("Font size of the label. Bigger = easier to read from afar.")]
    [SerializeField] int fontSize = 64;

    PrisonerStatusSystem status;
    PrisonerAIMovement movement;
    Collider bodyCollider;
    TextMesh label;
    string lastText;

    void Awake()
    {
        status = GetComponent<PrisonerStatusSystem>();
        movement = GetComponent<PrisonerAIMovement>();
        bodyCollider = GetComponent<Collider>();
        CreateLabel();
    }

    void OnDestroy()
    {
        if (label != null)
            Destroy(label.gameObject);
    }

    void LateUpdate()
    {
        if (label == null)
            return;

        label.transform.position = GetLabelPosition();

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            // Face the camera (TextMesh is readable when looking along +Z).
            label.transform.rotation =
                Quaternion.LookRotation(label.transform.position - mainCamera.transform.position);
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

        return movement.IsChasing
            ? "<color=red>CHASING</color>"
            : "<color=grey>PATROLLING</color>";
    }
}
