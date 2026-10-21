using UnityEngine;
using UnityEngine.AI;

// Owns the prisoner's NavMeshAgent: every frame picks one mode, other scripts ask through Command* methods
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(PrisonerStatusSystem))]
[RequireComponent(typeof(PrisonerPerception))]
[RequireComponent(typeof(PrisonerCompliance))]
public class PrisonerAIMovement : MonoBehaviour
{
    // In priority order, the first that fits wins
    public enum Mode
    {
        Asleep,
        Frozen,
        GoingToWall,
        Submitting,
        Attacking,
        Holding,
        BackingAway,
        Chasing,
        Searching,
        Confronting,
        Avoiding,
        Patrolling
    }

    [Tooltip("Points the prisoner walks between, in order")]
    [SerializeField] Transform[] patrolPoints;

    [Tooltip("Distance to a patrol point that counts as reached, m")]
    [SerializeField] float reachThreshold = 0.5f;

    [Tooltip("How far a hostile prisoner goes after an officer he sees, m, 0 disables chasing")]
    [SerializeField] float chaseRange = 6f;

    [Tooltip("Patrol speed, m/s")]
    [SerializeField] float patrolSpeed = 1.2f;

    [Tooltip("Chase speed, m/s")]
    [SerializeField] float chaseSpeed = 3f;

    [Tooltip("Speed when following orders, m/s")]
    [SerializeField] float compliantSpeed = 0.9f;

    [Tooltip("Turning speed while standing, degrees per second")]
    [SerializeField] float turnSpeed = 240f;

    [Tooltip("How far to look for a wall to face, m")]
    [SerializeField] float wallSearchRadius = 4f;

    [Tooltip("Seconds to reach the wall before he just turns to it where he is")]
    [SerializeField] float wallTimeout = 8f;

    [Tooltip("Within this an agitated prisoner who sees the officer stops and squares up, m")]
    [SerializeField] float confrontRange = 6f;

    public bool IsActive { get; private set; }

    public Mode CurrentMode { get; private set; } = Mode.Asleep;

    public bool IsChasing => CurrentMode == Mode.Chasing;

    // Standing at the wall with his back to the officer
    public bool IsAtWall => CurrentMode == Mode.Submitting && facingWall;

    NavMeshAgent agent;
    PrisonerStatusSystem status;
    PrisonerPerception perception;
    PrisonerCompliance compliance;
    PrisonerAttack attack;

