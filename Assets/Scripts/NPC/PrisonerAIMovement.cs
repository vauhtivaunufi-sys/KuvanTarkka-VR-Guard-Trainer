using UnityEngine;
using UnityEngine.AI;


// Patrol and chase movement for prisoner NPCs: walks between patrol points while idle, chases the player he can actually see
// (PrisonerPerception: view cone + line of sight, so no more chasing through walls), searches the last known position for a few
// seconds after losing sight, freezes while under an equipment effect and stops permanently once detained.
// Scared prisoners never chase.

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(PrisonerStatusSystem))]
public class PrisonerAIMovement : MonoBehaviour
{
    [Tooltip("Points the prisoner walks between, in order.")]
    [SerializeField] Transform[] patrolPoints;

    [Tooltip("Distance to a patrol point at which it counts as reached, in meters.")]
    [SerializeField] float reachThreshold = 0.5f;

    [Tooltip("Horizontal distance at which the prisoner notices the player and starts chasing, in meters. Set to 0 to disable chasing.")]
    [SerializeField] float chaseRange = 6f;

    [Tooltip("Agent speed while patrolling, in m/s. Overrides the NavMeshAgent's Speed field.")]
    [SerializeField] float patrolSpeed = 1.2f;

    [Tooltip("Agent speed while chasing the player, in m/s.")]
    [SerializeField] float chaseSpeed = 3f;

    // True while the prisoner is actively chasing the player.
    public bool IsChasing { get; private set; }

    // True once Activate() has been called - false means the prisoner is still asleep (agent disabled, no patrolling).
    public bool IsActive { get; private set; }

    NavMeshAgent agent;
    PrisonerStatusSystem status;
    PrisonerPerception perception;
    Vector3 chaseDestination;
    int currentPointIndex = -1;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        status = GetComponent<PrisonerStatusSystem>();
        perception = PrisonerPerception.GetOrAdd(gameObject);
        // Starts asleep: disabled until Activate() is called, e.g. by
        // CellDoorLink when this prisoner's cell door opens.
        agent.enabled = false;
    }

    void OnEnable()
    {
        status.OnConditionalStatusChanged += HandleConditionalChanged;
    }

    void OnDisable()
    {
        status.OnConditionalStatusChanged -= HandleConditionalChanged;
    }

    void Start()
    {
        if (IsActive)
            MoveToNextPoint();
    }

    // Wakes the prisoner: enables the NavMeshAgent and starts patrolling.
    // Safe to call more than once.
    public void Activate()
    {
        if (IsActive)
            return;

        IsActive = true;
        agent.enabled = true;
        MoveToNextPoint();
    }

    void Update()
    {
        if (!IsActive || agent.isStopped)
            return;

        if (ShouldChasePlayer())
        {
            IsChasing = true;
            agent.speed = chaseSpeed;
            agent.SetDestination(chaseDestination);
            return;
        }

        if (IsChasing)
        {
            // Lost the player: go back to the patrol route.
            IsChasing = false;
            ResumePatrol();
        }

        agent.speed = patrolSpeed;

        if (agent.pathPending || !agent.hasPath)
            return;

        if (agent.remainingDistance <= reachThreshold)
            MoveToNextPoint();
    }

    bool ShouldChasePlayer()
    {
        if (chaseRange <= 0f || status.CurrentEmotional == PrisonerEmotional.Scared)
            return false;

        PlayerRig rig = PlayerRig.Instance;
        if (rig == null)
            return false;

        if (perception == null)
        {
            // No perception component: old behaviour (distance only). Add PrisonerPerception to stop him sensing through walls.
            Vector3 offset = rig.FeetPosition - transform.position;
            offset.y = 0f;
            chaseDestination = rig.FeetPosition;
            return offset.magnitude <= chaseRange;
        }

        if (perception.CanSeePlayer && perception.DistanceToPlayer <= chaseRange)
        {
            chaseDestination = rig.FeetPosition;
            return true;
        }

        // Lost sight mid-chase: keep going to where the player was last seen (or heard) until memory runs out or he gets there.
        if (IsChasing && perception.IsAware)
        {
            chaseDestination = perception.LastKnownPlayerPosition;
            Vector3 toTarget = chaseDestination - transform.position;
            toTarget.y = 0f;
            return toTarget.magnitude > reachThreshold;
        }

        return false;
    }

    void MoveToNextPoint()
    {
        if (patrolPoints.Length == 0)
            return;

        currentPointIndex = (currentPointIndex + 1) % patrolPoints.Length;
        agent.SetDestination(patrolPoints[currentPointIndex].position);
    }

    void ResumePatrol()
    {
        if (patrolPoints.Length == 0)
        {
            agent.ResetPath();
            return;
        }

        agent.SetDestination(patrolPoints[currentPointIndex].position);
    }

    void HandleConditionalChanged(PrisonerConditional conditional)
    {
        switch (conditional)
        {
            case PrisonerConditional.Detained:
                // Objective complete: freeze and stop reacting for good.
                agent.isStopped = true;
                enabled = false;
                break;

            case PrisonerConditional.Electrocuted:
            case PrisonerConditional.InPain:
                agent.isStopped = true;
                break;

            default:
                agent.isStopped = false;
                break;
        }
    }
}
