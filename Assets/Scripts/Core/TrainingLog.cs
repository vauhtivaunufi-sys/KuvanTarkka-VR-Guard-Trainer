using System;
using System.Collections.Generic;
using UnityEngine;

public enum TrainingEventType
{
    VerbalCommand,
    CommandObeyed,
    CommandRefused,
    PrisonerState,
    Restraint,
    ForceUsed,
    ForceMissed,
    Info,
    // Probe or strike landed on a body zone
    BodyHit,
    // Anything that reached his head, the scenario fails
    HeadInjury,
    // Verbal force warning or a taser warning arc
    Warning,
    InmateAttack,
    OfficerHit,
    OfficerDown,
    // Force landed on an inmate who was doing what he was told
    ForceOnCompliant,
    // Equipment used against its training: an OC burst too long, sprayed too close
    TechniqueError
}

// Which kind of force an event was, so the report can count them
public enum ForceKind
{
    None,
    TaserProbes,
    TaserCycle,
    TaserDriveStun,
    OCSpray,
    // An item hitting him: strike or throw
    Impact
}

public readonly struct TrainingEvent
{
    public readonly float Time;
    public readonly TrainingEventType Type;
    public readonly string Text;
    public readonly ForceKind Kind;
    // Seconds of a taser cycle or an OC burst, damage of a blow
    public readonly float Value;

    public TrainingEvent(float time, TrainingEventType type, string text, ForceKind kind, float value)
    {
        Time = time;
        Type = type;
        Text = text;
        Kind = kind;
        Value = value;
    }
}

// Timeline of one training run, read by the use-of-force report
public static class TrainingLog
{
    static readonly List<TrainingEvent> events = new();
    static float startTime;

    public static event Action<TrainingEvent> EventRecorded;

    public static IReadOnlyList<TrainingEvent> Events => events;

    // Seconds since the run started
    public static float Elapsed => Time.time - startTime;

    // Statics survive between Play sessions when domain reload is off
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        events.Clear();
        startTime = 0f;
        EventRecorded = null;
    }

    // Starts a fresh timeline when a scenario starts
    public static void Begin()
    {
        events.Clear();
        startTime = Time.time;
    }

    public static void Record(TrainingEventType type, string text)
    {
        Record(type, text, ForceKind.None, 0f);
    }

    public static void Record(TrainingEventType type, string text, ForceKind kind, float value = 0f)
    {
        var e = new TrainingEvent(Elapsed, type, text, kind, value);
        events.Add(e);
        TrainingDebug.Log($"[Log {FormatTime(e.Time)}] {type}: {text}");
        EventRecorded?.Invoke(e);
    }

    public static string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{s / 60:00}:{s % 60:00}";
    }
}
