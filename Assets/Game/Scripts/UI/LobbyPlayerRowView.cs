using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One player's line in the lobby list: portrait, name, and a ready tick.
/// Pure display; LobbyView decides who goes in which row.
///
/// Setup:
///   1. Build one row by hand (Image + TMP label + a tick Image), add this,
///      drag the three in, and save it as a prefab.
///   2. Place four of them under the lobby's player list.
/// </summary>
public class LobbyPlayerRowView : MonoBehaviour
{
    [SerializeField] private Image m_portrait;
    [SerializeField] private TMP_Text m_nameLabel;

    [Tooltip("Shown while this player is ready.")]
    [SerializeField] private GameObject m_readyMark;

    public void Show(LobbyPlayer player)
    {
        gameObject.SetActive(true);

        CharacterDefinition character = player.Character;
        m_portrait.sprite = character != null ? character.Portrait : null;
        m_portrait.enabled = m_portrait.sprite != null;

        m_nameLabel.text = player == LobbyPlayer.Local ? $"{player.PlayerName} (you)" : player.PlayerName;
        m_readyMark.SetActive(player.IsReady);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
