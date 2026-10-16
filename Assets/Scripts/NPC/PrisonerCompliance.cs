using System.Collections;
using UnityEngine;

// Prisoner's attitude: one hidden number 0..1 that decides how he behaves
[RequireComponent(typeof(PrisonerStatusSystem))]
public class PrisonerCompliance : MonoBehaviour
{
    [Tooltip("Starting aggression, 0 calm - 1 violent")]
    [Range(0f, 1f)]
    [SerializeField] float initialAggression = 0.55f;

    [Tooltip("At or above this he chases and attacks on sight")]
    [Range(0f, 1f)]
    [SerializeField] float hostileThreshold = 0.7f;

    [Tooltip("Below this he is calm and cooperative")]
    [Range(0f, 1f)]
    [SerializeField] float calmThreshold = 0.35f;

    [Tooltip("Officer closer than this crowds him, m")]
    [SerializeField] float personalSpace = 1.8f;

    [Tooltip("Aggression gained per second while crowded")]
    [SerializeField] float crowdingAggressionPerSecond = 0.06f;

    [Tooltip("Seconds of crowding before an agitated prisoner lashes out")]
    [SerializeField] float crowdingBeforeLashOut = 1.5f;

    [Tooltip("Drift back to the starting aggression per second")]
    [SerializeField] float driftPerSecond = 0.004f;

    [Tooltip("Aggression removed by the first calm-down, repeats work less")]
    [SerializeField] float calmDownEffect = 0.18f;

    [Tooltip("Seconds between hearing a command and reacting (min, max)")]
    [SerializeField] Vector2 responseDelay = new Vector2(0.6f, 1.4f);

    static readonly string[] obeyLines = { "Okay, okay!", "Alright, I'm doing it.", "Fine...", "Okay! Easy!" };
    static readonly string[] refuseLines = { "Get lost!", "Make me!", "I'm not doing anything for you!", "Back off!", "Leave me alone!" };
    static readonly string[] calmLines = { "...fine. Just leave me be.", "Okay. Okay.", "Nobody listens to me in here..." };
    static readonly string[] hostileBarks = { "What are you looking at?!", "Come here!", "You want some?!" };

    PrisonerStatusSystem status;
    PrisonerAIMovement movement;
    PrisonerPerception perception;
    SpeechBubble speech;
    float crowdedTime;
    float calmDownStrength = 1f;
    float lastWarningTime = float.NegativeInfinity;
    float lastObeyTime = float.NegativeInfinity;
    float nextBarkTime;
    Coroutine pendingResponse;

    public float Aggression { get; private set; }

    public bool IsScared => status.CurrentEmotional == PrisonerEmotional.Scared;
    public bool IsHostile => !IsScared && Aggression >= hostileThreshold;
    public bool IsAgitated => !IsScared && Aggression >= calmThreshold && Aggression < hostileThreshold;
    public bool IsCalm => !IsHostile && !IsAgitated;

    // Obeyed recently and hasn't turned on the officer since
    public bool IsComplyingNow => Time.time - lastObeyTime <= 15f && !IsHostile;

    // Would he throw a punch right now if the officer were in reach
    public bool WantsToAttack => status.CurrentRestraint == PrisonerRestraint.Free &&
                                 (IsHostile || (IsAgitated && crowdedTime >= crowdingBeforeLashOut));

    public static PrisonerCompliance GetOrAdd(GameObject prisoner)
    {
        PrisonerCompliance c = prisoner.GetComponent<PrisonerCompliance>();
        return c != null ? c : prisoner.AddComponent<PrisonerCompliance>();
    }

    void Awake()
    {
        status = GetComponent<PrisonerStatusSystem>();
        movement = GetComponent<PrisonerAIMovement>();
        perception = PrisonerPerception.GetOrAdd(gameObject);
        speech = GetComponent<SpeechBubble>();
        if (speech == null)
            speech = gameObject.AddComponent<SpeechBubble>();
        Aggression = initialAggression;
    }

    void OnEnable()
    {
        status.OnEffectApplied += HandleEffectApplied;
    }

    void OnDisable()
    {
        status.OnEffectApplied -= HandleEffectApplied;
    }

    void Update()
    {
        // Still in his cell: nothing affects his mood yet
        if (movement != null && !movement.IsActive)
            return;
        if (status.IsDetained || status.IsCuffed)
            return;

        UpdateCrowding();

        Aggression = Mathf.MoveTowards(Aggression, initialAggression, driftPerSecond * Time.deltaTime);
        // Calm-down talk regains its strength over half a minute
        calmDownStrength = Mathf.MoveTowards(calmDownStrength, 1f, Time.deltaTime / 30f);

        if (IsHostile && perception.CanSeePlayer && Time.time >= nextBarkTime)
        {
            nextBarkTime = Time.time + Random.Range(6f, 11f);
            Say(Pick(hostileBarks));
        }
    }

    void UpdateCrowding()
    {
        // Submitting is the officer's cue to come close and cuff, it doesn't anger him
        bool crowded = perception.DistanceToPlayer < personalSpace &&
                       status.CurrentConditional == PrisonerConditional.Idle &&
                       status.CurrentRestraint == PrisonerRestraint.Free;
        if (crowded)
        {
            crowdedTime += Time.deltaTime;
            ChangeAggression(crowdingAggressionPerSecond * Time.deltaTime);
        }
        else
        {
            crowdedTime = Mathf.Max(0f, crowdedTime - Time.deltaTime * 2f);
        }
    }

