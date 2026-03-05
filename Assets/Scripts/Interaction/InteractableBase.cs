using UnityEngine;

/// <summary>
/// Abstract base MonoBehaviour for all interactable world objects.
/// Extend this for PickupObject, PushableObject, ClimbSurface, Lever, etc.
///
/// Default visual feedback: If a QuickOutline component exists on the object
/// (or its children), it will automatically enable/disable when targeted.
/// Subclasses like DialogueNPC can override OnTargeted/OnUntargeted for custom behavior.
/// </summary>
public abstract class InteractableBase : MonoBehaviour, IInteractable
{
    [SerializeField] protected string prompt = "Interact";

    // Cached outline reference for default targeting feedback (lazy lookup)
    private QuickOutline cachedOutline;
    private bool outlineLookedUp;

    public string InteractionPrompt => prompt;

    public virtual bool CanInteract(PlayerController player) => true;

    public abstract void Interact(PlayerController player);

    /// <summary>Called when this becomes the player's current target.</summary>
    public virtual void OnTargeted()
    {
        // Lazy lookup: only search once, then cache
        if (!outlineLookedUp)
        {
            cachedOutline = GetComponentInChildren<QuickOutline>(true);
            outlineLookedUp = true;
        }

        if (cachedOutline != null)
            cachedOutline.enabled = true;
    }

    /// <summary>Called when no longer the player's current target.</summary>
    public virtual void OnUntargeted()
    {
        if (cachedOutline != null)
            cachedOutline.enabled = false;
    }
}
