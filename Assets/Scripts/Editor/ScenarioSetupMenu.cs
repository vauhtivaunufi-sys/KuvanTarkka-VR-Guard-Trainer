using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

// Tools > KuvanTarkka > Set Up Scenarios: scenario assets, the MainMenu scene and the build order in one click
// Safe to run again: existing assets and an existing MainMenu scene are kept
static class ScenarioSetupMenu
{
    const string ScenarioFolder = "Assets/Resources/Scenarios";
    const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
    const string TrainingScenePath = "Assets/Scenes/MainTraining.unity";
    const string RoomMaterialPath = "Assets/Materials/MenuRoom.mat";

    // XR Origin (XR Rig) from the XRI Starter Assets, the same rig MainTraining uses
    const string RigPrefabGuid = "f6336ac4ac8b4d34bc5072418cdc62a0";

    struct Spec
    {
        public string File;
        public int Order;
        public string Title;
        public ScenarioDifficulty Difficulty;
        public string Briefing;
        public string Task;
        public string Objective;
        public float Aggression;
        public bool OutOfCell;
        public bool Armed;
    }

    static readonly Spec[] Specs =
    {
        new Spec
        {
            File = "01_RefusalToReturn",
            Order = 1,
            Title = "Refusal to return to cell",
            Difficulty = ScenarioDifficulty.Basic,
            Briefing = "Evening lock-up on a closed ward. Everyone has been told to return to their cells. One inmate is still out in the corridor and refuses to go back in. He is upset about a cancelled visit and is arguing loudly, but he has not attacked anyone and is not armed.",
            Task = "Keep a safe distance and talk to him. Calm him down, then have him face the wall. This can be solved with words - using force here is very likely a mistake.",
            Objective = "Get the inmate to comply without force",
            Aggression = 0.55f,
            OutOfCell = true,
            Armed = false
        },
        new Spec
        {
            File = "02_AggressiveInmate",
            Order = 2,
            Title = "Aggressive inmate",
            Difficulty = ScenarioDifficulty.Intermediate,
            Briefing = "An inmate has been kicking his cell door and shouting threats at staff for ten minutes. You are sent to deal with him and have to open his cell. Expect him to come straight at you.",
            Task = "Give clear commands and a warning before using force. If he attacks, respond in proportion, then bring him under control: have him face the wall.",
            Objective = "Bring the aggressive inmate under control",
            Aggression = 0.8f,
            OutOfCell = false,
            Armed = false
        },
        new Spec
        {
            File = "03_ArmedInmate",
            Order = 3,
            Title = "Armed inmate",
            Difficulty = ScenarioDifficulty.Advanced,
            Briefing = "An inmate is out in the corridor holding an improvised knife. He is agitated and will not drop it. A knife can kill in seconds, and his reach is longer than it looks.",
            Task = "Keep your distance and warn him clearly. If he comes at you, stop him from a distance with the taser, then have him face the wall. Do not let him close the gap.",
            Objective = "Stop the armed inmate safely",
            Aggression = 0.65f,
            OutOfCell = true,
            Armed = true
        }
    };

    [MenuItem("Tools/KuvanTarkka/Set Up Scenarios", priority = 20)]
    static void SetUp()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Set Up Scenarios: stop Play mode first");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        int created = CreateScenarioAssets();
        bool sceneCreated = CreateMenuScene();
        UpdateBuildSettings();

