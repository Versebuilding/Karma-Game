using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton manager for the quest system.
/// Tracks all quest states, processes objective progress, awards rewards,
/// and fires events for UI and other systems.
///
/// Flow:
///   1. Dialogue action calls StartQuest(questId) → quest becomes Active
///   2. World objects / dialogue actions call AdvanceObjective(questId, objectiveId)
///   3. When all required objectives are met → quest transitions to Completed
///   4. CompleteQuest(questId) awards rewards and transitions to Done
///
/// Integration:
///   - Dialogue: StartQuestAction, AdvanceQuestAction, CompleteQuestAction (IDialogueAction)
///   - Conditions: QuestStateCondition, QuestObjectiveCondition (IDialogueCondition)
///   - World: QuestTriggerZone, QuestItemPickup call AdvanceObjective()
///   - VariableStore: Flags set on quest completion for cross-system reactivity
///
/// Setup: Add to the "GameManagers" GameObject via Karma > Setup Game Systems.
/// </summary>
public class QuestManager : MonoBehaviour
{
    // ─── Singleton ──────────────────────────────────────────────
    public static QuestManager Instance { get; private set; }

    // ─── Configuration ──────────────────────────────────────────
    [Header("Quest Registry")]
    [Tooltip("All quest definitions in the game. Assign via Inspector or GameSystemsSetup.")]
    [SerializeField] private QuestSO[] questDefinitions;

    // ─── Runtime State ──────────────────────────────────────────
    private Dictionary<string, QuestSO> questRegistry = new Dictionary<string, QuestSO>();
    private Dictionary<string, QuestRuntimeState> questStates = new Dictionary<string, QuestRuntimeState>();

    // ─── Events ─────────────────────────────────────────────────

    /// <summary>Fired when a quest starts. Arg: questId.</summary>
    public event Action<string> OnQuestStarted;

    /// <summary>Fired when an objective is updated. Args: questId, objectiveId, current, required.</summary>
    public event Action<string, string, int, int> OnObjectiveUpdated;

    /// <summary>Fired when a quest is completed (all objectives done). Arg: questId.</summary>
    public event Action<string> OnQuestCompleted;

    /// <summary>Fired when a quest fails. Arg: questId.</summary>
    public event Action<string> OnQuestFailed;

    /// <summary>Fired when an objective fails. Args: questId, objectiveId, retryAllowed.</summary>
    public event Action<string, string, bool> OnObjectiveFailed;

    // ─── Public API: Quest State ────────────────────────────────

    /// <summary>Get the current state of a quest.</summary>
    public QuestState GetQuestState(string questId)
    {
        if (questStates.TryGetValue(questId, out var state))
            return state.state;
        return QuestState.Locked;
    }

    /// <summary>Check if a quest is currently Active.</summary>
    public bool IsQuestActive(string questId) => GetQuestState(questId) == QuestState.Active;

    /// <summary>Check if a quest is Completed (objectives done, not yet rewarded).</summary>
    public bool IsQuestCompleted(string questId) => GetQuestState(questId) == QuestState.Completed;

    /// <summary>Check if a quest is fully Done (rewarded).</summary>
    public bool IsQuestDone(string questId) => GetQuestState(questId) == QuestState.Done;

    /// <summary>Get all currently active quests.</summary>
    public List<QuestRuntimeState> GetActiveQuests()
    {
        var result = new List<QuestRuntimeState>();
        foreach (var kvp in questStates)
        {
            if (kvp.Value.state == QuestState.Active)
                result.Add(kvp.Value);
        }
        return result;
    }

    /// <summary>Get the quest definition by ID.</summary>
    public QuestSO GetQuestDefinition(string questId)
    {
        questRegistry.TryGetValue(questId, out var def);
        return def;
    }

    /// <summary>Get objective progress. Returns (current, required).</summary>
    public (int current, int required) GetObjectiveProgress(string questId, string objectiveId)
    {
        if (!questStates.TryGetValue(questId, out var state))
            return (0, 0);
        if (!questRegistry.TryGetValue(questId, out var def))
            return (0, 0);

        var objective = def.GetObjective(objectiveId);
        if (objective == null) return (0, 0);

        return (state.GetProgress(objectiveId), objective.requiredCount);
    }

