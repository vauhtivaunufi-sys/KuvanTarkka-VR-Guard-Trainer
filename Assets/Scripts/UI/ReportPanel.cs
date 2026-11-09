using System.Text;
using TMPro;
using UnityEngine;

// The report after a run: outcome, use-of-force checks and a paged timeline, built from code like the other demo panels
public class ReportPanel
{
    const int LinesPerPage = 14;

    // Hex colors: TextMeshPro knows only a few color names
    const string Green = "#4CE06A";
    const string Amber = "#FFC94D";
    const string Red = "#FF5A4D";
    const string Grey = "#9AA6B2";

    readonly ScenarioReport report;
    readonly TextMeshProUGUI timelineText;
    readonly TextMeshProUGUI pageText;
    readonly VrButton previousButton;
    readonly VrButton nextButton;
    int page;

    int PageCount => Mathf.Max(1, Mathf.CeilToInt(report.Timeline.Count / (float)LinesPerPage));

    public ReportPanel(ScenarioReport scenarioReport)
    {
        report = scenarioReport;

        RectTransform panel = VrUi.CreatePanel("Report", new Vector2(1300f, 900f));
        string title = report.Succeeded ? $"<color={Green}>SCENARIO COMPLETE</color>" : $"<color={Red}>SCENARIO FAILED</color>";
        VrUi.Text(panel, title, 40f, 28f, 1220f, 60f, 42f, bold: true);

        VrUi.Text(panel, "SUMMARY", 40f, 100f, 600f, 28f, 20f, true, VrUi.MutedColor);
        VrUi.Text(panel, BuildSummary(), 40f, 132f, 600f, 230f, 24f);
        VrUi.Text(panel, "CHECKS", 40f, 374f, 600f, 28f, 20f, true, VrUi.MutedColor);
        VrUi.Text(panel, BuildChecks(), 40f, 406f, 600f, 454f, 22f);

        VrUi.Text(panel, "TIMELINE", 680f, 100f, 580f, 28f, 20f, true, VrUi.MutedColor);
        VrUi.Card(panel, 670f, 132f, 600f, 560f);
        timelineText = VrUi.Text(panel, "", 690f, 146f, 560f, 532f, 20f);

        previousButton = VrUi.Button(panel, "<", 680f, 712f, 140f, 60f, 30f, () => ShowPage(page - 1));
        pageText = VrUi.Text(panel, "", 830f, 712f, 280f, 60f, 22f, color: VrUi.MutedColor, align: TextAlignmentOptions.Center);
        nextButton = VrUi.Button(panel, ">", 1120f, 712f, 140f, 60f, 30f, () => ShowPage(page + 1));

        VrUi.Button(panel, "Restart", 680f, 792f, 280f, 80f, 30f, ScenarioSession.Restart);
        VrUi.Button(panel, "Main menu", 980f, 792f, 280f, 80f, 30f, ScenarioSession.BackToMenu, primary: true);

        ShowPage(0);
        VrUi.PlaceAhead(panel);
    }

    string BuildSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<b>{report.ScenarioTitle}</b>");
        sb.AppendLine($"{report.Outcome}  ({TrainingLog.FormatTime(report.Duration)})");
        sb.AppendLine($"Commands: {report.Commands} + {report.Warnings} warning(s), {report.Obeyed} obeyed, {report.Refused} refused");
        sb.AppendLine($"Inmate attacks: {report.InmateAttacks}, officer hit {report.HitsTaken} time(s), -{report.DamageTaken:0} HP");

        if (!report.UsedForce)
            sb.AppendLine("Force: none");
        if (report.UsedTaser)
            sb.AppendLine($"Taser: {report.TaserShots} cartridge(s), {report.TaserCycles} cycle(s) ({report.TaserSeconds:0} s), {report.DriveStuns} drive stun(s)");
        if (report.UsedOC)
            sb.AppendLine($"OC: {report.OCBursts} burst(s), {report.OCSeconds:0.0} s");
        if (report.Strikes > 0)
            sb.AppendLine($"Strikes with items: {report.Strikes}");
        return sb.ToString();
    }

    string BuildChecks()
    {
        var sb = new StringBuilder();
        foreach (ReportCheck check in report.Checks)
        {
            sb.AppendLine($"{Badge(check.Result)}  <b>{check.Title}</b>");
            sb.AppendLine($"<size=80%><color={Grey}>{check.Detail}</color></size>");
        }
        return sb.ToString();
    }

    static string Badge(CheckResult result)
    {
        switch (result)
        {
            case CheckResult.Pass: return $"<color={Green}>OK</color>";
            case CheckResult.Review: return $"<color={Amber}>REVIEW</color>";
            case CheckResult.Fail: return $"<color={Red}>FAIL</color>";
            default: return $"<color={Grey}>N/A</color>";
        }
    }

    void ShowPage(int index)
    {
        page = Mathf.Clamp(index, 0, PageCount - 1);

        var sb = new StringBuilder();
        int first = page * LinesPerPage;
        int last = Mathf.Min(first + LinesPerPage, report.Timeline.Count);
        for (int i = first; i < last; i++)
        {
            TrainingEvent e = report.Timeline[i];
            sb.AppendLine($"<color={Grey}>{TrainingLog.FormatTime(e.Time)}</color>  <color={EventColor(e.Type)}>{e.Text}</color>");
        }

        timelineText.text = sb.ToString();
        pageText.text = $"{page + 1} / {PageCount}";
        previousButton.gameObject.SetActive(page > 0);
        nextButton.gameObject.SetActive(page < PageCount - 1);
    }

    static string EventColor(TrainingEventType type)
    {
        switch (type)
        {
            case TrainingEventType.ForceUsed:
            case TrainingEventType.BodyHit:
                return "#FF9A2E";
            case TrainingEventType.ForceMissed:
                return "#C9A06A";
            case TrainingEventType.HeadInjury:
            case TrainingEventType.ForceOnCompliant:
            case TrainingEventType.TechniqueError:
            case TrainingEventType.InmateAttack:
            case TrainingEventType.OfficerHit:
            case TrainingEventType.OfficerDown:
                return Red;
            case TrainingEventType.Warning:
            case TrainingEventType.CommandRefused:
                return Amber;
            case TrainingEventType.CommandObeyed:
            case TrainingEventType.Restraint:
                return Green;
            case TrainingEventType.VerbalCommand:
                return "#FFFFFF";
            default:
                return Grey;
        }
    }
}
