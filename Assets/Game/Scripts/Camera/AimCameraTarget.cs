using UnityEngine;

/// <summary>
/// Sits between the player and their aim point, pulled toward the aim point by
/// PlayerAim's Camera Pull Strength (0 = stays on the player, 1 = sits exactly
/// at the aim point). A Cinemachine Camera tracks this transform instead of the
/// player directly.
///
/// Setup (hand-placed scenes only — PlayerSpawner wires this automatically
/// when it instantiates the player rig at runtime):
///   1. Create an empty GameObject named "CameraTarget" in the scene.
///   2. Add this component and drag the Player's PlayerAim into the Player Aim slot.
///   3. Point the Cinemachine Camera's Tracking Target at this object.
/// </summary>
public class AimCameraTarget : MonoBehaviour
{
    [SerializeField] private PlayerAim m_playerAim;

    public void SetPlayerAim(PlayerAim playerAim)
    {
        m_playerAim = playerAim;
    }

    private void LateUpdate()
    {
        if (m_playerAim == null) return;

        Vector2 playerPosition = m_playerAim.transform.position;
        transform.position = Vector2.Lerp(playerPosition, m_playerAim.AimPoint, m_playerAim.CameraPullStrength);
    }
}
