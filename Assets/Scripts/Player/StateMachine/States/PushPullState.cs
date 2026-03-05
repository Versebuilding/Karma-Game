using UnityEngine;

/// <summary>
/// Push/pull state: player is locked to an object, forward = push, back = pull.
/// Transitions to: GroundedState (release with E), AirborneState (fall off edge)
///
/// Elegance features:
///   - Aligns player to nearest face of the object on enter
///   - Respects PushableObject.canPull (blocks backward input when false)
///   - Friction-based speed scaling (higher friction = slower push)
///   - Push loop audio (plays while moving, stops when idle)
///   - Fall-off-edge detection
/// </summary>
public class PushPullState : PlayerState
{
    private PushableObject target;

    public PushPullState(PlayerController player) : base(player) { }

    public override void Enter()
    {
        target = player.interactionTarget != null
            ? player.interactionTarget.GetComponent<PushableObject>()
            : null;

        // Align player to the nearest face of the pushable object
        if (target != null)
            AlignToNearestFace();

        player.anim.SetPushing(true);
        player.NotifyStateChanged("Pushing");
    }

    public override void Update()
    {
        // --- Release ---
        if (player.input.InteractPressed || target == null)
        {
            if (target != null) target.StopInteraction();
            player.stateMachine.SetState<GroundedState>();
            return;
        }

        // --- Fall off edge ---
        if (!player.isGrounded)
        {
            target.StopInteraction();
            player.stateMachine.SetState<AirborneState>();
            return;
        }

        float pushInput = player.input.MoveInput.y;

        // Block pull if object doesn't allow it
        if (pushInput < 0f && !target.canPull)
            pushInput = 0f;

        // Apply friction: higher friction = slower push speed
        // friction=0 → 1x speed, friction=0.5 → 0.67x, friction=1 → 0.5x
        float frictionMultiplier = 1f / (1f + target.friction);
        float effectiveSpeed = player.pushPullSpeed * frictionMultiplier;

        Vector3 pushDir = player.transform.forward * pushInput;

        // Move player
        player.velocity.x = pushDir.x * effectiveSpeed;
        player.velocity.z = pushDir.z * effectiveSpeed;

        // Move object in sync
        target.ApplyMovement(pushDir * effectiveSpeed * Time.deltaTime);

        // Audio: play push loop when actually moving
        if (Mathf.Abs(pushInput) > 0.1f)
            target.PlayPushLoop();
        else
            target.StopPushLoop();

        ApplyGravity();
        player.anim.SetSpeed(Mathf.Abs(pushInput) * player.pushAnimMultiplier);
    }

    public override void Exit()
    {
        if (target != null) target.StopPushLoop();
        player.anim.SetPushing(false);
        player.interactionTarget = null;
    }

    // ─── Face Alignment ──────────────────────────────────────────

    /// <summary>
    /// Snap the player to the nearest face center of the pushable object's collider.
    /// Ensures axis-aligned push/pull for clean movement and animation.
    /// </summary>
    private void AlignToNearestFace()
    {
        if (target == null) return;

        var col = target.GetComponent<Collider>();
        if (col == null) return;

        Bounds bounds = col.bounds;
        Vector3 playerPos = player.transform.position;
        Vector3 objCenter = bounds.center;
        Vector3 delta = playerPos - objCenter;

        // Determine which face (±X or ±Z) the player is closest to
        float absX = Mathf.Abs(delta.x) / Mathf.Max(bounds.extents.x, 0.01f);
        float absZ = Mathf.Abs(delta.z) / Mathf.Max(bounds.extents.z, 0.01f);

        Vector3 faceNormal;
        Vector3 faceCenter;

        if (absX > absZ)
        {
            float sign = Mathf.Sign(delta.x);
            faceNormal = new Vector3(sign, 0f, 0f);
            faceCenter = objCenter + new Vector3(sign * bounds.extents.x, 0f, 0f);
        }
        else
        {
            float sign = Mathf.Sign(delta.z);
            faceNormal = new Vector3(0f, 0f, sign);
            faceCenter = objCenter + new Vector3(0f, 0f, sign * bounds.extents.z);
        }

        // Position player just outside the face, keeping Y unchanged (feet on ground)
        Vector3 alignPos = faceCenter + faceNormal * 0.6f;
        alignPos.y = playerPos.y;

        player.transform.position = alignPos;

        // Rotate player to face the object (look inward toward face)
        player.transform.rotation = Quaternion.LookRotation(-faceNormal);
    }
}