    /// <summary>Get the runtime state for a quest (for UI display).</summary>
    public QuestRuntimeState GetRuntimeState(string questId)
    {
        questStates.TryGetValue(questId, out var state);
        return state;
    }

    /// <summary>Get all quest definitions that have a specific tag.</summary>
    public List<QuestSO> GetQuestsByTag(string tag)
    {
        var result = new List<QuestSO>();
        foreach (var kvp in questRegistry)
        {
            if (kvp.Value.HasTag(tag))
                result.Add(kvp.Value);
        }
        return result;
    }

    /// <summary>Get all quest runtime states (for debug/save).</summary>
    public Dictionary<string, QuestRuntimeState> GetAllQuestStates() => questStates;

    /// <summary>Get all registered quest definitions (for debug).</summary>
    public Dictionary<string, QuestSO> GetQuestRegistry() => questRegistry;

    // ─── Public API: Quest Actions ──────────────────────────────

    /// <summary>
    /// Start a quest. Transitions from Locked/Available to Active.
    /// Initializes objective progress tracking.
    /// </summary>
    public void StartQuest(string questId)
    {
        if (string.IsNullOrEmpty(questId))
        {
            Debug.LogWarning("QuestManager.StartQuest: questId is null or empty!");
            return;
        }

        if (!questRegistry.TryGetValue(questId, out var def))
        {
            Debug.LogWarning($"QuestManager.StartQuest: Quest '{questId}' not found in registry!");
            return;
        }

        var state = GetOrCreateState(questId);

        if (state.state == QuestState.Active)
        {
            Debug.Log($"QuestManager: Quest '{questId}' is already active.");
            return;
        }

        if (state.state == QuestState.Done && !def.isRepeatable)
        {
            Debug.Log($"QuestManager: Quest '{questId}' is already done and not repeatable.");
            return;
        }

        // Initialize objective progress
        if (def.objectives != null)
        {
            foreach (var obj in def.objectives)
            {
                if (!state.objectiveProgress.ContainsKey(obj.objectiveId))
                    state.SetProgress(obj.objectiveId, 0);
            }
        }

        state.state = QuestState.Active;
        OnQuestStarted?.Invoke(questId);
        Debug.Log($"QuestManager: Quest '{def.displayName}' ({questId}) started!");
    }

    /// <summary>
    /// Advance an objective's progress. Auto-completes the quest if all required
    /// objectives are met.
    /// </summary>
    public void AdvanceObjective(string questId, string objectiveId, int amount = 1)
    {
        if (!questStates.TryGetValue(questId, out var state) || state.state != QuestState.Active)
        {
            Debug.Log($"QuestManager.AdvanceObjective: Quest '{questId}' is not active (state={state?.state}).");
            return;
        }

        if (!questRegistry.TryGetValue(questId, out var def))
            return;

        var objective = def.GetObjective(objectiveId);
        if (objective == null)
        {
            Debug.LogWarning($"QuestManager.AdvanceObjective: Objective '{objectiveId}' not found in quest '{questId}'.");
            return;
        }

        int current = state.GetProgress(objectiveId);
        int newValue = Mathf.Min(current + amount, objective.requiredCount);
        state.SetProgress(objectiveId, newValue);

        OnObjectiveUpdated?.Invoke(questId, objectiveId, newValue, objective.requiredCount);
        Debug.Log($"QuestManager: Objective '{objectiveId}' → {newValue}/{objective.requiredCount}");

        // Check if all required objectives are complete
        if (AreAllObjectivesComplete(def, state))
        {
            state.state = QuestState.Completed;
            Debug.Log($"QuestManager: Quest '{def.displayName}' all objectives complete!");
            CompleteQuest(questId);
        }
    }

