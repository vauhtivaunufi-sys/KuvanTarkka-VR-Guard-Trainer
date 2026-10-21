using TMPro;
using UnityEngine;

// Main menu: pick a scenario on the left, read its briefing on the right, press START
public class MainMenuController : MonoBehaviour
{
    [Tooltip("Scenarios shown in the menu, in this order")]
    [SerializeField] ScenarioCatalog catalog;

    [Tooltip("Distance from the player's start to the menu, m")]
    [SerializeField] float distance = 2f;

    [Tooltip("Height of the menu centre above the floor, m")]
    [SerializeField] float height = 1.45f;

    ScenarioDefinition[] scenarios;
    VrButton[] listButtons;
    int selected = -1;
    RectTransform panel;
    TextMeshProUGUI titleText;
    TextMeshProUGUI difficultyText;
    TextMeshProUGUI briefingText;
    TextMeshProUGUI taskText;
    TextMeshProUGUI rulesText;
    VrButton startButton;

    void Start()
    {
        Time.timeScale = 1f;
        scenarios = LoadScenarios();
        Build();
        Place();

        if (scenarios.Length > 0)
            Select(0);
    }

    ScenarioDefinition[] LoadScenarios()
    {
        if (catalog == null)
        {
            Debug.LogWarning("Scenario Catalog is empty on MainMenuController, the menu has no scenarios", this);
            return new ScenarioDefinition[0];
        }

        var list = new System.Collections.Generic.List<ScenarioDefinition>();
        foreach (ScenarioDefinition s in catalog.Scenarios)
        {
            if (s != null)
                list.Add(s);
        }
        return list.ToArray();
    }

    void Build()
    {
        panel = VrUi.CreatePanel("Main Menu", new Vector2(1500f, 860f));

        VrUi.Text(panel, "KUVANTARKKA VR", 50f, 32f, 900f, 64f, 48f, bold: true);
        VrUi.Text(panel, "Prison officer training - choose a scenario", 50f, 96f, 900f, 36f, 24f, color: VrUi.MutedColor);

        VrUi.Text(panel, "SCENARIOS", 50f, 160f, 470f, 30f, 20f, true, VrUi.MutedColor);
        listButtons = new VrButton[scenarios.Length];
        for (int i = 0; i < scenarios.Length; i++)
        {
            int index = i;
            string label = $"{scenarios[i].Title}\n<size=70%>{scenarios[i].Difficulty}</size>";
            listButtons[i] = VrUi.Button(panel, label, 50f, 196f + i * 128f, 470f, 110f, 28f, () => Select(index));
        }

        if (scenarios.Length == 0)
            VrUi.Text(panel, "No scenarios.\nSet the Scenario Catalog on MainMenuController", 50f, 196f, 470f, 200f, 24f, color: VrUi.AccentColor);

        VrUi.Button(panel, "Exit", 50f, 770f, 200f, 62f, 24f, Quit);

        // CC BY models must credit their authors where users see it
        VrUi.Text(panel, "3D models: TASER 7 & holster by Javier Clemente Garcia, Dirty Shiv by DrewA (CC BY 4.0, sketchfab.com)",
            270f, 790f, 280f, 60f, 13f, color: VrUi.MutedColor);

        VrUi.Card(panel, 560f, 160f, 890f, 670f);
        titleText = VrUi.Text(panel, "", 600f, 186f, 820f, 56f, 40f, bold: true);
        difficultyText = VrUi.Text(panel, "", 600f, 244f, 820f, 32f, 24f, bold: true);
        VrUi.Text(panel, "SITUATION", 600f, 296f, 820f, 28f, 20f, true, VrUi.MutedColor);
        briefingText = VrUi.Text(panel, "", 600f, 326f, 820f, 150f, 25f);
        VrUi.Text(panel, "YOUR TASK", 600f, 488f, 820f, 28f, 20f, true, VrUi.MutedColor);
        taskText = VrUi.Text(panel, "", 600f, 518f, 820f, 120f, 25f);
        VrUi.Text(panel, "RULES", 600f, 652f, 500f, 28f, 20f, true, VrUi.MutedColor);
        rulesText = VrUi.Text(panel, "", 600f, 682f, 500f, 130f, 19f, color: VrUi.MutedColor);
        startButton = VrUi.Button(panel, "START", 1140f, 710f, 280f, 96f, 34f, StartSelected, primary: true);
        startButton.gameObject.SetActive(scenarios.Length > 0);
    }

    // In front of where the player starts, at standing eye level
    void Place()
    {
        PlayerRig rig = PlayerRig.Instance;
        Transform origin = rig != null ? rig.transform : transform;
        Vector3 forward = origin.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;

        Vector3 eyes = origin.position + Vector3.up * height;
        VrUi.PlaceInFrontOf(panel, eyes + forward * distance, eyes);
    }

    void Select(int index)
    {
        selected = index;
        ScenarioDefinition s = scenarios[index];

        for (int i = 0; i < listButtons.Length; i++)
            listButtons[i].Selected = i == index;

        titleText.text = s.Title;
        difficultyText.text = s.Difficulty.ToString().ToUpperInvariant();
        difficultyText.color = DifficultyColor(s.Difficulty);
        briefingText.text = s.Briefing;
        taskText.text = s.Task;
        rulesText.text = s.ForceRules;
    }

    static Color DifficultyColor(ScenarioDifficulty difficulty)
    {
        switch (difficulty)
        {
            case ScenarioDifficulty.Basic: return new Color(0.4f, 0.85f, 0.5f);
            case ScenarioDifficulty.Intermediate: return VrUi.AccentColor;
            default: return new Color(1f, 0.35f, 0.3f);
        }
    }

    void StartSelected()
    {
        if (selected >= 0)
            ScenarioSession.Begin(scenarios[selected]);
    }

    static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
