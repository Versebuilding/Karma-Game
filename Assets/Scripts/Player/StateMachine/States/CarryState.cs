using UnityEngine;

/// <summary>
/// Handles carrying objects: slower movement, drop (E), throw (left click), stack.
/// Transitions to: GroundedState (drop/throw), AirborneState (fall off edge)
///
/// Elegance features:
///   - Safe drop: SphereCast to avoid dropping through walls
///   - Weight-affected throw: heavier objects travel shorter (force / weight)
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

    // ─── Drop (with obstacle avoidance) ─────────────────────────

    private void DropObject()
    {
        if (player.carriedObject == null) return;

        var pickup = player.carriedObject.GetComponent<PickupObject>();
        if (pickup != null)
        {
            Vector3 dropPos = FindSafeDropPosition();
            pickup.Drop(dropPos);
        }

        player.carriedObject = null;
    }

    /// <summary>
    /// Find a safe position to drop the carried object.
    /// Tries forward first, then sides, then behind, then feet.
    /// </summary>
    private Vector3 FindSafeDropPosition()
    {
        float dropDistance = 2f;
        float checkRadius = 0.3f;
        Vector3 origin = player.carryPoint.position;

        // Try forward, right, left, behind
        Vector3[] directions = {
            player.transform.forward,
            player.transform.right,
            -player.transform.right,
            -player.transform.forward
        };

        foreach (var dir in directions)
        {
            if (!Physics.SphereCast(origin, checkRadius, dir, out _, dropDistance))
                return origin + dir * dropDistance;
        }

        // Fallback: drop at player's feet
        return player.transform.position + Vector3.up * 0.5f;
    }

    // ─── Throw (weight-affected) ────────────────────────────────

    private void ThrowObject()
    {
        if (player.carriedObject == null) return;

        var pickup = player.carriedObject.GetComponent<PickupObject>();
        if (pickup != null)
        {
            Vector3 throwDir = Quaternion.Euler(-player.throwUpAngle, 0f, 0f)
                * player.transform.forward;

            // Weight affects throw distance: heavier = less force
            float effectiveForce = player.throwForce / Mathf.Max(pickup.weight, 0.1f);
            pickup.Throw(throwDir * effectiveForce);
            player.anim.TriggerThrow();
        }

        player.carriedObject = null;
    }
}
