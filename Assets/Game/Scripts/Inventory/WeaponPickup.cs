using UnityEngine;

/// <summary>
/// A weapon lying on the floor, waiting for an E press. One prefab serves every
/// weapon in the project: which one it is arrives as a WeaponDefinition, and the
/// sprite is read from that definition's Icon.
///
/// That indirection is what keeps weapon authoring to one definition asset plus
/// one weapon prefab. Without it, every new gun would need its own pickup prefab.
///
/// Two ways to fill one in. A pickup dragged into a scene uses its authored
/// Weapon field; one thrown out by a chest or a trade is told what it is by
/// WeaponPickupSpawner.
///
/// Setup:
///   1. Author one prefab: SpriteRenderer + CircleCollider2D (Is Trigger on) +
///      Rigidbody2D (Gravity Scale 0, Linear Damping ~6) + this component.
///   2. Add a PromptAnchor child and assign it to the Prompt View slot.
///   3. Assign a "Pick Up" InteractionPromptDefinition to the Prompt slot.
///   4. Assign that prefab to the scene's WeaponPickupSpawner.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class WeaponPickup : Interactable
{
    [Tooltip("Which weapon this is, for pickups placed by hand in a scene. " +
             "Runtime-spawned pickups are told theirs by the spawner instead.")]
    [SerializeField] private WeaponDefinition m_weapon;

    [SerializeField] private SpriteRenderer m_spriteRenderer;

    [Tooltip("Speed a dropped or looted weapon slides away at. The Rigidbody2D's " +
             "Linear Damping is what brings it to rest.")]
    [SerializeField] private float m_launchSpeed = 3f;

    private Rigidbody2D m_rb;

    /// <summary>Which weapon is lying here, or null before the spawner has said.</summary>
    public WeaponDefinition Definition => m_weapon;

    private void Awake()
    {
        m_rb = GetComponent<Rigidbody2D>();
        ApplySprite();
    }

    /// <summary>
    /// Tells a freshly spawned pickup which weapon it holds. Call before it is
    /// seen, i.e. immediately after Instantiate.
    /// </summary>
    public void SetWeapon(WeaponDefinition weapon)
    {
        m_weapon = weapon;
        ApplySprite();
    }

    /// <summary>Slides this pickup out along a direction, so a chest's drops do not stack.</summary>
    public void Launch(Vector2 direction)
    {
        if (m_rb == null || direction.sqrMagnitude < 0.0001f) return;
        m_rb.linearVelocity = direction.normalized * m_launchSpeed;
    }

    /// <summary>Anything with an inventory can take this, player or otherwise.</summary>
    public override bool CanInteract(GameObject interactor)
    {
        return m_weapon != null && FindInventory(interactor) != null;
    }

    /// <summary>
    /// The inventory decides whether the weapon fits and what gets traded away.
    /// This only removes itself once the weapon has definitely been taken, so a
    /// rejected pickup stays on the floor rather than vanishing.
    /// </summary>
    public override void Activate(GameObject interactor)
    {
        Inventory inventory = FindInventory(interactor);
        if (inventory == null) return;
        if (!inventory.TryPickup(m_weapon)) return;

        Destroy(gameObject);
    }

    /// <summary>
    /// The inventory belonging to a character. Searched through the children
    /// rather than on the root alone, because the component naturally sits on
    /// the weapons pivot next to WeaponHolder rather than on the character root.
    /// </summary>
    private static Inventory FindInventory(GameObject interactor)
    {
        return interactor != null ? interactor.GetComponentInChildren<Inventory>() : null;
    }

    private void ApplySprite()
    {
        if (m_spriteRenderer == null) return;

        m_spriteRenderer.sprite = m_weapon != null ? m_weapon.Icon : null;
        m_spriteRenderer.enabled = m_spriteRenderer.sprite != null;
    }
}
