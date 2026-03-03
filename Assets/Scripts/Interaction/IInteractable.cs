/// <summary>
/// Interface for all objects the player can interact with.
/// </summary>
public interface IInteractable
{
    string InteractionPrompt { get; }
    bool CanInteract(PlayerController player);
    void Interact(PlayerController player);
}
