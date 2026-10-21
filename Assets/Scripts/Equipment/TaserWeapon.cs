using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Two-cartridge TASER 7: A/X safety and lasers, trigger fires, B/Y arc, both probes far enough apart to incapacitate
public class TaserWeapon : GrabbableWeapon
{
    [Header("Cartridges")]
    [SerializeField] int cartridgeCount = 2;

    [Tooltip("Probe reach = wire length, m. TASER 7 CQ 3.7, standard cartridges 4.6 or 7.6")]
    [SerializeField] float maxRange = 3.7f;

    [Tooltip("Angle between the two probes, degrees. TASER 7 CQ 12, TASER 7 standard 3.5, older X2 about 8")]
    [SerializeField] float probeSpreadAngle = 12f;

    [Tooltip("Probe flight speed, m/s")]
    [SerializeField] float probeSpeed = 55f;

    [Tooltip("Probes closer than this on the body only hurt, m")]
    [SerializeField] float minEffectiveSpread = 0.1f;

    [Header("Cycle")]
    [Tooltip("One energy cycle, s")]
    [SerializeField] float cycleDuration = 5f;

    [Tooltip("Time on the ground after a cycle, the window to get control of him, s")]
    [SerializeField] float postCycleRecovery = 3f;

    [Tooltip("Pain-only jolt from a drive stun or probes too close together, s")]
    [SerializeField] float painOnlyDuration = 1.5f;

    [Tooltip("Muzzle this close to him counts as a drive stun, m")]
    [SerializeField] float driveStunReach = 0.15f;

    [Tooltip("How far a warning arc is seen and heard, m")]
    [SerializeField] float warningArcRange = 8f;

    [Header("Visuals")]
    [Tooltip("Line for the top laser, can stay empty")]
    [SerializeField] LineRenderer beamRenderer;

    [SerializeField] Color laserColor = new Color(1f, 0.1f, 0.1f, 1f);
    [SerializeField] Color wireColor = new Color(0.8f, 0.8f, 0.75f, 1f);

    [Tooltip("SAFE / ARMED readout, a TextMeshPro object over the back of the taser")]
    [SerializeField] TextMeshPro display;

