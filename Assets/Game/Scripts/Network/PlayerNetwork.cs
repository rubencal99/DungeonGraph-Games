using System;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Turns one Player prefab into either "you" or "a teammate".
///
/// Every machine has a copy of every player. On the copy you control,
/// everything runs. On everyone else's copy, the components that read the mouse
/// and keyboard are switched off; that copy just shows what the network says.
///
/// Also dresses the avatar in its owner's lobby choices (character and name),
/// announces the local player so the camera rig can follow it, and receives
/// weapons the host hands over from pickups.
///
/// Setup:
///   1. Add to the Player prefab root, next to the NetworkObject.
///   2. List in Owner Only every component that reads input: PlayerController,
///      PlayerAim, PlayerShooter, PlayerInteractor, PlayerFootsteps, and
///      InventoryInput.
///   3. Drag in PlayerAim, Inventory, the Character child's Animator, and the
///      optional name label.
/// </summary>
public class PlayerNetwork : NetworkBehaviour
{
    private static readonly List<PlayerNetwork> s_all = new();

    /// <summary>The player this machine controls, or null before it spawns.</summary>
    public static PlayerNetwork Local { get; private set; }

    /// <summary>Raised on this machine when its own player spawns.</summary>
    public static event Action<PlayerNetwork> LocalSpawned;

    [Tooltip("Components that read this machine's mouse and keyboard. Switched off on every copy of this " +
             "player except the one its owner controls.")]
    [SerializeField] private Behaviour[] m_ownerOnly;

    [SerializeField] private PlayerAim m_aim;
    [SerializeField] private Inventory m_inventory;

    [Header("Appearance")]
    [Tooltip("The Character child's Animator. Its controller is swapped for the chosen character's.")]
    [SerializeField] private Animator m_animator;

    [Tooltip("Optional floating name. Shown over teammates, hidden over yourself.")]
    [SerializeField] private TMP_Text m_nameLabel;

    public PlayerAim Aim => m_aim;

    public override void OnNetworkSpawn()
    {
        s_all.Add(this);

        foreach (Behaviour component in m_ownerOnly)
        {
            if (component != null) component.enabled = IsOwner;
        }

        ApplyProfile();

        if (!IsOwner) return;

        Local = this;
        Inventory.SetLocal(m_inventory);
        LocalSpawned?.Invoke(this);
    }

    public override void OnNetworkDespawn()
    {
        s_all.Remove(this);

        if (Local != this) return;

        Local = null;
        Inventory.SetLocal(null);
    }

    /// <summary>The avatar a given client controls, or null. Used by the host to hand out pickups.</summary>
    public static PlayerNetwork ForClient(ulong clientId)
    {
        for (int i = 0; i < s_all.Count; i++)
        {
            if (s_all[i].OwnerClientId == clientId) return s_all[i];
        }

        return null;
    }

    /// <summary>The host calls this once a pickup is confirmed as yours. Runs on your machine.</summary>
    [Rpc(SendTo.Owner)]
    public void GiveWeaponRpc(int weaponId)
    {
        WeaponDefinition weapon = GameCatalog.Instance.GetWeapon(weaponId);
        if (weapon != null) m_inventory.TryPickup(weapon);
    }

    private void ApplyProfile()
    {
        LobbyPlayer profile = LobbyPlayer.ForClient(OwnerClientId);
        if (profile == null) return;

        CharacterDefinition character = profile.Character;
        if (m_animator != null && character != null && character.Animator != null)
        {
            m_animator.runtimeAnimatorController = character.Animator;
        }

        if (m_nameLabel != null)
        {
            m_nameLabel.text = profile.PlayerName;
            m_nameLabel.gameObject.SetActive(!IsOwner);
        }
    }
}
