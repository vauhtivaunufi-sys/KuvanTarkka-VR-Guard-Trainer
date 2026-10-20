using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// The scenario being played, carried from the menu into the training scene
public static class ScenarioSession
{
    public const string MenuScene = "MainMenu";
    const string ResourcesFolder = "Scenarios";

    public static ScenarioDefinition Current { get; private set; }

    // Statics survive between Play sessions when domain reload is off
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Current = null;
    }

    // Every scenario asset in Resources/Scenarios, in menu order
    public static ScenarioDefinition[] LoadAll()
    {
        ScenarioDefinition[] all = Resources.LoadAll<ScenarioDefinition>(ResourcesFolder);
        Array.Sort(all, (a, b) => a.Order.CompareTo(b.Order));
        return all;
    }

    // Pressing Play straight in the training scene: the first scenario stands in for the menu choice
    public static ScenarioDefinition CurrentOrDefault()
    {
        if (Current == null)
        {
            ScenarioDefinition[] all = LoadAll();
            if (all.Length > 0)
            {
                Current = all[0];
                Debug.Log($"No scenario chosen in the menu, playing '{Current.Title}'");
            }
        }
        return Current;
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
            Debug.LogError($"Scene '{scene}' is not in Build Settings - run Tools > KuvanTarkka > Set Up Scenarios");
            return;
        }
        SceneManager.LoadScene(scene);
    }
}
