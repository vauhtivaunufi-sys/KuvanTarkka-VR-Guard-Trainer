using System.Collections.Generic;
using UnityEngine;

// The scenarios the main menu offers, in menu order
[CreateAssetMenu(menuName = "KuvanTarkka/Scenario Catalog", fileName = "ScenarioCatalog")]
public class ScenarioCatalog : ScriptableObject
{
    [SerializeField] ScenarioDefinition[] scenarios;

    public IReadOnlyList<ScenarioDefinition> Scenarios => scenarios;
}
