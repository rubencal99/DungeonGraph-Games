using UnityEngine;

/// <summary>
/// The one place a weapon enters the world. Trades out of a full inventory go
/// through it, and so does every chest drop.
///
/// It exists as a single seam rather than as ceremony: because every caller asks
/// for "this weapon, at this point" and never touches a prefab, swapping the
/// Instantiate below for a pool is a change to this file alone. Pickups can wait
/// for that — drops happen at human speed, not per shot like projectiles.
///
/// Setup:
///   1. Create an empty GameObject named "WeaponPickupSpawner" in the scene.
///   2. Assign the shared weapon pickup prefab.
/// </summary>
public class WeaponPickupSpawner : MonoBehaviour
{
    public static WeaponPickupSpawner Instance { get; private set; }

    [Tooltip("The single shared pickup prefab. Every weapon in the game uses it.")]
    [SerializeField] private WeaponPickup m_pickupPrefab;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Puts a weapon on the floor and slides it out along a direction.
    /// </summary>
    /// <returns>The spawned pickup, or null when the spawn was rejected.</returns>
    public WeaponPickup Spawn(WeaponDefinition weapon, Vector2 position, Vector2 direction)
    {
        if (m_pickupPrefab == null || weapon == null)
        {
            Debug.LogError($"{nameof(WeaponPickupSpawner)} on '{name}' is missing its pickup prefab, " +
                           "or was asked to spawn nothing. Nothing was dropped.", this);
            return null;
        }

        WeaponPickup pickup = Instantiate(m_pickupPrefab, position, Quaternion.identity);

        // Set before the first frame it is visible, so it never flashes as an
        // empty pickup.
        pickup.SetWeapon(weapon);
        pickup.Launch(direction);
        return pickup;
    }
}
