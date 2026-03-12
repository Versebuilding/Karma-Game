using System;
using UnityEngine;

/// <summary>
/// Quest definition ScriptableObject. Create via: Right-click > Create > Karma > Quest.
///
/// Structure:
///   QuestSO holds a quest's metadata, ordered objectives, prerequisites, and rewards.
///   Objectives are checked sequentially — the first incomplete objective is the "active" one.
///   When all required objectives are complete, the quest transitions to Completed.
///
/// Integration:
///   - Dialogue: Use StartQuestAction / AdvanceQuestAction in dialogue choices
///   - Conditions: Use QuestStateCondition / QuestObjectiveCondition to gate dialogue nodes
///   - World: QuestTriggerZone and QuestItemPickup advance objectives automatically
/// </summary>
[CreateAssetMenu(fileName = "NewQuest", menuName = "Karma/Quest", order = 4)]
public class QuestSO : ScriptableObject
{
    [Tooltip("Unique identifier for this quest (used for save/load and cross-references)")]
    public string questId;

    [Tooltip("Display name shown in the quest log")]
    public string displayName;

    [Tooltip("Description shown when viewing quest details")]
    [TextArea(2, 5)]
    public string description;

    [Tooltip("Category of this quest")]
    public QuestType questType = QuestType.Main;

    [Tooltip("Ordered list of objectives. First incomplete objective is the active one.")]
    public QuestObjective[] objectives;

    [Tooltip("Rewards granted when the quest is completed")]
    public QuestRewards rewards;

    [Tooltip("Quest IDs that must be Done before this quest becomes Available")]
    public string[] prerequisites;

    [Tooltip("If true, quest auto-starts when all prerequisites are met")]
    public bool autoStart;

    [Tooltip("If true, quest can be replayed after completion")]
    public bool isRepeatable;

    [Tooltip("Semantic tags for filtering and categorization (e.g., 'realm_hungry_ghost', 'emotional', 'tutorialized')")]
    public string[] tags;

    /// <summary>Check if this quest has a specific tag.</summary>
    public bool HasTag(string tag)
    {
        if (tags == null || string.IsNullOrEmpty(tag)) return false;
        return System.Array.IndexOf(tags, tag) >= 0;
    }

    /// <summary>Find an objective by its ID. Returns null if not found.</summary>
    public QuestObjective GetObjective(string objectiveId)
    {
        if (objectives == null) return null;
        foreach (var obj in objectives)
        {
            if (obj.objectiveId == objectiveId)
                return obj;
        }
        return null;
    }
}

/// <summary>
/// Quest category.
/// </summary>
public enum QuestType
{
    Main,    // Main story quests
    Side,    // Optional side quests
    Bounty   // Repeatable / challenge quests
}

/// <summary>
/// Quest progression state (FSM).
/// </summary>
public enum QuestState
{
    Locked,      // Prerequisites not met
    Available,   // Prerequisites met, not yet accepted
    Active,      // Player has accepted, working on objectives
    Completed,   // All objectives done, awaiting reward collection
    Done,        // Rewards collected, quest fully resolved
    Failed       // Quest failed (time-limited or fail-able quests)
}

/// <summary>
/// A single objective within a quest.
/// </summary>
[Serializable]
public class QuestObjective
{
    [Tooltip("Unique ID within this quest (e.g. 'talk_serna', 'collect_food')")]
    public string objectiveId;

    [Tooltip("Description shown in the quest log (e.g. 'Talk to Serna')")]
    public string description;

    [Tooltip("Type of objective — determines how progress is tracked")]
    public ObjectiveType type = ObjectiveType.Custom;

    [Tooltip("Target identifier (NPC id, item id, location tag, minigame id)")]
    public string targetId;

    [Tooltip("How many times this objective must be completed (e.g. 'Collect 3 apples')")]
    [Min(1)]
    public int requiredCount = 1;

    [Tooltip("If true, this objective is not required for quest completion")]
    public bool isOptional;

    // ─── Visibility (Little Nightmares-style silent/environmental beats) ─────

    [Space(8)]
    [Tooltip("Controls how this objective appears in the UI")]
    public ObjectiveVisibility visibility = ObjectiveVisibility.JournalVisible;

    // ─── Fail-Soft Design (compassionate fallbacks) ─────────────────────────

    [Space(8)]
    [Tooltip("If true, this objective can fail (e.g., timed challenges, minigames). If false, it stays incomplete until succeeded.")]
    public bool canFail;

    [Tooltip("If canFail is true: allow the player to retry after failure")]
    public bool retryAllowed;

    [Tooltip("Dialogue ID to trigger on failure (compassionate fallback). Leave empty for no fallback dialogue.")]
    public string fallbackDialogueId;
}

/// <summary>
/// Controls how a quest objective appears in the UI.
/// Enables Little Nightmares-style silent/environmental beats alongside
/// explicit Witcher-style quest log entries.
/// </summary>
public enum ObjectiveVisibility
{
    Hidden,           // Silent/environmental beat — no UI, no journal entry
    SoftHint,         // Subtle hint only (NPC gives vague direction, no explicit tracker)
    JournalVisible,   // Shows in quest log text but no map marker
    MapMarkerVisible  // Full map marker + journal entry
}

/// <summary>
/// Types of quest objectives.
/// </summary>
public enum ObjectiveType
{
    Talk,       // Talk to an NPC (via dialogue)
    Gather,     // Collect items from the world
    GoTo,       // Reach a location (trigger zone)
    Activate,   // Interact with an object (lever, button, etc.)
    Kill,       // Defeat enemies
    Minigame,   // Complete a minigame (deferred implementation)
    Custom      // Advanced — triggered manually via AdvanceQuestAction
}

/// <summary>
/// Rewards granted on quest completion.
/// </summary>
[Serializable]
public class QuestRewards
{
    [Tooltip("Karma points awarded on completion")]
    public int karmaAmount;

    [Tooltip("Coins awarded on completion")]
    public int coinAmount;

    [Tooltip("Items awarded on completion")]
    public ItemSO[] items;

    [Tooltip("VariableStore flags to set on completion (e.g. 'serna_quest_done')")]
    public string[] flagsToSet;

    [Tooltip("Quest IDs to make Available on completion")]
    public string[] questsToUnlock;
}
