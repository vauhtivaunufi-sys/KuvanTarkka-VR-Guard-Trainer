using UnityEngine;


// Wakes this cell's prisoner when the door opens, only on cell doors

[RequireComponent(typeof(Door))]
public class CellDoorLink : MonoBehaviour
{
    [Tooltip("The prisoner housed in this cell, woken once the door opens.")]
    [SerializeField] PrisonerAIMovement prisoner;

    Door door;

    public PrisonerAIMovement Prisoner => prisoner;

    void Awake()
    {
        door = GetComponent<Door>();

        if (!door.IsCellDoor)
        {
            Debug.LogWarning($"CellDoorLink on '{name}' is attached to a door that isn't marked as a cell door - remove this component, it has no effect here.", this);
            enabled = false;
        }
    }

    void OnEnable()
    {
        door.OnDoorOpened += HandleDoorOpened;
    }

    void OnDisable()
    {
        door.OnDoorOpened -= HandleDoorOpened;
    }

    void HandleDoorOpened()
    {
        if (prisoner != null)
            prisoner.Activate();
    }
}
