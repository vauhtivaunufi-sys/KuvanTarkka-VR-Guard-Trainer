using System.Collections;
using UnityEngine;

// Runs the chosen scenario in the training scene: inmate profile, briefing, objective, result and pause
public class ScenarioRunner : MonoBehaviour
{
    [Tooltip("Played when Play is pressed straight in this scene, without the main menu")]
    [SerializeField] ScenarioDefinition defaultScenario;

    [Tooltip("Inmates the scenario sets up and watches")]
    [SerializeField] PrisonerStatusSystem[] inmates;

    [Tooltip("Cell doors opened at the start when the scenario has the inmate out of his cell")]
    [SerializeField] Door[] cellDoors;

    [SerializeField] ObjectiveUIController objectiveUI;

    [Tooltip("Seconds after the run ends before the report shows")]
    [SerializeField] float resultDelay = 3f;

    [Tooltip("A left menu press shorter than this pauses, a long hold stays the debug toggle, s")]
    [SerializeField] float pauseTapTime = 0.4f;

    ScenarioDefinition scenario;
    PlayerHealth officerHealth;
    RectTransform briefingPanel;
    RectTransform pausePanel;
    Coroutine resultRoutine;
    ReportPanel reportPanel;
    bool running;
    bool finished;
    float menuDownTime = float.NegativeInfinity;

    bool OfficerDown => officerHealth != null && officerHealth.IsIncapacitated;

    void Start()
    {
        if (PlayerRig.Instance != null)
            officerHealth = PlayerRig.Instance.GetComponent<PlayerHealth>();

        foreach (PrisonerStatusSystem inmate in inmates)
        {
            if (inmate != null)
                inmate.OnBodyHit += HandleBodyHit;
        }
        if (officerHealth != null)
            officerHealth.Incapacitated += HandleOfficerDown;

        scenario = ScenarioSession.Current != null ? ScenarioSession.Current : defaultScenario;
        if (scenario == null)
        {
            Debug.LogWarning("No scenario: Default Scenario is empty on ScenarioRunner, playing without one", this);
            BeginScenario();
            return;
        }

        ApplyProfile();
        ShowBriefing();
    }

    void OnDestroy()
    {
        foreach (PrisonerStatusSystem inmate in inmates)
        {
            if (inmate != null)
                inmate.OnBodyHit -= HandleBodyHit;
        }
        if (officerHealth != null)
            officerHealth.Incapacitated -= HandleOfficerDown;

        // Never leave the game frozen behind us
        if (Time.timeScale <= 0f)
            Time.timeScale = 1f;
    }

    void Update()
    {
        HandlePauseButton();

        if (!running || finished || scenario == null)
            return;

        foreach (PrisonerStatusSystem inmate in inmates)
        {
            // Facing the wall, cuffed or secured all count as under control
            if (inmate != null && (inmate.CurrentRestraint != PrisonerRestraint.Free || inmate.IsDetained))
            {
                Finish(inmate);
                break;
            }
        }
    }

    void ApplyProfile()
    {
        foreach (PrisonerStatusSystem inmate in inmates)
        {
            if (inmate == null)
                continue;

            PrisonerCompliance compliance = inmate.GetComponent<PrisonerCompliance>();
            if (compliance != null)
                compliance.SetStartingAggression(scenario.InitialAggression);

            PrisonerAttack attack = inmate.GetComponent<PrisonerAttack>();
            if (attack != null)
                attack.SetArmed(scenario.Armed);
        }
    }

    // The game waits on the briefing until the trainee presses START
    void ShowBriefing()
    {
        Time.timeScale = 0f;

        briefingPanel = VrUi.CreatePanel("Briefing", new Vector2(900f, 640f));
        VrUi.Text(briefingPanel, scenario.Title, 40f, 32f, 820f, 56f, 38f, bold: true);
        VrUi.Text(briefingPanel, "YOUR TASK", 40f, 104f, 820f, 30f, 20f, true, VrUi.MutedColor);
        VrUi.Text(briefingPanel, scenario.Task, 40f, 136f, 820f, 170f, 28f);
        VrUi.Text(briefingPanel, "RULES", 40f, 322f, 820f, 30f, 20f, true, VrUi.MutedColor);
        VrUi.Text(briefingPanel, scenario.ForceRules, 40f, 354f, 820f, 130f, 22f, color: VrUi.MutedColor);
        VrUi.Text(briefingPanel, ControlsHint(), 40f, 520f, 480f, 90f, 20f, color: VrUi.MutedColor);
        VrUi.Button(briefingPanel, "START", 560f, 510f, 300f, 92f, 34f, BeginScenario, primary: true);

        // Hidden until it has a proper place in front of the eyes
        briefingPanel.gameObject.SetActive(false);
        StartCoroutine(PlaceWhenTracked(briefingPanel));
    }

    static string ControlsHint()
    {
        return ControllerInput.Override != null
            ? "Aim with the crosshair, click to choose\nTab: pause"
            : "Point and pull the trigger to choose\nLeft menu button: pause";
    }

