using System.Collections.Generic;
using UnityEngine;

// What the prisoner knows about the officer: sight (view cone + line of sight, walls block it), hearing (doors, taser shots,
// shouted commands) and short-term memory of where the officer was last seen. Chasing and attacking are gated on this, so the
// prisoner no longer "sees" through walls or swings at someone on the other side of a closed door.
[RequireComponent(typeof(PrisonerStatusSystem))]
public class PrisonerPerception : MonoBehaviour
{
    static readonly List<PrisonerPerception> all = new();

    [Tooltip("How far the prisoner can see, in meters.")]
    [SerializeField] float viewDistance = 14f;

    [Tooltip("Full horizontal field of view, in degrees.")]
    [SerializeField] float viewAngle = 150f;

    [Tooltip("Within this distance he notices the officer even without looking (footsteps, presence), in meters.")]
    [SerializeField] float awarenessRadius = 1.8f;

    [Tooltip("How long he remembers the officer's last position after losing sight, in seconds.")]
    [SerializeField] float memoryDuration = 6f;

    [Tooltip("Layers that block sight. Everything by default; the prisoner and the player rig are ignored automatically.")]
    [SerializeField] LayerMask occlusionMask = ~0;

    [Tooltip("Seconds between line-of-sight checks. Raycasts are not free on Quest.")]
    [SerializeField] float checkInterval = 0.1f;

    static readonly RaycastHit[] hitBuffer = new RaycastHit[16];

    PrisonerStatusSystem status;
    Animator animator;
    Transform eye;
    float nextCheckTime;
    float lastSeenTime = float.NegativeInfinity;
    float lastHeardTime = float.NegativeInfinity;

    // True if the officer is currently in view with a clear line of sight.
    public bool CanSeePlayer { get; private set; }

    // True while the prisoner is aware of the officer: seen or heard within memoryDuration.
    public bool IsAware => Time.time - Mathf.Max(lastSeenTime, lastHeardTime) <= memoryDuration;

    // Last known position of the officer's feet.
    public Vector3 LastKnownPlayerPosition { get; private set; }

    public float TimeSinceSeen => Time.time - lastSeenTime;

    // Ground-truth horizontal distance to the officer (not limited by perception) - for range checks that need it.
    public float DistanceToPlayer { get; private set; } = float.PositiveInfinity;

    public Vector3 EyePosition => eye != null ? eye.position : transform.position + Vector3.up * 1.65f;

    void Awake()
    {
        status = GetComponent<PrisonerStatusSystem>();
        animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
            eye = animator.GetBoneTransform(HumanBodyBones.Head);
    }

    // Returns the prisoner's perception, adding one with default settings if the prefab doesn't have it yet
    // (Tools > KuvanTarkka > Setup adds it to the prefab so the values can be tuned).
    public static PrisonerPerception GetOrAdd(GameObject prisoner)
    {
        PrisonerPerception p = prisoner.GetComponent<PrisonerPerception>();
        return p != null ? p : prisoner.AddComponent<PrisonerPerception>();
    }

    void OnEnable() => all.Add(this);

    void OnDisable() => all.Remove(this);

    void Update()
    {
        PlayerRig rig = PlayerRig.Instance;
        if (rig == null || rig.Head == null)
        {
            CanSeePlayer = false;
            return;
        }

        Vector3 flat = rig.FeetPosition - transform.position;
        flat.y = 0f;
        DistanceToPlayer = flat.magnitude;

        if (Time.time < nextCheckTime)
            return;
        nextCheckTime = Time.time + checkInterval;

        CanSeePlayer = CheckSight(rig);
        if (CanSeePlayer)
        {
            lastSeenTime = Time.time;
            LastKnownPlayerPosition = rig.FeetPosition;
        }
    }

    bool CheckSight(PlayerRig rig)
    {
        // OC in the eyes: effectively blind, but still feels someone right next to him.
        bool blinded = status.CurrentConditional == PrisonerConditional.InPain;
        if (status.CurrentConditional == PrisonerConditional.Electrocuted)
            return false;

        if (DistanceToPlayer <= awarenessRadius)
            return HasLineOfSight(rig.ChestPosition, rig);

        if (blinded || DistanceToPlayer > viewDistance)
            return false;

        Vector3 toHead = rig.HeadPosition - EyePosition;
        Vector3 flatDir = new Vector3(toHead.x, 0f, toHead.z);
        if (Vector3.Angle(transform.forward, flatDir) > viewAngle * 0.5f)
            return false;

        return HasLineOfSight(rig.HeadPosition, rig) || HasLineOfSight(rig.ChestPosition, rig);
    }

    bool HasLineOfSight(Vector3 target, PlayerRig rig)
    {
        Vector3 origin = EyePosition;
        Vector3 dir = target - origin;
        float dist = dir.magnitude;
        if (dist < 0.01f)
            return true;

        int count = Physics.RaycastNonAlloc(origin, dir / dist, hitBuffer, dist, occlusionMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform t = hitBuffer[i].collider.transform;
            if (t.IsChildOf(transform) || rig.IsPartOfPlayer(t))
                continue;
            // Equipment in the officer's hands (and loose on the floor) doesn't hide him.
            if (t.GetComponentInParent<GrabbableWeapon>() != null)
                continue;
            return false;
        }
        return true;
    }

    // Something made a noise. fromPlayer noises reveal where the officer is.
    public void Hear(Vector3 position, bool fromPlayer)
    {
        if (status.CurrentConditional == PrisonerConditional.Electrocuted)
            return;

        if (fromPlayer)
        {
            lastHeardTime = Time.time;
            LastKnownPlayerPosition = position;
        }
    }

    // Broadcasts a noise to every prisoner within radius. Walls halve the radius (muffled).
    public static void ReportNoise(Vector3 position, float radius, bool fromPlayer)
    {
        for (int i = all.Count - 1; i >= 0; i--)
        {
            PrisonerPerception p = all[i];
            float d = Vector3.Distance(p.EyePosition, position);
            if (d > radius)
                continue;

            bool muffled = !p.IsLineClearIgnoringPlayer(position);
            if (muffled && d > radius * 0.5f)
                continue;

            p.Hear(position, fromPlayer);
        }
    }

    // Line check that treats the player's own colliders and equipment as transparent.
    bool IsLineClearIgnoringPlayer(Vector3 target)
    {
        PlayerRig rig = PlayerRig.Instance;
        return rig != null && HasLineOfSight(target, rig);
    }

    public static IReadOnlyList<PrisonerPerception> All => all;

    void OnDrawGizmosSelected()
    {
        Vector3 origin = Application.isPlaying ? EyePosition : transform.position + Vector3.up * 1.65f;
        Gizmos.color = CanSeePlayer ? Color.red : new Color(1f, 1f, 0f, 0.6f);
        Quaternion left = Quaternion.AngleAxis(-viewAngle * 0.5f, Vector3.up);
        Quaternion right = Quaternion.AngleAxis(viewAngle * 0.5f, Vector3.up);
        Gizmos.DrawLine(origin, origin + left * transform.forward * viewDistance);
        Gizmos.DrawLine(origin, origin + right * transform.forward * viewDistance);
        Gizmos.DrawWireSphere(transform.position, awarenessRadius);
        if (Application.isPlaying && IsAware)
            Gizmos.DrawLine(origin, LastKnownPlayerPosition);
    }
}
