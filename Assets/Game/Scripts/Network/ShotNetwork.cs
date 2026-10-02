using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Tells every other machine that this character fired, so they can spawn the
/// same bullets locally. Bullets themselves are never network objects: a
/// bullet-hell room would need hundreds of them. Instead one small message per
/// trigger pull carries where, which way, and a random seed, and each machine
/// rolls the exact same spread from that seed (see Weapon.SpawnShot).
///
/// Weapon finds this on its owner at Initialize and calls Send after firing.
///
/// Setup: add to the root of the Player prefab and of every ranged enemy
/// prefab, next to the NetworkObject.
/// </summary>
public class ShotNetwork : NetworkBehaviour
{
    public void Send(WeaponDefinition weapon, Vector2 origin, float aimAngle, float bloom, uint seed)
    {
        if (!IsSpawned) return;

        int weaponId = GameCatalog.Instance.GetWeaponId(weapon);
        if (weaponId == GameCatalog.NoId) return;

        ShotRpc(weaponId, origin, aimAngle, bloom, seed);
    }

    [Rpc(SendTo.NotMe)]
    private void ShotRpc(int weaponId, Vector2 origin, float aimAngle, float bloom, uint seed)
    {
        Weapon.SpawnShot(GameCatalog.Instance.GetWeapon(weaponId), origin, aimAngle, bloom, seed, gameObject);
    }
}
