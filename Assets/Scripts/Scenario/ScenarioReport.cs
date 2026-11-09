using System.Collections.Generic;

public enum CheckResult
{
    Pass,
    Review,
    Fail,
    NotApplicable
}

public readonly struct ReportCheck
{
    public readonly string Title;
    public readonly CheckResult Result;
    public readonly string Detail;

    public ReportCheck(string title, CheckResult result, string detail)
    {
        Title = title;
        Result = result;
        Detail = detail;
    }
}

// Use-of-force review of one run, worked out from the training log
public class ScenarioReport
{
    // Three standard 5 s cycles, past this the risk to the subject climbs
    const float MaxTaserSeconds = 15f;

    readonly List<TrainingEvent> timeline;
    readonly List<ReportCheck> checks = new();

    public string ScenarioTitle { get; }
    public bool Succeeded { get; }
    public string Outcome { get; }
    public float Duration { get; }
    public IReadOnlyList<TrainingEvent> Timeline => timeline;
    public IReadOnlyList<ReportCheck> Checks => checks;

    public int Commands { get; private set; }
    public int Warnings { get; private set; }
    public int Obeyed { get; private set; }
    public int Refused { get; private set; }
    public int InmateAttacks { get; private set; }
    public int HitsTaken { get; private set; }
    public float DamageTaken { get; private set; }
    public int TaserShots { get; private set; }
    public int TaserMisses { get; private set; }
    public int TaserCycles { get; private set; }
    public float TaserSeconds { get; private set; }
    public int DriveStuns { get; private set; }
    public int OCBursts { get; private set; }
    public int OCMisses { get; private set; }
    public float OCSeconds { get; private set; }
    public int Strikes { get; private set; }

    public bool UsedTaser => TaserShots + TaserCycles + DriveStuns > 0;
    public bool UsedOC => OCBursts > 0;
    public bool UsedForce { get; private set; }

    public ScenarioReport(string scenarioTitle, bool succeeded, string outcome, float duration, IReadOnlyList<TrainingEvent> events)
    {
        ScenarioTitle = scenarioTitle;
        Succeeded = succeeded;
        Outcome = outcome;
        Duration = duration;
        timeline = new List<TrainingEvent>(events);
        Analyse();
    }

    void Analyse()
    {
        bool talkedFirst = false;
        bool warnedFirst = false;
        bool resistedFirst = false;
        bool attackedFirst = false;
        bool forceOnCompliant = false;
        bool headInjury = false;
        var ocIssues = new List<string>();

        foreach (TrainingEvent e in timeline)
        {
            bool before = !UsedForce;

            switch (e.Type)
            {
                case TrainingEventType.VerbalCommand:
                    Commands++;
                    talkedFirst |= before;
                    break;
                case TrainingEventType.Warning:
                    Warnings++;
                    talkedFirst |= before;
                    warnedFirst |= before;
                    break;
                case TrainingEventType.CommandObeyed:
                    Obeyed++;
                    break;
                case TrainingEventType.CommandRefused:
                    Refused++;
                    resistedFirst |= before;
                    break;
                case TrainingEventType.InmateAttack:
                    InmateAttacks++;
                    resistedFirst |= before;
                    attackedFirst |= before;
                    break;
                case TrainingEventType.OfficerHit:
                    HitsTaken++;
                    DamageTaken += e.Value;
                    break;
                case TrainingEventType.ForceUsed:
                case TrainingEventType.ForceMissed:
                    CountForce(e);
                    UsedForce = true;
                    break;
                case TrainingEventType.BodyHit:
                case TrainingEventType.HeadInjury:
                    // Probe hits are already counted as shots, only item strikes add force here
                    if (e.Kind == ForceKind.Impact)
                    {
                        Strikes++;
                        UsedForce = true;
                    }
                    headInjury |= e.Type == TrainingEventType.HeadInjury;
                    break;
                case TrainingEventType.ForceOnCompliant:
                    forceOnCompliant = true;
                    break;
                case TrainingEventType.TechniqueError:
                    if (e.Kind == ForceKind.OCSpray)
                        ocIssues.Add(e.Text);
                    break;
            }
        }

        AddChecks(talkedFirst, warnedFirst, resistedFirst, attackedFirst, forceOnCompliant, headInjury, ocIssues);
    }

