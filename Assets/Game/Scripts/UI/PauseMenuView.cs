using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The pause menu. Opening it only stops your controls; the shared world keeps
/// running, because your teammates are still playing in it.
///
/// Setup:
///   1. Add to an always-active object on the gameplay HUD Canvas.
///   2. Drag in the panel (a child that starts hidden) and its three buttons.
/// </summary>
public class PauseMenuView : MonoBehaviour
{
    [SerializeField] private GameObject m_panel;
    [SerializeField] private Button m_resumeButton;
    [SerializeField] private Button m_mainMenuButton;
    [SerializeField] private Button m_exitButton;

    private void OnEnable()
    {
        GameSceneManager.PausedChanged += OnPausedChanged;
        m_resumeButton.onClick.AddListener(Resume);
        m_mainMenuButton.onClick.AddListener(MainMenu);
        m_exitButton.onClick.AddListener(Exit);

        OnPausedChanged(GameSceneManager.Instance != null && GameSceneManager.Instance.IsPaused);
    }

    private void OnDisable()
    {
        GameSceneManager.PausedChanged -= OnPausedChanged;
        m_resumeButton.onClick.RemoveListener(Resume);
        m_mainMenuButton.onClick.RemoveListener(MainMenu);
        m_exitButton.onClick.RemoveListener(Exit);
    }

    private void OnPausedChanged(bool paused) => m_panel.SetActive(paused);

    private void Resume() => GameSceneManager.Instance.SetPaused(false);

    private void MainMenu() => GameSceneManager.Instance.LeaveToMainMenu();

    private void Exit() => GameSceneManager.Instance.QuitGame();
}
