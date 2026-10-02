using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Level Complete screen, shown to everyone at once when LevelProgress
/// says the level is won.
///
/// Return to Lobby moves the whole group, so only the host can press it;
/// everyone else sees "Waiting for host…" until the host does. Main Menu and
/// Exit only take you out; if the host takes either, the session ends for all.
///
/// Setup:
///   1. Add to an always-active object on the gameplay HUD Canvas.
///   2. Drag in the panel (a child that starts hidden), its three buttons, and
///      the waiting label.
/// </summary>
public class LevelCompleteView : MonoBehaviour
{
    [SerializeField] private GameObject m_panel;
    [SerializeField] private Button m_returnToLobbyButton;
    [SerializeField] private Button m_mainMenuButton;
    [SerializeField] private Button m_exitButton;

    [Tooltip("Shown to everyone but the host, under the greyed-out Return to Lobby button.")]
    [SerializeField] private TMP_Text m_waitingLabel;

    private void OnEnable()
    {
        LevelProgress.Completed += Show;
        m_returnToLobbyButton.onClick.AddListener(ReturnToLobby);
        m_mainMenuButton.onClick.AddListener(MainMenu);
        m_exitButton.onClick.AddListener(Exit);

        if (LevelProgress.Instance != null && LevelProgress.Instance.IsComplete) Show();
        else m_panel.SetActive(false);
    }

    private void OnDisable()
    {
        LevelProgress.Completed -= Show;
        m_returnToLobbyButton.onClick.RemoveListener(ReturnToLobby);
        m_mainMenuButton.onClick.RemoveListener(MainMenu);
        m_exitButton.onClick.RemoveListener(Exit);
    }

    private void Show()
    {
        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        m_panel.SetActive(true);
        m_returnToLobbyButton.interactable = isHost;
        m_waitingLabel.gameObject.SetActive(!isHost);
    }

    private void ReturnToLobby() => GameSceneManager.Instance.ReturnToLobby();

    private void MainMenu() => GameSceneManager.Instance.LeaveToMainMenu();

    private void Exit() => GameSceneManager.Instance.QuitGame();
}
