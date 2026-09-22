/// <summary>
/// Implemented by anything that can take damage from a projectile or other
/// combat source. Keeping this generic lets the same hit-detection code (see
/// Projectile) work against players, enemies, or destructible scenery alike.
/// </summary>
public interface IDamageable
{
    void TakeDamage(DamageInfo damageInfo);
}
