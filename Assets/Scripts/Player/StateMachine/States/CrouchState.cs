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

        // Shrink CharacterController height while keeping the capsule BOTTOM
        // at exactly the same position (feet stay on ground).
        //
        // Standing bottom = originalCenter.y - standHeight/2
        // Crouch bottom must match: crouchCenter.y - crouchHeight/2 = standing bottom
        // => crouchCenter.y = standing bottom + crouchHeight/2
        float standingBottom = player.originalCCCenter.y - player.standHeight * 0.5f;
        float crouchCenterY = standingBottom + player.crouchHeight * 0.5f;

        player.controller.height = player.crouchHeight;
        player.controller.center = new Vector3(
            player.originalCCCenter.x, crouchCenterY, player.originalCCCenter.z);

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
        // Restore the exact original CC values from the prefab
        player.controller.height = player.standHeight;
        player.controller.center = player.originalCCCenter;
    }
}
