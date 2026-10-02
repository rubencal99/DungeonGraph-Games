using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The player's trigger finger: while Attack is held, asks the weapon in hand
/// to fire, and kicks the camera on each shot that goes out.
///
/// Kept apart from WeaponHolder, the same way InventoryInput is kept apart from
/// Inventory: a teammate's copy of you needs WeaponHolder to show your gun, but
/// must never read this machine's mouse button.
///
/// Setup:
///   1. Add to the Player prefab root.
///   2. Drag in the Player's WeaponHolder and the "Attack" action (Player map).
///   3. List it in PlayerNetwork's Owner Only array.
/// </summary>
public class PlayerShooter : MonoBehaviour
{
    [SerializeField] private WeaponHolder m_weaponHolder;
    [SerializeField] private InputActionReference m_fireAction;

    private void Update()
    {
        if (m_weaponHolder == null || m_fireAction == null || !m_fireAction.action.IsPressed()) return;

        Weapon held = m_weaponHolder.Held;

        // Shaken here rather than in Weapon, so an enemy's gun never shakes the player's screen.
        if (held != null && held.TryFire()) CameraShake.Play(held.Definition.FireShake);
    }
}
