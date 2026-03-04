using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dialogue tree ScriptableObject. Create via: Right-click > Create > Karma > Dialogue.
///
/// Structure:
///   DialogueSO contains an array of DialogueNodes.
///   Each node has speaker text and optional choices.
///   Choices lead to other nodes and can award karma/coins.
///   Nodes without choices auto-advance to nextNodeId or end the dialogue.
/// </summary>
[CreateAssetMenu(fileName = "NewDialogue", menuName = "Karma/Dialogue", order = 1)]
public class DialogueSO : ScriptableObject
{
    [Tooltip("Unique identifier for this dialogue (used for save/load and conditions)")]
    public string dialogueId;

    [Tooltip("All nodes in this dialogue tree")]
    public DialogueNode[] nodes;

    /// <summary>Find a node by its ID. Returns null if not found.</summary>
    public DialogueNode GetNode(string nodeId)
    {
        if (nodes == null) return null;
        foreach (var node in nodes)
        {
            if (node.nodeId == nodeId)
                return node;
        }
        Debug.LogWarning($"DialogueSO '{dialogueId}': Node '{nodeId}' not found!");
        return null;
    }

    /// <summary>Get the first node (index 0).</summary>
    public DialogueNode GetStartNode()
    {
        if (nodes == null || nodes.Length == 0) return null;
        return nodes[0];
    }
}

/// <summary>
/// A single node in a dialogue tree. Contains speaker text and optional choices.
/// </summary>
[Serializable]
public class DialogueNode
{
    [Tooltip("Unique ID within this dialogue (e.g. 'intro', 'choice_result_1')")]
    public string nodeId;

    [Tooltip("Name displayed above the dialogue text (NPC name)")]
    public string speakerName;

    [Tooltip("The dialogue text shown to the player")]
    [TextArea(2, 5)]
    public string dialogueText;

    [Tooltip("Player choices. Leave empty for auto-advance / click-to-continue nodes.")]
    public DialogueChoice[] choices;

    [Tooltip("If no choices, which node to advance to (leave empty to end dialogue)")]
    public string nextNodeId;

    [Tooltip("If true, this is the final node — dialogue ends after displaying it")]
    public bool isEnd;

    // ─── Extensibility (Conditions & Actions) ─────────────────

    [Space(8)]
    [Tooltip("Conditions that must ALL pass for this node to be shown. If any fail, the node is skipped (advances to nextNodeId).")]
    [SerializeReference]
    public List<IDialogueCondition> conditions = new List<IDialogueCondition>();

    [Tooltip("Actions to execute when this node is displayed (e.g., set flags, play effects).")]
    [SerializeReference]
    public List<IDialogueAction> onShowActions = new List<IDialogueAction>();

    // ─── Properties ───────────────────────────────────────────

    /// <summary>Does this node have player choices?</summary>
    public bool HasChoices => choices != null && choices.Length > 0;

    /// <summary>Does this node have conditions that must be evaluated?</summary>
    public bool HasConditions => conditions != null && conditions.Count > 0;
}

/// <summary>
/// A player choice within a dialogue node.
/// </summary>
[Serializable]
public class DialogueChoice
{
    [Tooltip("Text displayed on the choice button")]
    public string choiceText;

    [Tooltip("Input label shown on button (Z, X, C)")]
    public string inputLabel;

    [Tooltip("Which node this choice leads to")]
    public string nextNodeId;

    [Tooltip("Karma change when this choice is selected (+positive = good, -negative = bad)")]
    public int karmaChange;

    [Tooltip("Coin change when this choice is selected")]
    public int coinChange;

    [Tooltip("Minimum karma level required to see this choice (0 = always available). Legacy — use conditions list instead for new dialogues.")]
    public int requiredKarmaLevel;

    [Tooltip("Visual style of the choice button")]
    public ChoiceStyle choiceStyle = ChoiceStyle.Neutral;

    // ─── Extensibility (Conditions & Actions) ─────────────────

    [Space(8)]
    [Tooltip("Conditions that must ALL pass for this choice to be available. If any fail, choice is greyed out/hidden.")]
    [SerializeReference]
    public List<IDialogueCondition> conditions = new List<IDialogueCondition>();

    [Tooltip("Actions to execute when this choice is selected (karma, coins, flags, items, quests, etc.).")]
    [SerializeReference]
    public List<IDialogueAction> actions = new List<IDialogueAction>();
}

/// <summary>
/// Visual style for dialogue choice buttons (matches Figma mockup colors).
/// </summary>
public enum ChoiceStyle
{
    Empathetic,  // Orange/warm — kind, helpful choices
    Selfish,     // Darker/cool — greedy, self-serving choices
    Neutral      // White/light — neutral, safe choices
}
