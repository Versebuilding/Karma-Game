using UnityEngine;

/// <summary>
/// Objects that can be pushed and pulled by the player.
/// Requires Rigidbody + Collider on the GameObject.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PushableObject : InteractableBase
{
    [Header("Push/Pull Settings")]
    public float friction = 0.5f;
    public bool canPull = true;

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        prompt = "Push";

        // Constrain to prevent tipping over
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    public override void Interact(PlayerController player)
    {
        StartInteraction(player);
    }

    public void StartInteraction(PlayerController player)
    {
        player.interactionTarget = gameObject;
        player.stateMachine.SetState<PushPullState>();
    }

    public void ApplyMovement(Vector3 delta)
    {
        rb.MovePosition(rb.position + delta);
    }

    public void StopInteraction()
    {
        // Object stays where it is
    }
}
