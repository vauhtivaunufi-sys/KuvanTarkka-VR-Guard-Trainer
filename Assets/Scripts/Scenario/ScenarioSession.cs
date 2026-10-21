using UnityEngine;
using UnityEngine.SceneManagement;

// The scenario being played, carried from the menu into the training scene
public static class ScenarioSession
{
    public const string MenuScene = "MainMenu";

    public static ScenarioDefinition Current { get; private set; }

    // Statics survive between Play sessions when domain reload is off
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Current = null;
    }

    public static void Begin(ScenarioDefinition scenario)
    {
        Current = scenario;
        Load(scenario.SceneName);
    }

    public static void Restart()
    {
        Load(SceneManager.GetActiveScene().name);
    }

    public static void BackToMenu()
    {
        Current = null;
        Load(MenuScene);
    }

    static void Load(string scene)
    {
        // A pause must not carry over into the next scene
        Time.timeScale = 1f;

        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError($"Scene '{scene}' is not in Build Settings, add it in File > Build Profiles");
            return;
        }
        SceneManager.LoadScene(scene);
    }
}
