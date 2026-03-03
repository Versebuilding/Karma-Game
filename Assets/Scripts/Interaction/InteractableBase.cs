using UnityEngine;

/// <summary>
/// Abstract base MonoBehaviour for all interactable world objects.
/// Extend this for PickupObject, PushableObject, ClimbSurface, Lever, etc.
/// </summary>
public abstract class InteractableBase : MonoBehaviour, IInteractable
{
    [SerializeField] protected string prompt = "Interact";

    public string InteractionPrompt => prompt;

    public virtual bool CanInteract(PlayerController player) => true;

    public abstract void Interact(PlayerController player);

    /// <summary>Called when this becomes the player's current target.</summary>
    public virtual void OnTargeted() { }

    /// <summary>Called when no longer the player's current target.</summary>
    public virtual void OnUntargeted() { }
}
