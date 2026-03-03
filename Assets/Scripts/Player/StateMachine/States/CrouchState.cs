using UnityEngine;

/// <summary>
/// Crouch state: lowered profile, slower movement (sneak).
/// Transitions to: GroundedState (stand up), AirborneState (fall off edge or jump)
/// </summary>
public class CrouchState : PlayerState
{
    public CrouchState(PlayerController player) : base(player) { }

    public override void Enter()
    {
        player.isCrouching = true;

        // Shrink CharacterController height, keep feet on ground
        player.controller.height = player.crouchHeight;
        float centerY = player.crouchHeight * 0.5f;
        player.controller.center = new Vector3(0f, centerY, 0f);

        player.anim.SetCrouching(true);
    }

    public override void Update()
    {
        // --- Toggle crouch off ---
        if (player.input.CrouchPressed)
        {
            if (player.CanStandUp())
            {
                player.stateMachine.SetState<GroundedState>();
                return;
            }
        }

        // --- Jump from crouch ---
        if (player.input.JumpPressed && player.CanStandUp())
        {
            RestoreStandingHeight();
            player.velocity.y = player.jumpForce;
            player.jumpCount = 1;
            player.anim.TriggerJump();
            player.stateMachine.SetState<AirborneState>();
            return;
        }

        // --- Fell off edge while crouched ---
        if (!player.isGrounded)
        {
            player.stateMachine.SetState<AirborneState>();
            return;
        }

        // --- Crouch movement (sneak) ---
        Vector3 moveDir = GetCameraRelativeDirection(player.input.MoveInput);
        player.velocity.x = moveDir.x * player.crouchSpeed;
        player.velocity.z = moveDir.z * player.crouchSpeed;

        RotateToward(moveDir, player.rotationSpeed);
        ApplyGravity();

        player.anim.SetSpeed(player.input.MoveInput.magnitude * player.crouchAnimMultiplier);
    }

    public override void Exit()
    {
        RestoreStandingHeight();
        player.isCrouching = false;
        player.anim.SetCrouching(false);
    }

    private void RestoreStandingHeight()
    {
        player.controller.height = player.standHeight;
        float centerY = player.standHeight * 0.5f;
        player.controller.center = new Vector3(0f, centerY, 0f);
    }
}
