using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tool to create sample dialogue assets for testing.
/// Menu: Karma > Create Serna Intro Dialogue
///
/// Creates a DialogueSO ScriptableObject asset with Serna's introductory
/// conversation tree, using both legacy fields (backward compat) and
/// the new extensible actions/conditions system.
/// </summary>
public class DialogueDataCreator
{
    [MenuItem("Karma/Create Serna Intro Dialogue")]
    public static void CreateSernaIntroDialogue()
    {
        // Ensure the folder exists
        if (!AssetDatabase.IsValidFolder("Assets/Data"))
            AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder("Assets/Data/Dialogues"))
            AssetDatabase.CreateFolder("Assets/Data", "Dialogues");

        // Create the DialogueSO asset
        DialogueSO dialogue = ScriptableObject.CreateInstance<DialogueSO>();
        dialogue.dialogueId = "serna_intro";

        // Build the dialogue tree
        // Node 0: Serna's opening line (auto-advance, sets flag)
        // Node 1: Player responds (3 choices with extensible actions)
        // Node 2a: Empathetic response → karma gain via action
        // Node 2b: Selfish response → karma loss + coin gain via actions
        // Node 2c: Neutral response → no change
        // Node 3: Quest accepted (end)
        // Node 4: Selfish end
        // Node 5: Neutral end

        dialogue.nodes = new DialogueNode[]
        {
            // ─── Node 0: Serna's Introduction ─────────────────────────
            new DialogueNode
            {
                nodeId = "start",
                speakerName = "Serna",
                dialogueText = "Oh... you can see me? Most people walk right past without noticing. " +
                               "I've been here for so long, waiting for someone... anyone...",
                choices = new DialogueChoice[0], // auto-advance
                nextNodeId = "serna_ask",
                isEnd = false,
                // NEW: Set flag when this node is shown
                onShowActions = new List<IDialogueAction>
                {
                    new SetFlagAction { flagName = "metSerna", value = true }
                }
            },

            // ─── Node 1: Serna Asks for Help ──────────────────────────
            new DialogueNode
            {
                nodeId = "serna_ask",
                speakerName = "Serna",
                dialogueText = "My cup... it's been empty for so long. I can't remember what it feels " +
                               "like to not be hungry. Could you... would you help me?",
                choices = new DialogueChoice[]
                {
                    new DialogueChoice
                    {
                        choiceText = "Of course, Serna. I'd like to help you.",
                        inputLabel = "Z",
                        nextNodeId = "empathetic_response",
                        // Legacy fields (backward compat)
                        karmaChange = 50,
                        coinChange = 0,
                        requiredKarmaLevel = 0,
                        choiceStyle = ChoiceStyle.Empathetic,
                        // NEW: Extensible actions
                        actions = new List<IDialogueAction>
                        {
                            new ModifyKarmaAction { amount = 50 },
                            new SetFlagAction { flagName = "helpedSerna", value = true },
                            new ModifyCounterAction { counterName = "ghostsHelped", amount = 1 }
                        }
                    },
                    new DialogueChoice
                    {
                        choiceText = "What's in it for me?",
                        inputLabel = "X",
                        nextNodeId = "selfish_response",
                        // Legacy fields (backward compat)
                        karmaChange = -20,
                        coinChange = 100,
                        requiredKarmaLevel = 0,
                        choiceStyle = ChoiceStyle.Selfish,
                        // NEW: Extensible actions
                        actions = new List<IDialogueAction>
                        {
                            new ModifyKarmaAction { amount = -20 },
                            new ModifyCoinsAction { amount = 100 },
                            new SetFlagAction { flagName = "selfishWithSerna", value = true }
                        }
                    },
                    new DialogueChoice
                    {
                        choiceText = "I'll think about it.",
                        inputLabel = "C",
                        nextNodeId = "neutral_response",
                        karmaChange = 0,
                        coinChange = 0,
                        requiredKarmaLevel = 0,
                        choiceStyle = ChoiceStyle.Neutral,
                        // NEW: Even neutral has an action — track the choice
                        actions = new List<IDialogueAction>
                        {
                            new SetFlagAction { flagName = "neutralWithSerna", value = true }
                        }
                    }
                },
                nextNodeId = "",
                isEnd = false
            },

            // ─── Node 2a: Empathetic Response ─────────────────────────
            new DialogueNode
            {
                nodeId = "empathetic_response",
                speakerName = "Serna",
                dialogueText = "You... you would? Thank you! I haven't felt kindness in so long. " +
                               "There's a baker in the village — he sometimes leaves bread out. " +
                               "If you could bring some back... I would be forever grateful.",
                choices = new DialogueChoice[0],
                nextNodeId = "quest_accepted",
                isEnd = false
            },

            // ─── Node 2b: Selfish Response ────────────────────────────
            new DialogueNode
            {
                nodeId = "selfish_response",
                speakerName = "Serna",
                dialogueText = "I... I understand. Everyone needs something. Here, take these coins — " +
                               "they're all I have left. Just... please come back if you change your mind.",
                choices = new DialogueChoice[0],
                nextNodeId = "end_selfish",
                isEnd = false
            },

            // ─── Node 2c: Neutral Response ────────────────────────────
            new DialogueNode
            {
                nodeId = "neutral_response",
                speakerName = "Serna",
                dialogueText = "Of course... take your time. I'll be here. I'm always here. " +
                               "If you decide to help, I need bread from the village baker.",
                choices = new DialogueChoice[0],
                nextNodeId = "end_neutral",
                isEnd = false
            },

            // ─── Node 3: Quest Accepted ───────────────────────────────
            new DialogueNode
            {
                nodeId = "quest_accepted",
                speakerName = "Serna",
                dialogueText = "The baker's shop is to the east, past the old bridge. " +
                               "Be careful — the ghosts grow restless at night. " +
                               "And Sammy... thank you. Truly.",
                choices = new DialogueChoice[0],
                nextNodeId = "",
                isEnd = true,
                // NEW: Set quest flag on show
                onShowActions = new List<IDialogueAction>
                {
                    new SetFlagAction { flagName = "sernaQuestActive", value = true }
                }
            },

            // ─── Node 4: Selfish End ──────────────────────────────────
            new DialogueNode
            {
                nodeId = "end_selfish",
                speakerName = "Serna",
                dialogueText = "Maybe... maybe someday someone will help just because it's right...",
                choices = new DialogueChoice[0],
                nextNodeId = "",
                isEnd = true
            },

            // ─── Node 5: Neutral End ──────────────────────────────────
            new DialogueNode
            {
                nodeId = "end_neutral",
                speakerName = "Serna",
                dialogueText = "I'll wait. I've gotten good at waiting...",
                choices = new DialogueChoice[0],
                nextNodeId = "",
                isEnd = true
            }
        };

