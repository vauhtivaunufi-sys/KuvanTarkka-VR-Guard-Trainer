using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;


// Shield implementation: passive protection toggled on and off while held, unlike the one-shot Taser/OC Spray actions. While raised, blocks attacks from prisoners in front of it and shoves non-aggressive prisoners the player walks the shield into.

[RequireComponent(typeof(Rigidbody))]
public class ShieldEquipment : GrabbableWeapon
{
    [Tooltip("Whether the shield is currently raised and blocking.")]
    [SerializeField] bool isBlocking;

    [Tooltip("Trigger collider shaped to the shield, used to detect prisoners entering its blocking zone. Is Trigger must be checked.")]
    [SerializeField] BoxCollider blockZone;

    [Tooltip("Angle from the shield's forward direction, in degrees, within which an attack still counts as blocked.")]
    [SerializeField] float blockAngle = 60f;

    [Tooltip("How fast a prisoner is shoved, as a multiplier of the shield's own movement speed.")]
    [SerializeField] float pushSpeedMultiplier = 1.5f;

    class TrackedPrisoner
    {
        public Action Handler;
    }

    readonly Dictionary<PrisonerAttack, TrackedPrisoner> trackedPrisoners = new();

    Rigidbody rb;
    Vector3 previousPosition;
    Vector3 currentVelocity;

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody>();

        if (blockZone != null)
            blockZone.isTrigger = true;
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

    protected override void OnUse()
    {
        isBlocking = !isBlocking;
    }

    void OnTriggerEnter(Collider other)
    {
        PrisonerAttack attack = other.GetComponentInParent<PrisonerAttack>();
        if (attack == null || trackedPrisoners.ContainsKey(attack))
            return;

        Action handler = () => HandleAttack(attack);
        trackedPrisoners.Add(attack, new TrackedPrisoner { Handler = handler });
        attack.OnAttack += handler;
    }

    void OnTriggerExit(Collider other)
    {
        PrisonerAttack attack = other.GetComponentInParent<PrisonerAttack>();
        if (attack == null || !trackedPrisoners.TryGetValue(attack, out TrackedPrisoner tracked))
            return;

        attack.OnAttack -= tracked.Handler;
        trackedPrisoners.Remove(attack);
    }

    void HandleAttack(PrisonerAttack attack)
    {
        if (isBlocking && IsInFrontOfShield(attack.transform.position))
        {
            Debug.Log("Shield blocked an attack", this);
            return;
        }

        Debug.Log("Attack got through - shield was not blocking it", this);
    }

    bool IsInFrontOfShield(Vector3 attackerPosition)
    {
        Vector3 toAttacker = attackerPosition - transform.position;
        return Vector3.Angle(transform.forward, toAttacker) <= blockAngle;
    }

    // Requires a solid (non-trigger) collider on the shield matching its
    // physical shape, separate from the blockZone trigger above.
    void OnCollisionStay(Collision collision)
    {
        PrisonerStatusSystem status = collision.collider.GetComponentInParent<PrisonerStatusSystem>();
        if (status == null || !ShouldPush(status))
            return;

        // Move the NavMeshAgent instead of pushing the Rigidbody: the agent
        // owns the transform and cancels physics forces, and Move keeps the
        // prisoner on the NavMesh instead of shoving it through walls.
        NavMeshAgent agent = status.GetComponent<NavMeshAgent>();
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        // Push along the direction the shield is moving, not outwards from
        // its center - so the prisoner can be steered sideways too, not only
        // straight away from the player.
        Vector3 push = currentVelocity;
        push.y = 0f;
        if (push.sqrMagnitude < 0.0001f)
            return;

        agent.Move(push * pushSpeedMultiplier * Time.fixedDeltaTime);
    }

    // Push only affects prisoners that are not currently aggressive: an attacking prisoner should be blocked, not shoved aside.
    bool ShouldPush(PrisonerStatusSystem status)
    {
        return status.CurrentEmotional == PrisonerEmotional.Scared ||
               status.CurrentConditional == PrisonerConditional.Electrocuted ||
               status.CurrentConditional == PrisonerConditional.InPain;
    }
}
