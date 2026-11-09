using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;


// Base for handheld equipment: calls OnUse on trigger when the cooldown allows and tracks which hand holds it

[RequireComponent(typeof(XRGrabInteractable))]
public abstract class GrabbableWeapon : MonoBehaviour
{
    [Tooltip("Point the weapon acts from (beam origin, spray nozzle, etc.).")]
    [SerializeField] protected Transform usePoint;

    [Tooltip("Minimum time in seconds between two uses.")]
    [SerializeField] float cooldownDuration = 2f;

    [Tooltip("Where the hand closes on the item: up runs along the handle, forward where the item points. Empty = the grab attach point")]
    [SerializeField] Transform handGrip;

    [Tooltip("How far the handle leans, degrees, the bottom of a pistol grip sits behind its top")]
    [SerializeField] float handleRake;

    [Tooltip("Name in the training log, empty uses the object's name")]
    [SerializeField] string itemName;

    [Tooltip("An item has to move at least this fast for a hit on a prisoner to count as a strike, m/s")]
    [SerializeField] float strikeSpeed = 1.5f;

    [Tooltip("A thrown item still counts as the officer's strike for this long after leaving the hand, s")]
    [SerializeField] float thrownStrikeTime = 2f;

    static readonly List<GrabbableWeapon> all = new();

    XRGrabInteractable grabInteractable;
    float lastUseTime = Mathf.NegativeInfinity;
    float releasedTime = Mathf.NegativeInfinity;
    Vector3 lastPosition;

    public string ItemName => string.IsNullOrEmpty(itemName) ? name : itemName;

    // How fast the item moves, measured every physics step whether held, thrown or lying still
    protected Vector3 Velocity { get; private set; }

    // The hand holding the item, or null when it lies somewhere / sits in a holster
    public Hand? HoldingHand { get; private set; }

    public bool IsHeld => HoldingHand.HasValue;

    // Items that use B/Y themselves (the taser's arc later) keep the command menu off that hand
    public virtual bool UsesSecondaryButton => false;

    // Statics survive between Play sessions when domain reload is off
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        all.Clear();
    }

    // The item in that hand, or null, without searching the scene
    public static GrabbableWeapon HeldIn(Hand hand)
    {
        foreach (GrabbableWeapon item in all)
        {
            if (item.HoldingHand == hand)
                return item;
        }
        return null;
    }

    protected virtual void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        lastPosition = transform.position;
    }

    protected virtual void FixedUpdate()
    {
        Velocity = (transform.position - lastPosition) / Time.fixedDeltaTime;
        lastPosition = transform.position;
    }

    // A moving item touching a prisoner's hitbox: a strike, held or just thrown
    void OnTriggerEnter(Collider other)
    {
        HitZone zone = other.GetComponent<HitZone>();
        if (zone == null || zone.Owner == null)
            return;

        // Lying on the floor or dropped long ago it hits nobody
        bool thrown = !IsHeld && Time.time - releasedTime <= thrownStrikeTime;
        if (!IsHeld && !thrown)
            return;

        float speed = Velocity.magnitude;
        if (speed < strikeSpeed)
            return;

        zone.Owner.RegisterHit(zone.Zone, $"{ItemName} {(thrown ? "thrown" : "strike")} at {speed:0.0} m/s", ForceKind.Impact);
    }

    // The handle the hand closes on, null when the item never said where it is held
    Transform GripPoint => handGrip != null ? handGrip : grabInteractable.attachTransform;

    // Handle centre and axes in world space, so the hand can wrap round it
    public bool TryGetHandGrip(out Vector3 centre, out Vector3 handleUp, out Vector3 forward)
    {
        Transform grip = GripPoint;
        if (grip == null)
        {
            centre = handleUp = forward = Vector3.zero;
            return false;
        }

        // Rake turns the handle's top towards the front
        Quaternion rake = Quaternion.AngleAxis(handleRake, grip.right);
        centre = grip.position;
        handleUp = rake * grip.up;
        forward = rake * grip.forward;
        return true;
    }

    protected virtual void OnEnable()
    {
        all.Add(this);
        grabInteractable.activated.AddListener(HandleActivated);
        grabInteractable.deactivated.AddListener(HandleDeactivated);
        grabInteractable.selectEntered.AddListener(HandleSelectEntered);
        grabInteractable.selectExited.AddListener(HandleSelectExited);
    }

    protected virtual void OnDisable()
    {
        all.Remove(this);
        grabInteractable.activated.RemoveListener(HandleActivated);
        grabInteractable.deactivated.RemoveListener(HandleDeactivated);
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

    void HandleDeactivated(DeactivateEventArgs args)
    {
        OnUseEnded();
    }

    void HandleSelectEntered(SelectEnterEventArgs args)
    {
        // A socket (holster) "selects" the item too - that is storage, not a hand
        if (args.interactorObject is XRSocketInteractor)
            return;

        HoldingHand = args.interactorObject.handedness == InteractorHandedness.Left ? Hand.Left : Hand.Right;
    }

    void HandleSelectExited(SelectExitEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor)
            return;

        HoldingHand = null;
        releasedTime = Time.time;
        // Dropped with the trigger still down
        OnUseEnded();
    }

    // Weapon-specific behaviour, executed once per allowed trigger press
    protected abstract void OnUse();

    // Trigger let go or the item dropped, for items that act while the trigger is held
    protected virtual void OnUseEnded() { }
}
