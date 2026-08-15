using System;
using System.Collections.Generic;
using UnityEngine;


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

    [Tooltip("Force applied per m/s of shield movement speed when pushing a prisoner.")]
    [SerializeField] float pushForcePerSpeed = 20f;

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
        Rigidbody prisonerBody = collision.rigidbody;
        if (prisonerBody == null)
            return;

        PrisonerStatusSystem status = collision.collider.GetComponentInParent<PrisonerStatusSystem>();
        if (status == null || !ShouldPush(status))
            return;

        Vector3 pushDirection = prisonerBody.position - rb.position;
        pushDirection.y = 0f;
        pushDirection.Normalize();

        prisonerBody.AddForce(pushDirection * currentVelocity.magnitude * pushForcePerSpeed, ForceMode.Force);
    }

    // Push only affects prisoners that are not currently aggressive: an attacking prisoner should be blocked, not shoved aside.
    bool ShouldPush(PrisonerStatusSystem status)
    {
        return status.CurrentEmotional == PrisonerEmotional.Scared ||
               status.CurrentConditional == PrisonerConditional.Electrocuted ||
               status.CurrentConditional == PrisonerConditional.InPain;
    }
}
