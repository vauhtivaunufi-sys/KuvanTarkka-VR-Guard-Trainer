using System;
using UnityEngine;
using UnityEngine.AI;


// Melee attack behaviour for prisoner NPCs (M11): attacks the player when close enough, unless under an equipment effect, detained or scared.
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

    // Raised every time the prisoner performs an attack.
    public event Action OnAttack;

    // True while a swing is still playing out. PrisonerAnimationController
    // holds the Attack animation state for exactly as long as this is true.
    public bool IsAttacking => Time.time < attackEndTime;

    PrisonerStatusSystem status;
    NavMeshAgent agent;
    Transform playerTarget;
    float lastAttackTime = float.NegativeInfinity;
    float attackEndTime = float.NegativeInfinity;

    void Awake()
    {
        status = GetComponent<PrisonerStatusSystem>();
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        // While electrocuted, in pain or detained, PrisonerAIMovement owns
        // agent.isStopped; touch nothing so the two scripts don't conflict.
        if (status.CurrentConditional != PrisonerConditional.Idle)
        {
            // Getting tased or sprayed cancels the swing. Clear the root too,
            // or it outlives the interrupt and leaves him planted once he recovers.
            attackEndTime = float.NegativeInfinity;
            return;
        }

        if (playerTarget == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
                return;
            // The XR Origin's head camera is tagged MainCamera by default.
            playerTarget = mainCamera.transform;
        }

        // Scared prisoners never attack and keep patrolling instead.
        bool inRange = status.CurrentEmotional != PrisonerEmotional.Scared &&
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

        // isStopped only cuts the agent's throttle, it does not clear momentum;
        // without this he coasts on into the first half of the swing.
        if (agent != null && agent.isOnNavMesh)
            agent.velocity = Vector3.zero;

        OnAttack?.Invoke();
    }

    float HorizontalDistanceToPlayer()
    {
        // Ignore height: the target is the player's head camera (~1.7 m up),
        // so a 3D distance would never drop below a 1.5 m attack range.
        Vector3 offset = playerTarget.position - transform.position;
        offset.y = 0f;
        return offset.magnitude;
    }
}