    // The headset reports its position a few frames after the scene loads
    static IEnumerator PlaceWhenTracked(RectTransform panel)
    {
        yield return null;
        yield return new WaitForSecondsRealtime(0.3f);
        if (panel == null)
            yield break;
        panel.gameObject.SetActive(true);
        VrUi.PlaceAhead(panel);
    }

    void BeginScenario()
    {
        if (briefingPanel != null)
            Destroy(briefingPanel.gameObject);

        Time.timeScale = 1f;
        running = true;
        TrainingLog.Begin();
        TrainingLog.Record(TrainingEventType.Info, scenario != null ? $"Scenario started: {scenario.Title}" : "Free play started");

        if (scenario == null)
            return;

        if (scenario.StartsOutOfCell)
            ReleaseInmates();
        if (objectiveUI != null)
            objectiveUI.SetObjective(scenario.Objective);
    }

    // Opens the cell doors, the inmates walk out on their own
    void ReleaseInmates()
    {
        foreach (Door door in cellDoors)
        {
            if (door != null)
                door.SetOpen(true);
        }

        // Woken directly too, a cell door link may be switched off
        foreach (PrisonerStatusSystem inmate in inmates)
        {
            PrisonerAIMovement movement = inmate != null ? inmate.GetComponent<PrisonerAIMovement>() : null;
            if (movement != null)
                movement.Activate();
        }
    }

    void Finish(PrisonerStatusSystem prisoner)
    {
        finished = true;
        string how = prisoner.IsCuffed ? "cuffed" : prisoner.IsDetained ? "secured" : "facing the wall";
        TrainingLog.Record(TrainingEventType.Restraint, $"{prisoner.DisplayName} under control ({how})");
        if (objectiveUI != null)
            objectiveUI.SetObjectiveText("Objective complete");
        EndRun(true, $"Inmate under control - {how}");
    }

    // The head is never a target: any probe or strike there ends the run, even right after a success
    void HandleBodyHit(BodyZone zone, string cause)
    {
        if (zone != BodyZone.Head || !running || reportPanel != null)
            return;

        finished = true;
        TrainingLog.Record(TrainingEventType.Info, "Scenario failed - head injury");
        if (objectiveUI != null)
            objectiveUI.SetObjectiveText("Scenario failed");
        EndRun(false, $"Failed - head injury: {cause}");
    }

    void HandleOfficerDown()
    {
        if (!running || reportPanel != null)
            return;

        finished = true;
        if (objectiveUI != null)
            objectiveUI.SetObjectiveText("Scenario failed");
        EndRun(false, "Failed - officer incapacitated");
    }

    // A later ending replaces one still waiting to show, a failure always wins
    void EndRun(bool success, string outcome)
    {
        if (resultRoutine != null)
            StopCoroutine(resultRoutine);
        float duration = TrainingLog.Elapsed;
        resultRoutine = StartCoroutine(ShowReportAfterDelay(success, outcome, duration));
    }

    IEnumerator ShowReportAfterDelay(bool success, string outcome, float duration)
    {
        yield return new WaitForSeconds(resultDelay);

        if (pausePanel != null)
            Resume();

        // The officer-down fade would hide the report
        if (officerHealth != null)
            officerHealth.GetComponent<PlayerFeedbackOverlay>().FadeTo(0f, 0.5f);

        string title = scenario != null ? scenario.Title : "Free play";
        reportPanel = new ReportPanel(new ScenarioReport(title, success, outcome, duration, TrainingLog.Events));
    }

    void HandlePauseButton()
    {
        if (ControllerInput.GetButtonDown(Hand.Left, ControllerButton.Menu))
            menuDownTime = Time.unscaledTime;

        if (!ControllerInput.GetButtonUp(Hand.Left, ControllerButton.Menu) || Time.unscaledTime - menuDownTime > pauseTapTime)
            return;

        if (pausePanel != null)
            Resume();
        else if (running && reportPanel == null && !OfficerDown)
            Pause();
    }

    void Pause()
    {
        Time.timeScale = 0f;
        pausePanel = VrUi.CreatePanel("Pause", new Vector2(600f, 520f));
        VrUi.Text(pausePanel, "PAUSED", 40f, 30f, 520f, 60f, 40f, true, align: TMPro.TextAlignmentOptions.Top);
        VrUi.Button(pausePanel, "Resume", 40f, 120f, 520f, 100f, 32f, Resume, primary: true);
        VrUi.Button(pausePanel, "Restart scenario", 40f, 240f, 520f, 100f, 32f, ScenarioSession.Restart);
        VrUi.Button(pausePanel, "Main menu", 40f, 360f, 520f, 100f, 32f, ScenarioSession.BackToMenu);
        VrUi.PlaceAhead(pausePanel);
    }

    void Resume()
    {
        if (pausePanel != null)
            Destroy(pausePanel.gameObject);
        pausePanel = null;
        Time.timeScale = 1f;
    }
}
