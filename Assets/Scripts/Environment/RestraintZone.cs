using System;
using UnityEngine;


// Trigger zone  that detains prisoners who are walked into it while not aggressive - Scared, Electrocuted, or InPain. Same non-aggressive check as ShieldEquipment.ShouldPush; an aggressive prisoner passing through is left alone.

[RequireComponent(typeof(Collider))]
public class RestraintZone : MonoBehaviour
{
    [Tooltip("Placeholder visual marking the zone on the floor - a colored quad or outline until art replaces it.")]
    [SerializeField] GameObject visualMarker;

    // Raised whenever a prisoner is detained by this zone.
    public event Action OnPrisonerDetained;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        PrisonerStatusSystem status = other.GetComponentInParent<PrisonerStatusSystem>();
        if (status == null || !IsDetainable(status))
            return;

        status.Detain();
        OnPrisonerDetained?.Invoke();
    }

    bool IsDetainable(PrisonerStatusSystem status)
    {
        return status.CurrentEmotional == PrisonerEmotional.Scared ||
               status.CurrentConditional == PrisonerConditional.Electrocuted ||
               status.CurrentConditional == PrisonerConditional.InPain;
    }
}
