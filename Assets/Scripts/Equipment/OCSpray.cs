using System.Collections.Generic;
using UnityEngine;

// OC spray, stream type: hold the trigger to spray, it only works in the eyes and takes a couple of seconds to hit
public class OCSpray : GrabbableWeapon
{
    [Header("Canister")]
    [Tooltip("Total spray time in a full canister, s")]
    [SerializeField] float capacity = 10f;

    [Tooltip("How far the stream reaches, m")]
    [SerializeField] float range = 3.5f;

    [Tooltip("How much the stream fans out, degrees from its centre line")]
    [SerializeField] float streamSpread = 3f;

    [Header("Effect")]
    [Tooltip("Radius around his eyes that counts as the face, m")]
    [SerializeField] float faceRadius = 0.12f;

    [Tooltip("Stream time on the face in one burst before it takes effect, s")]
    [SerializeField] float doseToAffect = 0.4f;

    [Tooltip("How long he is blinded and in pain, s (real OC lasts 20-45 min)")]
    [SerializeField] float effectDuration = 25f;

    [Tooltip("Closer than this the spray splashes back into the officer's eyes, m")]
    [SerializeField] float minSafeDistance = 0.9f;

    [Tooltip("How long the officer's own eyes burn after a splashback, s")]
    [SerializeField] float splashbackDuration = 6f;

    [Tooltip("Bursts longer than this are flagged in the log, short bursts are the trained technique, s")]
    [SerializeField] float longBurst = 2f;

    [Header("Visuals and sound")]
    [Tooltip("Stream particles, Looping on and Play On Awake off in the prefab")]
    [SerializeField] ParticleSystem sprayEffect;

    [Tooltip("Hiss while spraying: clip and Loop set on this AudioSource")]
    [SerializeField] AudioSource audioSource;

    class Exposure
    {
        public float Dose;
        public float Distance = float.MaxValue;
        public bool Applied;
    }

    readonly Dictionary<PrisonerStatusSystem, Exposure> exposures = new();
    float remaining;
    bool spraying;
    float burstStart;
    float nextPulseTime;
    bool splashedBack;
    bool hitBody;

    // Spray time left in the canister, s
    public float Remaining => remaining;

    protected override void Awake()
    {
        base.Awake();
        remaining = capacity;
    }

    protected override void OnDisable()
    {
        if (spraying)
            StopSpraying();
        base.OnDisable();
    }

    protected override void OnUse()
    {
        if (remaining <= 0f)
        {
            if (HoldingHand.HasValue)
                Haptics.OCEmpty(HoldingHand.Value);
            TrainingLog.Record(TrainingEventType.Info, "OC trigger pressed, canister empty");
            return;
        }

        spraying = true;
        burstStart = Time.time;
        nextPulseTime = 0f;
        splashedBack = false;
        hitBody = false;
        exposures.Clear();

        if (sprayEffect != null)
            sprayEffect.Play();
        if (audioSource != null)
            audioSource.Play();
    }

    protected override void OnUseEnded()
    {
        if (spraying)
            StopSpraying();
    }

    void Update()
    {
        if (!spraying)
            return;

        remaining -= Time.deltaTime;
        if (HoldingHand.HasValue && Time.time >= nextPulseTime)
        {
            nextPulseTime = Time.time + 0.1f;
            Haptics.OCStream(HoldingHand.Value);
        }

        SprayFaces(Time.deltaTime);

        if (remaining <= 0f)
        {
            remaining = 0f;
            StopSpraying();
            TrainingLog.Record(TrainingEventType.Info, "OC canister empty");
        }
    }

