using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Anything that can stop a blow before it reaches the officer (today: the shield)
public interface IMeleeBlocker
{
    // True if a blow travelling from attackOrigin toward target is stopped by this blocker
    bool TryBlock(Vector3 attackOrigin, Vector3 target);
}

// Officer's health: a blow lands here, blockers like the shield get the first chance, at zero the scenario fails
[RequireComponent(typeof(PlayerRig))]
[RequireComponent(typeof(PlayerFeedbackOverlay))]
public class PlayerHealth : MonoBehaviour
{
    [SerializeField] float maxHealth = 100f;

    [Tooltip("Seconds from being incapacitated until the scene restarts.")]
    [SerializeField] float restartDelay = 5f;

    static readonly List<IMeleeBlocker> blockers = new();

    public static void RegisterBlocker(IMeleeBlocker blocker)
    {
        if (!blockers.Contains(blocker))
            blockers.Add(blocker);
    }

    public static void UnregisterBlocker(IMeleeBlocker blocker) => blockers.Remove(blocker);

    public float Health { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsIncapacitated { get; private set; }

    // (damage, attacker name) - raised for every blow that got through
    public event Action<float, string> Damaged;
    public event Action Blocked;
    public event Action Incapacitated;

    PlayerRig rig;
    PlayerFeedbackOverlay overlay;

    void Awake()
    {
        rig = GetComponent<PlayerRig>();
        overlay = GetComponent<PlayerFeedbackOverlay>();
        Health = maxHealth;
    }

    // Resolves a blow at the head or chest when it lands, a shield in between stops it
    public void ReceiveMeleeAttack(Vector3 attackOrigin, string attacker, float damage)
    {
        if (IsIncapacitated)
            return;

        Vector3 target = UnityEngine.Random.value < 0.6f ? rig.HeadPosition : rig.ChestPosition;

        for (int i = 0; i < blockers.Count; i++)
        {
            if (blockers[i].TryBlock(attackOrigin, target))
            {
                TrainingDebug.Log($"Shield blocked a blow from {attacker}", this);
                Blocked?.Invoke();
                return;
            }
        }

        Health = Mathf.Max(0f, Health - damage);
        TrainingDebug.Log($"Officer hit by {attacker}: -{damage:0}, health {Health:0}/{maxHealth:0}", this);

        Haptics.HitTaken();
        overlay.FlashDamage(damage / maxHealth);
        Damaged?.Invoke(damage, attacker);

        if (Health <= 0f)
            BecomeIncapacitated(attacker);
    }

    void BecomeIncapacitated(string attacker)
    {
        IsIncapacitated = true;
        Incapacitated?.Invoke();

        overlay.FadeTo(0.85f, 1.5f);
        overlay.ShowSubtitle($"You were incapacitated by {attacker}.\nScenario failed - restarting...", restartDelay);
        StartCoroutine(RestartAfterDelay());
    }

    IEnumerator RestartAfterDelay()
    {
        yield return new WaitForSeconds(restartDelay - 1f);
        overlay.FadeTo(1f, 0.8f);
        yield return new WaitForSeconds(1f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
