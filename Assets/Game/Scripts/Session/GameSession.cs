using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

/// <summary>
/// The one place that talks to Unity's online services: signing in, hosting a
/// session, joining one by code, and leaving. A session bundles the lobby (the
/// join code and the player cap) with Relay (the connection between machines),
/// and starts Netcode as host or client once it's made.
///
/// Everything here throws on failure (bad code, no internet, lobby full) so the
/// menu calling it can show the reason.
/// </summary>
public static class GameSession
{
    public const int MaxPlayers = 4;

    private static Task s_signIn;

    /// <summary>The session this machine is in, or null.</summary>
    public static ISession Current { get; private set; }

    public static bool IsActive => Current != null;
    public static string JoinCode => Current?.Code;

    /// <summary>Creates a private session and starts this machine as its host.</summary>
    public static async Task HostAsync()
    {
        await SignInAsync();

        SessionOptions options = new SessionOptions { MaxPlayers = MaxPlayers, IsPrivate = true }.WithRelayNetwork();
        Current = await MultiplayerService.Instance.CreateSessionAsync(options);
    }

    /// <summary>Joins a session by the code its host shared, and connects to the host.</summary>
    public static async Task JoinAsync(string code)
    {
        await SignInAsync();

        Current = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant());
    }

    /// <summary>
    /// Leaves the current session. Safe to call when not in one. When the host
    /// leaves, the session ends for everyone.
    /// </summary>
    public static async Task LeaveAsync()
    {
        ISession session = Current;
        Current = null;
        if (session == null) return;

        try
        {
            await session.LeaveAsync();
        }
        catch (Exception e)
        {
            // Already gone (the host ended it, or the connection dropped). Nothing left to leave.
            Debug.LogWarning($"Leaving the session reported: {e.Message}");
        }
    }

    /// <summary>Host only. A locked session refuses new players; it's locked while a level is running.</summary>
    public static async Task SetLockedAsync(bool locked)
    {
        if (Current == null || !Current.IsHost) return;

        try
        {
            IHostSession host = Current.AsHost();
            host.IsLocked = locked;
            await host.SavePropertiesAsync();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Couldn't {(locked ? "lock" : "unlock")} the session: {e.Message}");
        }
    }

    private static async Task SignInAsync()
    {
        // Shared so pressing Host and Join together signs in once. A failed attempt is retried next time.
        if (s_signIn == null || s_signIn.IsFaulted || s_signIn.IsCanceled) s_signIn = SignInOnceAsync();
        await s_signIn;
    }

    private static async Task SignInOnceAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            InitializationOptions options = new InitializationOptions();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Each test player (Multiplayer Play Mode, or two builds on one PC) runs in its own
            // process. A per-process profile gives each a separate anonymous account; otherwise
            // they'd all be the same player and couldn't join each other.
            options.SetProfile($"dev{System.Diagnostics.Process.GetCurrentProcess().Id}");
#endif
            await UnityServices.InitializeAsync(options);
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }
}
