using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Hover and select filter for XRGrabInteractable: the officer has to stand within reach to pick the item up
public class PickUpRange : MonoBehaviour, IXRHoverFilter, IXRSelectFilter
{
    [Tooltip("How close the officer has to stand, measured along the floor from his head to the item, m")]
    [SerializeField] float radius = 1f;

    Collider[] colliders;

    public bool canProcess => isActiveAndEnabled;

    void Awake()
    {
        colliders = GetComponentsInChildren<Collider>();
    }

    public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable)
    {
        return InReach(interactor);
    }

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        return InReach(interactor);
    }

    bool InReach(IXRInteractor interactor)
    {
        // A holster putting the item away isn't the officer reaching for it
        if (interactor is XRSocketInteractor)
            return true;

        PlayerRig rig = PlayerRig.Instance;
        if (rig == null)
            return true;

        return FlatDistance(rig.HeadPosition) <= radius;
    }

    // Distance on the floor plane to the nearest edge of the item, so height doesn't matter
    float FlatDistance(Vector3 from)
    {
        float best = float.MaxValue;
        foreach (Collider c in colliders)
        {
            if (c == null || !c.enabled)
                continue;
            Vector3 p = c.bounds.ClosestPoint(from);
            best = Mathf.Min(best, new Vector2(p.x - from.x, p.z - from.z).magnitude);
        }

        if (best == float.MaxValue)
            best = new Vector2(transform.position.x - from.x, transform.position.z - from.z).magnitude;
        return best;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
        Vector3 centre = transform.position;
        const int segments = 32;
        Vector3 previous = centre + Vector3.forward * radius;
        for (int i = 1; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            Vector3 next = centre + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
