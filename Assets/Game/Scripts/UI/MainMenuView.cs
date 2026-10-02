using System;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The title screen's front panel: Host, Join by code, Quit. Switches to the
/// lobby panel once in a session, and opens straight on the lobby when the
/// group returns from a level.
///
/// Setup:
///   1. Add to the MainMenu scene's Canvas.
///   2. Drag in the Main and Lobby panel objects, the three buttons, the join
///      code input field, and the status label.
/// </summary>
public class MainMenuView : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject m_mainPanel;
    [SerializeField] private GameObject m_lobbyPanel;

    [Header("Main panel")]
    [SerializeField] private Button m_hostButton;
    [SerializeField] private Button m_joinButton;
    [SerializeField] private Button m_quitButton;
    [SerializeField] private TMP_InputField m_joinCodeInput;

    [Tooltip("Shows progress (\"Creating lobby…\") and errors (\"Code not found\").")]
    [SerializeField] private TMP_Text m_statusLabel;

    private void OnEnable()
    {
        m_hostButton.onClick.AddListener(Host);
        m_joinButton.onClick.AddListener(Join);
        m_quitButton.onClick.AddListener(Quit);
    }

    private void OnDisable()
    {
        m_hostButton.onClick.RemoveListener(Host);
        m_joinButton.onClick.RemoveListener(Join);
        m_quitButton.onClick.RemoveListener(Quit);
    }

    private void Start()
    {
        NetworkManager network = NetworkManager.Singleton;
        ShowLobby(GameSession.IsActive && network != null && network.IsListening);

        string message = GameSceneManager.Instance != null ? GameSceneManager.Instance.ConsumeMessage() : null;
        SetStatus(message ?? string.Empty);
    }

    private async void Host()
    {
        SetBusy(true, "Creating lobby…");

        try
        {
            await GameSession.HostAsync();
            if (this == null) return;

            SetStatus(string.Empty);
            ShowLobby(true);
        }
        catch (Exception e)
        {
            if (this != null) SetStatus($"Couldn't create a lobby. {e.Message}");
        }
        finally
        {
            if (this != null) SetBusy(false);
        }
    }

    private async void Join()
    {
        string code = m_joinCodeInput.text;
        if (string.IsNullOrWhiteSpace(code))
        {
            SetStatus("Enter a join code first.");
            return;
        }

        SetBusy(true, "Joining…");

        try
        {
            await GameSession.JoinAsync(code);
            if (this == null) return;

            SetStatus(string.Empty);
            ShowLobby(true);
        }
        catch (Exception e)
        {
            if (this != null) SetStatus($"Couldn't join. Check the code and try again. {e.Message}");
        }
        finally
        {
            if (this != null) SetBusy(false);
        }
    }

    private void Quit() => GameSceneManager.Instance.QuitGame();

    private void ShowLobby(bool inLobby)
    {
        m_mainPanel.SetActive(!inLobby);
        m_lobbyPanel.SetActive(inLobby);
    }

    private void SetBusy(bool busy, string status = null)
    {
        m_hostButton.interactable = !busy;
        m_joinButton.interactable = !busy;
        m_joinCodeInput.interactable = !busy;
        if (status != null) SetStatus(status);
    }

    private void SetStatus(string text)
    {
        if (m_statusLabel != null) m_statusLabel.text = text;
    }
}