        string sceneState = sceneCreated ? "created" : File.Exists(MenuScenePath) ? "already there, kept" : "NOT created, see Console";
        EditorUtility.DisplayDialog("Set Up Scenarios",
            $"Scenario assets created: {created} (existing ones kept)\nMainMenu scene: {sceneState}\nBuild order: MainMenu, MainTraining\n\nOpen Assets/Scenes/MainMenu and press Play.",
            "OK");
    }

    static int CreateScenarioAssets()
    {
        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "Scenarios");

        int created = 0;
        foreach (Spec spec in Specs)
        {
            string path = $"{ScenarioFolder}/{spec.File}.asset";
            if (AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(path) != null)
                continue;

            var asset = ScriptableObject.CreateInstance<ScenarioDefinition>();
            // Fields are private, so they are filled the way the Inspector does it
            var so = new SerializedObject(asset);
            so.FindProperty("order").intValue = spec.Order;
            so.FindProperty("title").stringValue = spec.Title;
            so.FindProperty("difficulty").enumValueIndex = (int)spec.Difficulty;
            so.FindProperty("briefing").stringValue = spec.Briefing;
            so.FindProperty("task").stringValue = spec.Task;
            so.FindProperty("objective").stringValue = spec.Objective;
            so.FindProperty("initialAggression").floatValue = spec.Aggression;
            so.FindProperty("startsOutOfCell").boolValue = spec.OutOfCell;
            so.FindProperty("armed").boolValue = spec.Armed;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(asset, path);
            created++;
        }

        AssetDatabase.SaveAssets();
        return created;
    }

    static bool CreateMenuScene()
    {
        if (File.Exists(MenuScenePath))
            return false;

        var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(RigPrefabGuid));
        if (rigPrefab == null)
        {
            Debug.LogError("Set Up Scenarios: XR Origin (XR Rig) prefab not found - import the XRI Starter Assets sample");
            return false;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // The rig brings its own camera
        GameObject defaultCamera = GameObject.FindWithTag("MainCamera");
        if (defaultCamera != null)
            Object.DestroyImmediate(defaultCamera);

        // Plain room so the player isn't floating in a void, art replaces it later
        Material room = RoomMaterial();
        CreateBlock("Floor", new Vector3(0f, -0.05f, 0f), new Vector3(10f, 0.1f, 10f), room);
        CreateBlock("Wall North", new Vector3(0f, 1.5f, 5f), new Vector3(10f, 3f, 0.2f), room);
        CreateBlock("Wall South", new Vector3(0f, 1.5f, -5f), new Vector3(10f, 3f, 0.2f), room);
        CreateBlock("Wall East", new Vector3(5f, 1.5f, 0f), new Vector3(0.2f, 3f, 10f), room);
        CreateBlock("Wall West", new Vector3(-5f, 1.5f, 0f), new Vector3(0.2f, 3f, 10f), room);

        var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab, scene);
        rig.transform.SetPositionAndRotation(new Vector3(0f, 0f, -1.5f), Quaternion.identity);
        var playerRig = rig.AddComponent<PlayerRig>();
        var rigSettings = new SerializedObject(playerRig);
        rigSettings.FindProperty("officerGear").boolValue = false;
        rigSettings.ApplyModifiedPropertiesWithoutUndo();

        new GameObject("Main Menu").AddComponent<MainMenuController>();

        // Same UI input setup as MainTraining, XRI expects an event system in the scene
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        EditorSceneManager.SaveScene(scene, MenuScenePath);
        return true;
    }

    static void UpdateBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>();
        // The menu has to be first, it is the scene the build starts with
        if (File.Exists(MenuScenePath))
            scenes.Add(new EditorBuildSettingsScene(MenuScenePath, true));
        scenes.Add(new EditorBuildSettingsScene(TrainingScenePath, true));

        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            if (s.path != MenuScenePath && s.path != TrainingScenePath)
                scenes.Add(s);
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static void CreateBlock(string name, Vector3 position, Vector3 size, Material material)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.position = position;
        block.transform.localScale = size;
        block.GetComponent<Renderer>().sharedMaterial = material;
        block.isStatic = true;
    }

    static Material RoomMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(RoomMaterialPath);
        if (existing != null)
            return existing;

        EnsureFolder("Assets", "Materials");
        var material = new Material(Shader.Find("Standard")) { color = new Color(0.32f, 0.34f, 0.37f) };
        AssetDatabase.CreateAsset(material, RoomMaterialPath);
        return material;
    }

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
            AssetDatabase.CreateFolder(parent, name);
    }
}
