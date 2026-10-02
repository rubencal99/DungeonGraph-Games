using UnityEngine;

/// <summary>
/// Makes sure the persistent Core object (NetworkManager + GameSceneManager)
/// exists exactly once. The main menu is loaded again every time a player
/// returns to it; if Core were placed in that scene directly, each return would
/// add a second NetworkManager. This only creates it the first time.
///
/// Setup:
///   1. Add an empty "Bootstrap" GameObject to the MainMenu scene with this component.
///   2. Assign the Core prefab.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private GameSceneManager m_corePrefab;

    private void Awake()
    {
        if (GameSceneManager.Instance == null) Instantiate(m_corePrefab);
    }
}
