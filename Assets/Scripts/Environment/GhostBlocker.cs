using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Invisible wall that blocks ghosts (NavMeshAgent) but lets the player (CharacterController) pass through.
///
/// How it works:
///   - NavMeshObstacle with Carve=true cuts a hole in the NavMesh so ghosts won't path through.
///   - BoxCollider is set to isTrigger=true so the player's CharacterController ignores it.
///   - MeshRenderer (if any) is disabled to make the wall invisible at runtime.
///
/// Setup:
///   1. Create a GameObject with a BoxCollider sized to the wall you want
///   2. Add this script — it auto-configures everything in Awake
///   3. Scale/position in Scene view to block ghost paths (green carve gizmo shows the blocked area)
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class GhostBlocker : MonoBehaviour
{
    [Tooltip("Show the blocked area as a wireframe in Scene view")]
    [SerializeField] private bool showGizmo = true;

    [Tooltip("Gizmo color for the blocked area")]
    [SerializeField] private Color gizmoColor = new Color(1f, 0.3f, 0.3f, 0.25f);

    void Awake()
    {
        // ── 1. Make invisible ──
        // Disable any renderer so the wall is invisible at runtime
        var meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
            meshRenderer.enabled = false;

        // ── 2. Set collider to trigger so player passes through ──
        // CharacterController.Move() ignores trigger colliders,
        // so the player walks through freely.
        var boxCollider = GetComponent<BoxCollider>();
        if (boxCollider != null)
            boxCollider.isTrigger = true;

        // ── 3. Add NavMeshObstacle to block ghost pathfinding ──
        // Carve=true cuts the NavMesh at runtime so NavMeshAgent ghosts
        // recalculate paths and avoid this area.
        var obstacle = GetComponent<NavMeshObstacle>();
        if (obstacle == null)
            obstacle = gameObject.AddComponent<NavMeshObstacle>();

        obstacle.carving = true;
        obstacle.carvingMoveThreshold = 0.1f;
        obstacle.carvingTimeToStationary = 0.1f; // Carve almost immediately
        obstacle.shape = NavMeshObstacleShape.Box;

        // Sync obstacle size to the BoxCollider so the carve area matches
        if (boxCollider != null)
        {
            obstacle.size = boxCollider.size;
            obstacle.center = boxCollider.center;
        }
    }

    void OnDrawGizmos()
    {
        if (!showGizmo) return;

        var boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null) return;

        // Draw filled + wireframe box showing the blocked area
        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.color = gizmoColor;
        Gizmos.DrawCube(boxCollider.center, boxCollider.size);

        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.8f);
        Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
    }
}
