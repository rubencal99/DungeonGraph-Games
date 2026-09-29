using UnityEngine;

/// <summary>
/// A container the player opens once, which rolls its loot table and throws the
/// results onto the floor as ordinary weapon pickups.
///
/// It is an Interactable and nothing more. The proximity scan, the prompt, and
/// the E press are all inherited unchanged from the path a weapon pickup already
/// uses, which is why a chest costs two overrides rather than a subsystem.
///
/// Its loot reaches the world the same way a traded-away weapon does, through
/// WeaponPickupSpawner. A chest that instantiated its own pickups would be a
/// second spawn path, and the two would drift the moment pooling lands.
///
/// Tiering is authoring, not code. A wooden chest and a gold chest are the same
/// prefab pointing at different LootTableDefinition assets; because tables nest,
/// "a gold chest mostly rolls the legendary pool" is a table whose rows are
/// other tables. There is no chest-side notion of tiers at all.
///
/// Setup:
///   1. Author one prefab: SpriteRenderer + Animator + this component.
///   2. Add a PromptAnchor child and assign it to the Prompt View slot.
///   3. Assign an "Open" InteractionPromptDefinition and a Loot Table asset.
///   4. Give the Animator a bool parameter named "Open".
/// </summary>
public class Chest : Interactable
{
    [Tooltip("What this chest can produce. Swapping this asset is what makes one chest wooden and " +
             "another gold; nest tables inside it for tiered odds.")]
    [SerializeField] private LootTableDefinition m_lootTable;

    [Tooltip("Where loot is thrown from. Falls back to this object's position when empty.")]
    [SerializeField] private Transform m_lootSocket;

    [Header("Contents")]
    [Tooltip("How many weapons this chest drops: X is the minimum, Y the maximum, inclusive.")]
    [SerializeField] private Vector2Int m_dropCount = Vector2Int.one;

    [Tooltip("Total angle the drops are fanned across, centred on straight down, so several " +
             "items never land on one spot. Zero sends them all straight down.")]
    [SerializeField, Range(0f, 360f)] private float m_fanDegrees = 90f;

    [Header("Visuals")]
    [Tooltip("Driven by a bool parameter named \"Open\".")]
    [SerializeField] private Animator m_animator;

    private static readonly int OpenParam = Animator.StringToHash("Open");

    private bool m_isOpen;

    public bool IsOpen => m_isOpen;

    protected override void OnEnable()
    {
        base.OnEnable();
        ApplyOpenVisual();
    }

    /// <summary>
    /// An opened chest reports false, which is all it takes for the prompt to
    /// stop appearing: the scanner re-runs this every scan.
    /// </summary>
    public override bool CanInteract(GameObject interactor)
    {
        return !m_isOpen && m_lootTable != null;
    }

    public override void Activate(GameObject interactor)
    {
        if (m_isOpen) return;

        // Flagged open before anything is rolled. A chest that paid out twice is
        // a far worse bug than one that refuses a second open.
        m_isOpen = true;
        ApplyOpenVisual();
        PlayActivateCue();

        SetPromptVisible(false);
        SpawnLoot();
    }

    private void SpawnLoot()
    {
        if (WeaponPickupSpawner.Instance == null)
        {
            Debug.LogError($"{name}: no {nameof(WeaponPickupSpawner)} in the scene, so this chest " +
                           "opened but dropped nothing. Add one.", this);
            return;
        }

        Vector2 origin = m_lootSocket != null ? m_lootSocket.position : transform.position;
        int count = ResolveDropCount();

        for (int i = 0; i < count; i++)
        {
            WeaponDefinition weapon = m_lootTable.Roll();

            if (weapon == null)
            {
                Debug.LogWarning($"{name}: '{m_lootTable.name}' rolled nothing. Check that its rows " +
                                 "have weapons assigned and weights above zero.", this);
                continue;
            }

            // Fanned by direction rather than by writing velocities here; the
            // slide itself belongs to the pickup and is shared with player drops.
            float angle = ResolveFanAngle(i, count);
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

            WeaponPickupSpawner.Instance.Spawn(weapon, origin, direction);
        }
    }

    /// <summary>How many weapons to drop this time, inclusive of both ends of the authored range.</summary>
    private int ResolveDropCount()
    {
        int min = Mathf.Max(1, m_dropCount.x);
        int max = Mathf.Max(min, m_dropCount.y);

        return Random.Range(min, max + 1);
    }

    /// <summary>
    /// Launch angle for one drop, in degrees. Loot falls out of the bottom of the
    /// chest; a batch spreads evenly across the fan either side of that so the
    /// drops do not land on one spot.
    /// </summary>
    private float ResolveFanAngle(int index, int count)
    {
        const float straightDown = -90f;

        if (count <= 1) return straightDown;

        float t = index / (float)(count - 1);
        return straightDown + Mathf.Lerp(-m_fanDegrees * 0.5f, m_fanDegrees * 0.5f, t);
    }

    /// <summary>Drives the lid animation from the current state. Safe to call repeatedly.</summary>
    private void ApplyOpenVisual()
    {
        if (m_animator == null) return;

        m_animator.SetBool(OpenParam, m_isOpen);
    }

    private void OnValidate()
    {
        if (m_dropCount.x < 1 || m_dropCount.y < m_dropCount.x)
        {
            Debug.LogWarning($"{name}: Drop Count is ({m_dropCount.x}, {m_dropCount.y}). X must be at " +
                             "least 1 and Y at least X; the values are clamped at runtime.", this);
        }
    }
}