    /// <summary>
    /// Complete a quest — award rewards and transition to Done.
    /// Called automatically when all objectives are met, or manually via dialogue.
    /// </summary>
    public void CompleteQuest(string questId)
    {
        if (!questStates.TryGetValue(questId, out var state))
            return;

        if (state.state != QuestState.Active && state.state != QuestState.Completed)
        {
            Debug.Log($"QuestManager.CompleteQuest: Quest '{questId}' is not Active or Completed (state={state.state}).");
            return;
        }

        if (!questRegistry.TryGetValue(questId, out var def))
            return;

        // Award rewards
        AwardRewards(def.rewards);

        state.state = QuestState.Done;
        OnQuestCompleted?.Invoke(questId);
        Debug.Log($"QuestManager: Quest '{def.displayName}' ({questId}) completed and rewarded!");

        // Unlock follow-up quests
        UnlockFollowUpQuests(def);
    }

    /// <summary>
    /// Fail a quest.
    /// </summary>
    public void FailQuest(string questId)
    {
        if (!questStates.TryGetValue(questId, out var state))
            return;

        if (state.state != QuestState.Active)
            return;

        state.state = QuestState.Failed;
        OnQuestFailed?.Invoke(questId);
        Debug.Log($"QuestManager: Quest '{questId}' failed.");
    }

    /// <summary>
    /// Fail a specific objective within a quest (fail-soft design).
    ///
    /// Behavior depends on objective configuration:
    ///   - canFail=false → ignored (objective stays incomplete, player must succeed)
    ///   - canFail + retryAllowed → resets progress to 0, fires event (player can try again)
    ///   - canFail + !retryAllowed + optional → skips this objective, continues quest
    ///   - canFail + !retryAllowed + required → checks fallbackDialogueId, else fails quest
    /// </summary>
    public void FailObjective(string questId, string objectiveId)
    {
        if (!questStates.TryGetValue(questId, out var state) || state.state != QuestState.Active)
            return;
        if (!questRegistry.TryGetValue(questId, out var def))
            return;

        var objective = def.GetObjective(objectiveId);
        if (objective == null) return;

        // Not a failable objective — just ignore
        if (!objective.canFail)
        {
            Debug.Log($"QuestManager: Objective '{objectiveId}' cannot fail (canFail=false). Ignoring.");
            return;
        }

        if (objective.retryAllowed)
        {
            // Reset progress, let player try again
            state.SetProgress(objectiveId, 0);
            OnObjectiveFailed?.Invoke(questId, objectiveId, true);
            Debug.Log($"QuestManager: Objective '{objectiveId}' failed — retrying (progress reset to 0).");
            return;
        }

        // No retry allowed
        OnObjectiveFailed?.Invoke(questId, objectiveId, false);

        if (objective.isOptional)
        {
            // Optional objective — skip it, quest continues
            Debug.Log($"QuestManager: Optional objective '{objectiveId}' failed — skipping.");

            // Check if remaining required objectives are complete
            if (AreAllObjectivesComplete(def, state))
            {
                state.state = QuestState.Completed;
                CompleteQuest(questId);
            }
            return;
        }

        // Required objective failed permanently
        if (!string.IsNullOrEmpty(objective.fallbackDialogueId))
        {
            // Trigger fallback dialogue (compassionate fallback)
            Debug.Log($"QuestManager: Required objective '{objectiveId}' failed — triggering fallback dialogue '{objective.fallbackDialogueId}'.");

            // Try to start fallback dialogue via DialogueManager
            if (DialogueManager.Instance != null)
            {
                // Look for fallback dialogue SO — the fallbackDialogueId should match a DialogueSO.dialogueId
                // The actual dialogue trigger is left to the world (e.g., NPC notices failure and starts talking)
                // We set a flag so dialogue conditions can react
                if (VariableStore.Instance != null)
                    VariableStore.Instance.SetFlag($"{questId}_{objectiveId}_failed", true);
            }
        }
        else
        {
            // No fallback — fail the quest
            Debug.Log($"QuestManager: Required objective '{objectiveId}' failed permanently — failing quest '{questId}'.");
            FailQuest(questId);
        }
    }

    /// <summary>
    /// Reset all quest progress (for game reset).
    /// </summary>
    public void ResetAllQuests()
    {
        questStates.Clear();
        InitializeQuestStates();
        Debug.Log("QuestManager: All quest progress reset.");
    }

