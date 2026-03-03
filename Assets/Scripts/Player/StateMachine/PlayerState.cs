using UnityEngine;

/// <summary>
/// Abstract base class for all player states. Provides shared helpers
/// for camera-relative movement, rotation, and gravity.
/// </summary>
public abstract class PlayerState
{
    protected PlayerController player;

    public PlayerState(PlayerController player)
    {
        this.player = player;
    }

    public virtual void Enter() { }
    public virtual void Update() { }
    public virtual void FixedUpdate() { }
    public virtual void Exit() { }

    /// <summary>
    /// Compute camera-relative movement direction from 2D input.
    /// </summary>
    protected Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.001f) return Vector3.zero;

        Vector3 camForward = player.cameraTransform.forward;
        Vector3 camRight = player.cameraTransform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        return (camForward * input.y + camRight * input.x).normalized;
    }

    /// <summary>
    /// Smoothly rotate the player toward a movement direction.
    /// </summary>
    protected void RotateToward(Vector3 direction, float speed)
    {
        if (direction.sqrMagnitude < 0.001f) return;
        Quaternion target = Quaternion.LookRotation(direction);
        player.transform.rotation = Quaternion.Slerp(
            player.transform.rotation, target, speed * Time.deltaTime);
    }

    /// <summary>
    /// Apply gravity to player velocity. Uses fallMultiplier for heavier descent feel.
    /// </summary>
    protected void ApplyGravity()
    {
        if (player.isGrounded && player.velocity.y < 0f)
        {
            player.velocity.y = -2f;
            return;
        }

        float grav = player.gravity;
        if (player.velocity.y < 0f)
            grav *= player.fallMultiplier;

        player.velocity.y += grav * Time.deltaTime;
    }
}
