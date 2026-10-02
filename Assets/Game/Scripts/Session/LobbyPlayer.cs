using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// One player's lobby profile: their name, character, and whether they're
/// ready. It exists for as long as they're in the session, through every
/// trip between the lobby and a level, so the gameplay avatar reads its look
/// from here.
///
/// You edit your own name and character directly, and everyone sees the
/// change within a network tick. Ready goes through the host instead, so the
/// host can clear everyone's ready flag the moment a level starts. Then nobody
/// comes back to the lobby already marked ready.
///
/// Setup:
///   1. Make a prefab with a NetworkObject and this component.
///   2. Assign it as the NetworkManager's Player Prefab, so one is created for
///      every player who connects.
/// </summary>
public class LobbyPlayer : NetworkBehaviour
{
    private const string SavedNameKey = "PlayerName";

    private static readonly List<LobbyPlayer> s_all = new();

    /// <summary>Everyone in the session, host first, in join order.</summary>
    public static IReadOnlyList<LobbyPlayer> All => s_all;

    /// <summary>This machine's own profile, or null before it has connected.</summary>
    public static LobbyPlayer Local { get; private set; }

    /// <summary>Raised whenever anyone joins, leaves, or changes their name, character, or ready state.</summary>
    public static event Action Changed;

    private readonly NetworkVariable<FixedString32Bytes> m_name =
        new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<int> m_character =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<bool> m_ready = new();

    public string PlayerName { get; private set; } = string.Empty;
    public int CharacterIndex => m_character.Value;
    public CharacterDefinition Character => GameCatalog.Instance.GetCharacter(m_character.Value);
    public bool IsReady => m_ready.Value;

    public override void OnNetworkSpawn()
    {
        s_all.Add(this);
        s_all.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));

        m_name.OnValueChanged += OnNameChanged;
        m_character.OnValueChanged += OnCharacterChanged;
        m_ready.OnValueChanged += OnReadyChanged;
        PlayerName = m_name.Value.ToString();

        if (IsOwner)
        {
            Local = this;
            SetName(PlayerPrefs.GetString(SavedNameKey, string.Empty));
        }

        Changed?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        s_all.Remove(this);

        m_name.OnValueChanged -= OnNameChanged;
        m_character.OnValueChanged -= OnCharacterChanged;
        m_ready.OnValueChanged -= OnReadyChanged;

        if (Local == this) Local = null;
        Changed?.Invoke();
    }

    /// <summary>Owner only. Blank names fall back to "Player N". Typed names are remembered for next time.</summary>
    public void SetName(string playerName)
    {
        if (!IsOwner) return;

        if (string.IsNullOrWhiteSpace(playerName))
        {
            playerName = $"Player {OwnerClientId + 1}";
        }
        else
        {
            playerName = playerName.Trim();
            PlayerPrefs.SetString(SavedNameKey, playerName);
        }

        m_name.Value = ToFixedString(playerName);
    }

    /// <summary>Owner only. Wraps around the GameCatalog's character list in either direction.</summary>
    public void SetCharacter(int index)
    {
        if (!IsOwner) return;

        int count = GameCatalog.Instance.Characters.Count;
        if (count == 0) return;

        m_character.Value = ((index % count) + count) % count;
    }

    /// <summary>Owner only. Asks the host to mark this player ready or not.</summary>
    public void SetReady(bool ready)
    {
        if (IsOwner) SetReadyRpc(ready);
    }

    /// <summary>Host only. Used when a level starts, so everyone returns to the lobby unready.</summary>
    public void ClearReady()
    {
        if (IsServer) m_ready.Value = false;
    }

    public static LobbyPlayer ForClient(ulong clientId)
    {
        for (int i = 0; i < s_all.Count; i++)
        {
            if (s_all[i].OwnerClientId == clientId) return s_all[i];
        }

        return null;
    }

    [Rpc(SendTo.Server)]
    private void SetReadyRpc(bool ready, RpcParams rpcParams = default)
    {
        // Only you can ready yourself.
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        m_ready.Value = ready;
    }

    private void OnNameChanged(FixedString32Bytes previous, FixedString32Bytes current)
    {
        PlayerName = current.ToString();
        Changed?.Invoke();
    }

    private void OnCharacterChanged(int previous, int current) => Changed?.Invoke();

    private void OnReadyChanged(bool previous, bool current) => Changed?.Invoke();

    /// <summary>Trims characters off the end until the name fits the network string's 29 bytes.</summary>
    private static FixedString32Bytes ToFixedString(string text)
    {
        while (Encoding.UTF8.GetByteCount(text) > FixedString32Bytes.UTF8MaxLengthInBytes)
        {
            text = text.Substring(0, text.Length - 1);
        }

        return new FixedString32Bytes(text);
    }
}
