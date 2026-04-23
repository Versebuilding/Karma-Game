using UnityEngine;

/// <summary>
/// Trigger zone that advances a quest objective when the player enters.
/// Used for "GoTo" type quest objectives (e.g., "Go to the village square").
///
/// Setup:
///   1. Create an empty GameObject with a Collider (set isTrigger = true)
///   2. Add this component
///   3. Set the questId and objectiveId to match your QuestSO
///   4. Optionally enable singleUse to prevent re-triggering
///
/// Requires: Player must have a PlayerController component.
/// </summary>
[RequireComponent(typeof(Collider))]
public class QuestTriggerZone : MonoBehaviour
{
    [Header("Quest Objective")]
    [Tooltip("Quest ID this zone belongs to")]
    [SerializeField] private string questId;

    [Tooltip("Objective ID to advance when player enters")]
    [SerializeField] private string objectiveId;

    [Tooltip("Amount to advance the objective (default 1)")]
    [SerializeField] private int amount = 1;

    [Header("Settings")]
    [Tooltip("If true, only triggers once then disables")]
    [SerializeField] private bool singleUse = true;

    [Tooltip("If true, only the quest must be Active to trigger")]
    [SerializeField] private bool requireQuestActive = true;

    /// <summary>Quest ID this waypoint belongs to (for compass binding, etc.).</summary>
    public string QuestId => questId;

    /// <summary>Objective ID this waypoint advances (for compass binding, etc.).</summary>
    public string ObjectiveId => objectiveId;

    private bool hasTriggered;

    void OnTriggerEnter(Collider other)
    {
        if (singleUse && hasTriggered) return;

        // Only respond to player
        if (other.GetComponent<PlayerController>() == null &&
            other.GetComponentInParent<PlayerController>() == null)
            return;

        // Check if quest is active
        if (requireQuestActive && QuestManager.Instance != null &&
            !QuestManager.Instance.IsQuestActive(questId))
            return;

        if (QuestManager.Instance == null)
        {
            Debug.LogWarning($"QuestTriggerZone: QuestManager not found! Cannot advance {questId}.{objectiveId}");
            return;
        }

        hasTriggered = true;
        QuestManager.Instance.AdvanceObjective(questId, objectiveId, amount);
        Debug.Log($"QuestTriggerZone: Player entered zone → advanced {questId}.{objectiveId} +{amount}");
    }

    /// <summary>Reset the trigger so it can fire again (e.g., for repeatable quests).</summary>
    public void ResetTrigger()
    {
        hasTriggered = false;
    }

    void OnValidate()
    {
        // Ensure collider is a trigger
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
        }
    }
}
