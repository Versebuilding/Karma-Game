using System;
using UnityEngine;

/// <summary>
/// Interface for all dialogue actions. Implement this to create new action
/// types that execute when a dialogue node is shown or a choice is selected.
///
/// How to add a new action:
///   1. Create a new [Serializable] class implementing IDialogueAction
///   2. Add your fields ([SerializeField] for Inspector editing)
///   3. Implement Label (for editor display) and Execute()
///   4. Done — it auto-appears in the Dialogue Editor dropdown via reflection
///
/// Example:
///   [Serializable]
///   public class GiveItemAction : IDialogueAction
///   {
///       [SerializeField] ItemSO item;
///       [SerializeField] int quantity = 1;
///       public string Label => $"Give: {item?.itemName} x{quantity}";
///       public void Execute() => InventoryManager.Instance.AddItem(item, quantity);
///   }
/// </summary>
public interface IDialogueAction
{
    /// <summary>Short label shown in the editor (e.g., "+50 Karma").</summary>
    string Label { get; }

    /// <summary>Execute this action (called at runtime when the choice/node fires).</summary>
    void Execute();
}

// ═══════════════════════════════════════════════════════════════════
//  BUILT-IN ACTIONS
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Modifies the player's karma score.
/// Replaces the old hardcoded DialogueChoice.karmaChange field.
/// </summary>
[Serializable]
public class ModifyKarmaAction : IDialogueAction
{
    [Tooltip("Karma amount to add (+positive) or subtract (-negative)")]
    public int amount;

    public string Label => amount >= 0 ? $"+{amount} Karma" : $"{amount} Karma";

    public void Execute()
    {
        if (KarmaManager.Instance != null && amount != 0)
            KarmaManager.Instance.AddKarma(amount);
    }
}

/// <summary>
/// Modifies the player's coin balance.
/// Replaces the old hardcoded DialogueChoice.coinChange field.
/// </summary>
[Serializable]
public class ModifyCoinsAction : IDialogueAction
{
    [Tooltip("Coins to add (+positive) or subtract (-negative)")]
    public int amount;

    public string Label => amount >= 0 ? $"+{amount} Coins" : $"{amount} Coins";

    public void Execute()
    {
        if (WalletManager.Instance != null && amount != 0)
            WalletManager.Instance.AddCoins(amount);
    }
}

/// <summary>
/// Sets a boolean flag in the VariableStore.
/// Example: Set "hasMetAnanda" = true after first conversation with Ananda.
/// </summary>
[Serializable]
public class SetFlagAction : IDialogueAction
{
    [Tooltip("Name of the flag to set")]
    public string flagName;

    [Tooltip("Value to set (true = set, false = clear)")]
    public bool value = true;

    public string Label
    {
        get
        {
            if (string.IsNullOrEmpty(flagName)) return "Set Flag: (none)";
            return value ? $"Set: {flagName}" : $"Clear: {flagName}";
        }
    }

    public void Execute()
    {
        if (VariableStore.Instance != null && !string.IsNullOrEmpty(flagName))
            VariableStore.Instance.SetFlag(flagName, value);
    }
}

/// <summary>
/// Modifies a numeric counter in the VariableStore.
/// Example: Increment "ghostsHelped" by 1 when player helps a ghost.
/// </summary>
[Serializable]
public class ModifyCounterAction : IDialogueAction
{
    [Tooltip("Name of the counter to modify")]
    public string counterName;

    [Tooltip("Amount to add (+positive) or subtract (-negative)")]
    public int amount = 1;

    public string Label
    {
        get
        {
            if (string.IsNullOrEmpty(counterName)) return "Counter: (none)";
            string prefix = amount >= 0 ? "+" : "";
            return $"{counterName} {prefix}{amount}";
        }
    }

    public void Execute()
    {
        if (VariableStore.Instance != null && !string.IsNullOrEmpty(counterName) && amount != 0)
            VariableStore.Instance.ModifyCounter(counterName, amount);
    }
}

// ═══════════════════════════════════════════════════════════════════
//  QUEST ACTIONS
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Starts a quest from a dialogue choice or node.
/// Example: Player agrees to help Serna → StartQuestAction fires.
/// </summary>
[Serializable]
public class StartQuestAction : IDialogueAction
{
    [Tooltip("Quest ID to start (must match QuestSO.questId)")]
    public string questId;

    public string Label => string.IsNullOrEmpty(questId) ? "Start Quest: (none)" : $"Start Quest: {questId}";

    public void Execute()
    {
        if (QuestManager.Instance != null && !string.IsNullOrEmpty(questId))
            QuestManager.Instance.StartQuest(questId);
    }
}

/// <summary>
/// Advances a quest objective from a dialogue choice or node.
/// Example: "Talk to Serna" objective completes when player reaches a specific dialogue node.
/// </summary>
[Serializable]
public class AdvanceQuestAction : IDialogueAction
{
    [Tooltip("Quest ID containing the objective")]
    public string questId;

    [Tooltip("Objective ID to advance")]
    public string objectiveId;

    [Tooltip("Amount to advance (default 1)")]
    public int amount = 1;

    public string Label
    {
        get
        {
            if (string.IsNullOrEmpty(questId)) return "Advance Quest: (none)";
            return $"Quest {questId}: {objectiveId} +{amount}";
        }
    }

    public void Execute()
    {
        if (QuestManager.Instance != null && !string.IsNullOrEmpty(questId) && !string.IsNullOrEmpty(objectiveId))
            QuestManager.Instance.AdvanceObjective(questId, objectiveId, amount);
    }
}

/// <summary>
/// Force-completes a quest from a dialogue choice or node.
/// Useful for quests that end through dialogue rather than objective tracking.
/// </summary>
[Serializable]
public class CompleteQuestAction : IDialogueAction
{
    [Tooltip("Quest ID to complete")]
    public string questId;

    public string Label => string.IsNullOrEmpty(questId) ? "Complete Quest: (none)" : $"Complete Quest: {questId}";

    public void Execute()
    {
        if (QuestManager.Instance != null && !string.IsNullOrEmpty(questId))
            QuestManager.Instance.CompleteQuest(questId);
    }
}
