using System;
using UnityEngine;

/// <summary>
/// Main player controller. Owns the CharacterController, manages the state machine,
/// and holds shared data accessible by all states. Replaces ThirdPersonMove.cs.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInputHandler))]
public class PlayerController : MonoBehaviour
{
    // ─── Movement Speeds ──────────────────────────────────────
    [Header("Movement")]
    public float walkSpeed = 8f;
    public float sprintSpeed = 14f;
    public float sneakSpeed = 3f;
    public float crouchSpeed = 4f;
    public float carrySpeed = 5f;
    public float pushPullSpeed = 3f;
    public float rotationSpeed = 12f;

    // ─── Jump & Gravity ───────────────────────────────────────
    [Header("Jump & Gravity")]
    public float jumpForce = 12f;
    public float doubleJumpForce = 10f;
    public float gravity = -20f;
    public float fallMultiplier = 2.5f;
    public float coyoteTimeDuration = 0.12f;
    public float jumpBufferDuration = 0.1f;

    // ─── Crouch ───────────────────────────────────────────────
    [Header("Crouch")]
    public float crouchHeight = 3.5f;
    public float standHeight = 6f;

    // ─── Interaction ──────────────────────────────────────────
    [Header("Interaction")]
    public float interactRange = 4f;
    public Transform carryPoint;
    public float throwForce = 15f;
    public float throwUpAngle = 30f;

    // ─── Animation Tuning ────────────────────────────────────
    [Header("Animation Tuning")]
    [Tooltip("Multiplier for walk animation speed sent to BlendTree (match your BlendTree walk threshold)")]
    [Range(0.05f, 1f)] public float walkAnimMultiplier = 0.25f;

    [Tooltip("Multiplier for sprint animation speed sent to BlendTree (match your BlendTree run threshold)")]
    [Range(0.1f, 2f)] public float sprintAnimMultiplier = 1.0f;

    [Tooltip("Multiplier for crouch/sneak animation speed")]
    [Range(0.05f, 0.5f)] public float crouchAnimMultiplier = 0.15f;

    [Tooltip("Multiplier for carry animation speed")]
    [Range(0.05f, 0.5f)] public float carryAnimMultiplier = 0.2f;

    [Tooltip("Multiplier for push/pull animation speed")]
    [Range(0.05f, 0.5f)] public float pushAnimMultiplier = 0.15f;

    [Tooltip("Multiplier for climb animation speed")]
    [Range(0.05f, 0.5f)] public float climbAnimMultiplier = 0.2f;

    // ─── References ───────────────────────────────────────────
    [Header("References")]
    public Transform cameraTransform;

    // ─── Runtime references (auto-resolved) ───────────────────
    [HideInInspector] public CharacterController controller;
    [HideInInspector] public PlayerInputHandler input;
    [HideInInspector] public PlayerAnimationHandler anim;
    [HideInInspector] public InteractionDetector interactionDetector;
    [HideInInspector] public PlayerStateMachine stateMachine;

    // ─── Shared state (writable by states) ────────────────────
    [HideInInspector] public Vector3 velocity;
    [HideInInspector] public bool isGrounded;
    [HideInInspector] public int jumpCount;
    [HideInInspector] public bool isCrouching;
    [HideInInspector] public GameObject carriedObject;
    [HideInInspector] public GameObject interactionTarget;
    [HideInInspector] public float coyoteTimeCounter;
    [HideInInspector] public float jumpBufferCounter;

    // ─── Events for UI team ───────────────────────────────────
    public event Action<string> OnStateChanged;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        input = GetComponent<PlayerInputHandler>();
        anim = GetComponentInChildren<PlayerAnimationHandler>();
        interactionDetector = GetComponentInChildren<InteractionDetector>();

        if (anim == null)
        {
            Debug.LogWarning("PlayerController: No PlayerAnimationHandler found. " +
                "Add it to the child object with the Animator. Adding one at runtime.");
            anim = gameObject.AddComponent<PlayerAnimationHandler>();
        }

        // Initialize state machine with all states
        stateMachine = new PlayerStateMachine();
        stateMachine.RegisterState(new GroundedState(this));
        stateMachine.RegisterState(new AirborneState(this));
        stateMachine.RegisterState(new CrouchState(this));
        stateMachine.RegisterState(new CarryState(this));
        stateMachine.RegisterState(new PushPullState(this));
        stateMachine.RegisterState(new ClimbState(this));

        stateMachine.SetState<GroundedState>();
    }

    void Update()
    {
        isGrounded = controller.isGrounded;

        // Tick down timers
        if (coyoteTimeCounter > 0f)
            coyoteTimeCounter -= Time.deltaTime;
        if (jumpBufferCounter > 0f)
            jumpBufferCounter -= Time.deltaTime;

        // Buffer jump input
        if (input.JumpPressed)
            jumpBufferCounter = jumpBufferDuration;

        // Update current state
        stateMachine.Update();

        // Apply final velocity through CharacterController
        controller.Move(velocity * Time.deltaTime);
    }

    void FixedUpdate()
    {
        stateMachine.FixedUpdate();
    }

    public void NotifyStateChanged(string stateName)
    {
        OnStateChanged?.Invoke(stateName);
    }

    /// <summary>
    /// Check if there's enough clearance above to stand up from crouch.
    /// </summary>
    public bool CanStandUp()
    {
        float checkDistance = standHeight - crouchHeight + 0.1f;
        Vector3 origin = transform.position + Vector3.up * crouchHeight;
        return !Physics.Raycast(origin, Vector3.up, checkDistance);
    }
}
