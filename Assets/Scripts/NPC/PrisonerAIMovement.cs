using UnityEngine;
using UnityEngine.AI;


/// Patrol and chase movement for prisoner NPCs: walks between patrol
/// points while idle, chases the player when close enough, freezes while
/// under an equipment effect and stops permanently once detained.
/// Scared prisoners never chase.

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

    /// True while the prisoner is actively chasing the player.
    public bool IsChasing { get; private set; }

    NavMeshAgent agent;
    PrisonerStatusSystem status;
    Transform playerTarget;
    int currentPointIndex = -1;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        status = GetComponent<PrisonerStatusSystem>();
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
        MoveToNextPoint();
    }

    void Update()
    {
        if (agent.isStopped)
            return;

        if (ShouldChasePlayer())
        {
            IsChasing = true;
            agent.SetDestination(playerTarget.position);
            return;
        }

        if (IsChasing)
        {
            // Lost the player: go back to the patrol route.
            IsChasing = false;
            ResumePatrol();
        }

        if (agent.pathPending || !agent.hasPath)
            return;

        if (agent.remainingDistance <= reachThreshold)
            MoveToNextPoint();
    }

    bool ShouldChasePlayer()
    {
        if (chaseRange <= 0f || status.CurrentEmotional == PrisonerEmotional.Scared)
            return false;

        if (playerTarget == null)
        {
            // The XR Origin's head camera is tagged MainCamera by default.
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
                return false;
            playerTarget = mainCamera.transform;
        }

        Vector3 offset = playerTarget.position - transform.position;
        offset.y = 0f;
        return offset.magnitude <= chaseRange;
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
