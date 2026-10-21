#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

// EDITOR ONLY dev tool: picks headset or keyboard and mouse on Play and adds DesktopPlayer, the game itself is VR only
public static class DesktopMode
{
    public enum Setting
    {
        Auto,
        Desktop,
        Headset
    }

    // Shared with the editor menu (Editor/KeyboardMouseModeMenu.cs)
    public const string EditorPrefsKey = "KuvanTarkka.PlayMode";

    public static bool IsActive { get; private set; }

    public static Setting CurrentSetting => (Setting)UnityEditor.EditorPrefs.GetInt(EditorPrefsKey, (int)Setting.Auto);

    // Statics survive between Play sessions when domain reload is turned off - start clean every time
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        IsActive = false;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    // XR is started before the first scene loads, so by now we can tell whether a headset is really there
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        switch (CurrentSetting)
        {
            case Setting.Desktop:
                IsActive = true;
                break;
            case Setting.Headset:
                IsActive = false;
                break;
            default:
                IsActive = !HeadsetRunning();
                break;
        }

        if (!IsActive)
            return;

        Debug.Log("[Desktop Testing] No headset - keyboard & mouse test mode (editor only). Press F1 in the Game view for the controls.");
        SceneManager.sceneLoaded += HandleSceneLoaded;
        AttachToRig();
        ReportBrokenAffordances();
    }

    // Names the object whose missing XRI affordance theme throws every frame, the exception itself doesn't
    static void ReportBrokenAffordances()
    {
        foreach (MonoBehaviour mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb == null || !IsAffordanceReceiver(mb.GetType()))
                continue;

            object theme = null;
            try
            {
                theme = mb.GetType().GetProperty("affordanceTheme", BindingFlags.Public | BindingFlags.Instance)?.GetValue(mb);
            }
            catch (Exception)
            {
                // A broken datum can throw instead of returning null - same problem
            }

            if (theme == null)
                Debug.LogWarning($"[Desktop Testing] XRI affordance with NO theme (spams NullReferenceException on hover): " +
                                 $"{PathOf(mb.transform)} - {mb.GetType().Name}", mb);
        }
    }

    static bool IsAffordanceReceiver(Type type)
    {
        for (Type t = type; t != null; t = t.BaseType)
        {
            if (t.Name.StartsWith("BaseAsyncAffordanceStateReceiver"))
                return true;
        }
        return false;
    }

    static string PathOf(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent)
            path = p.name + "/" + path;
        return path;
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AttachToRig();
    }

    // An XR display that is running means frames really go to a headset
    static bool HeadsetRunning()
    {
        var displays = new List<XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(displays);
        foreach (XRDisplaySubsystem display in displays)
        {
            if (display.running)
                return true;
        }
        return false;
    }

    static void AttachToRig()
    {
        XROrigin origin = UnityEngine.Object.FindFirstObjectByType<XROrigin>();
        if (origin == null)
        {
            Debug.LogWarning("[Desktop Testing] No XR Origin in this scene - nothing to control.");
            return;
        }

        if (origin.GetComponent<DesktopPlayer>() == null)
            origin.gameObject.AddComponent<DesktopPlayer>();
    }
}
#endif
