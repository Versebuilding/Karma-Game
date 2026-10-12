using UnityEngine;

/// <summary>
/// World-space hum statue. Walk near it, "Press E to Hum" appears (via
/// the standard InteractionDetector flow), E opens the hum statue screen
/// and runs a HumSessionController session.
/// </summary>
public class HumStatue : InteractableBase
{
    private void Reset()
    {
        prompt = "Hum";
    }

    public override bool CanInteract(PlayerController player)
    {
        return HumSessionController.Instance != null
            && !HumSessionController.Instance.SessionActive;
    }

    public override void Interact(PlayerController player)
    {
        if (HumSessionController.Instance == null)
        {
            Debug.LogWarning("HumStatue: No HumSessionController in scene. Run Karma > Build Hum Statue UI.");
            return;
        }
        HumSessionController.Instance.BeginSession(player);
    }
}
