using System.Collections;
using UnityEngine;


/// Taser implementation: raycasts from the use point and
/// applies the taser effect to prisoners, showing a short visual beam.

public class TaserWeapon : GrabbableWeapon
{
    [Tooltip("Maximum distance the taser can reach, in meters.")]
    [SerializeField] float range = 5f;

    [Tooltip("How long the visual beam stays visible, in seconds.")]
    [SerializeField] float beamDuration = 0.1f;

    [Tooltip("Renderer used to draw the beam from the use point to the hit point.")]
    [SerializeField] LineRenderer beamRenderer;

    Coroutine hideBeamRoutine;

    protected override void Awake()
    {
        base.Awake();

        if (beamRenderer != null)
        {
            beamRenderer.positionCount = 2;
            beamRenderer.useWorldSpace = true;
            beamRenderer.enabled = false;
        }
    }

    protected override void OnUse()
    {
        Vector3 origin = usePoint.position;
        Vector3 direction = usePoint.forward;
        Vector3 beamEnd = origin + direction * range;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, range))
        {
            beamEnd = hit.point;

            if (hit.collider.CompareTag("Prisoner") &&
                hit.collider.TryGetComponent(out PrisonerStatusSystem prisoner))
            {
                prisoner.ApplyTaser();
            }
        }

        ShowBeam(origin, beamEnd);
    }

    void ShowBeam(Vector3 start, Vector3 end)
    {
        if (beamRenderer == null)
            return;

        beamRenderer.SetPosition(0, start);
        beamRenderer.SetPosition(1, end);
        beamRenderer.enabled = true;

        if (hideBeamRoutine != null)
            StopCoroutine(hideBeamRoutine);
        hideBeamRoutine = StartCoroutine(HideBeamAfterDelay());
    }

    IEnumerator HideBeamAfterDelay()
    {
        yield return new WaitForSeconds(beamDuration);
        beamRenderer.enabled = false;
    }
}
