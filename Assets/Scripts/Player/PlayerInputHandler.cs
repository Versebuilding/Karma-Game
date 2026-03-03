using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Wraps the New Input System and exposes clean, polled input state
/// for use by the player state machine.
/// </summary>
public class PlayerInputHandler : MonoBehaviour
{
    [Header("Input Actions Asset")]
    [SerializeField] private InputActionAsset inputActions;

    // Cached action references
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    private InputAction crouchAction;
    private InputAction interactAction;
    private InputAction attackAction;

    // --- Polled input state (read by player states each frame) ---

    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool SprintHeld { get; private set; }
    public bool CrouchPressed { get; private set; }
    public bool InteractPressed { get; private set; }
    public bool InteractHeld { get; private set; }
    public bool AttackPressed { get; private set; }

    // One-frame flags
    private bool jumpPressedThisFrame;
    private bool crouchPressedThisFrame;
    private bool interactPressedThisFrame;
    private bool attackPressedThisFrame;

    void Awake()
    {
        if (inputActions == null)
        {
            Debug.LogWarning("PlayerInputHandler: No InputActionAsset assigned. " +
                "Drag InputSystem_Actions into the Inspector.");
            return;
        }

        var playerMap = inputActions.FindActionMap("Player", true);

        moveAction = playerMap.FindAction("Move", true);
        lookAction = playerMap.FindAction("Look", true);
        jumpAction = playerMap.FindAction("Jump", true);
        sprintAction = playerMap.FindAction("Sprint", true);
        crouchAction = playerMap.FindAction("Crouch", true);
        interactAction = playerMap.FindAction("Interact", true);
        attackAction = playerMap.FindAction("Attack", true);

        // Subscribe to one-shot button events
        jumpAction.performed += _ => jumpPressedThisFrame = true;
        crouchAction.performed += _ => crouchPressedThisFrame = true;
        interactAction.started += _ => interactPressedThisFrame = true;
        attackAction.performed += _ => attackPressedThisFrame = true;
    }

    void OnEnable()
    {
        inputActions?.Enable();
    }

    void OnDisable()
    {
        inputActions?.Disable();
    }

    void Update()
    {
        if (inputActions == null) return;

        // Continuous inputs
        MoveInput = moveAction.ReadValue<Vector2>();
        LookInput = lookAction.ReadValue<Vector2>();
        SprintHeld = sprintAction.IsPressed();
        InteractHeld = interactAction.IsPressed();

        // One-frame press flags
        JumpPressed = jumpPressedThisFrame;
        CrouchPressed = crouchPressedThisFrame;
        InteractPressed = interactPressedThisFrame;
        AttackPressed = attackPressedThisFrame;

        // Reset for next frame
        jumpPressedThisFrame = false;
        crouchPressedThisFrame = false;
        interactPressedThisFrame = false;
        attackPressedThisFrame = false;
    }
}
