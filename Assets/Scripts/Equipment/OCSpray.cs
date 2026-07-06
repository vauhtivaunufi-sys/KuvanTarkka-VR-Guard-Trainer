using UnityEngine;


/// OC spray implementation: affects prisoners inside a short cone
/// in front of the use point, with particle and sound effects.

public class OCSpray : GrabbableWeapon
{
    [Tooltip("Maximum distance the spray can reach, in meters.")]
    [SerializeField] float range = 3f;

    [Tooltip("Full angle of the spray cone, in degrees.")]
    [SerializeField] float coneAngle = 30f;

    [Tooltip("Particle effect played on each spray burst. Visuals configured separately.")]
    [SerializeField] ParticleSystem sprayEffect;

    [Tooltip("Source used to play the spray sound.")]
    [SerializeField] AudioSource audioSource;

    [Tooltip("Sound played on each spray burst. Optional.")]
    [SerializeField] AudioClip sprayClip;

    protected override void OnUse()
    {
        SprayTargets();
        PlayEffects();
    }

    void SprayTargets()
    {
        Collider[] hits = Physics.OverlapSphere(usePoint.position, range);
        float halfAngle = coneAngle * 0.5f;

        foreach (Collider hit in hits)
        {
            Vector3 toTarget = hit.bounds.center - usePoint.position;
            if (Vector3.Angle(usePoint.forward, toTarget) > halfAngle)
                continue;

            if (hit.CompareTag("Prisoner") &&
                hit.TryGetComponent(out PrisonerStatusSystem prisoner))
            {
                prisoner.ApplyOCSpray();
            }
        }
    }

    void PlayEffects()
    {
        if (sprayEffect != null)
            sprayEffect.Play();

        if (audioSource != null && sprayClip != null)
            audioSource.PlayOneShot(sprayClip);
    }
}
