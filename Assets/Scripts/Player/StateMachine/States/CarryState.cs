using UnityEngine;

/// <summary>
/// Handles carrying objects: slower movement, drop (E), throw (left click), stack.
/// Transitions to: GroundedState (drop/throw), AirborneState (fall off edge)
/// </summary>
public class CarryState : PlayerState
{
    public CarryState(PlayerController player) : base(player) { }

    public override void Enter()
    {
        player.anim.SetCarrying(true);
        player.NotifyStateChanged("Carrying");
    }

    public override void Update()
    {
        // --- Drop ---
        if (player.input.InteractPressed)
        {
            DropObject();
            player.stateMachine.SetState<GroundedState>();
            return;
        }

        // --- Throw ---
        if (player.input.AttackPressed)
        {
            ThrowObject();
            player.stateMachine.SetState<GroundedState>();
            return;
        }

        // --- Fall off edge ---
        if (!player.isGrounded)
        {
            player.stateMachine.SetState<AirborneState>();
            return;
        }

        // --- Jump while carrying ---
        if (player.input.JumpPressed)
        {
            player.velocity.y = player.jumpForce * 0.8f; // reduced jump when carrying
            player.jumpCount = 1;
            player.anim.TriggerJump();
            player.stateMachine.SetState<AirborneState>();
            return;
        }

        // --- Movement (slower while carrying) ---
        Vector3 moveDir = GetCameraRelativeDirection(player.input.MoveInput);
        player.velocity.x = moveDir.x * player.carrySpeed;
        player.velocity.z = moveDir.z * player.carrySpeed;

        RotateToward(moveDir, player.rotationSpeed * 0.7f);
        ApplyGravity();

        player.anim.SetSpeed(player.input.MoveInput.magnitude * player.carryAnimMultiplier);
    }

    public override void Exit()
    {
        player.anim.SetCarrying(false);
    }

    private void DropObject()
    {
        if (player.carriedObject == null) return;

        var pickup = player.carriedObject.GetComponent<PickupObject>();
        if (pickup != null)
        {
            Vector3 dropPos = player.transform.position + player.transform.forward * 2f;
            pickup.Drop(dropPos);
        }

        player.carriedObject = null;
    }

    private void ThrowObject()
    {
        if (player.carriedObject == null) return;

        var pickup = player.carriedObject.GetComponent<PickupObject>();
        if (pickup != null)
        {
            Vector3 throwDir = Quaternion.Euler(-player.throwUpAngle, 0f, 0f)
                * player.transform.forward;
            pickup.Throw(throwDir * player.throwForce);
            player.anim.TriggerThrow();
        }

        player.carriedObject = null;
    }
}