    // ─── Internal ───────────────────────────────────────────────

    private QuestRuntimeState GetOrCreateState(string questId)
    {
        if (!questStates.TryGetValue(questId, out var state))
        {
            state = new QuestRuntimeState(questId);
            questStates[questId] = state;
        }
        return state;
    }

    private bool AreAllObjectivesComplete(QuestSO def, QuestRuntimeState state)
    {
        if (def.objectives == null) return true;

        foreach (var obj in def.objectives)
        {
            if (obj.isOptional) continue;
            int progress = state.GetProgress(obj.objectiveId);
            if (progress < obj.requiredCount)
                return false;
        }
        return true;
    }

    private void AwardRewards(QuestRewards rewards)
    {
        if (rewards == null) return;

        // Karma
        if (rewards.karmaAmount != 0 && KarmaManager.Instance != null)
        {
            KarmaManager.Instance.AddKarma(rewards.karmaAmount);
            Debug.Log($"QuestManager: Awarded {rewards.karmaAmount} karma.");
        }

        // Coins
        if (rewards.coinAmount != 0 && WalletManager.Instance != null)
        {
            WalletManager.Instance.AddCoins(rewards.coinAmount);
            Debug.Log($"QuestManager: Awarded {rewards.coinAmount} coins.");
        }

        // Set VariableStore flags
        if (rewards.flagsToSet != null && VariableStore.Instance != null)
        {
            foreach (var flag in rewards.flagsToSet)
            {
                if (!string.IsNullOrEmpty(flag))
                {
                    VariableStore.Instance.SetFlag(flag, true);
                    Debug.Log($"QuestManager: Set flag '{flag}' = true.");
                }
            }
        }

        // TODO: Items — requires InventoryManager (not yet implemented)
        // if (rewards.items != null) { ... }
    }

    private void UnlockFollowUpQuests(QuestSO completedQuest)
    {
        if (completedQuest.rewards?.questsToUnlock == null) return;

        foreach (var unlockId in completedQuest.rewards.questsToUnlock)
        {
            if (string.IsNullOrEmpty(unlockId)) continue;

            var state = GetOrCreateState(unlockId);
            if (state.state == QuestState.Locked)
            {
                // Check if ALL prerequisites are met (not just this one)
                if (ArePrerequisitesMet(unlockId))
                {
                    state.state = QuestState.Available;
                    Debug.Log($"QuestManager: Quest '{unlockId}' is now Available.");

                    // Auto-start if configured
                    if (questRegistry.TryGetValue(unlockId, out var def) && def.autoStart)
                    {
                        StartQuest(unlockId);
                    }
                }
            }
        }
    }

    private bool ArePrerequisitesMet(string questId)
    {
        if (!questRegistry.TryGetValue(questId, out var def))
            return false;

        if (def.prerequisites == null || def.prerequisites.Length == 0)
            return true;

        foreach (var prereq in def.prerequisites)
        {
            if (string.IsNullOrEmpty(prereq)) continue;
            if (GetQuestState(prereq) != QuestState.Done)
                return false;
        }
        return true;
    }

    private void InitializeQuestStates()
    {
        if (questDefinitions == null) return;

        foreach (var def in questDefinitions)
        {
            if (def == null || string.IsNullOrEmpty(def.questId)) continue;

            var state = GetOrCreateState(def.questId);

            // If no prerequisites, quest is immediately Available
            if (state.state == QuestState.Locked && ArePrerequisitesMet(def.questId))
            {
                state.state = QuestState.Available;

                if (def.autoStart)
                    StartQuest(def.questId);
            }
        }
    }

    // ─── Unity Lifecycle ────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("QuestManager: Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Build registry
        if (questDefinitions != null)
        {
            foreach (var def in questDefinitions)
            {
                if (def != null && !string.IsNullOrEmpty(def.questId))
                    questRegistry[def.questId] = def;
            }
        }

        Debug.Log($"QuestManager: Registered {questRegistry.Count} quest definitions.");
    }

    void Start()
    {
        InitializeQuestStates();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
