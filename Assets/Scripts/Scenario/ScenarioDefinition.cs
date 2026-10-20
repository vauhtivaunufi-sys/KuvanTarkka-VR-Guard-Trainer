using UnityEngine;

public enum ScenarioDifficulty
{
    Basic,
    Intermediate,
    Advanced
}

// One training scenario: what the trainee is told and how the inmate behaves
// Assets live in Resources/Scenarios, the menu lists them automatically
[CreateAssetMenu(menuName = "KuvanTarkka/Scenario", fileName = "Scenario")]
public class ScenarioDefinition : ScriptableObject
{
    [Tooltip("Position in the menu, lower first")]
    [SerializeField] int order;

    [SerializeField] string title = "New scenario";

    [SerializeField] ScenarioDifficulty difficulty;

    [Tooltip("What is happening, read in the menu before starting")]
    [TextArea(3, 8)]
    [SerializeField] string briefing;

    [Tooltip("What the trainee has to do")]
    [TextArea(2, 6)]
    [SerializeField] string task;

    [Tooltip("Short reminder of the use-of-force rules")]
    [TextArea(2, 6)]
    [SerializeField] string forceRules =
        "Words first. Use force only against active resistance and no more than needed. Warn before force when you can. Every use of force is recorded.";

    [Tooltip("One line on the objective board during the scenario")]
    [SerializeField] string objective = "Get the inmate under control";

    [Header("Inmate")]
    [Tooltip("Starting aggression, 0 calm - 1 violent")]
    [Range(0f, 1f)]
    [SerializeField] float initialAggression = 0.55f;

    [Tooltip("His cell door is open at the start, so he is already out in the corridor")]
    [SerializeField] bool startsOutOfCell;

    [Tooltip("Carries an improvised knife")]
    [SerializeField] bool armed;

    [Tooltip("Scene the scenario plays in, must be in Build Settings")]
    [SerializeField] string sceneName = "MainTraining";

    public int Order => order;
    public string Title => title;
    public ScenarioDifficulty Difficulty => difficulty;
    public string Briefing => briefing;
    public string Task => task;
    public string ForceRules => forceRules;
    public string Objective => objective;
    public float InitialAggression => initialAggression;
    public bool StartsOutOfCell => startsOutOfCell;
    public bool Armed => armed;
    public string SceneName => sceneName;
}
