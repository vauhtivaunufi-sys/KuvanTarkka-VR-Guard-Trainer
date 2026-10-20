using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;


// Base class for all handheld equipment (OC Spray, Taser, Shield).
// Listens to the XRGrabInteractable activate action (trigger press while held) and calls <see cref="OnUse"/> when the cooldown allows it.
// Also tracks which hand holds the item (a belt holster later does not count as a hand), so equipment can vibrate the right controller.

[RequireComponent(typeof(XRGrabInteractable))]
public abstract class GrabbableWeapon : MonoBehaviour
{
    [Tooltip("Point the weapon acts from (beam origin, spray nozzle, etc.).")]
    [SerializeField] protected Transform usePoint;

    [Tooltip("Minimum time in seconds between two uses.")]
    [SerializeField] float cooldownDuration = 2f;

    XRGrabInteractable grabInteractable;
    float lastUseTime = Mathf.NegativeInfinity;

    // The hand holding the item, or null when it lies somewhere / sits in a holster.
    public Hand? HoldingHand { get; private set; }

    public bool IsHeld => HoldingHand.HasValue;

    // Items that use B/Y themselves (the taser's arc later) keep the command menu off that hand
    public virtual bool UsesSecondaryButton => false;

    protected virtual void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    protected virtual void OnEnable()
    {
        grabInteractable.activated.AddListener(HandleActivated);
        grabInteractable.selectEntered.AddListener(HandleSelectEntered);
        grabInteractable.selectExited.AddListener(HandleSelectExited);
    }

    protected virtual void OnDisable()
    {
        grabInteractable.activated.RemoveListener(HandleActivated);
        grabInteractable.selectEntered.RemoveListener(HandleSelectEntered);
        grabInteractable.selectExited.RemoveListener(HandleSelectExited);
    }

    void HandleActivated(ActivateEventArgs args)
    {
        // Paused game: the trigger is for the menu
        if (Time.timeScale <= 0f || Time.time - lastUseTime < cooldownDuration)
            return;

        lastUseTime = Time.time;
        OnUse();
    }

    void HandleSelectEntered(SelectEnterEventArgs args)
    {
        // A socket (holster) "selects" the item too - that is storage, not a hand.
        if (args.interactorObject is XRSocketInteractor)
            return;

        HoldingHand = args.interactorObject.handedness == InteractorHandedness.Left ? Hand.Left : Hand.Right;
    }

    void HandleSelectExited(SelectExitEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor)
            return;

        HoldingHand = null;
    }

    // Weapon-specific behaviour, executed once per allowed trigger press.
    protected abstract void OnUse();
}
