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

    [Tooltip("How long the prisoner stays in pain after an OC spray hit, in seconds.")]
    [SerializeField] float inPainDuration = 8f;

    [Tooltip("How long the prisoner stays scared (won't chase or attack) after an effect wears off, in seconds. 0 = never gets scared.")]
    [SerializeField] float scaredDuration = 20f;

    // Raised whenever the conditional status changes
    public event Action<PrisonerConditional> OnConditionalStatusChanged;

    // Raised on every hit, also when the status stays the same (a second taser hit)
    public event Action<PrisonerConditional, float> OnEffectApplied;

    // Raised whenever the emotional status changes
    public event Action<PrisonerEmotional> OnEmotionalStatusChanged;

    public event Action<PrisonerRestraint> OnRestraintChanged;

    public string DisplayName => displayName;

    public PrisonerConditional CurrentConditional { get; private set; } = PrisonerConditional.Idle;

    public PrisonerEmotional CurrentEmotional { get; private set; } = PrisonerEmotional.Neutral;

    public PrisonerRestraint CurrentRestraint { get; private set; } = PrisonerRestraint.Free;

    // Effect durations, exposed so other systems (e.g. animation) can sync to the effect timeline
    public float ElectrocutedDuration =>
        CurrentConditional == PrisonerConditional.Electrocuted && lastEffectDuration > 0f ? lastEffectDuration : electrocutedDuration;

    // When the current effect wears off, moves later if the taser cycles again
    public float EffectEndTime { get; private set; }

    public float InPainDuration => inPainDuration;

    public bool IsDetained => CurrentConditional == PrisonerConditional.Detained;

    public bool IsCuffed => CurrentRestraint == PrisonerRestraint.Cuffed;

    // An effect stops him acting on his own right now
    public bool IsUnderEffect => CurrentConditional == PrisonerConditional.Electrocuted ||
                                 CurrentConditional == PrisonerConditional.InPain ||
                                 CurrentConditional == PrisonerConditional.Staggered;

    Coroutine recoverRoutine;
    Coroutine calmDownRoutine;
    float lastEffectDuration;

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

    public void ApplyOCSpray()
    {
        ApplyTemporaryEffect(PrisonerConditional.InPain, inPainDuration);
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