    int currentPointIndex = -1;
    float holdUntil;
    float backAwayUntil;
    Vector3 backAwayTarget;
    bool hasWallTarget;
    bool facingWall;
    Vector3 wallStandPoint;
    Vector3 wallFacing;
    Vector3 turnAwayFacing;
    float wallDeadline;
    bool hasSearchedPoint;
    Vector3 searchedPoint;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        status = GetComponent<PrisonerStatusSystem>();
        perception = GetComponent<PrisonerPerception>();
        compliance = GetComponent<PrisonerCompliance>();
        attack = GetComponent<PrisonerAttack>();
        // Asleep until Activate(), e.g. CellDoorLink when his cell door opens
        agent.enabled = false;
    }

    void OnEnable()
    {
        status.OnRestraintChanged += HandleRestraintChanged;
    }

    void OnDisable()
    {
        status.OnRestraintChanged -= HandleRestraintChanged;
    }

    // Wakes the prisoner, safe to call more than once
    public void Activate()
    {
        if (IsActive)
            return;

        IsActive = true;
        agent.enabled = true;
        CurrentMode = Mode.Patrolling;
        TrainingLog.Record(TrainingEventType.Info, $"{status.DisplayName} is out of his cell");
        MoveToNextPoint();
    }

    public void CommandHold(float seconds)
    {
        holdUntil = Time.time + seconds;
        backAwayUntil = 0f;
    }

    public void CommandBackAway(float distance)
    {
        PlayerRig rig = PlayerRig.Instance;
        if (rig == null || !agent.isOnNavMesh)
            return;

        Vector3 away = transform.position - rig.FeetPosition;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
            away = -transform.forward;
        Vector3 target = transform.position + away.normalized * distance;

        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
        {
            backAwayTarget = hit.position;
            backAwayUntil = Time.time + 4f;
            holdUntil = 0f;
        }
    }

    // Walks to the nearest wall and faces it, false if there is no wall close enough
    public bool CommandGoToWall()
    {
        if (!agent.isOnNavMesh || !FindWallStandPoint(out wallStandPoint, out wallFacing))
            return false;

        hasWallTarget = true;
        facingWall = false;
        wallDeadline = Time.time + wallTimeout;
        return true;
    }

    // Stops and turns his back to the officer where he stands
    public void CommandTurnAway()
    {
        hasWallTarget = false;
        facingWall = false;
        PlayerRig rig = PlayerRig.Instance;
        Vector3 away = rig != null ? transform.position - rig.FeetPosition : transform.forward;
        away.y = 0f;
        turnAwayFacing = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;
    }

    void Update()
    {
        if (!IsActive)
            return;

        Mode mode = ChooseMode();
        if (mode != CurrentMode)
            EnterMode(mode);
        CurrentMode = mode;

        TickMode(mode);
    }

    Mode ChooseMode()
    {
        // Cuffed stands still until escort arrives in step 9
        if (status.IsDetained || status.IsUnderEffect || status.IsCuffed)
            return Mode.Frozen;

        if (status.CurrentRestraint == PrisonerRestraint.Submitting)
            return hasWallTarget && !facingWall ? Mode.GoingToWall : Mode.Submitting;

        if (attack != null && attack.IsAttacking)
            return Mode.Attacking;

        if (Time.time < holdUntil)
            return Mode.Holding;

        if (Time.time < backAwayUntil)
            return Mode.BackingAway;

        bool scared = status.CurrentEmotional == PrisonerEmotional.Scared;
        bool sees = perception.CanSeePlayer;
        float distance = perception.DistanceToPlayer;

        if (compliance.IsHostile && chaseRange > 0f)
        {
            if (sees && distance <= chaseRange)
                return Mode.Chasing;
            // Lost sight or heard a shout: goes to where the officer was until he forgets
            if (perception.IsAware && !AlreadySearched(perception.LastKnownPlayerPosition))
                return Mode.Searching;
        }

        // In reach and wants to fight: stand still so PrisonerAttack can swing
        if (attack != null && attack.IsInReach && compliance.WantsToAttack)
            return Mode.Attacking;

        if (sees && distance < confrontRange)
        {
            if (scared && distance < 3f)
                return Mode.Avoiding;
            if (compliance.IsAgitated)
                return Mode.Confronting;
        }

        return Mode.Patrolling;
    }

    void EnterMode(Mode mode)
    {
        if (!agent.isOnNavMesh)
            return;

        switch (mode)
        {
            case Mode.Patrolling:
                ResumePatrol();
                break;
            case Mode.Frozen:
            case Mode.Holding:
            case Mode.Submitting:
            case Mode.Attacking:
            case Mode.Confronting:
                Stop();
                break;
        }
    }

    void TickMode(Mode mode)
    {
        if (!agent.isOnNavMesh)
            return;

        PlayerRig rig = PlayerRig.Instance;

        switch (mode)
        {
            case Mode.GoingToWall:
                agent.speed = compliantSpeed;
                agent.stoppingDistance = 0f;
                SetDestinationIfChanged(wallStandPoint);
                bool arrived = !agent.pathPending && agent.remainingDistance <= 0.15f;
                // Blocked on the way: he faces the wall from where he got to
                if (arrived || Time.time > wallDeadline)
                {
                    facingWall = true;
                    Stop();
                }
                break;

            case Mode.Submitting:
                FaceDirection(facingWall ? wallFacing : turnAwayFacing);
                break;

            case Mode.Attacking:
            case Mode.Holding:
            case Mode.Confronting:
                if (rig != null)
                    FaceDirection(rig.FeetPosition - transform.position);
                break;

            case Mode.BackingAway:
                agent.speed = compliantSpeed;
                agent.stoppingDistance = 0f;
                SetDestinationIfChanged(backAwayTarget);
                break;

            case Mode.Avoiding:
                agent.speed = patrolSpeed;
                agent.stoppingDistance = 0f;
                if (rig != null)
                {
                    Vector3 away = transform.position - rig.FeetPosition;
                    away.y = 0f;
                    Vector3 target = transform.position + away.normalized * 2f;
                    if (NavMesh.SamplePosition(target, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                        agent.SetDestination(hit.position);
                }
                break;

            case Mode.Chasing:
                agent.speed = chaseSpeed;
                // Stops just inside punching range instead of walking into the officer
                agent.stoppingDistance = attack != null ? attack.AttackRange * 0.8f : 1f;
                if (rig != null)
                    agent.SetDestination(rig.FeetPosition);
                break;

            case Mode.Searching:
                agent.speed = patrolSpeed * 1.5f;
                agent.stoppingDistance = 0.3f;
                SetDestinationIfChanged(perception.LastKnownPlayerPosition);
                // Nobody there: remember the spot so he doesn't keep coming back to it
                if (IsNear(perception.LastKnownPlayerPosition))
                {
                    hasSearchedPoint = true;
                    searchedPoint = perception.LastKnownPlayerPosition;
                }
                break;

            case Mode.Patrolling:
                agent.speed = patrolSpeed;
                agent.stoppingDistance = 0f;
                if (!agent.pathPending && (!agent.hasPath || agent.remainingDistance <= reachThreshold))
                    MoveToNextPoint();
                break;
        }
    }

    void Stop()
    {
        if (!agent.isOnNavMesh)
            return;
        // ResetPath alone lets him coast on, so the velocity is zeroed too
        agent.velocity = Vector3.zero;
        agent.ResetPath();
    }

    // Repathing every frame to the same point wastes time and can stall the agent
    void SetDestinationIfChanged(Vector3 target)
    {
        if (!agent.hasPath || (agent.destination - target).sqrMagnitude > 0.04f)
            agent.SetDestination(target);
    }

    bool AlreadySearched(Vector3 point)
    {
        return hasSearchedPoint && (point - searchedPoint).sqrMagnitude < 0.01f;
    }

    bool IsNear(Vector3 point)
    {
        Vector3 offset = point - transform.position;
        offset.y = 0f;
        return offset.magnitude <= reachThreshold;
    }

    void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return;
        Quaternion target = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
    }

    void HandleRestraintChanged(PrisonerRestraint restraint)
    {
        if (restraint == PrisonerRestraint.Free)
        {
            hasWallTarget = false;
            facingWall = false;
        }
    }

    // Rays around him at chest height, picks the closest wall with walkable floor in front of it
    bool FindWallStandPoint(out Vector3 standPoint, out Vector3 facing)
    {
        standPoint = default;
        facing = default;
        float best = float.PositiveInfinity;
        Vector3 origin = transform.position + Vector3.up * 1.2f;

        for (int i = 0; i < 12; i++)
        {
            Vector3 dir = Quaternion.AngleAxis(i * 30f, Vector3.up) * Vector3.forward;
            if (!Physics.Raycast(origin, dir, out RaycastHit hit, wallSearchRadius, ~0, QueryTriggerInteraction.Ignore))
                continue;
            if (hit.collider.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<GrabbableWeapon>() != null)
                continue;
            if (PlayerRig.Instance != null && PlayerRig.Instance.IsPartOfPlayer(hit.collider.transform))
                continue;
            // Only near-vertical surfaces are walls
            if (Mathf.Abs(hit.normal.y) > 0.3f)
                continue;

            // Stand half a meter out from the wall, facing into it
            Vector3 normal = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
            Vector3 candidate = hit.point + normal * 0.55f;
            candidate.y = transform.position.y;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit navHit, 0.5f, NavMesh.AllAreas))
                continue;

            if (hit.distance < best)
            {
                best = hit.distance;
                standPoint = navHit.position;
                facing = -normal;
            }
        }

        return best < float.PositiveInfinity;
    }

    void MoveToNextPoint()
    {
        if (patrolPoints == null || patrolPoints.Length == 0 || !agent.isOnNavMesh)
            return;

        currentPointIndex = (currentPointIndex + 1) % patrolPoints.Length;
        if (patrolPoints[currentPointIndex] != null)
            agent.SetDestination(patrolPoints[currentPointIndex].position);
    }

    void ResumePatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            agent.ResetPath();
            return;
        }

        if (currentPointIndex < 0)
        {
            MoveToNextPoint();
            return;
        }

        if (patrolPoints[currentPointIndex] != null)
            agent.SetDestination(patrolPoints[currentPointIndex].position);
    }
}
