using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Raycasts the mouse position onto the AimPlane layer each frame to find where
/// in the world the player is aiming, and derives the angle/direction/side other
/// systems (camera, weapon rotation, character flip) read from.
///
/// Setup:
///   1. Add to the Player prefab root, alongside PlayerController.
///   2. Drag the "AimPoint" action (Player map, InputSystem_Actions asset) into
///      the Aim Position Action slot.
///   3. Create a Layer named "AimPlane" and set the Aim Plane Mask to it.
///   4. Leave Camera empty to use Camera.main, or assign one explicitly.
/// </summary>
public class PlayerAim : AimSource
{
    [SerializeField] private InputActionReference m_aimPositionAction;
    [SerializeField] private LayerMask m_aimPlaneMask;
    [SerializeField] private float m_maxRayDistance = 500f;
    [SerializeField, Range(0f, 1f)] private float m_cameraPullStrength = 0.3f;
    [SerializeField] private Camera m_camera;

    public float CameraPullStrength => m_cameraPullStrength;

    private void Awake()
    {
        if (m_camera == null) m_camera = Camera.main;
        SetAimPoint(transform.position);
    }

    private void OnEnable()
    {
        m_aimPositionAction?.action.Enable();
    }

    private void OnDisable()
    {
        m_aimPositionAction?.action.Disable();
    }

    private void Update()
    {
        if (m_aimPositionAction == null || m_camera == null) return;

        Vector2 screenPos = m_aimPositionAction.action.ReadValue<Vector2>();
        Ray ray = m_camera.ScreenPointToRay(screenPos);

        // Missing the plane keeps the last point, re-derived from where the player now stands.
        Vector2 point = Physics.Raycast(ray, out RaycastHit hit, m_maxRayDistance, m_aimPlaneMask) ? hit.point : AimPoint;
        SetAimPoint(point);
    }
}
