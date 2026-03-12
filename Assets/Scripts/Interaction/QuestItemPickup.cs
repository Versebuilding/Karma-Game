using UnityEngine;

/// <summary>
/// Interactable pickup that advances a quest objective when collected.
/// Used for "Gather" type quest objectives (e.g., "Collect 3 food items").
///
/// Extends InteractableBase so it integrates with the existing InteractionDetector
/// (player's trigger collider detects this, GroundedState calls Interact on E press).
///
/// Setup:
///   1. Add this component to a collectible GameObject
///   2. Add a Collider (so InteractionDetector can detect it)
///   3. Set the questId and objectiveId to match your QuestSO
///   4. Optionally assign an ItemSO for display info
///
/// Behavior on pickup:
///   - Advances the quest objective
///   - Plays a pickup effect (optional)
///   - Deactivates the GameObject (can be re-enabled for repeatable quests)
/// </summary>
public class QuestItemPickup : InteractableBase
{
    [Header("Quest Objective")]
    [Tooltip("Quest ID this item belongs to")]
    [SerializeField] private string questId;

    [Tooltip("Objective ID to advance when collected")]
    [SerializeField] private string objectiveId;

    [Tooltip("Amount to advance the objective (default 1)")]
    [SerializeField] private int amount = 1;

    [Header("Item Info")]
    [Tooltip("Item definition (optional — for display name and icon)")]
    [SerializeField] private ItemSO item;

    [Header("Pickup Behavior")]
    [Tooltip("If true, only collect when the quest is Active")]
    [SerializeField] private bool requireQuestActive = true;

    [Tooltip("Audio clip played on pickup")]
    [SerializeField] private AudioClip pickupSound;

    void Awake()
    {
        // Set interaction prompt from item name or default
        if (item != null && !string.IsNullOrEmpty(item.itemName))
            prompt = $"Pick up {item.itemName}";
        else if (string.IsNullOrEmpty(prompt) || prompt == "Interact")
            prompt = "Pick up";
    }

    public override bool CanInteract(PlayerController player)
    {
        if (requireQuestActive && QuestManager.Instance != null &&
            !QuestManager.Instance.IsQuestActive(questId))
            return false;

        return true;
    }

    public override void Interact(PlayerController player)
    {
        if (QuestManager.Instance == null)
        {
            Debug.LogWarning($"QuestItemPickup: QuestManager not found! Cannot advance {questId}.{objectiveId}");
            return;
        }

        // Advance the quest objective
        QuestManager.Instance.AdvanceObjective(questId, objectiveId, amount);

        // Play pickup sound
        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);

        // Award item karma if applicable
        if (item != null && item.karmaOnCollect != 0 && KarmaManager.Instance != null)
            KarmaManager.Instance.AddKarma(item.karmaOnCollect);

        // Award item coin value if applicable
        if (item != null && item.coinValue != 0 && WalletManager.Instance != null)
            WalletManager.Instance.AddCoins(item.coinValue);

        Debug.Log($"QuestItemPickup: Collected '{(item != null ? item.itemName : objectiveId)}' → {questId}.{objectiveId} +{amount}");

        // Deactivate the pickup
        gameObject.SetActive(false);
    }
}
