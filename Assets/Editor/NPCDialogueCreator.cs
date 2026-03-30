using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tool to create Chapter 1 NPC dialogue assets, quests, and items.
///
/// Menu items:
///   Karma > Chapter 1 > Create Old Man Ghost Dialogue   (22 nodes, 3 choice points)
///   Karma > Chapter 1 > Create Ananda Intro Dialogue    (~33 nodes, callback waterfall)
///   Karma > Chapter 1 > Create Chapter 1 Quests         (Q1, Q2, Q3)
///   Karma > Chapter 1 > Create Bread Item               (ItemSO for moral choice)
///   Karma > Chapter 1 > Setup All Chapter 1 Assets      (runs all above)
/// </summary>
public class NPCDialogueCreator
{
    // ═══════════════════════════════════════════════════════════════
    //  SETUP ALL
    // ═══════════════════════════════════════════════════════════════

    [MenuItem("Karma/Chapter 1/Setup All Chapter 1 Assets")]
    public static void SetupAllChapter1()
    {
        CreateBreadItem();
        CreateChapter1Quests();
        CreateOldManGhostDialogue();
        CreateAnandaIntroDialogue();

        Debug.Log("═══════════════════════════════════════════════════");
        Debug.Log("Chapter 1 assets created successfully!");
        Debug.Log("═══════════════════════════════════════════════════");
        Debug.Log("Next steps in Unity:");
        Debug.Log("  1. Run Karma > Setup Game Systems (registers quests + InventoryManager)");
        Debug.Log("  2. Place Old Man Ghost in starting area:");
        Debug.Log("     - Add DialogueNPC component");
        Debug.Log("     - Assign OldManGhost_Intro dialogue");
        Debug.Log("  3. Place Ananda at the temple:");
        Debug.Log("     - Add DialogueNPC component");
        Debug.Log("     - Assign Ananda_Intro dialogue");
        Debug.Log("  4. Add QuestTriggerZone at temple:");
        Debug.Log("     - questId = Q1_find_ananda");
        Debug.Log("     - objectiveId = reach_temple");
        Debug.Log("═══════════════════════════════════════════════════");
    }

    // ═══════════════════════════════════════════════════════════════
    //  BREAD ITEM
    // ═══════════════════════════════════════════════════════════════

    [MenuItem("Karma/Chapter 1/Create Bread Item")]
    public static void CreateBreadItem()
    {
        EnsureFolder("Assets/Data/Items");

        ItemSO bread = ScriptableObject.CreateInstance<ItemSO>();
        bread.itemName = "Bread";
        bread.description = "A warm loaf of bread given by Ananda. Someone hungrier might need this more than you.";
        bread.category = ItemCategory.QuestItem;
        bread.isQuestItem = true;
        bread.questId = "Q3_hunger_test";
        bread.karmaOnCollect = 0;
        bread.coinValue = 0;
        bread.flavorText = "Given by Ananda at the hilltop temple.";

        SaveAsset(bread, "Assets/Data/Items/Bread.asset");
        Debug.Log("Created Bread item at Assets/Data/Items/Bread.asset");
    }

    // ═══════════════════════════════════════════════════════════════
    //  CHAPTER 1 QUESTS
    // ═══════════════════════════════════════════════════════════════