        // Save the asset
        string path = "Assets/Data/Dialogues/Serna_Intro.asset";
        AssetDatabase.CreateAsset(dialogue, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Select the created asset
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = dialogue;

        Debug.Log($"Created Serna intro dialogue asset at: {path}");
        Debug.Log("Dialogue tree: start → serna_ask → (empathetic +50K / selfish -20K +100C / neutral) → end");
        Debug.Log("NEW: Uses extensible actions (ModifyKarma, SetFlag, ModifyCounter) + onShowActions");
    }

    [MenuItem("Karma/Create Serna Return Dialogue")]
    public static void CreateSernaReturnDialogue()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Data"))
            AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder("Assets/Data/Dialogues"))
            AssetDatabase.CreateFolder("Assets/Data", "Dialogues");

        DialogueSO dialogue = ScriptableObject.CreateInstance<DialogueSO>();
        dialogue.dialogueId = "serna_return";

        dialogue.nodes = new DialogueNode[]
        {
            // Node 0: Serna recognizes you (conditional — requires met flag)
            new DialogueNode
            {
                nodeId = "return_start",
                speakerName = "Serna",
                dialogueText = "You came back! I... I wasn't sure you would.",
                choices = new DialogueChoice[0],
                nextNodeId = "return_choices",
                isEnd = false,
                // Only show if player has met Serna before
                conditions = new List<IDialogueCondition>
                {
                    new FlagCondition { flagName = "metSerna", expectedValue = true }
                }
            },

            // Node 1: Return choices — conditional based on previous interaction
            new DialogueNode
            {
                nodeId = "return_choices",
                speakerName = "Serna",
                dialogueText = "Have you found any bread? The hunger... it never stops.",
                choices = new DialogueChoice[]
                {
                    new DialogueChoice
                    {
                        choiceText = "Here, I brought bread for you.",
                        inputLabel = "Z",
                        nextNodeId = "return_grateful",
                        choiceStyle = ChoiceStyle.Empathetic,
                        actions = new List<IDialogueAction>
                        {
                            new ModifyKarmaAction { amount = 100 },
                            new SetFlagAction { flagName = "fedSerna", value = true },
                            new SetFlagAction { flagName = "sernaQuestActive", value = false }
                        },
                        // Only available if player accepted the quest
                        conditions = new List<IDialogueCondition>
                        {
                            new FlagCondition { flagName = "sernaQuestActive", expectedValue = true }
                        }
                    },
                    new DialogueChoice
                    {
                        choiceText = "Not yet, but I'm still looking.",
                        inputLabel = "X",
                        nextNodeId = "return_waiting",
                        choiceStyle = ChoiceStyle.Neutral,
                        actions = new List<IDialogueAction>
                        {
                            new ModifyKarmaAction { amount = 10 }
                        }
                    },
                    new DialogueChoice
                    {
                        choiceText = "I've changed my mind. I want to help.",
                        inputLabel = "C",
                        nextNodeId = "return_redemption",
                        choiceStyle = ChoiceStyle.Empathetic,
                        actions = new List<IDialogueAction>
                        {
                            new ModifyKarmaAction { amount = 30 },
                            new SetFlagAction { flagName = "helpedSerna", value = true },
                            new SetFlagAction { flagName = "sernaQuestActive", value = true }
                        },
                        // Only show if player was selfish/neutral first time
                        conditions = new List<IDialogueCondition>
                        {
                            new FlagCondition { flagName = "helpedSerna", expectedValue = false }
                        }
                    }
                },
                nextNodeId = "",
                isEnd = false
            },

            // Grateful ending
            new DialogueNode
            {
                nodeId = "return_grateful",
                speakerName = "Serna",
                dialogueText = "Thank you... truly. The warmth of this bread... " +
                               "I had forgotten what it felt like to be cared for.",
                choices = new DialogueChoice[0],
                nextNodeId = "",
                isEnd = true
            },

            // Still waiting
            new DialogueNode
            {
                nodeId = "return_waiting",
                speakerName = "Serna",
                dialogueText = "I understand. The village can be confusing. " +
                               "The baker is to the east, past the old bridge. Take your time.",
                choices = new DialogueChoice[0],
                nextNodeId = "",
                isEnd = true
            },

            // Redemption path
            new DialogueNode
            {
                nodeId = "return_redemption",
                speakerName = "Serna",
                dialogueText = "You... you want to help? Even after before? " +
                               "The baker's shop is east, past the old bridge. " +
                               "Thank you for giving me a second chance.",
                choices = new DialogueChoice[0],
                nextNodeId = "",
                isEnd = true
            }
        };

        string path = "Assets/Data/Dialogues/Serna_Return.asset";
        AssetDatabase.CreateAsset(dialogue, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = dialogue;

        Debug.Log($"Created Serna return dialogue at: {path}");
        Debug.Log("Demonstrates: FlagCondition gates, conditional choices, extensible actions");
    }

    [MenuItem("Karma/Create Karma Config")]
    public static void CreateKarmaConfig()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Data"))
            AssetDatabase.CreateFolder("Assets", "Data");

        KarmaConfig config = ScriptableObject.CreateInstance<KarmaConfig>();
        config.maxLevel = 7;
        config.xpPerLevel = 500;
        config.startingKarma = 0;

        string path = "Assets/Data/KarmaConfig.asset";
        AssetDatabase.CreateAsset(config, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = config;

        Debug.Log($"Created KarmaConfig asset at: {path}");
    }

    [MenuItem("Karma/Create Variable Store")]
    public static void CreateVariableStore()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var store = ScriptableObject.CreateInstance<VariableStore>();

        string path = "Assets/Resources/GameVariables.asset";
        AssetDatabase.CreateAsset(store, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = store;

        Debug.Log($"Created VariableStore at: {path}");
        Debug.Log("Access via: VariableStore.Instance.GetFlag(\"flagName\")");
    }
}
