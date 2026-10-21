using System;
using UnityEngine;


// Detains a prisoner moved in while Electrocuted or InPain, Scared doesn't count or a patrol route would detain him

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
