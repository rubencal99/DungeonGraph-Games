using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Makes a weapon on the floor shared loot: one pickup everybody sees, and
/// first come, first served.
///
/// Pressing E only asks the host for it. The host checks it is still there,
/// removes it for everyone, then hands the weapon to whoever asked. Two players
/// pressing E on the same frame can't both get it, because only the host ever
/// decides.
///
/// Setup: add to the weapon pickup prefab, next to WeaponPickup and the
/// NetworkObject.
/// </summary>
[RequireComponent(typeof(WeaponPickup))]
public class WeaponPickupNetwork : NetworkBehaviour
{
    private readonly NetworkVariable<int> m_weaponId = new(GameCatalog.NoId);

    private WeaponPickup m_pickup;

    private void Awake()
    {
        m_pickup = GetComponent<WeaponPickup>();
    }

    /// <summary>Host only, before Spawn: which weapon this is, so it arrives with the pickup.</summary>
    public void SetWeaponId(int weaponId) => m_weaponId.Value = weaponId;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // A pickup placed by hand in a scene carries its weapon in the Inspector instead.
            if (m_weaponId.Value == GameCatalog.NoId) m_weaponId.Value = GameCatalog.Instance.GetWeaponId(m_pickup.Definition);
            return;
        }

        m_pickup.SetWeapon(GameCatalog.Instance.GetWeapon(m_weaponId.Value));
    }

    public void RequestTake() => TakeRpc();

    [Rpc(SendTo.Server)]
    private void TakeRpc(RpcParams rpcParams = default)
    {
        if (!IsSpawned) return;

        PlayerNetwork taker = PlayerNetwork.ForClient(rpcParams.Receive.SenderClientId);
        if (taker == null) return;

        int weaponId = m_weaponId.Value;
        NetworkObject.Despawn();
        taker.GiveWeaponRpc(weaponId);
    }
}