    [MenuItem("Karma/Chapter 1/Create Chapter 1 Quests")]
    public static void CreateChapter1Quests()
    {
        EnsureFolder("Assets/Data/Quests");

        // ─── Q1: Find the Seeker of Truth ───────────────────────
        QuestSO q1 = ScriptableObject.CreateInstance<QuestSO>();
        q1.questId = "Q1_find_ananda";
        q1.displayName = "Find the Seeker of Truth";
        q1.description = "The old ghost spoke of a monk named Ananda who lives at the hilltop temple. Perhaps he can help.";
        q1.questType = QuestType.Main;
        q1.objectives = new QuestObjective[]
        {
            new QuestObjective
            {
                objectiveId = "reach_temple",
                description = "Find the hilltop temple",
                type = ObjectiveType.GoTo,
                targetId = "temple_entrance",
                requiredCount = 1,
                visibility = ObjectiveVisibility.JournalVisible
            }
        };
        q1.rewards = new QuestRewards
        {
            karmaAmount = 25,
            flagsToSet = new string[] { "Q1_complete" }
        };
        q1.prerequisites = new string[0];
        q1.tags = new string[] { "chapter_1", "main_story" };

        SaveAsset(q1, "Assets/Data/Quests/Q1_FindAnanda.asset");
        Debug.Log("Created Q1: Find the Seeker of Truth");

        // ─── Q2: Observe Before You Act ─────────────────────────
        QuestSO q2 = ScriptableObject.CreateInstance<QuestSO>();
        q2.questId = "Q2_observe_self";
        q2.displayName = "Observe Before You Act";
        q2.description = "Ananda says to watch your own reactions before acting. Pay attention to how you feel.";
        q2.questType = QuestType.Main;
        q2.objectives = new QuestObjective[]
        {
            new QuestObjective
            {
                objectiveId = "observe",
                description = "Observe your reactions",
                type = ObjectiveType.Custom,
                requiredCount = 1,
                visibility = ObjectiveVisibility.Hidden
            }
        };
        q2.rewards = new QuestRewards
        {
            flagsToSet = new string[] { "Q2_complete" }
        };
        q2.prerequisites = new string[] { "Q1_find_ananda" };
        q2.tags = new string[] { "chapter_1", "main_story", "environmental" };

        SaveAsset(q2, "Assets/Data/Quests/Q2_ObserveSelf.asset");
        Debug.Log("Created Q2: Observe Before You Act");

        // ─── Q3: Hunger Returns ─────────────────────────────────
        QuestSO q3 = ScriptableObject.CreateInstance<QuestSO>();
        q3.questId = "Q3_hunger_test";
        q3.displayName = "Hunger Returns";
        q3.description = "Ananda gave you bread, but you notice someone who looks hungrier than you.";
        q3.questType = QuestType.Main;
        q3.objectives = new QuestObjective[]
        {
            new QuestObjective
            {
                objectiveId = "decide_bread",
                description = "Decide what to do with the bread",
                type = ObjectiveType.Custom,
                requiredCount = 1,
                visibility = ObjectiveVisibility.SoftHint
            }
        };
        q3.rewards = new QuestRewards
        {
            flagsToSet = new string[] { "Q3_complete" }
        };
        q3.prerequisites = new string[] { "Q1_find_ananda" };
        q3.tags = new string[] { "chapter_1", "main_story", "moral_choice" };

        SaveAsset(q3, "Assets/Data/Quests/Q3_HungerTest.asset");
        Debug.Log("Created Q3: Hunger Returns");
    }

    // ═══════════════════════════════════════════════════════════════
    //  OLD MAN GHOST DIALOGUE (22 nodes)
    // ═══════════════════════════════════════════════════════════════

