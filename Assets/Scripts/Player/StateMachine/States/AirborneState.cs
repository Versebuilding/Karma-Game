using UnityEngine;

/// <summary>
/// Handles jumping, falling, and double jump.
/// Transitions to: GroundedState (on land), CrouchState (if was crouching)
/// </summary>
public class AirborneState : PlayerState
{
    public AirborneState(PlayerController player) : base(player) { }

    public override void Enter()
    {
        player.anim.SetGrounded(false);
    }

    public override void Update()
    {
        // --- Double jump ---
        if (player.input.JumpPressed && player.jumpCount < 2)
        {
            player.velocity.y = player.doubleJumpForce;
            player.jumpCount = 2;
            player.anim.TriggerDoubleJump();
        }

        // --- Coyote time: allow late jump if walked off edge ---
        if (player.input.JumpPressed && player.jumpCount == 0 && player.coyoteTimeCounter > 0f)
        {
            player.velocity.y = player.jumpForce;
            player.jumpCount = 1;
            player.coyoteTimeCounter = 0f;
            player.anim.TriggerJump();
        }

        // --- Air control (reduced) ---
        float airControlFactor = 0.6f;
        Vector3 moveDir = GetCameraRelativeDirection(player.input.MoveInput);
        player.velocity.x = moveDir.x * player.walkSpeed * airControlFactor;
        player.velocity.z = moveDir.z * player.walkSpeed * airControlFactor;

        RotateToward(moveDir, player.rotationSpeed * 0.5f);
        ApplyGravity();

        player.anim.SetVerticalVelocity(player.velocity.y);

        // --- Land ---
        if (player.isGrounded && player.velocity.y <= 0f)
        {
            player.anim.TriggerLand();
            player.anim.SetGrounded(true);

            if (player.isCrouching)
            {
                player.stateMachine.SetState<CrouchState>();
            }
            else
            {
                player.stateMachine.SetState<GroundedState>();

                // Jump buffer: if player pressed jump just before landing
                if (player.jumpBufferCounter > 0f)
                {
                    player.jumpBufferCounter = 0f;
                    player.velocity.y = player.jumpForce;
                    player.jumpCount = 1;
                    player.anim.TriggerJump();
                    player.stateMachine.SetState<AirborneState>();
                }
            }
        }
    }

    public override void Exit()
    {
        player.coyoteTimeCounter = 0f;
    }
}
