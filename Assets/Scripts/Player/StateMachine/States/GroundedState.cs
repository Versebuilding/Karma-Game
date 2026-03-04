using UnityEngine;

/// <summary>
/// Default ground state: handles idle, walking, and sprinting.
/// Transitions to: AirborneState (jump/fall), CrouchState, CarryState, PushPullState, ClimbState
/// </summary>
public class GroundedState : PlayerState
{
    public GroundedState(PlayerController player) : base(player) { }

    public override void Enter()
    {
        player.jumpCount = 0;
        player.anim.SetGrounded(true);
    }

    public override void Update()
    {
        // --- Transition: fell off edge ---
        if (!player.isGrounded)
        {
            player.coyoteTimeCounter = player.coyoteTimeDuration;
            player.stateMachine.SetState<AirborneState>();
            return;
        }

        // --- Transition: jump ---
        if (player.input.JumpPressed)
        {
            player.velocity.y = player.jumpForce;
            player.jumpCount = 1;
            player.anim.TriggerJump();
            player.stateMachine.SetState<AirborneState>();
            return;
        }

        // --- Transition: crouch ---
        if (player.input.CrouchPressed)
        {
            player.stateMachine.SetState<CrouchState>();
            return;
        }

        // --- Transition: interact with world objects ---
        if (player.input.InteractPressed && player.interactionDetector != null
            && player.interactionDetector.CurrentTarget != null)
        {
            var target = player.interactionDetector.CurrentTarget;
            if (target.CanInteract(player))
            {
                target.Interact(player);
                return;
            }
        }

        // --- Movement ---
        bool sprinting = player.input.SprintHeld && player.input.MoveInput.sqrMagnitude > 0.01f;
        float speed = sprinting ? player.sprintSpeed : player.walkSpeed;

        // Slow down during stumble
        if (player.IsStumbling)
            speed *= 0.15f;

        Vector3 moveDir = GetCameraRelativeDirection(player.input.MoveInput);
        player.velocity.x = moveDir.x * speed;
        player.velocity.z = moveDir.z * speed;

        RotateToward(moveDir, player.rotationSpeed);
        ApplyGravity();

        // --- Animation ---
        float moveAmount = player.input.MoveInput.magnitude;
        float animSpeed;
        if (sprinting && moveAmount > 0.1f)
            animSpeed = player.sprintAnimMultiplier;
        else
            animSpeed = moveAmount * player.walkAnimMultiplier;

        player.anim.SetSpeed(animSpeed);
        player.anim.SetSprinting(sprinting);
        player.anim.SetGrounded(true);
    }

    public override void Exit()
    {
        player.anim.SetSprinting(false);
    }
}
