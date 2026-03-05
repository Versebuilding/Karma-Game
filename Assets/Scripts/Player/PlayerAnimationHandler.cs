using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralized interface for all player animation parameter management.
/// Attach to the child object with the Animator, or the root (auto-finds Animator).
///
/// Optimization: Caches valid parameter hashes in a HashSet during Awake().
/// This replaces try-catch on every SetBool/SetFloat/SetTrigger call (~10+/frame)
/// with a zero-allocation O(1) HashSet lookup.
/// </summary>
public class PlayerAnimationHandler : MonoBehaviour
{
    private Animator animator;
    private HashSet<int> validParams;

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

        // Cache all valid parameter hashes once at startup
        validParams = new HashSet<int>();
        if (animator != null)
        {
            foreach (var param in animator.parameters)
                validParams.Add(param.nameHash);
        }
    }

    // --- Continuous parameters ---

    public void SetSpeed(float speed)
    {
        if (animator != null && validParams.Contains(SpeedHash))
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

    // --- Safe setters (check cached parameter existence, no try-catch) ---

    private void SetBoolSafe(int hash, bool value)
    {
        if (animator != null && validParams.Contains(hash))
            animator.SetBool(hash, value);
    }

    private void SetFloatSafe(int hash, float value)
    {
        if (animator != null && validParams.Contains(hash))
            animator.SetFloat(hash, value);
    }

    private void SetTriggerSafe(int hash)
    {
        if (animator != null && validParams.Contains(hash))
            animator.SetTrigger(hash);
    }
}
