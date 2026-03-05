using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton manager that drives the dialogue system.
/// Processes dialogue trees (DialogueSO), handles player choices,
/// applies karma/coin rewards, and fires events for the UI layer.
///
/// Flow:
///   1. NPC calls StartDialogue(dialogueSO)
///   2. Manager disables player movement, fires OnDialogueStarted
///   3. For each node: fires OnNodeChanged → UI displays text + choices
///   4. Player selects a choice → SelectChoice(index)
///      → applies karma/coins → advances to next node
///   5. On terminal node or no next: EndDialogue()
///      → re-enables player movement, fires OnDialogueEnded
///
/// Setup: Add to the "GameManagers" GameObject in the scene.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    // ─── Singleton ──────────────────────────────────────────────
    public static DialogueManager Instance { get; private set; }

    // ─── Runtime State ──────────────────────────────────────────
    private DialogueSO currentDialogue;
    private DialogueNode currentNode;
    private bool isActive;

    // ─── One-Time Reward Tracking ────────────────────────────────
    // Uses a runtime-only HashSet (NOT VariableStore) because VariableStore
    // is a ScriptableObject whose changes persist in the editor between
    // play sessions, causing rewards to be permanently skipped.
    // This HashSet resets automatically each time you enter Play Mode.
    [NonSerialized] private HashSet<string> rewardedChoices = new HashSet<string>();

    // ─── Public Properties ──────────────────────────────────────

    /// <summary>Whether a dialogue is currently active.</summary>
    public bool IsDialogueActive => isActive;

    /// <summary>The current dialogue asset being played.</summary>
    public DialogueSO CurrentDialogue => currentDialogue;

    /// <summary>The current node being displayed.</summary>
    public DialogueNode CurrentNode => currentNode;

    /// <summary>NPC speaker name for the current dialogue (set by DialogueNPC before StartDialogue).</summary>
    public string ActiveNPCSpeakerName { get; set; }

    /// <summary>The transform of the NPC currently in dialogue (for camera + facing).</summary>
    public Transform ActiveNPCTransform { get; set; }

    // ─── Events ─────────────────────────────────────────────────

    /// <summary>Fired when a dialogue begins. Arg: the dialogue asset.</summary>
    public event Action<DialogueSO> OnDialogueStarted;

    /// <summary>Fired when the current node changes (new text to display). Arg: the new node.</summary>
    public event Action<DialogueNode> OnNodeChanged;

    /// <summary>Fired when the player makes a choice. Arg: the choice made.</summary>
    public event Action<DialogueChoice> OnChoiceMade;

    /// <summary>Fired when the dialogue ends.</summary>
    public event Action OnDialogueEnded;

    // ─── Public API ─────────────────────────────────────────────

    /// <summary>
    /// Start a dialogue tree. Disables player input and shows the first node.
    /// </summary>
    public void StartDialogue(DialogueSO dialogue)
    {
        if (dialogue == null)
        {
            Debug.LogWarning("DialogueManager: Cannot start null dialogue!");
            return;
        }

        if (isActive)
        {
            Debug.LogWarning("DialogueManager: Already in dialogue! Ending previous first.");
            EndDialogue();
        }

        currentDialogue = dialogue;
        isActive = true;

        // Disable player movement
        SetPlayerInputEnabled(false);

        OnDialogueStarted?.Invoke(dialogue);
        Debug.Log($"DialogueManager: Starting dialogue '{dialogue.dialogueId}'");

        // Show first node
        DialogueNode startNode = dialogue.GetStartNode();
        if (startNode != null)
        {
            ShowNode(startNode);
        }
        else
        {
            Debug.LogError($"DialogueManager: Dialogue '{dialogue.dialogueId}' has no nodes!");
            EndDialogue();
        }
    }

    /// <summary>
    /// Select a choice by index (0-based). Applies karma/coin rewards and advances.
    /// Called by the UI when the player presses Z/X/C.
    /// </summary>
    public void SelectChoice(int choiceIndex)
    {
        if (!isActive || currentNode == null || !currentNode.HasChoices)
        {
            Debug.LogWarning($"DialogueManager.SelectChoice({choiceIndex}): Blocked — isActive={isActive}, currentNode={currentNode != null}, HasChoices={currentNode?.HasChoices}");
            return;
        }
        if (choiceIndex < 0 || choiceIndex >= currentNode.choices.Length)
        {
            Debug.LogWarning($"DialogueManager.SelectChoice({choiceIndex}): Invalid index (choices count={currentNode.choices.Length})");
            return;
        }

        DialogueChoice choice = currentNode.choices[choiceIndex];

        // One-time rewards: generate a unique key for this choice and check
        // if rewards have already been given (this play session only).
        // Uses a runtime HashSet — resets each time you enter Play Mode.
        string rewardKey = $"{currentDialogue.dialogueId}_{currentNode.nodeId}_{choiceIndex}";
        bool alreadyRewarded = rewardedChoices.Contains(rewardKey);

        Debug.Log($"DialogueManager.SelectChoice({choiceIndex}): choice='{choice.choiceText}', karmaChange={choice.karmaChange}, coinChange={choice.coinChange}, alreadyRewarded={alreadyRewarded}");

        if (!alreadyRewarded)
        {
            // Legacy: Apply hardcoded karma reward (backward compat)
            if (choice.karmaChange != 0)
            {
                if (KarmaManager.Instance != null)
                {
                    KarmaManager.Instance.AddKarma(choice.karmaChange);
                    Debug.Log($"DialogueManager: ✓ Karma {(choice.karmaChange > 0 ? "+" : "")}{choice.karmaChange} applied (total: {KarmaManager.Instance.CurrentKarma})");
                }
                else
                {
                    Debug.LogWarning("DialogueManager: KarmaManager.Instance is NULL — cannot apply karma reward!");
                }
            }

            // Legacy: Apply hardcoded coin reward (backward compat)
            if (choice.coinChange != 0)
            {
                if (WalletManager.Instance != null)
                {
                    WalletManager.Instance.AddCoins(choice.coinChange);
                    Debug.Log($"DialogueManager: ✓ Coins {(choice.coinChange > 0 ? "+" : "")}{choice.coinChange} applied (total: {WalletManager.Instance.Coins})");
                }
                else
                {
                    Debug.LogWarning("DialogueManager: WalletManager.Instance is NULL — cannot apply coin reward!");
                }
            }

            // Execute extensible actions (from the actions list)
            if (choice.actions != null)
            {
                foreach (var action in choice.actions)
                    action?.Execute();
            }

            // Mark this choice as rewarded for this play session
            rewardedChoices.Add(rewardKey);
        }
        else
        {
            Debug.Log($"DialogueManager: Choice already rewarded this session (key={rewardKey}), skipping rewards.");
        }

        OnChoiceMade?.Invoke(choice);

        // Auto-sparkle on compassionate choices
        if (choice.choiceStyle == ChoiceStyle.Empathetic)
        {
            if (SparkleVFXManager.Instance != null)
                SparkleVFXManager.Instance.PlayAtPlayer();
        }

        // Advance to next node
        if (!string.IsNullOrEmpty(choice.nextNodeId))
        {
            DialogueNode nextNode = currentDialogue.GetNode(choice.nextNodeId);
            if (nextNode != null)
            {
                ShowNode(nextNode);
                return;
            }
        }

        // No next node — end dialogue
        EndDialogue();
    }

    /// <summary>
    /// Clear all rewarded choice tracking (used by ResetGameButton).
    /// Allows all dialogue rewards to fire again.
    /// </summary>
    public void ClearRewardedChoices()
    {
        rewardedChoices.Clear();
        Debug.Log("DialogueManager: Rewarded choices cleared — all rewards re-enabled.");
    }

    /// <summary>
    /// Advance to the next node (for nodes without choices — click-to-continue).
    /// Called by UI when player presses E/Space on a non-choice node.
    /// </summary>
    public void AdvanceDialogue()
    {
        if (!isActive || currentNode == null) return;

        // Don't advance if there are choices — player must pick one
        if (currentNode.HasChoices) return;

        // Check for end node
        if (currentNode.isEnd || string.IsNullOrEmpty(currentNode.nextNodeId))
        {
            EndDialogue();
            return;
        }

        // Advance to next node
        DialogueNode nextNode = currentDialogue.GetNode(currentNode.nextNodeId);
        if (nextNode != null)
        {
            ShowNode(nextNode);
        }
        else
        {
            Debug.LogWarning($"DialogueManager: Next node '{currentNode.nextNodeId}' not found. Ending dialogue.");
            EndDialogue();
        }
    }

    /// <summary>
    /// End the current dialogue. Re-enables player input.
    /// </summary>
    public void EndDialogue()
    {
        if (!isActive) return;

        isActive = false;
        currentDialogue = null;
        currentNode = null;
        ActiveNPCSpeakerName = null;
        ActiveNPCTransform = null;

        // Re-enable player movement
        SetPlayerInputEnabled(true);

        OnDialogueEnded?.Invoke();
        Debug.Log("DialogueManager: Dialogue ended.");
    }

    /// <summary>
    /// Check if a choice is available. Checks both legacy karmaLevel field
    /// AND the extensible conditions list.
    /// </summary>
    public bool IsChoiceAvailable(DialogueChoice choice)
    {
        // Legacy check (backward compat)
        if (choice.requiredKarmaLevel > 0 && KarmaManager.Instance != null
            && KarmaManager.Instance.CurrentLevel < choice.requiredKarmaLevel)
            return false;

        // NEW: Check extensible conditions
        if (choice.conditions != null)
        {
            foreach (var cond in choice.conditions)
            {
                if (cond != null && !cond.Evaluate())
                    return false;
            }
        }

        return true;
    }

    // ─── Internal ───────────────────────────────────────────────

    private void ShowNode(DialogueNode node)
    {
        // Evaluate node-level conditions — if any fail, skip this node
        if (node.HasConditions)
        {
            foreach (var cond in node.conditions)
            {
                if (cond != null && !cond.Evaluate())
                {
                    Debug.Log($"DialogueManager: Node '{node.nodeId}' skipped (condition failed: {cond.Label})");
                    // Skip to nextNodeId or end dialogue
                    if (!string.IsNullOrEmpty(node.nextNodeId))
                    {
                        DialogueNode fallback = currentDialogue.GetNode(node.nextNodeId);
                        if (fallback != null)
                        {
                            ShowNode(fallback);
                            return;
                        }
                    }
                    EndDialogue();
                    return;
                }
            }
        }

        // Execute onShow actions (set flags, play effects, etc.)
        if (node.onShowActions != null)
        {
            foreach (var action in node.onShowActions)
                action?.Execute();
        }

        currentNode = node;
        OnNodeChanged?.Invoke(node);
    }

    private void SetPlayerInputEnabled(bool enabled)
    {
        // Find the PlayerController and toggle its input
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null && player.input != null)
        {
            player.input.enabled = enabled;

            // Stop player movement when entering dialogue
            if (!enabled)
            {
                player.velocity = Vector3.zero;
            }
        }
    }

    // ─── Unity Lifecycle ────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("DialogueManager: Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
