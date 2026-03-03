using UnityEngine;

/// <summary>
/// Push/pull state: player is locked to an object, forward = push, back = pull.
/// Transitions to: GroundedState (release with E)
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

        // Forward input = push, backward input = pull
        float pushInput = player.input.MoveInput.y;
        Vector3 pushDir = player.transform.forward * pushInput;

        // Move player
        player.velocity.x = pushDir.x * player.pushPullSpeed;
        player.velocity.z = pushDir.z * player.pushPullSpeed;

        // Move object in sync
        target.ApplyMovement(pushDir * player.pushPullSpeed * Time.deltaTime);

        ApplyGravity();
        player.anim.SetSpeed(Mathf.Abs(pushInput) * player.pushAnimMultiplier);
    }

    public override void Exit()
    {
        player.anim.SetPushing(false);
        player.interactionTarget = null;
    }
}
