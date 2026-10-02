using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The lobby panel: the join code to share, a row per player, your own name
/// and character, the Ready button, and the countdown.
///
/// Like the inventory bar, it's a reader. It redraws when any player's profile
/// changes (LobbyPlayer.Changed) and only writes through your own LobbyPlayer,
/// so what it shows is always what everyone else sees.
///
/// Setup:
///   1. Add to the Lobby panel object.
///   2. Drag in the labels, the four authored player rows (in order), the name
///      input field, the character arrows, portrait and name, and the Ready
///      and Leave buttons.
/// </summary>
public class LobbyView : MonoBehaviour
{
    [SerializeField] private TMP_Text m_joinCodeLabel;

    [Tooltip("One authored row per possible player, top to bottom. Unused rows hide.")]
    [SerializeField] private LobbyPlayerRowView[] m_rows;

    [Header("Your profile")]
    [SerializeField] private TMP_InputField m_nameInput;
    [SerializeField] private Button m_previousCharacterButton;
    [SerializeField] private Button m_nextCharacterButton;
    [SerializeField] private Image m_characterPortrait;
    [SerializeField] private TMP_Text m_characterNameLabel;

    [Header("Ready")]
    [SerializeField] private Button m_readyButton;

    [Tooltip("Optional. Flips between \"Ready\" and \"Not Ready\". Leave empty for an icon-only button.")]
    [SerializeField] private TMP_Text m_readyButtonLabel;
    [SerializeField] private TMP_Text m_countdownLabel;
    [SerializeField] private Button m_leaveButton;

    // The second currently on the countdown label, so its text is only rebuilt once a second.
    private int m_shownSeconds = -1;

    private void OnEnable()
    {
        LobbyPlayer.Changed += Refresh;

        m_nameInput.onEndEdit.AddListener(OnNameEdited);
        m_previousCharacterButton.onClick.AddListener(PreviousCharacter);
        m_nextCharacterButton.onClick.AddListener(NextCharacter);
        m_readyButton.onClick.AddListener(ToggleReady);
        m_leaveButton.onClick.AddListener(Leave);

        m_shownSeconds = -1;
        m_countdownLabel.text = string.Empty;
        Refresh();
    }

    private void OnDisable()
    {
        LobbyPlayer.Changed -= Refresh;

        m_nameInput.onEndEdit.RemoveListener(OnNameEdited);
        m_previousCharacterButton.onClick.RemoveListener(PreviousCharacter);
        m_nextCharacterButton.onClick.RemoveListener(NextCharacter);
        m_readyButton.onClick.RemoveListener(ToggleReady);
        m_leaveButton.onClick.RemoveListener(Leave);
    }

    private void Update()
    {
        LobbyState lobby = LobbyState.Instance;
        int seconds = lobby != null && lobby.IsCountingDown ? Mathf.CeilToInt(lobby.SecondsRemaining) : -1;
        if (seconds == m_shownSeconds) return;

        m_shownSeconds = seconds;
        m_countdownLabel.text = seconds >= 0 ? $"Starting in {seconds}…" : string.Empty;
    }

    private void Refresh()
    {
        m_joinCodeLabel.text = GameSession.JoinCode ?? string.Empty;

        for (int i = 0; i < m_rows.Length; i++)
        {
            if (i < LobbyPlayer.All.Count) m_rows[i].Show(LobbyPlayer.All[i]);
            else m_rows[i].Hide();
        }

        LobbyPlayer local = LobbyPlayer.Local;
        bool connected = local != null;

        m_nameInput.interactable = connected;
        m_previousCharacterButton.interactable = connected;
        m_nextCharacterButton.interactable = connected;
        m_readyButton.interactable = connected;
        if (!connected) return;

        // Never overwrite the box while you're typing in it.
        if (!m_nameInput.isFocused) m_nameInput.SetTextWithoutNotify(local.PlayerName);

        CharacterDefinition character = local.Character;
        m_characterPortrait.sprite = character != null ? character.Portrait : null;
        m_characterPortrait.enabled = m_characterPortrait.sprite != null;
        // Both labels are optional: a ready button can be an icon instead of text.
        if (m_characterNameLabel != null) m_characterNameLabel.text = character != null ? character.DisplayName : string.Empty;
        if (m_readyButtonLabel != null) m_readyButtonLabel.text = local.IsReady ? "Not Ready" : "Ready";
    }

    private void OnNameEdited(string text)
    {
        LobbyPlayer local = LobbyPlayer.Local;
        if (local != null) local.SetName(text);
    }

    private void PreviousCharacter() => StepCharacter(-1);

    private void NextCharacter() => StepCharacter(1);

    private void StepCharacter(int step)
    {
        LobbyPlayer local = LobbyPlayer.Local;
        if (local != null) local.SetCharacter(local.CharacterIndex + step);
    }

    private void ToggleReady()
    {
        Debug.Log("Toggling ready state");
        LobbyPlayer local = LobbyPlayer.Local;
        if (local != null) local.SetReady(!local.IsReady);
    }

    private void Leave() => GameSceneManager.Instance.LeaveToMainMenu();
}
