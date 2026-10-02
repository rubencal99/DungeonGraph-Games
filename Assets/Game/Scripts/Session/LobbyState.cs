using Unity.Netcode;
using UnityEngine;

/// <summary>
/// The lobby's shared clock. When every player is ready, the host starts a
/// countdown; if anyone un-readies or a new player joins (unready), it stops.
/// When it reaches zero the host takes everyone into the level.
///
/// The countdown is stored as the server time it ends at, not as a number
/// ticking down, so every screen shows the same second without a message per
/// tick.
///
/// Spawned by GameSceneManager when a session starts and kept for the whole
/// session.
///
/// Setup:
///   1. Make a prefab with a NetworkObject and this component.
///   2. Add it to the NetworkManager's Network Prefabs list.
///   3. Assign it to GameSceneManager's LobbyState Prefab slot.
/// </summary>
public class LobbyState : NetworkBehaviour
{
    private const double NotCounting = -1d;

    public static LobbyState Instance { get; private set; }

    [SerializeField, Min(0f)] private float m_countdownSeconds = 5f;

    private readonly NetworkVariable<double> m_countdownEndTime = new(NotCounting);

    public bool IsCountingDown => m_countdownEndTime.Value >= 0d;

    public float SecondsRemaining =>
        IsCountingDown ? (float)System.Math.Max(0d, m_countdownEndTime.Value - NetworkManager.ServerTime.Time) : 0f;

    public override void OnNetworkSpawn()
    {
        Instance = this;
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!IsServer) return;

        GameSceneManager scenes = GameSceneManager.Instance;
        bool canStart = scenes != null && scenes.InMainMenu && EveryoneReady();

        if (!canStart)
        {
            if (IsCountingDown) m_countdownEndTime.Value = NotCounting;
            return;
        }

        double now = NetworkManager.ServerTime.Time;

        if (!IsCountingDown)
        {
            m_countdownEndTime.Value = now + m_countdownSeconds;
            return;
        }

        if (now < m_countdownEndTime.Value) return;

        m_countdownEndTime.Value = NotCounting;
        foreach (LobbyPlayer player in LobbyPlayer.All) player.ClearReady();
        scenes.StartGameplay();
    }

    private static bool EveryoneReady()
    {
        if (LobbyPlayer.All.Count == 0) return false;

        foreach (LobbyPlayer player in LobbyPlayer.All)
        {
            if (!player.IsReady) return false;
        }

        return true;
    }
}
