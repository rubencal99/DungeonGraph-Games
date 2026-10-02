using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Moves the game between screens: main menu to gameplay, back to the lobby,
/// out to the main menu, and quitting. Also owns pausing and whether gameplay
/// input is on.
///
/// Scene changes inside a session go through Netcode, so when the host moves,
/// everyone moves with them. Leaving to the main menu is a local load, because
/// by then this machine has left the session.
///
/// Pausing is a menu over the game, not a stopped clock: the world is shared,
/// so it keeps running for your teammates and for you.
///
/// Lives on the persistent Core prefab next to NetworkManager, created once by
/// GameBootstrap and kept for the whole run.
///
/// Setup (on the Core prefab):
///   1. Type the exact Main Menu and Gameplay scene names, and add both scenes
///      to File > Build Profiles > Scene List.
///   2. Assign the LobbyState prefab.
///   3. Assign the UI map's "Pause" action. It must not be in the Player map,
///      or pausing would switch off the key that unpauses.
/// </summary>
public class GameSceneManager : MonoBehaviour
{
    public static GameSceneManager Instance { get; private set; }

    /// <summary>Raised when the pause menu opens (true) or closes (false).</summary>
    public static event Action<bool> PausedChanged;

    [Header("Scenes")]
    [SerializeField] private string m_mainMenuScene = "MainMenu";
    [SerializeField] private string m_gameplayScene = "Demo 1";

    [Header("Session")]
    [Tooltip("Spawned by the host when a session starts and kept until it ends. Runs the ready-up countdown.")]
    [SerializeField] private NetworkObject m_lobbyStatePrefab;

    [Header("Input")]
    [Tooltip("Opens and closes the pause menu during gameplay. Keep it in the UI map, not the Player map.")]
    [SerializeField] private InputActionReference m_pauseAction;

    [Tooltip("The action map switched off while paused or after the level ends.")]
    [SerializeField] private string m_gameplayActionMap = "Player";

    private NetworkManager m_network;

    // Set while this machine is leaving on purpose, so its own disconnect isn't
    // mistaken for the host vanishing.
    private bool m_expectingStop;

    private string m_pendingMessage;

    public bool IsPaused { get; private set; }
    public bool InMainMenu => SceneManager.GetActiveScene().name == m_mainMenuScene;
    public bool InGameplay => SceneManager.GetActiveScene().name == m_gameplayScene;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        m_network = GetComponent<NetworkManager>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        LevelProgress.Completed += OnLevelCompleted;

        if (m_pauseAction != null)
        {
            m_pauseAction.action.Enable();
            m_pauseAction.action.performed += OnPausePerformed;
        }

        if (m_network != null)
        {
            m_network.OnServerStarted += OnServerStarted;
            m_network.OnClientStopped += OnClientStopped;
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        LevelProgress.Completed -= OnLevelCompleted;

        if (m_pauseAction != null) m_pauseAction.action.performed -= OnPausePerformed;

        if (m_network != null)
        {
            m_network.OnServerStarted -= OnServerStarted;
            m_network.OnClientStopped -= OnClientStopped;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Host only. Takes everyone in the session into the gameplay scene.</summary>
    public void StartGameplay()
    {
        if (!m_network.IsServer) return;

        _ = GameSession.SetLockedAsync(true);
        m_network.SceneManager.LoadScene(m_gameplayScene, LoadSceneMode.Single);
    }

    /// <summary>Host only. Takes everyone back to the lobby screen, still together in the session.</summary>
    public void ReturnToLobby()
    {
        if (!m_network.IsServer) return;

        SetPaused(false);
        _ = GameSession.SetLockedAsync(false);
        m_network.SceneManager.LoadScene(m_mainMenuScene, LoadSceneMode.Single);
    }

    /// <summary>
    /// Leaves the session and goes to the main menu. If this machine is the host,
    /// the session ends and everyone else is sent to their main menu too.
    /// </summary>
    public async void LeaveToMainMenu()
    {
        SetPaused(false);
        await LeaveSessionAsync();
        SceneManager.LoadScene(m_mainMenuScene);
    }

    /// <summary>Leaves the session, then closes the game.</summary>
    public async void QuitGame()
    {
        await LeaveSessionAsync();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void TogglePause() => SetPaused(!IsPaused);

    public void SetPaused(bool paused)
    {
        // Nothing to pause outside gameplay, and the level-complete screen replaces the pause menu.
        if (paused && (!InGameplay || IsLevelComplete())) return;
        if (IsPaused == paused) return;

        IsPaused = paused;
        SetGameplayInputEnabled(!paused);
        PausedChanged?.Invoke(paused);
    }

    /// <summary>Switches the Player action map (move, aim, shoot, interact, scroll) on or off.</summary>
    public void SetGameplayInputEnabled(bool enabled)
    {
        InputActionMap map = InputSystem.actions != null ? InputSystem.actions.FindActionMap(m_gameplayActionMap) : null;
        if (map == null) return;

        if (enabled) map.Enable();
        else map.Disable();
    }

    /// <summary>A message for the main menu to show once (e.g. "The host ended the session"), then forget.</summary>
    public string ConsumeMessage()
    {
        string message = m_pendingMessage;
        m_pendingMessage = null;
        return message;
    }

    private async System.Threading.Tasks.Task LeaveSessionAsync()
    {
        m_expectingStop = m_network.IsListening || m_network.ShutdownInProgress;

        await GameSession.LeaveAsync();
        if (m_network.IsListening) m_network.Shutdown();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Every new screen starts unpaused with the controls on.
        IsPaused = false;
        PausedChanged?.Invoke(false);
        SetGameplayInputEnabled(true);
    }

    private void OnLevelCompleted()
    {
        SetPaused(false);
        SetGameplayInputEnabled(false);
    }

    private void OnPausePerformed(InputAction.CallbackContext context) => TogglePause();

    private void OnServerStarted()
    {
        if (m_lobbyStatePrefab == null)
        {
            Debug.LogError($"{nameof(GameSceneManager)} has no LobbyState prefab, so the ready-up countdown " +
                           "can't run. Assign it on the Core prefab.", this);
            return;
        }

        Instantiate(m_lobbyStatePrefab).Spawn();
    }

    /// <summary>
    /// Fires when this machine's connection ends. Unexpected ends mean the host
    /// left or the connection dropped, so the player is taken to the main menu
    /// with a note saying why.
    /// </summary>
    private void OnClientStopped(bool wasHost)
    {
        if (m_expectingStop)
        {
            m_expectingStop = false;
            return;
        }

        m_pendingMessage = "The session ended: the host left or the connection was lost.";
        _ = GameSession.LeaveAsync();
        SetPaused(false);
        SceneManager.LoadScene(m_mainMenuScene);
    }

    private static bool IsLevelComplete() => LevelProgress.Instance != null && LevelProgress.Instance.IsComplete;
}
