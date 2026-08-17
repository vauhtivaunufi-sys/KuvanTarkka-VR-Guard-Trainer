using System.Collections;
using TMPro;
using UnityEngine;


// Objective text HUD (M15): shows the current objective on a world-space canvas and cross-fades whenever it changes.
// Listens to RestraintZone.OnPrisonerDetained for completion; PrisonerAIMovement exposes no activation event, so the sleep-to-active transition is detected by polling IsActive.

public class ObjectiveUIController : MonoBehaviour
{
    [Tooltip("TextMeshPro element the objective is written to.")]
    [SerializeField] TMP_Text objectiveText;

    [Tooltip("Zone whose detain event marks the objective complete.")]
    [SerializeField] RestraintZone restraintZone;

    [Tooltip("Prisoner whose wake-up starts the objective.")]
    [SerializeField] PrisonerAIMovement prisoner;

    [Tooltip("Duration of each fade half (out, then in), in seconds.")]
    [SerializeField] float fadeDuration = 0.4f;

    bool prisonerSeenActive;
    Coroutine fadeRoutine;

    void OnEnable()
    {
        if (restraintZone != null)
            restraintZone.OnPrisonerDetained += HandlePrisonerDetained;
    }

    void OnDisable()
    {
        if (restraintZone != null)
            restraintZone.OnPrisonerDetained -= HandlePrisonerDetained;
    }

    void Update()
    {
        if (prisonerSeenActive || prisoner == null || !prisoner.IsActive)
            return;

        prisonerSeenActive = true;
        SetObjectiveText("Subdue and restrain the inmate");
    }

    // Fades the current text out, swaps it, and fades the new text back in.
    public void SetObjectiveText(string text)
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeToText(text));
    }

    void HandlePrisonerDetained()
    {
        SetObjectiveText("Objective Complete");
    }

    IEnumerator FadeToText(string text)
    {
        yield return Fade(objectiveText.alpha, 0f);
        objectiveText.text = text;
        yield return Fade(0f, 1f);
        fadeRoutine = null;
    }

    IEnumerator Fade(float from, float to)
    {
        for (float elapsed = 0f; elapsed < fadeDuration; elapsed += Time.deltaTime)
        {
            objectiveText.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }

        objectiveText.alpha = to;
    }
}
