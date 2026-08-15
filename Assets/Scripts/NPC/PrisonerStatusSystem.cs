using System;
using System.Collections;
using UnityEngine;


public enum PrisonerConditional
{
    Idle,
    Electrocuted,
    InPain,
    Detained
}


public enum PrisonerEmotional
{
    Neutral,
    Scared
}


// Tracks the prisoner's current conditional status and its reactions to equipment effects. Temporary effects wear off after their duration;
// Detained is permanent. Also tracks the emotional status, which other systems (e.g. attack behaviour) read and set.

public class PrisonerStatusSystem : MonoBehaviour
{
    [Tooltip("How long the prisoner stays electrocuted after a taser hit, in seconds.")]
    [SerializeField] float electrocutedDuration = 5f;

    [Tooltip("How long the prisoner stays in pain after an OC spray hit, in seconds.")]
    [SerializeField] float inPainDuration = 8f;

    [Tooltip("How long the prisoner stays scared (won't chase or attack) after an effect wears off, in seconds. 0 = never gets scared.")]
    [SerializeField] float scaredDuration = 20f;

    // Raised whenever the conditional status changes.
    public event Action<PrisonerConditional> OnConditionalStatusChanged;

    // Raised whenever the emotional status changes.
    public event Action<PrisonerEmotional> OnEmotionalStatusChanged;

    public PrisonerConditional CurrentConditional { get; private set; } = PrisonerConditional.Idle;

    public PrisonerEmotional CurrentEmotional { get; private set; } = PrisonerEmotional.Neutral;

    Coroutine recoverRoutine;
    Coroutine calmDownRoutine;

    public void ApplyTaser()
    {
        ApplyTemporaryEffect(PrisonerConditional.Electrocuted, electrocutedDuration);
    }

    public void ApplyOCSpray()
    {
        ApplyTemporaryEffect(PrisonerConditional.InPain, inPainDuration);
    }

    // Permanently detains the prisoner (training objective completed).
    public void Detain()
    {
        if (CurrentConditional == PrisonerConditional.Detained)
            return;

        if (recoverRoutine != null)
            StopCoroutine(recoverRoutine);
        if (calmDownRoutine != null)
            StopCoroutine(calmDownRoutine);
        SetConditional(PrisonerConditional.Detained);
    }

    public void SetEmotional(PrisonerEmotional emotional)
    {
        if (CurrentEmotional == emotional)
            return;

        CurrentEmotional = emotional;
        Debug.Log($"Prisoner emotional status -> {emotional}", this);
        OnEmotionalStatusChanged?.Invoke(emotional);
    }

    void ApplyTemporaryEffect(PrisonerConditional conditional, float duration)
    {
        if (CurrentConditional == PrisonerConditional.Detained)
            return;

        if (recoverRoutine != null)
            StopCoroutine(recoverRoutine);
        SetConditional(conditional);
        recoverRoutine = StartCoroutine(RecoverAfterDelay(duration));
    }

    IEnumerator RecoverAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);
        recoverRoutine = null;
        SetConditional(PrisonerConditional.Idle);
        // Being hit teaches the prisoner to keep away for a while: scared
        // prisoners neither chase nor attack, they go back to patrolling.
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

        CurrentConditional = conditional;
        Debug.Log($"Prisoner conditional status -> {conditional}", this);
        OnConditionalStatusChanged?.Invoke(conditional);
    }
}
