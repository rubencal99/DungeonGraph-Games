using UnityEngine;

public enum EnemyRarity
{
    Common,
    Rare,
    Strong
}

/// <summary>
/// Identifies one spawnable enemy type and its rarity tier. Stats (health,
/// damage, speed, etc.) belong here too once a real Enemy behavior exists —
/// left out for now since nothing would read them yet.
///
/// Create via Assets > Create > Game > Enemy Definition.
/// </summary>
[CreateAssetMenu(fileName = "New Enemy", menuName = "Game/Enemy Definition")]
public class EnemyDefinition : ScriptableObject
{
    public GameObject Prefab;
    public EnemyRarity Rarity = EnemyRarity.Common;
}
