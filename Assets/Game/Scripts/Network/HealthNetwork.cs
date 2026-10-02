using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Decides which machine a hit counts on, so every hit counts exactly once.
///
/// Every machine runs its own copy of every bullet and swing, so the same hit
/// is "seen" up to four times. The rule for which copy counts:
///   * Something a player did (their bullet) counts on that player's machine.
///     You never miss a shot that hit on your screen.
///   * Anything else (an enemy's bullet or swing) counts on the victim's
///     machine. You never get hit by a bullet you dodged on your screen.
/// If the deciding machine isn't the one that owns the target's health, it
/// sends the hit there. Players own their own health; the host owns enemies'.
///
/// Health asks this before applying any hit.
///
/// Setup: add to the root of the Player prefab and of every enemy prefab, next
/// to Health and the NetworkObject.
/// </summary>
[RequireComponent(typeof(Health))]
public class HealthNetwork : NetworkBehaviour
{
    private Health m_health;

    private void Awake()
    {
        m_health = GetComponent<Health>();
    }

    /// <summary>
    /// True when this machine should apply the hit to its own Health. When this
    /// machine decides the hit but the health lives elsewhere, the hit is sent
    /// there and this returns false.
    /// </summary>
    public bool ShouldApplyLocally(DamageInfo damageInfo)
    {
        if (!IsSpawned) return true;

        NetworkObject source = damageInfo.Source != null ? damageInfo.Source.GetComponentInParent<NetworkObject>() : null;
        bool sourceIsPlayer = source != null && source.TryGetComponent(out PlayerNetwork _);
        bool decidesHere = sourceIsPlayer ? source.IsOwner : IsOwner;

        if (!decidesHere) return false;
        if (IsOwner) return true;

        NetworkObjectReference sourceReference = source != null ? new NetworkObjectReference(source) : default;
        ApplyDamageRpc(damageInfo.Amount, damageInfo.HitPoint, GameCatalog.Instance.GetWeaponId(damageInfo.Weapon), sourceReference);
        return false;
    }

    [Rpc(SendTo.Owner)]
    private void ApplyDamageRpc(float amount, Vector2 hitPoint, int weaponId, NetworkObjectReference source)
    {
        GameObject sourceObject = source.TryGet(out NetworkObject sourceNetworkObject) ? sourceNetworkObject.gameObject : null;
        m_health.ApplyDamage(new DamageInfo(amount, sourceObject, GameCatalog.Instance.GetWeapon(weaponId), hitPoint));
    }
}
