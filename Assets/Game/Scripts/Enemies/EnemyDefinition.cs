using UnityEngine;

public enum EnemyRarity
{
    Common,
    Rare,
    Strong
}

/// <summary>
/// Everything tunable about one enemy type: which prefab it is, how rare, which
/// brain it starts in, and its stats. A tougher variant of an existing enemy is
/// a new asset, not new code.
///
/// Ranges are read by the AI's conditions (Target In Range), speed by
/// EnemyMotor, health by Enemy, and the attack numbers by AttackTargetAction
/// and MeleeAttack.
///
/// Create via Assets > Create > Game > Enemy Definition.
/// </summary>
[CreateAssetMenu(fileName = "New Enemy", menuName = "Game/Enemy Definition")]
public class EnemyDefinition : ScriptableObject
{
    [Header("Identity")]
    public GameObject Prefab;
    public EnemyRarity Rarity = EnemyRarity.Common;

    [Header("AI")]
    [Tooltip("The state the brain starts in on every spawn — usually Idle.")]
    public AIState StartState;

    [Header("Health")]
    public float MaxHealth = 30f;

    [Header("Movement")]
    public float MoveSpeed = 3f;
    [Tooltip("Units/second² toward the desired speed, both speeding up and slowing down.")]
    public float Acceleration = 20f;

    [Header("Ranges")]
    [Tooltip("How close the player must be before this enemy notices them.")]
    public float DetectionRange = 6f;
    [Tooltip("How close the player must be before this enemy attacks.")]
    public float AttackRange = 1.2f;
    [Tooltip("How far the player must get before this enemy gives up. Keep it above Detection " +
             "Range, or an enemy at the edge flickers between noticing and forgetting.")]
    public float LoseTargetRange = 9f;

    [Header("Attack")]
    [Tooltip("Damage per melee hit. Ranged enemies use their weapon's damage instead.")]
    public float AttackDamage = 10f;
    [Tooltip("Seconds between attacks. For a ranged enemy, 0 lets the weapon's own fire rate " +
             "decide; higher values make it fire in bursts.")]
    public float AttackCooldown = 1f;

    [Header("Loot")]
    [Tooltip("Rolled on death. Leave empty for no drops.")]
    public LootTableDefinition DeathLoot;
    [Range(0f, 1f)] public float DropChance = 0.1f;
}
