using UnityEngine;

/// <summary>
/// Centralized interface for all player animation parameter management.
/// Attach to the child object with the Animator, or the root (auto-finds Animator).
/// </summary>
public class PlayerAnimationHandler : MonoBehaviour
{
    private Animator animator;

    // Cached parameter hashes
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int VerticalVelocityHash = Animator.StringToHash("VerticalVelocity");
    private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
    private static readonly int IsCarryingHash = Animator.StringToHash("IsCarrying");
    private static readonly int IsClimbingHash = Animator.StringToHash("IsClimbing");
    private static readonly int IsPushingHash = Animator.StringToHash("IsPushing");
    private static readonly int IsSprintingHash = Animator.StringToHash("IsSprinting");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int DoubleJumpHash = Animator.StringToHash("DoubleJump");
    private static readonly int LandHash = Animator.StringToHash("Land");
    private static readonly int ThrowHash = Animator.StringToHash("Throw");
    private static readonly int StumbleHash = Animator.StringToHash("Stumble");

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    // --- Continuous parameters ---

    public void SetSpeed(float speed)
    {
        if (animator != null)
            animator.SetFloat(SpeedHash, speed, 0.1f, Time.deltaTime);
    }

    public void SetGrounded(bool grounded)
    {
        SetBoolSafe(IsGroundedHash, grounded);
    }

    public void SetVerticalVelocity(float vel)
    {
        SetFloatSafe(VerticalVelocityHash, vel);
    }

    public void SetCrouching(bool crouching)
    {
        SetBoolSafe(IsCrouchingHash, crouching);
    }

    public void SetCarrying(bool carrying)
    {
        SetBoolSafe(IsCarryingHash, carrying);
    }

    public void SetClimbing(bool climbing)
    {
        SetBoolSafe(IsClimbingHash, climbing);
    }

    public void SetPushing(bool pushing)
    {
        SetBoolSafe(IsPushingHash, pushing);
    }

    public void SetSprinting(bool sprinting)
    {
        SetBoolSafe(IsSprintingHash, sprinting);
    }

    // --- One-shot triggers ---

    public void TriggerJump()
    {
        SetTriggerSafe(JumpHash);
    }

    public void TriggerDoubleJump()
    {
        SetTriggerSafe(DoubleJumpHash);
    }

    public void TriggerLand()
    {
        SetTriggerSafe(LandHash);
    }

    public void TriggerThrow()
    {
        SetTriggerSafe(ThrowHash);
    }

    public void TriggerStumble()
    {
        SetTriggerSafe(StumbleHash);
    }

    // --- Safe setters (don't error if parameter doesn't exist yet in animator) ---

    private void SetBoolSafe(int hash, bool value)
    {
        if (animator != null)
        {
            try { animator.SetBool(hash, value); }
            catch { } // Parameter may not exist yet in the animator controller
        }
    }

    private void SetFloatSafe(int hash, float value)
    {
        if (animator != null)
        {
            try { animator.SetFloat(hash, value); }
            catch { }
        }
    }

    private void SetTriggerSafe(int hash)
    {
        if (animator != null)
        {
            try { animator.SetTrigger(hash); }
            catch { }
        }
    }
}