    [MenuItem("Karma/Chapter 1/Create Old Man Ghost Dialogue")]
    public static void CreateOldManGhostDialogue()
    {
        EnsureFolder("Assets/Data/Dialogues");

        DialogueSO dialogue = ScriptableObject.CreateInstance<DialogueSO>();
        dialogue.dialogueId = "oldmanghost_intro";

        dialogue.nodes = new DialogueNode[]
        {
            // ─── omg_start: Ghost notices the player ─────────────
            new DialogueNode
            {
                nodeId = "omg_start",
                speakerName = "Old Man Ghost",
                dialogueText = "Oh... you can see me? Most living souls pass right through without a glance. It has been so long since anyone stopped.",
                choices = new DialogueChoice[0],
                nextNodeId = "omg_strange",
                onShowActions = new List<IDialogueAction>
                {
                    new SetFlagAction { flagName = "met_old_man_ghost", value = true }
                }
            },

            // ─── omg_strange: Ghost marvels ──────────────────────
            new DialogueNode
            {
                nodeId = "omg_strange",
                speakerName = "Old Man Ghost",
                dialogueText = "Strange... you have a warmth about you. The others here, they've forgotten what that feels like. I almost have too.",
                choices = new DialogueChoice[0],
                nextNodeId = "omg_choice1"
            },

            // ─── omg_choice1: First choice point ─────────────────
            new DialogueNode
            {
                nodeId = "omg_choice1",
                speakerName = "Old Man Ghost",
                dialogueText = "I've been trapped here, between worlds. This hunger... it's not of the body. It's something deeper. Do you understand?",
                choices = new DialogueChoice[]
                {
                    new DialogueChoice
                    {
                        choiceText = "I'm not sure, but I'm listening.",
                        inputLabel = "Z",
                        nextNodeId = "omg_r1_neutral",
                        choiceStyle = ChoiceStyle.Neutral,
                        actions = new List<IDialogueAction>
                        {
                            new SetFlagAction { flagName = "chose_neutral_npc1", value = true }
                        }
                    },
                    new DialogueChoice
                    {
                        choiceText = "That sounds painful. I'm sorry you're going through this.",
                        inputLabel = "X",
                        nextNodeId = "omg_r1_empathetic",
                        choiceStyle = ChoiceStyle.Empathetic,
                        karmaChange = 15,
                        actions = new List<IDialogueAction>
                        {
                            new ModifyKarmaAction { amount = 15 },
                            new SetFlagAction { flagName = "chose_compassion_npc1", value = true }
                        }
                    },
                    new DialogueChoice
                    {
                        choiceText = "Sounds like a you problem. Why should I care?",
                        inputLabel = "C",
                        nextNodeId = "omg_r1_harsh",
                        choiceStyle = ChoiceStyle.Selfish,
                        karmaChange = -10,
                        actions = new List<IDialogueAction>
                        {
                            new ModifyKarmaAction { amount = -10 },
                            new SetFlagAction { flagName = "chose_harsh_npc1", value = true }
                        }
                    }
                },
                nextNodeId = ""
            },

            // ─── omg_r1_neutral ──────────────────────────────────
            new DialogueNode
            {
                nodeId = "omg_r1_neutral",
                speakerName = "Old Man Ghost",
                dialogueText = "Listening... yes, that's more than most do. Perhaps that is enough for now.",
                choices = new DialogueChoice[0],
                nextNodeId = "omg_lore1"
            },

            // ─── omg_r1_empathetic ───────────────────────────────
            new DialogueNode
            {
                nodeId = "omg_r1_empathetic",
                speakerName = "Old Man Ghost",
                dialogueText = "You feel it too, don't you? That ache when you see another suffer. That is a rare gift, child. Hold onto it.",
                choices = new DialogueChoice[0],
                nextNodeId = "omg_lore1"
            },

            // ─── omg_r1_harsh ────────────────────────────────────
            new DialogueNode
            {
                nodeId = "omg_r1_harsh",
                speakerName = "Old Man Ghost",
                dialogueText = "Hmm... I once thought like that too. Before I ended up here. The world has a way of teaching us, whether we want to learn or not.",
                choices = new DialogueChoice[0],
                nextNodeId = "omg_lore1"
            },

            // ─── omg_lore1: Worldbuilding ────────────────────────
            new DialogueNode
            {
                nodeId = "omg_lore1",
                speakerName = "Old Man Ghost",
                dialogueText = "This place... they call it the Hungry Ghost Realm. Everyone here is consumed by something — greed, loneliness, regret.",
                choices = new DialogueChoice[0],
                nextNodeId = "omg_lore2"
            },

            // ─── omg_lore2 ──────────────────────────────────────
            new DialogueNode
            {
                nodeId = "omg_lore2",
                speakerName = "Old Man Ghost",
                dialogueText = "We wander with bellies that can never be filled and throats too narrow to swallow. A fitting punishment, some would say.",
                choices = new DialogueChoice[0],
                nextNodeId = "omg_lore3"
            },

            // ─── omg_lore3 ──────────────────────────────────────
            new DialogueNode
            {
                nodeId = "omg_lore3",
                speakerName = "Old Man Ghost",
                dialogueText = "But I wonder... is it punishment? Or is it a mirror? Showing us what we refused to see when we were alive?",
                choices = new DialogueChoice[0],
                nextNodeId = "omg_choice2"
            },

            // ─── omg_choice2: Second choice point ────────────────
            new DialogueNode
            {
                nodeId = "omg_choice2",
                speakerName = "Old Man Ghost",
                dialogueText = "In life, I hoarded everything — food, wealth, even kindness. I kept it all for myself. And now look at me. What do you think of that?",
                choices = new DialogueChoice[]
                {
                    new DialogueChoice
                    {
                        choiceText = "That's an interesting perspective.",
                        inputLabel = "Z",
                        nextNodeId = "omg_r2_neutral",
                        choiceStyle = ChoiceStyle.Neutral,
                        actions = new List<IDialogueAction>()
                    },
                    new DialogueChoice
                    {
                        choiceText = "It takes courage to see your own mistakes. That's the first step to freedom.",
                        inputLabel = "X",
                        nextNodeId = "omg_r2_empathetic",
                        choiceStyle = ChoiceStyle.Empathetic,
                        karmaChange = 20,
                        coinChange = 10,
                        actions = new List<IDialogueAction>
                        {
                            new ModifyKarmaAction { amount = 20 },
                            new ModifyCoinsAction { amount = 10 }
                        }
                    },
                    new DialogueChoice
                    {
                        choiceText = "What was it like, hoarding all that? Did it feel good?",
                        inputLabel = "C",
                        nextNodeId = "omg_r2_curious",
                        choiceStyle = ChoiceStyle.Neutral,
                        actions = new List<IDialogueAction>()
                    }
                },
                nextNodeId = ""
            },

            // ─── omg_r2_neutral ──────────────────────────────────
            new DialogueNode
            {
                nodeId = "omg_r2_neutral",
                speakerName = "Old Man Ghost",
                dialogueText = "Perspective... yes. That's all a ghost has left, I suppose.",
                choices = new DialogueChoice[0],
                nextNodeId = "omg_quest_hook"
            },

            // ─── omg_r2_empathetic ───────────────────────────────
            new DialogueNode
            {
                nodeId = "omg_r2_empathetic",
                speakerName = "Old Man Ghost",
                dialogueText = "Freedom... I hadn't dared to hope for that. But your words stir something I thought was long dead. Here — take these coins. I have no use for them now.",
                choices = new DialogueChoice[0],
                nextNodeId = "omg_quest_hook"
            },

            // ─── omg_r2_curious ──────────────────────────────────
            new DialogueNode
            {
                nodeId = "omg_r2_curious",
                speakerName = "Old Man Ghost",
                dialogueText = "Feel good? Ha! It felt like power. Until it didn't. Until I realized I was building a cage around myself, one coin at a time.",
                choices = new DialogueChoice[0],
                nextNodeId = "omg_quest_hook"
            },

            // ─── omg_quest_hook: Transition to quest ─────────────
            new DialogueNode
            {
                nodeId = "omg_quest_hook",
                speakerName = "Old Man Ghost",
                dialogueText = "But you... you're different. You're still alive. You can still change. There's someone who might help you understand all this better.",
                choices = new DialogueChoice[0],
                nextNodeId = "omg_ananda1"
            },

            // ─── omg_ananda1 ─────────────────────────────────────
            new DialogueNode
            {
                nodeId = "omg_ananda1",
                speakerName = "Old Man Ghost",
                dialogueText = "Up on the hill, past the old stone path, there's a temple. A monk lives there — they call him Ananda. He sees things others can't.",
                choices = new DialogueChoice[0],
                nextNodeId = "omg_ananda2"
            },

            // ─── omg_ananda2 ─────────────────────────────────────
            new DialogueNode
            {
                nodeId = "omg_ananda2",
                speakerName = "Old Man Ghost",
                dialogueText = "He helped me once, long ago. Tried to, anyway. I wasn't ready to listen then. Maybe you will be.",
                choices = new DialogueChoice[0],
                nextNodeId = "omg_choice3"
            },

            // ─── omg_choice3: Third choice point ─────────────────
            new DialogueNode
            {
                nodeId = "omg_choice3",
                speakerName = "Old Man Ghost",
                dialogueText = "Will you seek him out? Ananda, at the hilltop temple? He may have answers that I never found.",
                choices = new DialogueChoice[]
                {
                    new DialogueChoice
                    {
                        choiceText = "I'll check it out.",
                        inputLabel = "Z",
                        nextNodeId = "omg_r3_neutral",
                        choiceStyle = ChoiceStyle.Neutral,
                        actions = new List<IDialogueAction>()
                    },
                    new DialogueChoice
                    {
                        choiceText = "Of course. If there's a chance to help, I want to try.",
                        inputLabel = "X",
                        nextNodeId = "omg_r3_empathetic",
                        choiceStyle = ChoiceStyle.Empathetic,
                        karmaChange = 10,
                        actions = new List<IDialogueAction>
                        {
                            new ModifyKarmaAction { amount = 10 }
                        }
                    },
                    new DialogueChoice
                    {
                        choiceText = "Why would I waste my time on some monk?",
                        inputLabel = "C",
                        nextNodeId = "omg_r3_harsh",
                        choiceStyle = ChoiceStyle.Selfish,
                        karmaChange = -5,
                        actions = new List<IDialogueAction>
                        {
                            new ModifyKarmaAction { amount = -5 }
                        }
                    }
                },
                nextNodeId = ""
            },

            // ─── omg_r3_neutral ──────────────────────────────────
            new DialogueNode
            {
                nodeId = "omg_r3_neutral",
                speakerName = "Old Man Ghost",
                dialogueText = "Good enough. The path reveals itself to those who walk it.",
                choices = new DialogueChoice[0],
                nextNodeId = "omg_quest_start"
            },

            // ─── omg_r3_empathetic ───────────────────────────────
            new DialogueNode
            {
                nodeId = "omg_r3_empathetic",
                speakerName = "Old Man Ghost",
                dialogueText = "Your heart is in the right place, child. Ananda will see that. Go — and carry that warmth with you.",
                choices = new DialogueChoice[0],
                nextNodeId = "omg_quest_start"
            },

            // ─── omg_r3_harsh ────────────────────────────────────
            new DialogueNode
            {
                nodeId = "omg_r3_harsh",
                speakerName = "Old Man Ghost",
                dialogueText = "Because the alternative is ending up like me. But suit yourself. The temple is up the hill, if you change your mind.",
                choices = new DialogueChoice[0],
                nextNodeId = "omg_quest_start"
            },

            // ─── omg_quest_start: Start Q1 ──────────────────────
            new DialogueNode
            {
                nodeId = "omg_quest_start",
                speakerName = "Old Man Ghost",
                dialogueText = "Follow the stone path up the hill. You'll see the temple at the top. Ananda will be there — he always is.",
                choices = new DialogueChoice[0],
                nextNodeId = "omg_final",
                onShowActions = new List<IDialogueAction>
                {
                    new StartQuestAction { questId = "Q1_find_ananda" }
                }
            },

            // ─── omg_final: End ──────────────────────────────────
            new DialogueNode
            {
                nodeId = "omg_final",
                speakerName = "Old Man Ghost",
                dialogueText = "Go now. And Sammy... thank you for stopping. It's been a long time since anyone did.",
                choices = new DialogueChoice[0],
                nextNodeId = "",
                isEnd = true
            }
        };

        SaveAsset(dialogue, "Assets/Data/Dialogues/OldManGhost_Intro.asset");
        Debug.Log("Created Old Man Ghost dialogue (22 nodes, 3 choice points)");
        Debug.Log("  Flags: met_old_man_ghost, chose_compassion_npc1 / chose_neutral_npc1 / chose_harsh_npc1");
        Debug.Log("  Quest: Starts Q1_find_ananda");
    }

