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
        int sprayedCount = 0;

        foreach (Collider hit in hits)
        {
            Vector3 toTarget = hit.bounds.center - usePoint.position;
            if (Vector3.Angle(usePoint.forward, toTarget) > halfAngle)
                continue;

            // Search parents too so child colliders (e.g. on an artist's
            // model) still register hits.
            PrisonerStatusSystem prisoner = hit.GetComponentInParent<PrisonerStatusSystem>();
            if (prisoner != null)
            {
                prisoner.ApplyOCSpray();
                sprayedCount++;
            }
        }

        Debug.Log(sprayedCount > 0
            ? $"OC spray hit {sprayedCount} prisoner(s)"
            : "OC spray missed: no prisoner in the cone", this);
    }

    void PlayEffects()
    {
        if (sprayEffect != null)
            sprayEffect.Play();

        if (audioSource != null && sprayClip != null)
            audioSource.PlayOneShot(sprayClip);
    }

    /// Draws the spray cone in the Scene view while the object is selected,
    /// so the reach and aim direction can be checked without playing.
    void OnDrawGizmosSelected()
    {
        if (usePoint == null)
            return;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);

        Vector3 origin = usePoint.position;
        Vector3 forward = usePoint.forward;
        float endRadius = Mathf.Tan(coneAngle * 0.5f * Mathf.Deg2Rad) * range;
        Vector3 endCenter = origin + forward * range;

        Gizmos.DrawLine(origin, endCenter);

        const int segments = 16;
        Vector3 previousPoint = Vector3.zero;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * 360f / segments;
            Vector3 rimPoint = endCenter +
                Quaternion.AngleAxis(angle, forward) * usePoint.up * endRadius;

            if (i > 0)
                Gizmos.DrawLine(previousPoint, rimPoint);
            if (i % 4 == 0)
                Gizmos.DrawLine(origin, rimPoint);
            previousPoint = rimPoint;
        }
    }
}
