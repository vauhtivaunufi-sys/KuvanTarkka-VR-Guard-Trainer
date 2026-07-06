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


/// Tracks the prisoner's current conditional status and its reactions to
/// equipment effects. Temporary effects wear off after their duration;
/// Detained is permanent.

public class PrisonerStatusSystem : MonoBehaviour
{
    [Tooltip("How long the prisoner stays electrocuted after a taser hit, in seconds.")]
    [SerializeField] float electrocutedDuration = 5f;

    [Tooltip("How long the prisoner stays in pain after an OC spray hit, in seconds.")]
    [SerializeField] float inPainDuration = 8f;

    /// Raised whenever the conditional status changes.
    public event Action<PrisonerConditional> OnConditionalStatusChanged;

    public PrisonerConditional CurrentConditional { get; private set; } = PrisonerConditional.Idle;

    Coroutine recoverRoutine;

    public void ApplyTaser()
    {
        ApplyTemporaryEffect(PrisonerConditional.Electrocuted, electrocutedDuration);
    }

    public void ApplyOCSpray()
    {
        ApplyTemporaryEffect(PrisonerConditional.InPain, inPainDuration);
    }

    /// Permanently detains the prisoner (training objective completed).
    public void Detain()
    {
        if (CurrentConditional == PrisonerConditional.Detained)
            return;

        if (recoverRoutine != null)
            StopCoroutine(recoverRoutine);
        SetConditional(PrisonerConditional.Detained);
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
    }

    void SetConditional(PrisonerConditional conditional)
    {
        if (CurrentConditional == conditional)
            return;

        CurrentConditional = conditional;
        OnConditionalStatusChanged?.Invoke(conditional);
    }
}
