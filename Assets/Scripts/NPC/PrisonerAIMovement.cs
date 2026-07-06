using UnityEngine;
using UnityEngine.AI;


/// Patrol movement for prisoner NPCs: walks between patrol points while
/// idle, freezes while under an equipment effect and stops permanently
/// once detained.

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(PrisonerStatusSystem))]
public class PrisonerAIMovement : MonoBehaviour
{
    [Tooltip("Points the prisoner walks between, in order.")]
    [SerializeField] Transform[] patrolPoints;

    [Tooltip("Distance to a patrol point at which it counts as reached, in meters.")]
    [SerializeField] float reachThreshold = 0.5f;

    NavMeshAgent agent;
    PrisonerStatusSystem status;
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
        if (agent.isStopped || agent.pathPending || !agent.hasPath)
            return;

        if (agent.remainingDistance <= reachThreshold)
            MoveToNextPoint();
    }

    void MoveToNextPoint()
    {
        if (patrolPoints.Length == 0)
            return;

        currentPointIndex = (currentPointIndex + 1) % patrolPoints.Length;
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
