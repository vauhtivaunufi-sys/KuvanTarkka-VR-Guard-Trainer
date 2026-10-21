using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

// Pauses walking while something else needs the left stick (the command menu)
public static class LocomotionLock
{
    static int locks;
    static readonly List<Behaviour> disabledByUs = new();

    public static bool IsLocked => locks > 0;

    // Statics survive between Play sessions when domain reload is off
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        locks = 0;
        disabledByUs.Clear();
    }

    // Counted, so two features can hold the lock without unlocking each other
    public static void Acquire()
    {
        locks++;
        if (locks == 1)
            DisableMoveProviders();
    }

    public static void Release()
    {
        if (locks == 0)
            return;
        locks--;
        if (locks == 0)
            RestoreMoveProviders();
    }

    // Remembers what it switched off, so Release never turns on something that was off on purpose
    static void DisableMoveProviders()
    {
        foreach (ContinuousMoveProvider provider in Object.FindObjectsByType<ContinuousMoveProvider>(FindObjectsSortMode.None))
        {
            if (!provider.enabled)
                continue;
            provider.enabled = false;
            disabledByUs.Add(provider);
        }
    }

    static void RestoreMoveProviders()
    {
        foreach (Behaviour b in disabledByUs)
        {
            if (b != null)
                b.enabled = true;
        }
        disabledByUs.Clear();
    }
}