    // ═══════════════════════════════════════════════════════════════
    //  ANANDA INTRO DIALOGUE (~33 nodes)
    // ═══════════════════════════════════════════════════════════════

    [MenuItem("Karma/Chapter 1/Create Ananda Intro Dialogue")]
    public static void CreateAnandaIntroDialogue()
    {
        EnsureFolder("Assets/Data/Dialogues");

        // Load bread item for GiveItemAction
        ItemSO breadItem = AssetDatabase.LoadAssetAtPath<ItemSO>("Assets/Data/Items/Bread.asset");
        if (breadItem == null)
        {
            Debug.LogWarning("Bread item not found! Run 'Karma > Chapter 1 > Create Bread Item' first. Creating without item reference.");
        }

        DialogueSO dialogue = ScriptableObject.CreateInstance<DialogueSO>();
        dialogue.dialogueId = "ananda_intro";

        var nodes = new List<DialogueNode>();

        // ─── an_intro: Ananda greets the player ─────────────────
        nodes.Add(new DialogueNode
        {
            nodeId = "an_intro",
            speakerName = "Ananda",
            dialogueText = "Ah... a visitor. I felt your presence on the path before you arrived. Please, sit. You've come a long way.",
            choices = new DialogueChoice[0],
            nextNodeId = "an_pause"
        });

        // ─── an_pause: Ananda observes ──────────────────────────
        nodes.Add(new DialogueNode
        {
            nodeId = "an_pause",
            speakerName = "Ananda",
            dialogueText = "You carry something with you. Not in your hands — in your heart. I can see it in the way you move. Tell me, what brings you to this temple?",
            choices = new DialogueChoice[0],
            nextNodeId = "an_choice1"
        });

        // ─── an_choice1: First choice point ─────────────────────
        nodes.Add(new DialogueNode
        {
            nodeId = "an_choice1",
            speakerName = "Ananda",
            dialogueText = "Take your time. There is no rush here.",
            choices = new DialogueChoice[]
            {
                new DialogueChoice
                {
                    choiceText = "A ghost told me to come here. Said you could help.",
                    inputLabel = "Z",
                    nextNodeId = "an_r1_neutral",
                    choiceStyle = ChoiceStyle.Neutral,
                    actions = new List<IDialogueAction>()
                },
                new DialogueChoice
                {
                    choiceText = "I want to understand what's happening in this place. Why are the ghosts suffering?",
                    inputLabel = "X",
                    nextNodeId = "an_r1_curious",
                    choiceStyle = ChoiceStyle.Neutral,
                    actions = new List<IDialogueAction>()
                },
                new DialogueChoice
                {
                    choiceText = "I met a spirit in pain down the hill. I want to help, but I don't know how.",
                    inputLabel = "C",
                    nextNodeId = "an_r1_empathetic",
                    choiceStyle = ChoiceStyle.Empathetic,
                    karmaChange = 15,
                    actions = new List<IDialogueAction>
                    {
                        new ModifyKarmaAction { amount = 15 }
                    }
                }
            },
            nextNodeId = ""
        });

        // ─── an_r1_neutral ──────────────────────────────────────
        nodes.Add(new DialogueNode
        {
            nodeId = "an_r1_neutral",
            speakerName = "Ananda",
            dialogueText = "The old ghost sent you? Then he saw something in you worth sending. That alone tells me much.",
            choices = new DialogueChoice[0],
            nextNodeId = "an_callback_compassion"
        });

        // ─── an_r1_curious ──────────────────────────────────────
        nodes.Add(new DialogueNode
        {
            nodeId = "an_r1_curious",
            speakerName = "Ananda",
            dialogueText = "A question worth asking. Most who come here see the suffering and look away. You looked closer. That takes a certain kind of courage.",
            choices = new DialogueChoice[0],
            nextNodeId = "an_callback_compassion"
        });

        // ─── an_r1_empathetic ───────────────────────────────────
        nodes.Add(new DialogueNode
        {
            nodeId = "an_r1_empathetic",
            speakerName = "Ananda",
            dialogueText = "You felt his pain and came seeking a way to ease it. That impulse — don't lose it. It's rarer than you know.",
            choices = new DialogueChoice[0],
            nextNodeId = "an_callback_compassion"
        });

        // ═══ CALLBACK WATERFALL ═══════════════════════════════════
        // Three conditional nodes chained together.
        // ShowNode() evaluates conditions; if ANY fail, follows nextNodeId.
        // Exactly one passes based on NPC1 choice flags.
        // If player skipped NPC1 entirely, all fall through to an_mechanic1.

        // ─── an_callback_compassion (if chose_compassion_npc1) ──
        nodes.Add(new DialogueNode
        {
            nodeId = "an_callback_compassion",
            speakerName = "Ananda",
            dialogueText = "I sense you showed the old spirit compassion. That warmth you offered him — it rippled through this realm like a stone in still water. Even here, kindness echoes.",
            choices = new DialogueChoice[0],
            nextNodeId = "an_callback_neutral",
            conditions = new List<IDialogueCondition>
            {
                new FlagCondition { flagName = "chose_compassion_npc1", expectedValue = true }
            }
        });

        // ─── an_callback_neutral (if chose_neutral_npc1) ────────
        nodes.Add(new DialogueNode
        {
            nodeId = "an_callback_neutral",
            speakerName = "Ananda",
            dialogueText = "You listened to the old spirit with patience, neither rushing to comfort nor turning away. There is wisdom in simply being present. The ghosts here have forgotten what that feels like.",
            choices = new DialogueChoice[0],
            nextNodeId = "an_callback_harsh",
            conditions = new List<IDialogueCondition>
            {
                new FlagCondition { flagName = "chose_neutral_npc1", expectedValue = true }
            }
        });

        // ─── an_callback_harsh (if chose_harsh_npc1) ────────────
        nodes.Add(new DialogueNode
        {
            nodeId = "an_callback_harsh",
            speakerName = "Ananda",
            dialogueText = "The old spirit... he told me you were sharp with him. Don't be ashamed — honesty, even harsh honesty, is its own form of truth. But ask yourself: was it honesty, or was it armor?",
            choices = new DialogueChoice[0],
            nextNodeId = "an_mechanic1",
            conditions = new List<IDialogueCondition>
            {
                new FlagCondition { flagName = "chose_harsh_npc1", expectedValue = true }
            }
        });

        // ═══ END CALLBACK WATERFALL ══════════════════════════════

        // ─── an_mechanic1: Teaching begins ──────────────────────
        nodes.Add(new DialogueNode
        {
            nodeId = "an_mechanic1",
            speakerName = "Ananda",
            dialogueText = "This realm feeds on craving — on wanting what you cannot have and clinging to what you cannot keep. The ghosts here are trapped in that cycle.",
            choices = new DialogueChoice[0],
            nextNodeId = "an_mechanic2"
        });

        // ─── an_mechanic2 ───────────────────────────────────────
        nodes.Add(new DialogueNode
        {
            nodeId = "an_mechanic2",
            speakerName = "Ananda",
            dialogueText = "But here's what they've forgotten: the hunger isn't the punishment. It's the teacher. Every pang is asking the same question — what do you truly need?",
            choices = new DialogueChoice[0],
            nextNodeId = "an_choice2"
        });

        // ─── an_choice2: Second choice point ────────────────────
        nodes.Add(new DialogueNode
        {
            nodeId = "an_choice2",
            speakerName = "Ananda",
            dialogueText = "What do you think? Can suffering teach us something, or is it just... suffering?",
            choices = new DialogueChoice[]
            {
                new DialogueChoice
                {
                    choiceText = "Maybe both? It hurts, but sometimes that's how we learn.",
                    inputLabel = "Z",
                    nextNodeId = "an_r2_curious",
                    choiceStyle = ChoiceStyle.Neutral,
                    actions = new List<IDialogueAction>()
                },
                new DialogueChoice
                {
                    choiceText = "If suffering has a lesson, I want to learn it so I can help others avoid it.",
                    inputLabel = "X",
                    nextNodeId = "an_r2_empathetic",
                    choiceStyle = ChoiceStyle.Empathetic,
                    karmaChange = 10,
                    actions = new List<IDialogueAction>
                    {
                        new ModifyKarmaAction { amount = 10 }
                    }
                },
                new DialogueChoice
                {
                    choiceText = "I don't know. I just want to get through this.",
                    inputLabel = "C",
                    nextNodeId = "an_r2_neutral",
                    choiceStyle = ChoiceStyle.Neutral,
                    actions = new List<IDialogueAction>()
                }
            },
            nextNodeId = ""
        });

        // ─── an_r2_curious ──────────────────────────────────────
        nodes.Add(new DialogueNode
        {
            nodeId = "an_r2_curious",
            speakerName = "Ananda",
            dialogueText = "Both — yes. You're learning already. The pain and the wisdom are two sides of the same coin.",
            choices = new DialogueChoice[0],
            nextNodeId = "an_mechanic3"
        });

        // ─── an_r2_empathetic ───────────────────────────────────
        nodes.Add(new DialogueNode
        {
            nodeId = "an_r2_empathetic",
            speakerName = "Ananda",
            dialogueText = "To learn from your own suffering so that others might suffer less — that is the bodhisattva's path. You walk it more naturally than you know.",
            choices = new DialogueChoice[0],
            nextNodeId = "an_mechanic3"
        });

        // ─── an_r2_neutral ──────────────────────────────────────
        nodes.Add(new DialogueNode
        {
            nodeId = "an_r2_neutral",
            speakerName = "Ananda",
            dialogueText = "That honesty is a start. You don't have to understand everything at once. Just keep your eyes open.",
            choices = new DialogueChoice[0],
            nextNodeId = "an_mechanic3"
        });

        // ─── an_mechanic3: Bridge to humming ────────────────────
        nodes.Add(new DialogueNode
        {
            nodeId = "an_mechanic3",
            speakerName = "Ananda",
            dialogueText = "Let me teach you something. When the noise of this realm becomes too much — the cravings, the confusion — there is a way to find stillness.",
            choices = new DialogueChoice[0],
            nextNodeId = "an_humming"
        });

        // ─── an_humming: Teach humming mechanic ─────────────────
        nodes.Add(new DialogueNode
        {
            nodeId = "an_humming",
            speakerName = "Ananda",
            dialogueText = "Close your eyes. Breathe. And hum — a low, steady tone. Feel it in your chest. This is how you center yourself when the world pulls at you.",
            choices = new DialogueChoice[0],
            nextNodeId = "an_reflection",
            onShowActions = new List<IDialogueAction>
            {
                new SetFlagAction { flagName = "humming_unlocked", value = true }
            }
        });

        // ─── an_reflection: Before third choice ─────────────────
        nodes.Add(new DialogueNode
        {
            nodeId = "an_reflection",
            speakerName = "Ananda",
            dialogueText = "Good. Remember that feeling — that calm. You'll need it. This realm will test you in ways you don't expect. Not with monsters, but with choices.",
            choices = new DialogueChoice[0],
            nextNodeId = "an_choice3"
        });

        // ─── an_choice3: Third choice point ─────────────────────
        nodes.Add(new DialogueNode
        {
            nodeId = "an_choice3",
            speakerName = "Ananda",
            dialogueText = "Before you go further, tell me — when you face a difficult choice, what guides you?",
            choices = new DialogueChoice[]
            {
                new DialogueChoice
                {
                    choiceText = "I try to think about what's fair for everyone.",
                    inputLabel = "Z",
                    nextNodeId = "an_r3_neutral",
                    choiceStyle = ChoiceStyle.Neutral,
                    actions = new List<IDialogueAction>()
                },
                new DialogueChoice
                {
                    choiceText = "My heart. I feel what the right thing is, even when it's hard.",
                    inputLabel = "X",
                    nextNodeId = "an_r3_empathetic",
                    choiceStyle = ChoiceStyle.Empathetic,
                    karmaChange = 15,
                    actions = new List<IDialogueAction>
                    {
                        new ModifyKarmaAction { amount = 15 }
                    }
                },
                new DialogueChoice
                {
                    choiceText = "I'm curious to see what happens. Every choice reveals something.",
                    inputLabel = "C",
                    nextNodeId = "an_r3_curious",
                    choiceStyle = ChoiceStyle.Neutral,
                    actions = new List<IDialogueAction>()
                }
            },
            nextNodeId = ""
        });

        // ─── an_r3_neutral ──────────────────────────────────────
        nodes.Add(new DialogueNode
        {
            nodeId = "an_r3_neutral",
            speakerName = "Ananda",
            dialogueText = "Fairness — a noble compass. But remember: what seems fair on the surface may not address the deeper need. Look beyond the obvious.",
            choices = new DialogueChoice[0],
            nextNodeId = "an_quest_complete"
        });

        // ─── an_r3_empathetic ───────────────────────────────────
        nodes.Add(new DialogueNode
        {
            nodeId = "an_r3_empathetic",
            speakerName = "Ananda",
            dialogueText = "The heart knows what the mind argues against. Trust that voice, Sammy. It will not lead you astray — even when the path it shows is the harder one.",
            choices = new DialogueChoice[0],
            nextNodeId = "an_quest_complete"
        });

        // ─── an_r3_curious ──────────────────────────────────────
        nodes.Add(new DialogueNode
        {
            nodeId = "an_r3_curious",
            speakerName = "Ananda",
            dialogueText = "A seeker! Yes — curiosity is the first step on every worthy path. Just be mindful that seeking doesn't become avoiding. Sometimes you must choose, not just observe.",
            choices = new DialogueChoice[0],
            nextNodeId = "an_quest_complete"
        });

        // ─── an_quest_complete: Complete Q1, Start Q2 ───────────
        nodes.Add(new DialogueNode
        {
            nodeId = "an_quest_complete",
            speakerName = "Ananda",
            dialogueText = "You found me, just as the old spirit hoped. Your first journey in this realm is complete. But the real test is only beginning.",
            choices = new DialogueChoice[0],
            nextNodeId = "an_bread_give",
            onShowActions = new List<IDialogueAction>
            {
                new CompleteQuestAction { questId = "Q1_find_ananda" },
                new StartQuestAction { questId = "Q2_observe_self" }
            }
        });

        // ─── an_bread_give: Give bread + set flag ───────────────
        var breadGiveActions = new List<IDialogueAction>
        {
            new SetFlagAction { flagName = "ananda_test_started", value = true }
        };

        if (breadItem != null)
        {
            breadGiveActions.Insert(0, new GiveItemAction { item = breadItem });
        }

        nodes.Add(new DialogueNode
        {
            nodeId = "an_bread_give",
            speakerName = "Ananda",
            dialogueText = "Here — take this bread. I baked it this morning. It's warm, and it will fill your belly.",
            choices = new DialogueChoice[0],
            nextNodeId = "an_bread_explain",
            onShowActions = breadGiveActions
        });

        // ─── an_bread_explain ───────────────────────────────────
        nodes.Add(new DialogueNode
        {
            nodeId = "an_bread_explain",
            speakerName = "Ananda",
            dialogueText = "But Sammy — pay attention as you walk. You may find that you aren't the hungriest person on this path. When that moment comes, listen to what your heart tells you.",
            choices = new DialogueChoice[0],
            nextNodeId = "an_player_thought1"
        });

        // ─── an_player_thought1: Inner monologue (bottom panel) ─
        // speakerName = "Sammy" != ActiveNPCSpeakerName "Ananda"
        // → routes to DialogueUI bottom panel automatically
        nodes.Add(new DialogueNode
        {
            nodeId = "an_player_thought1",
            speakerName = "Sammy",
            dialogueText = "(The bread is warm in my hands. It smells like home — like something I'd almost forgotten.)",
            choices = new DialogueChoice[0],
            nextNodeId = "an_player_thought2"
        });

        // ─── an_player_thought2: Inner monologue continued ──────
        nodes.Add(new DialogueNode
        {
            nodeId = "an_player_thought2",
            speakerName = "Sammy",
            dialogueText = "(Ananda said to pay attention. To notice when someone needs this more than I do. I wonder what he means...)",
            choices = new DialogueChoice[0],
            nextNodeId = "an_quest_hunger"
        });

        // ─── an_quest_hunger: Start Q3 ──────────────────────────
        nodes.Add(new DialogueNode
        {
            nodeId = "an_quest_hunger",
            speakerName = "Ananda",
            dialogueText = "Now go. Walk the path ahead and observe — not just the world around you, but the world within you. We will meet again.",
            choices = new DialogueChoice[0],
            nextNodeId = "an_final",
            onShowActions = new List<IDialogueAction>
            {
                new StartQuestAction { questId = "Q3_hunger_test" }
            }
        });

        // ─── an_final: End ──────────────────────────────────────
        nodes.Add(new DialogueNode
        {
            nodeId = "an_final",
            speakerName = "Ananda",
            dialogueText = "May your steps be mindful, Sammy. And remember — the humming. It will bring you back to yourself when you need it most.",
            choices = new DialogueChoice[0],
            nextNodeId = "",
            isEnd = true
        });

        dialogue.nodes = nodes.ToArray();

        SaveAsset(dialogue, "Assets/Data/Dialogues/Ananda_Intro.asset");
        Debug.Log($"Created Ananda intro dialogue ({nodes.Count} nodes, 3 choice points + callback waterfall)");
        Debug.Log("  Callback waterfall: an_callback_compassion → an_callback_neutral → an_callback_harsh");
        Debug.Log("  Flags: humming_unlocked, ananda_test_started");
        Debug.Log("  Quests: Completes Q1_find_ananda, Starts Q2_observe_self + Q3_hunger_test");
        Debug.Log("  Items: Gives Bread via GiveItemAction");
        Debug.Log("  Inner monologue: an_player_thought1/2 (speaker: Sammy → bottom panel)");
    }

    // ═══════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════

    static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0]; // "Assets"

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    static void SaveAsset(Object asset, string path)
    {
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = asset;
    }
}
