using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;


// Base class for all handheld equipment (OC Spray, Taser, Shield).
// Listens to the XRGrabInteractable activate action (trigger press while held) and calls <see cref="OnUse"/> when the cooldown allows it.

[RequireComponent(typeof(XRGrabInteractable))]
public abstract class GrabbableWeapon : MonoBehaviour
{
    [Tooltip("Point the weapon acts from (beam origin, spray nozzle, etc.).")]
    [SerializeField] protected Transform usePoint;

    [Tooltip("Minimum time in seconds between two uses.")]
    [SerializeField] float cooldownDuration = 2f;

    XRGrabInteractable grabInteractable;
    float lastUseTime = Mathf.NegativeInfinity;

    protected virtual void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    protected virtual void OnEnable()
    {
        grabInteractable.activated.AddListener(HandleActivated);
    }

    protected virtual void OnDisable()
    {
        grabInteractable.activated.RemoveListener(HandleActivated);
    }

    void HandleActivated(ActivateEventArgs args)
    {
        if (Time.time - lastUseTime < cooldownDuration)
            return;

        lastUseTime = Time.time;
        OnUse();
    }

    // Weapon-specific behaviour, executed once per allowed trigger press.
    protected abstract void OnUse();
}
