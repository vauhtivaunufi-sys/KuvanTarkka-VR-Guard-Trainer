using System;
using UnityEngine;
using UnityEngine.AI;


// Melee attack behaviour for prisoner NPCs (M11): attacks the player when close enough, unless under an equipment effect, detained or scared.
// The blow is resolved when it LANDS (hitTime into the swing), not when the swing starts: stepping back out of reach or getting the
// shield between you and him in time avoids it - exactly what a trainee should learn to do. Damage goes to PlayerHealth.
// Only once he is out of his cell (PrisonerAIMovement.IsActive) and only at a player he can actually see (PrisonerPerception) - so no more swings from inside a closed cell or through a door.
// Stops the NavMeshAgent while the player is within attack range so the attack does not fight with patrol movement, and keeps it stopped
// until the swing has played out - otherwise backing out of range mid-swing hands the agent straight back to the chase and the prisoner
// glides after the player in his attack pose, with no walk cycle under him.

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

    // True while a swing is still playing out. PrisonerAnimationController holds the Attack animation state for exactly as long as this is true.
    public bool IsAttacking => Time.time < attackEndTime;

    PrisonerStatusSystem status;
    PrisonerAIMovement movement;
    PrisonerPerception perception;
    NavMeshAgent agent;
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
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        // While electrocuted, in pain or detained, PrisonerAIMovement owns agent.isStopped; touch nothing so the two scripts don't conflict.
        if (status.CurrentConditional != PrisonerConditional.Idle)
        {
            // Getting tased or sprayed cancels the swing. Clear it, or the swing outlives the interrupt and leaves him planted
            // once he recovers - and a blow that hasn't landed yet never lands.
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

        // Officer already down: stop hitting him.
        if (playerHealth == null)
            playerHealth = PlayerHealth.GetOrAdd(rig);
        if (playerHealth != null && playerHealth.IsIncapacitated)
            return;

        // Scared prisoners never attack and keep patrolling instead. A player he can't see (wall, closed door) can't be hit.
        bool canSee = perception == null || perception.CanSeePlayer;
        bool inRange = status.CurrentEmotional != PrisonerEmotional.Scared && canSee &&
                       HorizontalDistanceToPlayer() <= attackRange;

        if (agent != null && agent.isOnNavMesh)
            agent.isStopped = inRange || IsAttacking;

        if (inRange && Time.time - lastAttackTime >= attackCooldown)
            Attack();
    }

    void Attack()
    {
        lastAttackTime = Time.time;
        attackEndTime = Time.time + attackDuration;
        pendingHitTime = Time.time + hitTime;
        hitPending = true;

        // isStopped only cuts the agent's throttle, it does not clear momentum
        if (agent != null && agent.isOnNavMesh)
            agent.velocity = Vector3.zero;

        OnAttack?.Invoke();
    }

    void CancelSwing()
    {
        attackEndTime = float.NegativeInfinity;
        hitPending = false;
    }

    // The moment the fist arrives: is the officer still in reach and in front of him?
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

        // Fist height: roughly shoulder level of an average man.
        Vector3 fist = transform.position + Vector3.up * 1.35f + transform.forward * 0.3f;
        if (playerHealth == null)
            playerHealth = PlayerHealth.GetOrAdd(rig);
        if (playerHealth != null)
            playerHealth.ReceiveMeleeAttack(fist, name, damage);
    }

    float HorizontalDistanceToPlayer()
    {
        // Horizontal only: the player's head is ~1.7 m up, so a 3D distance would never drop below a 1.5 m attack range
        Vector3 offset = PlayerRig.Instance.FeetPosition - transform.position;
        offset.y = 0f;
        return offset.magnitude;
    }
}
