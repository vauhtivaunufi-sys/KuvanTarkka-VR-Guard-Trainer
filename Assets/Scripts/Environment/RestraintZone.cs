using System;
using UnityEngine;


// Trigger zone that detains a prisoner who is moved into it while disabled - Electrocuted or InPain (in practice: pushed in with the shield).
// Scared does NOT count any more: a scared prisoner keeps patrolling, so a patrol route crossing the zone made him "detain himself".
// Interim rule until handcuffs and escort replace this zone in step 9.

[RequireComponent(typeof(Collider))]
public class RestraintZone : MonoBehaviour
{
    [Tooltip("Placeholder visual marking the zone on the floor - a colored quad or outline until art replaces it.")]
    [SerializeField] GameObject visualMarker;

    // Raised whenever a prisoner is detained by this zone
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
        return status.CurrentConditional == PrisonerConditional.Electrocuted ||
               status.CurrentConditional == PrisonerConditional.InPain;
    }
}
