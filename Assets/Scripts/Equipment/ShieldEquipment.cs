using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;


// Shield blocks physically: a blow stops only if the held shield's collider is between the prisoner and the target

[RequireComponent(typeof(Rigidbody))]
public class ShieldEquipment : GrabbableWeapon, IMeleeBlocker
{
    [Tooltip("How fast a prisoner is shoved, as a multiplier of the shield's own movement speed.")]
    [SerializeField] float pushSpeedMultiplier = 1.5f;

    Rigidbody rb;
    Collider[] solidColliders;
    Vector3 previousPosition;
    Vector3 currentVelocity;

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody>();

        // Blocking uses every non-trigger collider on the shield, triggers are sensing zones
        var solids = new List<Collider>();
        foreach (Collider c in GetComponentsInChildren<Collider>(true))
        {
            if (!c.isTrigger)
                solids.Add(c);
        }
        solidColliders = solids.ToArray();

        if (solidColliders.Length == 0)
            Debug.LogWarning($"Shield '{name}' has no solid (non-trigger) collider - it can never block a blow.", this);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        PlayerHealth.RegisterBlocker(this);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        PlayerHealth.UnregisterBlocker(this);
    }

    void Start()
    {
        previousPosition = rb.position;
    }

    void FixedUpdate()
    {
        currentVelocity = (rb.position - previousPosition) / Time.fixedDeltaTime;
        previousPosition = rb.position;
    }

    // The trigger has no job on a shield - holding it is what matters
    protected override void OnUse()
    {
    }

    // IMeleeBlocker: is the shield, held in a hand, on the line from the attacker's fist to where he is hitting?
    public bool TryBlock(Vector3 attackOrigin, Vector3 target)
    {
        if (!IsHeld)
            return false;

        Vector3 toTarget = target - attackOrigin;
        float distance = toTarget.magnitude;
        if (distance < 0.001f)
            return false;

        // Tests only this collider, so walls, bodies and hands can't get in the way
        var ray = new Ray(attackOrigin, toTarget / distance);
        foreach (Collider c in solidColliders)
        {
            if (c != null && c.enabled && c.Raycast(ray, out _, distance))
            {
                Haptics.ShieldBlock(HoldingHand.Value);
                return true;
            }
        }
        return false;
    }

    // Requires a solid (non-trigger) collider on the shield matching its physical shape
    void OnCollisionStay(Collision collision)
    {
        PrisonerStatusSystem status = collision.collider.GetComponentInParent<PrisonerStatusSystem>();
        if (status == null || !ShouldPush(status))
            return;

        // Move the agent, not the Rigidbody, so the prisoner stays on the NavMesh
        NavMeshAgent agent = status.GetComponent<NavMeshAgent>();
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        // Push along the shield's movement so the prisoner can be steered sideways
        Vector3 push = currentVelocity;
        push.y = 0f;
        if (push.sqrMagnitude < 0.0001f)
            return;

        agent.Move(push * pushSpeedMultiplier * Time.fixedDeltaTime);
    }

    // Only non-aggressive prisoners get pushed, an attacker is blocked instead
    bool ShouldPush(PrisonerStatusSystem status)
    {
        return status.CurrentEmotional == PrisonerEmotional.Scared ||
               status.CurrentConditional == PrisonerConditional.Electrocuted ||
               status.CurrentConditional == PrisonerConditional.InPain;
    }
}
