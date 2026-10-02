using Unity.Netcode;
using UnityEngine;

/// <summary>
/// The one place a weapon enters the world. Trades out of a full inventory go
/// through it, and so does every chest drop and enemy drop.
///
/// It exists as a single seam rather than as ceremony: because every caller asks
/// for "this weapon, at this point" and never touches a prefab, swapping the
/// Instantiate below for a pool is a change to this file alone. Pickups can wait
/// for that — drops happen at human speed, not per shot like projectiles.
///
/// In a session only the host creates pickups, so everyone sees the same one.
/// A player trading a weapon away calls Spawn on their own machine, and it
/// quietly forwards the request to the host.
///
/// Setup:
///   1. Create an empty GameObject named "WeaponPickupSpawner" in the gameplay
///      scene, with a NetworkObject and this component.
///   2. Assign the shared weapon pickup prefab.
/// </summary>
public class WeaponPickupSpawner : NetworkBehaviour
{
    public static WeaponPickupSpawner Instance { get; private set; }

    [Tooltip("The single shared pickup prefab. Every weapon in the game uses it.")]
    [SerializeField] private WeaponPickup m_pickupPrefab;

    private void Awake()
    {
        Instance = this;
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    /// <summary>
    /// Puts a weapon on the floor and slides it out along a direction. Callable
    /// from any machine; non-hosts forward the request to the host.
    /// </summary>
    public void Spawn(WeaponDefinition weapon, Vector2 position, Vector2 direction)
    {
        if (m_pickupPrefab == null || weapon == null)
        {
            Debug.LogError($"{nameof(WeaponPickupSpawner)} on '{name}' is missing its pickup prefab, " +
                           "or was asked to spawn nothing. Nothing was dropped.", this);
            return;
        }

        if (IsSpawned && !IsServer)
        {
            SpawnRpc(GameCatalog.Instance.GetWeaponId(weapon), position, direction);
            return;
        }

        WeaponPickup pickup = Instantiate(m_pickupPrefab, position, Quaternion.identity);

        // Set before the first frame it is visible, so it never flashes as an
        // empty pickup.
        pickup.SetWeapon(weapon);

        if (IsSpawned && pickup.TryGetComponent(out WeaponPickupNetwork network))
        {
            network.SetWeaponId(GameCatalog.Instance.GetWeaponId(weapon));
            network.NetworkObject.Spawn(destroyWithScene: true);
        }

        pickup.Launch(direction);
    }

    [Rpc(SendTo.Server)]
    private void SpawnRpc(int weaponId, Vector2 position, Vector2 direction)
    {
        Spawn(GameCatalog.Instance.GetWeapon(weaponId), position, direction);
    }
}
