using System;
using UnityEngine;

// Prisoner's punch: swings only when free, wants to fight, sees the officer and has him in reach
// The blow is resolved when it lands, so stepping back or raising the shield in time avoids it
[RequireComponent(typeof(PrisonerStatusSystem))]
public class PrisonerAttack : MonoBehaviour
{
    [Tooltip("Horizontal distance to the player at which the prisoner attacks, in meters.")]
    [SerializeField] float attackRange = 1.5f;

    [Tooltip("Minimum time between attacks, in seconds. Keep above Attack Duration, or the next swing cuts the previous clip short.")]
    [SerializeField] float attackCooldown = 3f;

    [Tooltip("How long a swing roots the prisoner in place, in seconds. Match to the attack clip length.")]
    [SerializeField] float attackDuration = 2.4f;

    [Tooltip("Seconds into the swing at which the fist lands. Match to the impact frame of the attack clip.")]
    [SerializeField] float hitTime = 0.9f;

    [Tooltip("How far the blow reaches when it lands, in meters. A bit more than Attack Range, since he steps into the punch.")]
    [SerializeField] float hitReach = 1.8f;

    [Tooltip("Damage of one blow. The officer has 100 health.")]
    [SerializeField] float damage = 20f;

    // Raised every time the prisoner performs an attack
    public event Action OnAttack;

    // True while a swing is playing out
    public bool IsAttacking => Time.time < attackEndTime;

    // Close enough to swing at an officer he can see
    public bool IsInReach => perception.CanSeePlayer && HorizontalDistanceToPlayer() <= attackRange;

    public float AttackRange => attackRange;

    PrisonerStatusSystem status;
    PrisonerAIMovement movement;
    PrisonerPerception perception;
    PrisonerCompliance compliance;
    PlayerHealth playerHealth;
    float lastAttackTime = float.NegativeInfinity;
    float attackEndTime = float.NegativeInfinity;
    float pendingHitTime;
    bool hitPending;

    void Awake()
    {
        status = GetComponent<PrisonerStatusSystem>();
        movement = GetComponent<PrisonerAIMovement>();
        perception = PrisonerPerception.GetOrAdd(gameObject);
        compliance = PrisonerCompliance.GetOrAdd(gameObject);
    }

    void Update()
    {
        // Tased, sprayed, submitting or cuffed: a blow that hasn't landed yet never lands
        if (status.CurrentConditional != PrisonerConditional.Idle || status.CurrentRestraint != PrisonerRestraint.Free)
        {
            CancelSwing();
            return;
        }

        // Still asleep in his cell: no attacks, and clear any half-finished swing
        if (movement != null && !movement.IsActive)
        {
            CancelSwing();
            return;
        }

        PlayerRig rig = PlayerRig.Instance;
        if (rig == null)
            return;

        if (hitPending && Time.time >= pendingHitTime)
        {
            hitPending = false;
            ResolveHit(rig);
        }

        if (playerHealth == null)
            playerHealth = PlayerHealth.GetOrAdd(rig);
        // Officer already down: stop hitting him
        if (playerHealth.IsIncapacitated)
            return;

        if (IsAttacking || Time.time - lastAttackTime < attackCooldown)
            return;

        if (compliance.WantsToAttack && IsInReach)
            Attack();
    }

    void Attack()
    {
        lastAttackTime = Time.time;
        attackEndTime = Time.time + attackDuration;
        pendingHitTime = Time.time + hitTime;
        hitPending = true;
        OnAttack?.Invoke();
    }

    void CancelSwing()
    {
        attackEndTime = float.NegativeInfinity;
        hitPending = false;
    }

    // The moment the fist arrives: is the officer still in reach and in front of him
    void ResolveHit(PlayerRig rig)
    {
        Vector3 toPlayer = rig.FeetPosition - transform.position;
        toPlayer.y = 0f;
        bool inFront = Vector3.Angle(transform.forward, toPlayer) <= 70f;

        if (toPlayer.magnitude > hitReach || !inFront)
        {
            TrainingDebug.Log($"{name} swung and missed - the officer was out of reach", this);
            return;
        }

        // Fist at roughly shoulder height
        Vector3 fist = transform.position + Vector3.up * 1.35f + transform.forward * 0.3f;
        if (playerHealth == null)
            playerHealth = PlayerHealth.GetOrAdd(rig);
        playerHealth.ReceiveMeleeAttack(fist, name, damage);
    }

    float HorizontalDistanceToPlayer()
    {
        PlayerRig rig = PlayerRig.Instance;
        if (rig == null)
            return float.PositiveInfinity;
        // Horizontal only: the head is 1.7 m up, a 3D distance would never fit a 1.5 m range
        Vector3 offset = rig.FeetPosition - transform.position;
        offset.y = 0f;
        return offset.magnitude;
    }
}
