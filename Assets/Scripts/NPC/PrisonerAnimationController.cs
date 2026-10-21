using UnityEngine;
using UnityEngine.AI;


// Picks the Animator state from gameplay state and cross-fades to it, the controller has no transitions

[RequireComponent(typeof(PrisonerStatusSystem))]
public class PrisonerAnimationController : MonoBehaviour
{
    [Tooltip("Animator on the character model (child of this object).")]
    [SerializeField] Animator animator;

    [Tooltip("Blend time between animation states, in seconds.")]
    [SerializeField] float crossFadeDuration = 0.2f;

    [Tooltip("Agent speed above which the walk/chase animation plays instead of idle, in m/s.")]
    [SerializeField] float movingSpeedThreshold = 0.1f;

    [Tooltip("Movement speed the walk clip is authored for, in m/s - moving faster or slower scales the animation to match, so the feet don't slide.")]
    [SerializeField] float walkAnimationSpeed = 1.2f;

    [Tooltip("Movement speed the chase clip is authored for, in m/s.")]
    [SerializeField] float chaseAnimationSpeed = 3f;

    [Tooltip("Seconds into the Tased clip at which the prisoner has fallen and lies still - playback pauses there.")]
    [SerializeField] float tasedLieTime = 3.5f;

    [Tooltip("Seconds the get-up tail of the Tased clip takes. It starts so that it ends together with the stun.")]
    [SerializeField] float tasedGetUpTime = 3f;

    [Tooltip("Full length of the Tased clip, in seconds.")]
    [SerializeField] float tasedClipLength = 12.4f;

    enum TasedPhase { None, Falling, Lying, GettingUp }

    PrisonerStatusSystem status;
    PrisonerAIMovement movement;
    PrisonerAttack attack;
    NavMeshAgent agent;
    float tasedStartTime;
    float tasedSpeed = 1f;
    TasedPhase tasedPhase;
    string currentState;
    bool wasMoving;

    void Awake()
    {
        status = GetComponent<PrisonerStatusSystem>();
        movement = GetComponent<PrisonerAIMovement>();
        attack = GetComponent<PrisonerAttack>();
        agent = GetComponent<NavMeshAgent>();

        // The agent moves the transform, root motion would make him drift
        if (animator != null)
            animator.applyRootMotion = false;
    }

    void OnEnable()
    {
        status.OnConditionalStatusChanged += HandleConditionalChanged;
        status.OnEffectApplied += HandleEffectApplied;
        if (attack != null)
            attack.OnAttack += HandleAttack;
    }

    void OnDisable()
    {
        status.OnConditionalStatusChanged -= HandleConditionalChanged;
        status.OnEffectApplied -= HandleEffectApplied;
        if (attack != null)
            attack.OnAttack -= HandleAttack;
    }

    void Update()
    {
        if (animator == null)
            return;

        UpdateLocomotionSpeed();

        string nextState = ResolveState();
        if (nextState != currentState)
        {
            currentState = nextState;
            animator.CrossFadeInFixedTime(nextState, crossFadeDuration);
        }

        if (currentState == "Tased")
            UpdateTasedPhase();
    }

    void HandleConditionalChanged(PrisonerConditional conditional)
    {
        if (animator == null)
            return;

        switch (conditional)
        {
            case PrisonerConditional.Electrocuted:
                // If the stun is shorter than the fall plus the get-up at normal speed, play both faster so they still fit
                tasedSpeed = Mathf.Max(1f, (tasedLieTime + tasedGetUpTime) / status.ElectrocutedDuration);
                tasedStartTime = Time.time;
                tasedPhase = TasedPhase.Falling;
                animator.SetFloat("TasedSpeed", tasedSpeed);
                break;

            default:
                tasedPhase = TasedPhase.None;
                animator.SetFloat("TasedSpeed", 1f);
                break;
        }
    }

    // Taser cycled again while he was already getting up: down he goes again
    void HandleEffectApplied(PrisonerConditional effect, float duration)
    {
        if (animator == null || effect != PrisonerConditional.Electrocuted || tasedPhase != TasedPhase.GettingUp)
            return;

        tasedStartTime = Time.time;
        tasedPhase = TasedPhase.Falling;
        animator.SetFloat("TasedSpeed", tasedSpeed);
        animator.CrossFadeInFixedTime("Tased", crossFadeDuration, 0, 0f);
    }

    // Scales walk and chase playback to the real speed so the feet don't slide
    void UpdateLocomotionSpeed()
    {
        float speed = agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
        animator.SetFloat("WalkSpeed", Mathf.Clamp(speed / walkAnimationSpeed, 0.5f, 2f));
        animator.SetFloat("ChaseSpeed", Mathf.Clamp(speed / chaseAnimationSpeed, 0.5f, 2f));
    }

    void UpdateTasedPhase()
    {
        if (tasedPhase == TasedPhase.Falling &&
            Time.time - tasedStartTime >= tasedLieTime / tasedSpeed)
        {
            animator.SetFloat("TasedSpeed", 0f);
            tasedPhase = TasedPhase.Lying;
        }

        // Read every frame: another taser cycle moves the end later
        bool getUpDue = status.EffectEndTime - Time.time <= tasedGetUpTime / tasedSpeed;
        if (tasedPhase != TasedPhase.None && tasedPhase != TasedPhase.GettingUp && getUpDue)
        {
            animator.SetFloat("TasedSpeed", tasedSpeed);
            animator.CrossFadeInFixedTime("Tased", crossFadeDuration, 0, tasedClipLength - tasedGetUpTime);
            tasedPhase = TasedPhase.GettingUp;
        }
    }

    void HandleAttack()
    {
        if (animator == null)
            return;

        // Restart from the beginning even if the previous attack is still playing
        currentState = "Attack";
        animator.CrossFadeInFixedTime("Attack", crossFadeDuration, 0, 0f);
    }

    string ResolveState()
    {
        switch (status.CurrentConditional)
        {
            case PrisonerConditional.Detained:
                return "Detained";
            case PrisonerConditional.Electrocuted:
                return "Tased";
            case PrisonerConditional.InPain:
                return "Sprayed";
            // No dedicated clip yet, the scared flinch reads as a jolt of pain
            case PrisonerConditional.Staggered:
                return "Scared";
        }

        // Obeyed "face the wall" and got there: hands on the wall
        if (movement != null && movement.IsAtWall)
            return "Detained";

        // Movement stands him still for exactly as long as the swing lasts
        if (attack != null && attack.IsAttacking)
            return "Attack";

        float speed = agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
        // Lower exit threshold stops walk and idle flickering while the agent brakes
        bool isMoving = speed > (wasMoving ? movingSpeedThreshold * 0.5f : movingSpeedThreshold);
        wasMoving = isMoving;
        if (isMoving)
            return movement != null && movement.IsChasing ? "Chase" : "Walk";

        return status.CurrentEmotional == PrisonerEmotional.Scared ? "Scared" : "Idle";
    }
}
