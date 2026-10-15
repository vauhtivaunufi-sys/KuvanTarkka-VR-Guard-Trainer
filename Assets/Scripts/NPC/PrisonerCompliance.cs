using UnityEngine;

// Prisoner's attitude: one hidden number 0..1 that decides how he behaves
[RequireComponent(typeof(PrisonerStatusSystem))]
public class PrisonerCompliance : MonoBehaviour
{
    [Tooltip("Starting aggression, 0 calm - 1 violent")]
    [Range(0f, 1f)]
    [SerializeField] float initialAggression = 0.55f;

    [Tooltip("At or above this he chases and attacks on sight")]
    [Range(0f, 1f)]
    [SerializeField] float hostileThreshold = 0.7f;

    [Tooltip("Below this he is calm and cooperative")]
    [Range(0f, 1f)]
    [SerializeField] float calmThreshold = 0.35f;

    [Tooltip("Officer closer than this crowds him, m")]
    [SerializeField] float personalSpace = 1.8f;

    [Tooltip("Aggression gained per second while crowded")]
    [SerializeField] float crowdingAggressionPerSecond = 0.06f;

    [Tooltip("Seconds of crowding before an agitated prisoner lashes out")]
    [SerializeField] float crowdingBeforeLashOut = 1.5f;

    PrisonerStatusSystem status;
    PrisonerAIMovement movement;
    PrisonerPerception perception;
    float crowdedTime;

    public float Aggression { get; private set; }

    public bool IsScared => status.CurrentEmotional == PrisonerEmotional.Scared;
    public bool IsHostile => !IsScared && Aggression >= hostileThreshold;
    public bool IsAgitated => !IsScared && Aggression >= calmThreshold && Aggression < hostileThreshold;
    public bool IsCalm => !IsHostile && !IsAgitated;

    // Would he throw a punch right now if the officer were in reach
    public bool WantsToAttack => IsHostile || (IsAgitated && crowdedTime >= crowdingBeforeLashOut);

    public static PrisonerCompliance GetOrAdd(GameObject prisoner)
    {
        PrisonerCompliance c = prisoner.GetComponent<PrisonerCompliance>();
        return c != null ? c : prisoner.AddComponent<PrisonerCompliance>();
    }

    void Awake()
    {
        status = GetComponent<PrisonerStatusSystem>();
        movement = GetComponent<PrisonerAIMovement>();
        perception = PrisonerPerception.GetOrAdd(gameObject);
        Aggression = initialAggression;
    }

    void Update()
    {
        // Still in his cell: nothing affects his mood yet
        if (movement != null && !movement.IsActive)
            return;

        UpdateCrowding();
    }

    void UpdateCrowding()
    {
        Debug.Log($"{name} dist {perception.DistanceToPlayer:0.00} cond {status.CurrentConditional}");

        bool crowded = perception.DistanceToPlayer < personalSpace &&
                       status.CurrentConditional == PrisonerConditional.Idle;
        if (crowded)
        {
            crowdedTime += Time.deltaTime;
            ChangeAggression(crowdingAggressionPerSecond * Time.deltaTime);
        }
        else
        {
            crowdedTime = Mathf.Max(0f, crowdedTime - Time.deltaTime * 2f);
        }
    }

    public void ChangeAggression(float delta)
    {
        Aggression = Mathf.Clamp01(Aggression + delta);
    }
}