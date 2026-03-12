using System;
using UnityEngine;

/// <summary>
/// Interface for all dialogue conditions. Implement this to create new condition
/// types that gate dialogue nodes or choices.
///
/// How to add a new condition:
///   1. Create a new [Serializable] class implementing IDialogueCondition
///   2. Add your fields ([SerializeField] for Inspector editing)
///   3. Implement Label (for editor display) and Evaluate()
///   4. Done — it auto-appears in the Dialogue Editor dropdown via reflection
///
/// Example:
///   [Serializable]
///   public class HasItemCondition : IDialogueCondition
///   {
///       [SerializeField] string itemId;
///       public string Label => $"Has Item: {itemId}";
///       public bool Evaluate() => InventoryManager.Instance.HasItem(itemId);
///   }
/// </summary>
public interface IDialogueCondition
{
    /// <summary>Short label shown in the editor (e.g., "Karma ≥ 3").</summary>
    string Label { get; }

    /// <summary>Evaluate this condition. Returns true if the condition is met.</summary>
    bool Evaluate();
}

// ═══════════════════════════════════════════════════════════════════
//  Comparison operator shared by multiple conditions
// ═══════════════════════════════════════════════════════════════════

/// <summary>Comparison operator for numeric conditions.</summary>
public enum ComparisonOp
{
    AtLeast,       // >=
    GreaterThan,   // >
    Equals,        // ==
    LessThan,      // <
    AtMost         // <=
}

// ═══════════════════════════════════════════════════════════════════
//  BUILT-IN CONDITIONS
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Checks the player's karma level against a threshold.
/// Example: "Only show this choice if karma level >= 3"
/// </summary>
[Serializable]
public class KarmaLevelCondition : IDialogueCondition
{
    [Tooltip("Comparison operator")]
    public ComparisonOp comparison = ComparisonOp.AtLeast;

    [Tooltip("Karma level to compare against")]
    [Range(0, 12)]
    public int level = 1;

    public string Label
    {
        get
        {
            string op = comparison switch
            {
                ComparisonOp.AtLeast => ">=",
                ComparisonOp.GreaterThan => ">",
                ComparisonOp.Equals => "==",
                ComparisonOp.LessThan => "<",
                ComparisonOp.AtMost => "<=",
                _ => "?"
            };
            return $"Karma Lv {op} {level}";
        }
    }

    public bool Evaluate()
    {
        if (KarmaManager.Instance == null) return true;
        int current = KarmaManager.Instance.CurrentLevel;
        return comparison switch
        {
            ComparisonOp.AtLeast => current >= level,
            ComparisonOp.GreaterThan => current > level,
            ComparisonOp.Equals => current == level,
            ComparisonOp.LessThan => current < level,
            ComparisonOp.AtMost => current <= level,
            _ => true
        };
    }
}

/// <summary>
/// Checks a boolean flag in the VariableStore.
/// Example: "Only show if player has met Ananda" (flag: hasMetAnanda = true)
/// </summary>
[Serializable]
public class FlagCondition : IDialogueCondition
{
    [Tooltip("Name of the flag to check")]
    public string flagName;

    [Tooltip("Expected value (true = flag must be set, false = flag must be unset)")]
    public bool expectedValue = true;

    public string Label
    {
        get
        {
            if (string.IsNullOrEmpty(flagName)) return "Flag: (none)";
            return expectedValue ? $"Flag: {flagName}" : $"Flag: !{flagName}";
        }
    }

    public bool Evaluate()
    {
        if (VariableStore.Instance == null) return true;
        return VariableStore.Instance.GetFlag(flagName) == expectedValue;
    }
}

/// <summary>
/// Checks a numeric counter in the VariableStore.
/// Example: "Only show if ghostsHelped >= 3"
/// </summary>
[Serializable]
public class CounterCondition : IDialogueCondition
{
    [Tooltip("Name of the counter to check")]
    public string counterName;

    [Tooltip("Comparison operator")]
    public ComparisonOp comparison = ComparisonOp.AtLeast;

    [Tooltip("Value to compare against")]
    public int value = 1;

    public string Label
    {
        get
        {
            if (string.IsNullOrEmpty(counterName)) return "Counter: (none)";
            string op = comparison switch
            {
                ComparisonOp.AtLeast => ">=",
                ComparisonOp.GreaterThan => ">",
                ComparisonOp.Equals => "==",
                ComparisonOp.LessThan => "<",
                ComparisonOp.AtMost => "<=",
                _ => "?"
            };
            return $"{counterName} {op} {value}";
        }
    }

    public bool Evaluate()
    {
        if (VariableStore.Instance == null) return true;
        int current = VariableStore.Instance.GetCounter(counterName);
        return comparison switch
        {
            ComparisonOp.AtLeast => current >= value,
            ComparisonOp.GreaterThan => current > value,
            ComparisonOp.Equals => current == value,
            ComparisonOp.LessThan => current < value,
            ComparisonOp.AtMost => current <= value,
            _ => true
        };
    }
}

// ═══════════════════════════════════════════════════════════════════
//  QUEST CONDITIONS
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Checks a quest's current state.
/// Example: "Only show this dialogue node if quest 'help_serna' is Active"
/// </summary>
[Serializable]
public class QuestStateCondition : IDialogueCondition
{
    [Tooltip("Quest ID to check")]
    public string questId;

    [Tooltip("Required quest state")]
    public QuestState requiredState = QuestState.Active;

    public string Label
    {
        get
        {
            if (string.IsNullOrEmpty(questId)) return "Quest: (none)";
            return $"Quest {questId} is {requiredState}";
        }
    }

    public bool Evaluate()
    {
        if (QuestManager.Instance == null) return true;
        return QuestManager.Instance.GetQuestState(questId) == requiredState;
    }
}

/// <summary>
/// Checks a quest objective's progress against a threshold.
/// Example: "Only show this choice if player has collected >= 2 food items for quest 'help_serna'"
/// </summary>
[Serializable]
public class QuestObjectiveCondition : IDialogueCondition
{
    [Tooltip("Quest ID containing the objective")]
    public string questId;

    [Tooltip("Objective ID to check")]
    public string objectiveId;

    [Tooltip("Comparison operator")]
    public ComparisonOp comparison = ComparisonOp.AtLeast;

    [Tooltip("Value to compare progress against")]
    public int value = 1;

    public string Label
    {
        get
        {
            if (string.IsNullOrEmpty(questId) || string.IsNullOrEmpty(objectiveId))
                return "Quest Objective: (none)";
            string op = comparison switch
            {
                ComparisonOp.AtLeast => ">=",
                ComparisonOp.GreaterThan => ">",
                ComparisonOp.Equals => "==",
                ComparisonOp.LessThan => "<",
                ComparisonOp.AtMost => "<=",
                _ => "?"
            };
            return $"{questId}.{objectiveId} {op} {value}";
        }
    }

    public bool Evaluate()
    {
        if (QuestManager.Instance == null) return true;
        var (current, _) = QuestManager.Instance.GetObjectiveProgress(questId, objectiveId);
        return comparison switch
        {
            ComparisonOp.AtLeast => current >= value,
            ComparisonOp.GreaterThan => current > value,
            ComparisonOp.Equals => current == value,
            ComparisonOp.LessThan => current < value,
            ComparisonOp.AtMost => current <= value,
            _ => true
        };
    }
}
