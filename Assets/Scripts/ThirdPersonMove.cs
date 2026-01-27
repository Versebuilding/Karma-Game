using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonMove : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 4f;
    public float rotationSpeed = 12f;

    [Header("Gravity")]
    public float gravity = -20f;

    [Header("References")]
    public Transform cameraTransform;   // drag Main Camera here
    public Animator animator;           // drag Animator here

    private CharacterController controller;
    private Vector3 verticalVelocity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (!animator) animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        // --- Input (WASD / arrows) ---
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        Vector3 input = new Vector3(x, 0f, z).normalized;

        // --- Camera-relative move direction ---
        Vector3 moveDir = Vector3.zero;
        if (input.sqrMagnitude > 0.001f)
        {
            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            moveDir = camForward * input.z + camRight * input.x;
            moveDir.Normalize();

            // --- Rotate towards movement ---
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                rotationSpeed * Time.deltaTime
            );
        }

        // --- Gravity ---
        if (controller.isGrounded && verticalVelocity.y < 0f)
            verticalVelocity.y = -2f; // keeps grounded

        verticalVelocity.y += gravity * Time.deltaTime;

        // --- Move ---
        Vector3 velocity = moveDir * moveSpeed + verticalVelocity;
        controller.Move(velocity * Time.deltaTime);

        // --- Animation ---
        float speed01 = Mathf.Clamp01(moveDir.magnitude); // 0 idle, 1 walk
        animator.SetFloat("Speed", speed01, 0.1f, Time.deltaTime); // damped
    }
}
