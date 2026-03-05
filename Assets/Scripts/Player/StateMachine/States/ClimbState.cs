using UnityEngine;

/// <summary>
/// Climbing state: player climbs designated surfaces.
/// Transitions to: AirborneState (jump off / let go / vault over top)
///
/// Elegance features:
///   - Enforces ClimbSurface.climbHeight (blocks upward movement at max)
///   - Lateral boundary checking (prevents climbing off the side)
///   - Uses player.climbSpeed (configurable in Inspector, not hardcoded)
///   - Drop off bottom: trying to climb down past start position lets go
/// </summary>
public class ClimbState : PlayerState
{
    private ClimbSurface surface;
    private float startY;

    public ClimbState(PlayerController player) : base(player) { }

    public override void Enter()
    {
        // Zero velocity and disable gravity
        player.velocity = Vector3.zero;
        player.anim.SetClimbing(true);
        player.NotifyStateChanged("Climbing");

        // Get the climb surface from interactionTarget (set by ClimbSurface.Interact)
        if (player.interactionTarget != null)
        {
            surface = player.interactionTarget.GetComponent<ClimbSurface>();
        }

        // Track starting height for climbHeight enforcement
        startY = player.transform.position.y;
    }

    public override void Update()
    {
        if (surface == null)
        {
            player.stateMachine.SetState<AirborneState>();
            return;
        }

        // --- Jump off wall ---
        if (player.input.JumpPressed)
        {
            player.velocity.y = player.jumpForce * 0.7f;
            player.velocity += -surface.SurfaceNormal * 3f; // push away from wall
            player.jumpCount = 1;
            player.stateMachine.SetState<AirborneState>();
            return;
        }

        // --- Let go (crouch to drop) ---
        if (player.input.CrouchPressed)
        {
            player.stateMachine.SetState<AirborneState>();
            return;
        }

        // --- Climb movement ---
        Vector3 climbUp = Vector3.up;
        Vector3 climbRight = Vector3.Cross(surface.SurfaceNormal, Vector3.up).normalized;

        float verticalInput = player.input.MoveInput.y;
        float horizontalInput = player.input.MoveInput.x;

        // Enforce climbHeight: block upward movement if at max height
        float currentClimbHeight = player.transform.position.y - startY;
        if (currentClimbHeight >= surface.climbHeight && verticalInput > 0f)
            verticalInput = 0f;

        // Drop off bottom: let go if trying to climb down past the start
        if (player.transform.position.y <= startY && verticalInput < 0f)
        {
            player.stateMachine.SetState<AirborneState>();
            return;
        }

        Vector3 climbVelocity = (climbUp * verticalInput
            + climbRight * horizontalInput) * player.climbSpeed;

        player.controller.Move(climbVelocity * Time.deltaTime);

        // --- Lateral boundary: check if still on the surface ---
        if (!IsOnSurface())
        {
            // Nudge back toward surface center
            Vector3 toSurface = surface.transform.position - player.transform.position;
            toSurface.y = 0f;
            if (toSurface.sqrMagnitude > 0.001f)
                player.controller.Move(toSurface.normalized * 0.1f);
        }

        player.anim.SetSpeed(player.input.MoveInput.magnitude * player.climbAnimMultiplier);

        // --- Check if reached top (vault over ledge) ---
        if (ReachedLedgeTop())
        {
            player.velocity.y = player.jumpForce * 0.5f;
            player.velocity += surface.SurfaceNormal * 2f; // push over ledge
            player.stateMachine.SetState<AirborneState>();
        }
    }

    public override void Exit()
    {
        player.anim.SetClimbing(false);
        player.interactionTarget = null;
        surface = null;
    }

    // ─── Surface Checks ──────────────────────────────────────────

    private bool ReachedLedgeTop()
    {
        Vector3 checkPos = player.transform.position + Vector3.up * player.standHeight;
        return !Physics.Raycast(checkPos, -surface.SurfaceNormal, 1.5f);
    }

    /// <summary>
    /// Check if the player is still within the lateral bounds of the climb surface.
    /// Casts a ray from the player toward the surface; if it misses, player has moved off the edge.
    /// </summary>
    private bool IsOnSurface()
    {
        Vector3 rayOrigin = player.transform.position;
        Vector3 rayDir = -surface.SurfaceNormal;
        return Physics.Raycast(rayOrigin, rayDir, 1.5f);
    }
}