    void CountForce(TrainingEvent e)
    {
        bool missed = e.Type == TrainingEventType.ForceMissed;
        switch (e.Kind)
        {
            case ForceKind.TaserProbes:
                TaserShots++;
                if (missed)
                    TaserMisses++;
                break;
            case ForceKind.TaserCycle:
                TaserCycles++;
                TaserSeconds += e.Value;
                break;
            case ForceKind.TaserDriveStun:
                DriveStuns++;
                break;
            case ForceKind.OCSpray:
                OCBursts++;
                OCSeconds += e.Value;
                if (missed)
                    OCMisses++;
                break;
        }
    }

    void AddChecks(bool talkedFirst, bool warnedFirst, bool resistedFirst, bool attackedFirst,
        bool forceOnCompliant, bool headInjury, List<string> ocIssues)
    {
        if (!UsedForce)
        {
            checks.Add(new ReportCheck("Resolved without force", CheckResult.Pass, "No force was used"));
        }
        else
        {
            checks.Add(talkedFirst
                ? new ReportCheck("Talked before force", CheckResult.Pass, "Commands were given before the first use of force")
                : new ReportCheck("Talked before force", CheckResult.Fail, "Force came before any command"));

            if (warnedFirst)
                checks.Add(new ReportCheck("Warned before force", CheckResult.Pass, "A force warning was given first"));
            else if (attackedFirst)
                checks.Add(new ReportCheck("Warned before force", CheckResult.Review, "No warning, but he attacked first - was there time to warn?"));
            else
                checks.Add(new ReportCheck("Warned before force", CheckResult.Fail, "No warning before force"));

            checks.Add(resistedFirst
                ? new ReportCheck("Force only against resistance", CheckResult.Pass, "He refused or attacked before force was used")
                : new ReportCheck("Force only against resistance", CheckResult.Review, "He had not refused an order or attacked yet - was force necessary?"));

            checks.Add(forceOnCompliant
                ? new ReportCheck("No force on a complying inmate", CheckResult.Fail, "Force hit him while he was doing what he was told")
                : new ReportCheck("No force on a complying inmate", CheckResult.Pass, "Force stopped once he complied"));
        }

        checks.Add(headInjury
            ? new ReportCheck("Head never targeted", CheckResult.Fail, "Something hit his head - never a legal target")
            : new ReportCheck("Head never targeted", UsedForce ? CheckResult.Pass : CheckResult.NotApplicable, "No hits to the head"));

        if (UsedTaser)
        {
            string detail = $"{TaserShots} cartridge(s), {TaserMisses} missed, {TaserCycles} cycle(s) = {TaserSeconds:0} s, {DriveStuns} drive stun(s)";
            if (TaserSeconds > MaxTaserSeconds)
                checks.Add(new ReportCheck("Taser", CheckResult.Fail, $"{detail} - over {MaxTaserSeconds:0} s raises the risk to him"));
            else if (TaserCycles > 1)
                checks.Add(new ReportCheck("Taser", CheckResult.Review, $"{detail} - each extra cycle needs its own reason"));
            else
                checks.Add(new ReportCheck("Taser", CheckResult.Pass, detail));
        }

        if (UsedOC)
        {
            string detail = $"{OCBursts} burst(s), {OCSeconds:0.0} s, {OCMisses} without effect";
            checks.Add(ocIssues.Count > 0
                ? new ReportCheck("OC spray", CheckResult.Review, $"{detail}; {string.Join("; ", ocIssues)}")
                : new ReportCheck("OC spray", CheckResult.Pass, detail));
        }
    }
}
