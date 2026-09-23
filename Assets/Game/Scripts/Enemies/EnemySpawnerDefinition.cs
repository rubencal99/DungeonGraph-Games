using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The weighted enemy pool an EnemySpawner draws from: which enemies can
/// appear, how much more likely Common is than Rare or Strong, and how many
/// spawn at once. Tier weights live here (not on EnemyDefinition) since "how
/// common is common" is a per-encounter tuning knob, not a property of the
/// enemy itself — the same enemy can be the bread-and-butter spawn in one
/// room and a rare treat in another.
///
/// Create via Assets > Create > Game > Enemy Spawner Definition.
/// </summary>
[CreateAssetMenu(fileName = "New Enemy Spawner", menuName = "Game/Enemy Spawner Definition")]
public class EnemySpawnerDefinition : ScriptableObject
{
    [Header("Pool")]
    public List<EnemyDefinition> Enemies = new();

    [Header("Tier Weights")]
    public float CommonWeight = 10f;
    public float RareWeight = 3f;
    public float StrongWeight = 1f;

    [Header("Spawn Count")]
    public int MinCount = 3;
    public int MaxCount = 6;

    /// <summary>Picks one enemy, weighted by its rarity tier. Null if the pool is empty.</summary>
    public EnemyDefinition GetRandomEnemy()
    {
        if (Enemies == null || Enemies.Count == 0) return null;

        float totalWeight = 0f;
        foreach (EnemyDefinition enemy in Enemies) totalWeight += WeightFor(enemy.Rarity);

        if (totalWeight <= 0f) return Enemies[Random.Range(0, Enemies.Count)];

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        foreach (EnemyDefinition enemy in Enemies)
        {
            cumulative += WeightFor(enemy.Rarity);
            if (roll <= cumulative) return enemy;
        }

        return Enemies[^1]; // floating-point rounding fallback
    }

    private float WeightFor(EnemyRarity rarity) => rarity switch
    {
        EnemyRarity.Common => CommonWeight,
        EnemyRarity.Rare => RareWeight,
        EnemyRarity.Strong => StrongWeight,
        _ => 0f
    };
}