    [Header("Audio, optional")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip safetyClip;
    [SerializeField] AudioClip fireClip;
    [SerializeField] AudioClip arcClip;
    [SerializeField] AudioClip dryFireClip;

    const float LaserReach = 15f;


    // The two probes of one cartridge, stuck in someone or something
    class Deployment
    {
        public LineRenderer WireA;
        public LineRenderer WireB;
        public Transform AnchorA;
        public Transform AnchorB;
        public Vector3 LocalA;
        public Vector3 LocalB;
        public PrisonerStatusSystem Target;
        public bool Connected;
        public bool PainOnly;
    }

    static readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    static readonly Collider[] overlapBuffer = new Collider[16];

    readonly List<Deployment> deployments = new();
    readonly Dictionary<PrisonerStatusSystem, int> cyclesPerTarget = new();

    bool armed;
    int cartridgesLeft;
    LineRenderer topLaser;
    LineRenderer bottomLaser;
    LineRenderer topDot;
    LineRenderer bottomDot;
    LineRenderer arcSpark;
    float arcSparkUntil;

    public bool IsArmed => armed;
    public int CartridgesLeft => cartridgesLeft;

    // Armed, B/Y is the arc, so the command menu stays off this hand
    public override bool UsesSecondaryButton => armed;

    protected override void Awake()
    {
        base.Awake();
        cartridgesLeft = cartridgeCount;

        Color beam = new Color(laserColor.r, laserColor.g, laserColor.b, 0.25f);
        topLaser = beamRenderer;
        if (topLaser != null)
        {
            topLaser.positionCount = 2;
            topLaser.useWorldSpace = true;
            topLaser.startWidth = topLaser.endWidth = 0.0015f;
            topLaser.startColor = topLaser.endColor = beam;
            topLaser.sharedMaterial = LineVisuals.SharedMaterial();
        }
        else
        {
            topLaser = LineVisuals.Create($"{name} Top Laser", 0.0015f, beam);
        }
        // The TASER 7 has a second laser for where the bottom probe lands
        bottomLaser = LineVisuals.Create($"{name} Bottom Laser", 0.0015f, beam);
        topDot = LineVisuals.Create($"{name} Top Dot", 0.012f, laserColor);
        bottomDot = LineVisuals.Create($"{name} Bottom Dot", 0.012f, laserColor);
        arcSpark = LineVisuals.Create($"{name} Arc", 0.003f, new Color(0.6f, 0.8f, 1f, 1f));
        arcSpark.positionCount = 8;
        // Kept under the taser so the scene root stays clean, world-space lines ignore the parent
        foreach (LineRenderer line in new[] { topLaser, bottomLaser, topDot, bottomDot, arcSpark })
        {
            // The prefab's own beam lives on the taser itself
            if (line.transform != transform)
                line.transform.SetParent(transform, true);
        }
        HideLasers();
        arcSpark.enabled = false;

        UpdateDisplay();
    }

    void OnDestroy()
    {
        foreach (Deployment d in deployments)
            DestroyDeployment(d);
        deployments.Clear();
        DestroyLine(bottomLaser);
        DestroyLine(topDot);
        DestroyLine(bottomDot);
        DestroyLine(arcSpark);
        if (beamRenderer == null)
            DestroyLine(topLaser);
    }

    // After the hand and the head have moved this frame
    void LateUpdate()
    {
        FaceEyes();
    }

    void Update()
    {
        UpdateWires();
        UpdateArcSpark();
        UpdateDisplay();

        // Lying around or the game paused: no buttons, no lasers
        if (!IsHeld || Time.timeScale <= 0f)
        {
            HideLasers();
            return;
        }

        Hand hand = HoldingHand.Value;
        if (ControllerInput.GetButtonDown(hand, ControllerButton.Primary))
            ToggleSafety(hand);
        else if (armed && ControllerInput.GetButtonDown(hand, ControllerButton.Secondary))
            Arc(hand);

        if (armed)
            UpdateLasers();
        else
            HideLasers();
    }

    void ToggleSafety(Hand hand)
    {
        armed = !armed;
        Play(safetyClip);
        Haptics.TaserSafety(hand);
        TrainingLog.Record(TrainingEventType.Info, armed ? "Taser armed" : "Taser on safe");
    }

    void UpdateLasers()
    {
        Vector3 origin = usePoint.position;
        Vector3 up = usePoint.up * 0.01f;

        bool topHit = DrawLaser(topLaser, topDot, origin + up, usePoint.forward, out RaycastHit hit);
        DrawLaser(bottomLaser, bottomDot, origin - up, BottomProbeDirection(), out _);

        // The red dot on his chest is a strong deterrent on its own
        if (topHit)
        {
            PrisonerCompliance compliance = hit.collider.GetComponentInParent<PrisonerCompliance>();
            if (compliance != null)
                compliance.NotifyTaserAimed();
        }
    }

    bool DrawLaser(LineRenderer beam, LineRenderer dot, Vector3 origin, Vector3 direction, out RaycastHit hit)
    {
        bool hitSomething = RaycastIgnoringPlayer(origin, direction, LaserReach, out hit);
        Vector3 end = hitSomething ? hit.point : origin + direction * LaserReach;

        beam.enabled = true;
        beam.SetPosition(0, origin);
        beam.SetPosition(1, end);

        dot.enabled = hitSomething;
        if (hitSomething)
        {
            // A tiny disc lying on the surface
            dot.SetPosition(0, end + hit.normal * 0.002f);
            dot.SetPosition(1, end + hit.normal * 0.004f);
        }
        return hitSomething;
    }

    void HideLasers()
    {
        topLaser.enabled = false;
        bottomLaser.enabled = false;
        topDot.enabled = false;
        bottomDot.enabled = false;
    }

    // Bottom probe leaves at the spread angle below the top one
    Vector3 BottomProbeDirection()
    {
        return Quaternion.AngleAxis(probeSpreadAngle, usePoint.right) * usePoint.forward;
    }

    protected override void OnUse()
    {
        // On safe the trigger does nothing, like the real one
        if (!armed || !IsHeld)
            return;

        Hand hand = HoldingHand.Value;
        if (cartridgesLeft <= 0)
        {
            Play(dryFireClip);
            Haptics.TaserDryFire(hand);
            TrainingLog.Record(TrainingEventType.Info, "Taser trigger pulled, no cartridges left");
            return;
        }

        cartridgesLeft--;
        Play(fireClip);
        Haptics.TaserFire(hand);
        PrisonerPerception.ReportNoise(usePoint.position, 15f, fromPlayer: true);
        StartCoroutine(FireProbes(hand));
    }

    IEnumerator FireProbes(Hand hand)
    {
        Vector3 up = usePoint.up * 0.01f;
        Vector3 originA = usePoint.position + up;
        Vector3 originB = usePoint.position - up;
        Vector3 dirA = usePoint.forward;
        Vector3 dirB = BottomProbeDirection();

        bool hitA = RaycastIgnoringPlayer(originA, dirA, maxRange, out RaycastHit a);
        bool hitB = RaycastIgnoringPlayer(originB, dirB, maxRange, out RaycastHit b);
        Vector3 endA = hitA ? a.point : originA + dirA * maxRange;
        Vector3 endB = hitB ? b.point : originB + dirB * maxRange;

        // Probes are fast but not instant, a target moving at the right moment matters
        float flight = Mathf.Max(Vector3.Distance(originA, endA), Vector3.Distance(originB, endB)) / probeSpeed;
        yield return new WaitForSeconds(flight);

        PrisonerStatusSystem targetA = hitA ? a.collider.GetComponentInParent<PrisonerStatusSystem>() : null;
        PrisonerStatusSystem targetB = hitB ? b.collider.GetComponentInParent<PrisonerStatusSystem>() : null;

        var d = new Deployment
        {
            WireA = LineVisuals.Create($"{name} Wire A", 0.0012f, wireColor),
            WireB = LineVisuals.Create($"{name} Wire B", 0.0012f, wireColor),
            AnchorA = hitA ? a.collider.transform : null,
            AnchorB = hitB ? b.collider.transform : null
        };
        d.WireA.transform.SetParent(transform, true);
        d.WireB.transform.SetParent(transform, true);
        d.LocalA = d.AnchorA != null ? d.AnchorA.InverseTransformPoint(endA) : endA;
        d.LocalB = d.AnchorB != null ? d.AnchorB.InverseTransformPoint(endB) : endB;
        deployments.Add(d);

        float distance = Vector3.Distance(usePoint.position, (endA + endB) * 0.5f);
        string at = $"at {distance:0.0} m, {cartridgesLeft} cartridge(s) left";

        if (targetA != null && targetA == targetB)
        {
            float spread = Vector3.Distance(endA, endB);
            d.Target = targetA;
            d.Connected = true;

            if (spread >= minEffectiveSpread)
            {
                TrainingLog.Record(TrainingEventType.ForceUsed,
                    $"Taser: both probes hit {targetA.DisplayName} {at}, spread {spread * 100f:0} cm - incapacitated");
                Energize(targetA, hand);
            }
            else
            {
                // Current crosses only a few centimetres of muscle: it hurts, nothing locks up
                d.PainOnly = true;
                TrainingLog.Record(TrainingEventType.ForceUsed,
                    $"Taser: probes only {spread * 100f:0} cm apart on {targetA.DisplayName} {at} - pain only, too close");
                targetA.ApplyStagger(painOnlyDuration);
            }
        }
        else if (targetA != null || targetB != null)
        {
            PrisonerStatusSystem t = targetA != null ? targetA : targetB;
            TrainingLog.Record(TrainingEventType.ForceUsed, $"Taser: only one probe hit {t.DisplayName} {at} - no circuit, no effect");
        }
        else
        {
            TrainingLog.Record(TrainingEventType.ForceMissed, $"Taser: both probes missed {at}");
        }
    }

    void Energize(PrisonerStatusSystem target, Hand hand)
    {
        target.ApplyTaser(cycleDuration + postCycleRecovery);
        Haptics.TaserCycle(hand, cycleDuration);

        cyclesPerTarget.TryGetValue(target, out int cycles);
        cycles++;
        cyclesPerTarget[target] = cycles;
        TrainingLog.Record(TrainingEventType.ForceUsed, $"Taser cycle {cycles} on {target.DisplayName}, {cycleDuration:0} s");
    }

    // Arc button: another cycle through connected probes, else a drive stun, else a warning arc
    void Arc(Hand hand)
    {
        Play(arcClip);
        Haptics.TaserArc(hand);
        arcSparkUntil = Time.time + 0.4f;

        foreach (Deployment d in deployments)
        {
            if (!d.Connected || d.Target == null || d.Target.IsDetained)
                continue;

            if (d.PainOnly)
            {
                TrainingLog.Record(TrainingEventType.ForceUsed, $"Taser re-energized on {d.Target.DisplayName} - pain only, probes too close");
                d.Target.ApplyStagger(painOnlyDuration);
            }
            else
            {
                Energize(d.Target, hand);
            }
            return;
        }

        PrisonerStatusSystem contact = FindContactTarget();
        if (contact != null)
        {
            TrainingLog.Record(TrainingEventType.ForceUsed, $"Taser drive stun on {contact.DisplayName} - pain only");
            contact.ApplyStagger(painOnlyDuration);
            return;
        }

        // Seen and heard crackle, a last warning before firing
        TrainingLog.Record(TrainingEventType.Info, "Taser warning arc");
        PrisonerPerception.ReportNoise(usePoint.position, warningArcRange, fromPlayer: true);
        foreach (PrisonerPerception p in PrisonerPerception.All)
        {
            if (p.DistanceToPlayer > warningArcRange || !p.CanSeePlayer)
                continue;
            PrisonerCompliance compliance = p.GetComponent<PrisonerCompliance>();
            if (compliance != null)
                compliance.NotifyWarningArc();
        }
    }

    PrisonerStatusSystem FindContactTarget()
    {
        int count = Physics.OverlapSphereNonAlloc(usePoint.position, driveStunReach, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            PrisonerStatusSystem s = overlapBuffer[i].GetComponentInParent<PrisonerStatusSystem>();
            if (s != null)
                return s;
        }
        return null;
    }

    void UpdateWires()
    {
        Vector3 muzzle = usePoint.position;
        // Stretched past their length the wires snap
        float limit = maxRange + 0.5f;

        for (int i = deployments.Count - 1; i >= 0; i--)
        {
            Deployment d = deployments[i];
            Vector3 pa = d.AnchorA != null ? d.AnchorA.TransformPoint(d.LocalA) : d.LocalA;
            Vector3 pb = d.AnchorB != null ? d.AnchorB.TransformPoint(d.LocalB) : d.LocalB;

            if (Vector3.Distance(muzzle, pa) > limit || Vector3.Distance(muzzle, pb) > limit)
            {
                if (d.Connected)
                    TrainingLog.Record(TrainingEventType.Info, "Taser wires broke, connection lost");
                DestroyDeployment(d);
                deployments.RemoveAt(i);
                continue;
            }

            DrawWire(d.WireA, muzzle, pa);
            DrawWire(d.WireB, muzzle, pb);
        }
    }

    // A slight sag reads as a wire, not a laser
    static void DrawWire(LineRenderer wire, Vector3 from, Vector3 to)
    {
        Vector3 mid = (from + to) * 0.5f + Vector3.down * Mathf.Min(0.15f, Vector3.Distance(from, to) * 0.04f);
        wire.positionCount = 3;
        wire.SetPosition(0, from);
        wire.SetPosition(1, mid);
        wire.SetPosition(2, to);
    }

    // Jagged blue-white crackle in front of the muzzle
    void UpdateArcSpark()
    {
        bool on = Time.time < arcSparkUntil;
        arcSpark.enabled = on;
        if (!on)
            return;

        Vector3 start = usePoint.position + usePoint.up * 0.012f;
        Vector3 end = usePoint.position - usePoint.up * 0.012f;
        for (int i = 0; i < arcSpark.positionCount; i++)
        {
            float t = i / (arcSpark.positionCount - 1f);
            Vector3 jitter = i == 0 || i == arcSpark.positionCount - 1 ? Vector3.zero : Random.insideUnitSphere * 0.006f;
            arcSpark.SetPosition(i, Vector3.Lerp(start, end, t) + usePoint.forward * 0.005f + jitter);
        }
    }

    // Turns the readout to the officer's eyes so it reads at any angle the taser is held
    void FaceEyes()
    {
        PlayerRig rig = PlayerRig.Instance;
        if (display == null || rig == null || rig.Head == null)
            return;

        Vector3 away = display.transform.position - rig.HeadPosition;
        if (away.sqrMagnitude > 0.0001f)
            display.transform.rotation = Quaternion.LookRotation(away, Vector3.up);
    }

    void UpdateDisplay()
    {
        if (display == null)
            return;
        string text = armed ? $"ARMED  {cartridgesLeft}" : "SAFE";
        Color color = armed ? new Color(1f, 0.3f, 0.2f) : new Color(0.4f, 0.9f, 0.5f);
        // Colour checked too, the prefab may already say SAFE in white
        if (display.text != text || display.color != color)
        {
            display.text = text;
            display.color = color;
        }
    }

    // Skips the officer's own body and anything held, this taser included
    bool RaycastIgnoringPlayer(Vector3 origin, Vector3 direction, float range, out RaycastHit result)
    {
        int count = Physics.RaycastNonAlloc(origin, direction, hitBuffer, range, ~0, QueryTriggerInteraction.Ignore);
        float best = float.PositiveInfinity;
        result = default;
        PlayerRig rig = PlayerRig.Instance;

        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hitBuffer[i];
            Transform t = h.collider.transform;
            if (t.IsChildOf(transform) || (rig != null && rig.IsPartOfPlayer(t)))
                continue;
            GrabbableWeapon item = t.GetComponentInParent<GrabbableWeapon>();
            if (item != null && item.IsHeld)
                continue;
            if (h.distance < best)
            {
                best = h.distance;
                result = h;
            }
        }
        return best < float.PositiveInfinity;
    }

    void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    static void DestroyDeployment(Deployment d)
    {
        DestroyLine(d.WireA);
        DestroyLine(d.WireB);
    }

    static void DestroyLine(LineRenderer line)
    {
        if (line != null)
            Destroy(line.gameObject);
    }

    void OnDrawGizmosSelected()
    {
        if (usePoint == null)
            return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(usePoint.position, usePoint.position + usePoint.forward * maxRange);
        Gizmos.DrawLine(usePoint.position, usePoint.position + Quaternion.AngleAxis(probeSpreadAngle, usePoint.right) * usePoint.forward * maxRange);
    }
}
