using System;
using System.Collections;
using UnityEngine;


public enum PrisonerConditional
{
    Idle,
    Electrocuted,
    InPain,
    Detained,
    // Short jolt of pain without collapse: taser probes too close, drive stun
    Staggered
}


public enum PrisonerEmotional
{
    Neutral,
    Scared
}


// Restraint progress, separate from effects: he can be in pain and already submitting
public enum PrisonerRestraint
{
    Free,
    Submitting,
    Cuffed
}


// Prisoner's status under equipment effects, temporary ones wear off, Detained stays

public class PrisonerStatusSystem : MonoBehaviour
{
    [Tooltip("Name in the training log")]
    [SerializeField] string displayName = "Inmate";

    [Tooltip("How long the prisoner stays electrocuted after a taser hit, in seconds.")]
    [SerializeField] float electrocutedDuration = 5f;

    [Tooltip("Seconds from OC in the eyes to the full effect (min, max)")]
    [SerializeField] Vector2 ocOnsetDelay = new Vector2(1.5f, 3f);

    [Tooltip("Chance he barely reacts to OC (drugs, adrenaline, rage), rolled once per run")]
    [Range(0f, 1f)]
    [SerializeField] float ocNonResponderChance = 0.15f;

    [Tooltip("How long the prisoner stays scared (won't chase or attack) after an effect wears off, in seconds. 0 = never gets scared.")]
    [SerializeField] float scaredDuration = 20f;

    // Raised whenever the conditional status changes
    public event Action<PrisonerConditional> OnConditionalStatusChanged;

    // Raised on every hit, also when the status stays the same (a second taser hit)
    public event Action<PrisonerConditional, float> OnEffectApplied;

    // Raised whenever the emotional status changes
    public event Action<PrisonerEmotional> OnEmotionalStatusChanged;

    public event Action<PrisonerRestraint> OnRestraintChanged;

    // (zone, what hit it) - raised for every probe or strike that landed on him
    public event Action<BodyZone, string> OnBodyHit;

    public string DisplayName => displayName;

    public PrisonerConditional CurrentConditional { get; private set; } = PrisonerConditional.Idle;

    public PrisonerEmotional CurrentEmotional { get; private set; } = PrisonerEmotional.Neutral;

    public PrisonerRestraint CurrentRestraint { get; private set; } = PrisonerRestraint.Free;

    // Effect durations, exposed so other systems (e.g. animation) can sync to the effect timeline
    public float ElectrocutedDuration =>
        CurrentConditional == PrisonerConditional.Electrocuted && lastEffectDuration > 0f ? lastEffectDuration : electrocutedDuration;

    // When the current effect wears off, moves later if the taser cycles again
    public float EffectEndTime { get; private set; }

    public bool IsDetained => CurrentConditional == PrisonerConditional.Detained;

    public bool IsCuffed => CurrentRestraint == PrisonerRestraint.Cuffed;

    // An effect stops him acting on his own right now
    public bool IsUnderEffect => CurrentConditional == PrisonerConditional.Electrocuted ||
                                 CurrentConditional == PrisonerConditional.InPain ||
                                 CurrentConditional == PrisonerConditional.Staggered;

    Coroutine recoverRoutine;
    Coroutine calmDownRoutine;
    Coroutine ocOnsetRoutine;
    float lastEffectDuration;
    bool ocNonResponder;

    // OC is in his eyes but hasn't taken hold yet
    public bool IsOCPending => ocOnsetRoutine != null;

    void Awake()
    {
        ocNonResponder = UnityEngine.Random.value < ocNonResponderChance;
        if (ocNonResponder)
            TrainingDebug.Log($"{displayName} will barely react to OC this run", this);
    }

    public void ApplyTaser()
    {
        ApplyTaser(electrocutedDuration);
    }

    // Full incapacitation for one taser cycle plus the time on the ground, again while down extends it
    public void ApplyTaser(float duration)
    {
        ApplyTemporaryEffect(PrisonerConditional.Electrocuted, duration);
    }

    // Pain only: he flinches and stops what he was doing, no fall
    public void ApplyStagger(float duration)
    {
        if (CurrentConditional == PrisonerConditional.Electrocuted || CurrentConditional == PrisonerConditional.InPain)
            return;
        ApplyTemporaryEffect(PrisonerConditional.Staggered, duration);
    }

    // OC in the eyes: takes a couple of seconds to hit, a new dose while it lasts keeps it going
    public void ApplyOCSpray(float duration)
    {
        if (IsDetained)
            return;

        if (CurrentConditional == PrisonerConditional.InPain)
        {
            ApplyTemporaryEffect(PrisonerConditional.InPain, duration);
            return;
        }

        if (ocOnsetRoutine == null)
            ocOnsetRoutine = StartCoroutine(OCOnset(duration));
    }

