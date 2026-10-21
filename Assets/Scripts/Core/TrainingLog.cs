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
    Info
}

public readonly struct TrainingEvent
{
    public readonly float Time;
    public readonly TrainingEventType Type;
    public readonly string Text;

    public TrainingEvent(float time, TrainingEventType type, string text)
    {
        Time = time;
        Type = type;
        Text = text;
    }
}

// Timeline of one training run, the base for the use-of-force report in step 10
public static class TrainingLog
{
    static readonly List<TrainingEvent> events = new();
    static float startTime;

    public static event Action<TrainingEvent> EventRecorded;

    public static IReadOnlyList<TrainingEvent> Events => events;

    // Statics survive between Play sessions when domain reload is off
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        events.Clear();
        startTime = 0f;
        EventRecorded = null;
    }

    // Starts a fresh timeline, called by the scenario system from step 5
    public static void Begin()
    {
        events.Clear();
        startTime = Time.time;
    }

    public static void Record(TrainingEventType type, string text)
    {
        var e = new TrainingEvent(Time.time - startTime, type, text);
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