    public void ChangeAggression(float delta)
    {
        Aggression = Mathf.Clamp01(Aggression + delta);
    }

    // Called by the command menu for every prisoner who can hear the shout
    public void ReceiveCommand(VerbalCommand command, float distance)
    {
        if (status.IsDetained || (movement != null && !movement.IsActive))
            return;

        if (command == VerbalCommand.ForceWarning)
            lastWarningTime = Time.time;

        // A new command replaces the one he is still thinking about
        if (pendingResponse != null)
            StopCoroutine(pendingResponse);
        pendingResponse = StartCoroutine(RespondAfterDelay(command, distance));
    }

    IEnumerator RespondAfterDelay(VerbalCommand command, float distance)
    {
        yield return new WaitForSeconds(Random.Range(responseDelay.x, responseDelay.y));
        pendingResponse = null;
        Respond(command, distance);
    }

    void Respond(VerbalCommand command, float distance)
    {
        // Down and twitching: can't take in anything
        if (status.CurrentConditional == PrisonerConditional.Electrocuted || status.IsCuffed)
            return;

        if (command == VerbalCommand.CalmDown)
        {
            RespondToCalmDown(distance);
            return;
        }
        if (command == VerbalCommand.ForceWarning)
        {
            RespondToWarning();
            return;
        }

        // Already submitting: he keeps doing what he was told instead of deciding again
        bool submitting = status.CurrentRestraint == PrisonerRestraint.Submitting;
        if (submitting || Random.value < ComplianceChance(distance))
            Obey(command);
        else
            Refuse(command);
    }

    float ComplianceChance(float distance)
    {
        float p = 1f - Aggression;
        if (IsScared) p += 0.25f;
        if (status.CurrentConditional == PrisonerConditional.InPain) p += 0.3f;
        if (Time.time - lastWarningTime < 30f) p += 0.12f;
        if (distance > 8f) p -= 0.15f;
        // Never fully certain either way, people are unpredictable
        return Mathf.Clamp(p, 0.03f, 0.97f);
    }

    void Obey(VerbalCommand command)
    {
        lastObeyTime = Time.time;
        crowdedTime = 0f;
        ChangeAggression(-0.04f);
        Say(Pick(obeyLines));
        TrainingLog.Record(TrainingEventType.CommandObeyed, $"{status.DisplayName} obeyed: {VerbalCommands.MenuLabel(command)}");

        // OC in the eyes: he can't see where to walk
        bool blinded = status.CurrentConditional == PrisonerConditional.InPain;

        switch (command)
        {
            case VerbalCommand.Stop:
                movement?.CommandHold(8f);
                break;

            case VerbalCommand.StepBack:
                if (!blinded)
                    movement?.CommandBackAway(2.5f);
                break;

            case VerbalCommand.FaceTheWall:
                if (movement != null && movement.IsAtWall)
                    break;
                // No wall nearby or blinded: turning his back where he stands is accepted too
                if (blinded || movement == null || !movement.CommandGoToWall())
                    movement?.CommandTurnAway();
                status.SetRestraint(PrisonerRestraint.Submitting);
                break;

            case VerbalCommand.HandsBehindBack:
                if (status.CurrentRestraint != PrisonerRestraint.Submitting)
                    movement?.CommandTurnAway();
                status.SetRestraint(PrisonerRestraint.Submitting);
                break;
        }
    }

    void Refuse(VerbalCommand command)
    {
        ChangeAggression(0.02f);
        Say(Pick(refuseLines));
        TrainingLog.Record(TrainingEventType.CommandRefused, $"{status.DisplayName} refused: {VerbalCommands.MenuLabel(command)}");
    }

    void RespondToCalmDown(float distance)
    {
        // Talking works only from a respectful distance and before he has lost it
        if (Aggression >= 0.9f || distance < personalSpace)
        {
            Refuse(VerbalCommand.CalmDown);
            return;
        }

        ChangeAggression(-calmDownEffect * calmDownStrength);
        calmDownStrength *= 0.6f;
        Say(Pick(calmLines));
        TrainingLog.Record(TrainingEventType.CommandObeyed, $"{status.DisplayName} calming down (aggression {Aggression:0.00})");
    }

    void RespondToWarning()
    {
        if (Aggression >= 0.9f)
        {
            ChangeAggression(0.03f);
            Say(Pick(refuseLines));
            TrainingLog.Record(TrainingEventType.CommandRefused, $"{status.DisplayName} ignored the force warning");
            return;
        }

        ChangeAggression(-0.12f);
        Say(Aggression < 0.5f ? Pick(obeyLines) : Pick(refuseLines));
    }

    void HandleEffectApplied(PrisonerConditional effect, float duration)
    {
        bool wasComplying = IsComplyingNow || status.CurrentRestraint == PrisonerRestraint.Submitting;

        if (effect == PrisonerConditional.Electrocuted)
            ChangeAggression(-0.45f);
        else if (effect == PrisonerConditional.InPain)
            ChangeAggression(-0.3f);

        // Force on someone who was doing what he was told makes things worse
        if (wasComplying && !status.IsCuffed)
        {
            ChangeAggression(0.3f);
            lastObeyTime = float.NegativeInfinity;
            status.SetRestraint(PrisonerRestraint.Free);
        }

        if (pendingResponse != null)
        {
            StopCoroutine(pendingResponse);
            pendingResponse = null;
        }
    }

    public void Say(string line)
    {
        if (speech != null)
            speech.Say(line);
    }

    static string Pick(string[] lines) => lines[Random.Range(0, lines.Length)];
}
