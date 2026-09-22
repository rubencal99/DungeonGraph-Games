using UnityEngine;

/// <summary>
/// Everything an IDamageable needs to know about a single hit: how much
/// damage, who/what dealt it, which weapon fired it, and where it landed.
/// </summary>
public readonly struct DamageInfo
{
    public readonly float Amount;
    public readonly GameObject Source;
    public readonly WeaponDefinition Weapon;
    public readonly Vector2 HitPoint;

    public DamageInfo(float amount, GameObject source, WeaponDefinition weapon, Vector2 hitPoint)
    {
        Amount = amount;
        Source = source;
        Weapon = weapon;
        HitPoint = hitPoint;
    }
}
