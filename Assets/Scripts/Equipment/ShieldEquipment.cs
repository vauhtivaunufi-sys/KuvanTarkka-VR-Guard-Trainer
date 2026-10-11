using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;


// Shield implementation. Blocking is physical, like a real shield: a blow is stopped when the shield is held in a hand and its solid
// collider is actually between the prisoner and the spot he is hitting (head or chest). Hold it up in front of you and you are
// covered; let it hang at your side and you are not - there is no on/off button to remember.
// Walking the shield into a non-aggressive prisoner still shoves him (reworked in step 8).

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

        // The block test uses the shield's real shape: every non-trigger collider on it. Triggers are sensing zones, not plate.
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

    // The trigger has no job on a shield - holding it is what matters.
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

        // Collider.Raycast tests only this one collider, so walls, the prisoner's own body or the player's hands can't get in the way.
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

    // Requires a solid (non-trigger) collider on the shield matching its physical shape.
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
