using UnityEngine;

public enum BodyZone
{
    Head,
    Torso,
    Arms,
    Legs
}

// Hitbox on a prisoner's bone, a trigger collider: tells which part of the body a probe or a strike landed on
[RequireComponent(typeof(Collider))]
public class HitZone : MonoBehaviour
{
    [SerializeField] BodyZone zone = BodyZone.Torso;

    PrisonerStatusSystem owner;
    Collider area;

    public BodyZone Zone => zone;
    public PrisonerStatusSystem Owner => owner;

    void Awake()
    {
        owner = GetComponentInParent<PrisonerStatusSystem>();
        area = GetComponent<Collider>();
    }

    // His hitbox closest to a point on his body, null when the prefab has none
    public static HitZone Nearest(PrisonerStatusSystem prisoner, Vector3 point)
    {
        HitZone best = null;
        float bestDistance = float.MaxValue;
        foreach (HitZone z in prisoner.GetComponentsInChildren<HitZone>())
        {
            if (z.area == null || !z.area.enabled)
                continue;
            float d = (z.area.ClosestPoint(point) - point).sqrMagnitude;
            if (d < bestDistance)
            {
                bestDistance = d;
                best = z;
            }
        }
        return best;
    }
}
