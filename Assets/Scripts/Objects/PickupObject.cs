using UnityEngine;

/// <summary>
/// Objects that can be picked up, carried, thrown, dropped, and stacked.
/// Requires Rigidbody + Collider on the GameObject.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PickupObject : InteractableBase
{
    [Header("Pickup Settings")]
    public float weight = 1f;
    public bool isStackable = false;
    public Vector3 stackOffset = Vector3.up;

    private Rigidbody rb;
    private Collider col;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        prompt = "Pick Up";
    }

    public override bool CanInteract(PlayerController player)
    {
        // Can't pick up if already carrying something
        return player.carriedObject == null;
    }

    public override void Interact(PlayerController player)
    {
        PickUp(player);
    }

    public void PickUp(PlayerController player)
    {
        if (player.carryPoint == null)
        {
            Debug.LogWarning("PickupObject: No carryPoint assigned on PlayerController.");
            return;
        }

        // Disable physics and parent to carry point
        rb.isKinematic = true;
        col.enabled = false;
        transform.SetParent(player.carryPoint);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        player.carriedObject = gameObject;
        player.stateMachine.SetState<CarryState>();
    }

    public void Drop(Vector3 position)
    {
        transform.SetParent(null);
        transform.position = position;
        rb.isKinematic = false;
        col.enabled = true;
    }

    public void Throw(Vector3 force)
    {
        transform.SetParent(null);
        rb.isKinematic = false;
        col.enabled = true;
        rb.AddForce(force, ForceMode.Impulse);
    }

    /// <summary>
    /// Stack this object on top of another PickupObject.
    /// </summary>
    public void StackOn(PickupObject other)
    {
        Drop(other.transform.position + stackOffset);
    }
}
