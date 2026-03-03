using UnityEngine;

/// <summary>
/// Marks a surface as climbable. Attach to walls, ledges, or vertical surfaces.
/// The transform.forward should face AWAY from the climbing surface (outward normal).
/// </summary>
public class ClimbSurface : InteractableBase
{
    [Header("Climb Settings")]
    public float climbHeight = 10f;

    /// <summary>The outward-facing normal of the climbing surface.</summary>
    public Vector3 SurfaceNormal => transform.forward;

    void Awake()
    {
        prompt = "Climb";
    }

    public override void Interact(PlayerController player)
    {
        player.interactionTarget = gameObject;
        player.stateMachine.SetState<ClimbState>();
    }
}
