using UnityEngine;

/// <summary>
/// Climbing state: player climbs designated surfaces.
/// Transitions to: AirborneState (jump off / let go / vault over top)
/// </summary>
public class ClimbState : PlayerState
{
    private ClimbSurface surface;
    private float climbSpeed = 4f;

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

        Vector3 climbVelocity = (climbUp * player.input.MoveInput.y
            + climbRight * player.input.MoveInput.x) * climbSpeed;

        player.controller.Move(climbVelocity * Time.deltaTime);
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

    private bool ReachedLedgeTop()
    {
        Vector3 checkPos = player.transform.position + Vector3.up * player.standHeight;
        return !Physics.Raycast(checkPos, -surface.SurfaceNormal, 1.5f);
    }
}