    IEnumerator OCOnset(float duration)
    {
        yield return new WaitForSeconds(UnityEngine.Random.Range(ocOnsetDelay.x, ocOnsetDelay.y));
        // A taser cycle running now finishes first, the OC is still there after it
        while (CurrentConditional == PrisonerConditional.Electrocuted)
            yield return null;
        ocOnsetRoutine = null;

        if (IsDetained)
            yield break;

        if (ocNonResponder)
        {
            TrainingLog.Record(TrainingEventType.PrisonerState, $"{displayName} barely reacts to the OC");
            ApplyStagger(1.5f);
            yield break;
        }

        ApplyTemporaryEffect(PrisonerConditional.InPain, duration);
    }

    // A probe or a strike landed on a body zone, the head is never a legal target
    public void RegisterHit(BodyZone zone, string cause, ForceKind kind)
    {
        if (zone == BodyZone.Head)
            TrainingLog.Record(TrainingEventType.HeadInjury, $"{displayName} hit in the head: {cause}", kind);
        else
            TrainingLog.Record(TrainingEventType.BodyHit, $"{displayName} hit on the {zone.ToString().ToLowerInvariant()}: {cause}", kind);

        OnBodyHit?.Invoke(zone, cause);
    }

    // Permanently detains the prisoner (training objective completed)
    public void Detain()
    {
        if (CurrentConditional == PrisonerConditional.Detained)
            return;

        if (recoverRoutine != null)
            StopCoroutine(recoverRoutine);
        if (calmDownRoutine != null)
            StopCoroutine(calmDownRoutine);
        if (ocOnsetRoutine != null)
            StopCoroutine(ocOnsetRoutine);
        ocOnsetRoutine = null;
        SetConditional(PrisonerConditional.Detained);
        TrainingLog.Record(TrainingEventType.Restraint, $"{displayName} secured");
    }

    public void SetRestraint(PrisonerRestraint restraint)
    {
        if (CurrentRestraint == restraint)
            return;
        // Cuffs come off only through the scenario, never by a stray command
        if (CurrentRestraint == PrisonerRestraint.Cuffed)
            return;

        CurrentRestraint = restraint;
        TrainingDebug.Log($"{displayName} restraint -> {restraint}", this);
        OnRestraintChanged?.Invoke(restraint);
    }

    public void SetEmotional(PrisonerEmotional emotional)
    {
        if (CurrentEmotional == emotional)
            return;

        CurrentEmotional = emotional;
        TrainingDebug.Log($"{displayName} emotional status -> {emotional}", this);
        OnEmotionalStatusChanged?.Invoke(emotional);
    }

    void ApplyTemporaryEffect(PrisonerConditional conditional, float duration)
    {
        if (CurrentConditional == PrisonerConditional.Detained)
            return;

        if (recoverRoutine != null)
            StopCoroutine(recoverRoutine);
        lastEffectDuration = duration;
        EffectEndTime = Time.time + duration;
        SetConditional(conditional);
        OnEffectApplied?.Invoke(conditional, duration);
        recoverRoutine = StartCoroutine(RecoverAfterDelay(duration, conditional));
    }

    IEnumerator RecoverAfterDelay(float duration, PrisonerConditional effect)
    {
        yield return new WaitForSeconds(duration);
        recoverRoutine = null;
        SetConditional(PrisonerConditional.Idle);
        // Being hit scares him off for a while, a short jolt of pain doesn't
        if (effect != PrisonerConditional.Staggered)
            BecomeScared();
    }

    void BecomeScared()
    {
        if (scaredDuration <= 0f)
            return;

        if (calmDownRoutine != null)
            StopCoroutine(calmDownRoutine);
        SetEmotional(PrisonerEmotional.Scared);
        calmDownRoutine = StartCoroutine(CalmDownAfterDelay());
    }

    IEnumerator CalmDownAfterDelay()
    {
        yield return new WaitForSeconds(scaredDuration);
        calmDownRoutine = null;
        SetEmotional(PrisonerEmotional.Neutral);
    }

    void SetConditional(PrisonerConditional conditional)
    {
        if (CurrentConditional == conditional)
            return;

        PrisonerConditional previous = CurrentConditional;
        CurrentConditional = conditional;
        TrainingDebug.Log($"{displayName} conditional status -> {conditional}", this);

        if (conditional == PrisonerConditional.Electrocuted || conditional == PrisonerConditional.InPain ||
            conditional == PrisonerConditional.Staggered)
            TrainingLog.Record(TrainingEventType.PrisonerState, $"{displayName}: {conditional}");
        else if (conditional == PrisonerConditional.Idle)
            TrainingLog.Record(TrainingEventType.PrisonerState, $"{displayName} recovered from {previous}");

        OnConditionalStatusChanged?.Invoke(conditional);
    }
}
