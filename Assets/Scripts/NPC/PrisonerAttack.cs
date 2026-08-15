using System;
using UnityEngine;
using UnityEngine.AI;


// Melee attack behaviour for prisoner NPCs (M11): attacks the player when close enough, unless under an equipment effect, detained or scared.
// Stops the NavMeshAgent while the player is within attack range so the attack does not fight with patrol movement.

[RequireComponent(typeof(PrisonerStatusSystem))]
public class PrisonerAttack : MonoBehaviour
{
    [Tooltip("Horizontal distance to the player at which the prisoner attacks, in meters.")]
    [SerializeField] float attackRange = 1.5f;

    [Tooltip("Minimum time between attacks, in seconds.")]
    [SerializeField] float attackCooldown = 2f;

    // Raised every time the prisoner performs an attack.
    public event Action OnAttack;

    PrisonerStatusSystem status;
    NavMeshAgent agent;
    Animator animator;
    Transform playerTarget;
    float lastAttackTime = float.NegativeInfinity;

    void Awake()
    {
        status = GetComponent<PrisonerStatusSystem>();
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        // While electrocuted, in pain or detained, PrisonerAIMovement owns
        // agent.isStopped; touch nothing so the two scripts don't conflict.
        if (status.CurrentConditional != PrisonerConditional.Idle)
            return;

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
            agent.isStopped = inRange;

        if (inRange && Time.time - lastAttackTime >= attackCooldown)
            Attack();
    }

    void Attack()
    {
        lastAttackTime = Time.time;
        Debug.Log("Prisoner attacks!");

        if (animator != null)
            animator.SetTrigger("Attack");

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