    // Counts how long the stream sits on each face this burst
    void SprayFaces(float dt)
    {
        Vector3 origin = usePoint.position;
        Vector3 dir = usePoint.forward;
        float tanSpread = Mathf.Tan(streamSpread * Mathf.Deg2Rad);

        foreach (PrisonerPerception prisoner in PrisonerPerception.All)
        {
            Vector3 toFace = prisoner.EyePosition - origin;
            float along = Vector3.Dot(toFace, dir);
            if (along <= 0f || along > range)
                continue;

            // Distance from the stream's centre line to his eyes
            float off = (toFace - dir * along).magnitude;
            if (off > faceRadius + along * tanSpread)
            {
                // Still on him, just not the eyes: chest, shoulder or past the ear
                if (off < 0.6f)
                    hitBody = true;
                continue;
            }

            if (!prisoner.IsLineClearIgnoringPlayer(origin))
                continue;

            PrisonerStatusSystem status = prisoner.GetComponent<PrisonerStatusSystem>();
            if (status == null)
                continue;

            if (!exposures.TryGetValue(status, out Exposure e))
            {
                e = new Exposure();
                exposures[status] = e;
            }
            e.Dose += dt;
            e.Distance = Mathf.Min(e.Distance, along);

            if (!splashedBack && along < minSafeDistance)
                Splashback(along);

            if (!e.Applied && e.Dose >= doseToAffect)
            {
                e.Applied = true;
                status.ApplyOCSpray(effectDuration);
            }
        }
    }

    // Sprayed too close: the mist comes back into the officer's own eyes
    void Splashback(float distance)
    {
        splashedBack = true;
        PlayerRig rig = PlayerRig.Instance;
        PlayerFeedbackOverlay overlay = rig != null ? rig.GetComponent<PlayerFeedbackOverlay>() : null;
        if (overlay != null)
            overlay.Contaminate(splashbackDuration, 0.6f);
        TrainingLog.Record(TrainingEventType.TechniqueError,
            $"OC sprayed from {distance:0.0} m, closer than {minSafeDistance:0.0} m - officer contaminated", ForceKind.OCSpray);
    }

    void StopSpraying()
    {
        spraying = false;
        if (sprayEffect != null)
            sprayEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (audioSource != null)
            audioSource.Stop();

        float burst = Time.time - burstStart;
        string length = $"{burst:0.0} s burst";
        if (burst > longBurst)
            TrainingLog.Record(TrainingEventType.TechniqueError,
                $"OC burst of {burst:0.0} s, longer than {longBurst:0} s - use short bursts", ForceKind.OCSpray, burst);

        foreach (KeyValuePair<PrisonerStatusSystem, Exposure> pair in exposures)
        {
            Exposure e = pair.Value;
            string result = e.Applied ? "" : " - too short to take effect";
            TrainingLog.Record(TrainingEventType.ForceUsed,
                $"OC spray: {length}, {e.Dose:0.0} s in {pair.Key.DisplayName}'s eyes from {e.Distance:0.0} m{result}", ForceKind.OCSpray, burst);
        }

        if (exposures.Count == 0)
            TrainingLog.Record(TrainingEventType.ForceMissed,
                hitBody ? $"OC spray: {length} on the body, not the eyes - no effect" : $"OC spray: {length} missed",
                ForceKind.OCSpray, burst);

        exposures.Clear();
    }

    // Draws the stream's reach in the Scene view while the object is selected
    void OnDrawGizmosSelected()
    {
        if (usePoint == null)
            return;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
        Vector3 origin = usePoint.position;
        Vector3 forward = usePoint.forward;
        Vector3 end = origin + forward * range;
        float endRadius = faceRadius + Mathf.Tan(streamSpread * Mathf.Deg2Rad) * range;
        Gizmos.DrawLine(origin, end);

        const int segments = 16;
        Vector3 previous = Vector3.zero;
        for (int i = 0; i <= segments; i++)
        {
            Vector3 rim = end + Quaternion.AngleAxis(i * 360f / segments, forward) * usePoint.up * endRadius;
            if (i > 0)
                Gizmos.DrawLine(previous, rim);
            previous = rim;
        }

        // Splashback zone
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.8f);
        Gizmos.DrawLine(origin, origin + forward * minSafeDistance);
    }
}
