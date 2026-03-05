using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Main Dialogue Editor Window for the Karma Content Designer Toolkit.
/// Menu: Karma > Dialogue Editor
///
/// Three-panel layout:
///   Left Panel  — Node list with connection arrows, color-coded dots, choice branches
///   Right Panel — Full editable detail for the selected node
///   Bottom-Left — Inline dialogue preview/simulator
///
/// Features:
///   - Visual node list with ● color dots (green=start, red=end, orange=choices, blue=linear)
///   - Choice branches indented (├─Z→, ├─X→, └─C→)
///   - Orphan node detection (yellow highlight)
///   - Duplicate ID warnings
///   - Node detail editing with condition/action dropdowns
///   - nextNodeId as dropdown of all node IDs
///   - Embedded dialogue preview/simulator
/// </summary>
public class DialogueEditorWindow : EditorWindow
{
    // ─── State ───────────────────────────────────────────────
    private DialogueSO currentDialogue;
    private SerializedObject serializedDialogue;
    private int selectedNodeIndex = -1;

    // Scrolling
    private Vector2 nodeListScroll;
    private Vector2 nodeDetailScroll;
    private Vector2 previewScroll;

    // Preview state
    private bool previewActive;
    private int previewNodeIndex;
    private List<string> previewLog = new List<string>();

    // Layout
    private float leftPanelWidth = 250f;
    private bool isResizing;

    [MenuItem("Karma/Dialogue Editor")]
    public static void ShowWindow()
    {
        var window = GetWindow<DialogueEditorWindow>("Dialogue Editor");
        window.minSize = new Vector2(700, 450);
    }

    void OnEnable()
    {
        // Try to load previously selected dialogue
        string guid = EditorPrefs.GetString("KarmaDialogueEditor_LastAsset", "");
        if (!string.IsNullOrEmpty(guid))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
            {
                var asset = AssetDatabase.LoadAssetAtPath<DialogueSO>(path);
                if (asset != null) SetDialogue(asset);
            }
        }
    }

    void OnSelectionChange()
    {
        // Auto-select if a DialogueSO is selected in the project
        if (Selection.activeObject is DialogueSO dialogue)
        {
            SetDialogue(dialogue);
        }
    }

    private void SetDialogue(DialogueSO dialogue)
    {
        currentDialogue = dialogue;
        serializedDialogue = new SerializedObject(dialogue);
        selectedNodeIndex = dialogue.nodes != null && dialogue.nodes.Length > 0 ? 0 : -1;
        previewActive = false;
        previewLog.Clear();

        // Remember for next session
        string path = AssetDatabase.GetAssetPath(dialogue);
        if (!string.IsNullOrEmpty(path))
            EditorPrefs.SetString("KarmaDialogueEditor_LastAsset", AssetDatabase.AssetPathToGUID(path));

        Repaint();
    }

    void OnGUI()
    {
        DrawToolbar();

        if (currentDialogue == null)
        {
            DrawNoDialogueMessage();
            return;
        }

        serializedDialogue.Update();

        EditorGUILayout.BeginHorizontal();

        // Left panel (node list + preview)
        DrawLeftPanel();

        // Resizable splitter
        DrawSplitter();

        // Right panel (node detail)
        DrawRightPanel();

        EditorGUILayout.EndHorizontal();

        serializedDialogue.ApplyModifiedProperties();
    }

    // ─── Toolbar ─────────────────────────────────────────────

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        EditorGUILayout.LabelField("Karma Dialogue Editor", EditorStyles.boldLabel, GUILayout.Width(170));

        GUILayout.FlexibleSpace();

        // Asset selector
        EditorGUILayout.LabelField("Asset:", GUILayout.Width(40));
        var newDialogue = (DialogueSO)EditorGUILayout.ObjectField(
            currentDialogue, typeof(DialogueSO), false, GUILayout.Width(200));
        if (newDialogue != currentDialogue && newDialogue != null)
        {
            SetDialogue(newDialogue);
        }

        GUILayout.Space(8);

        // Quick actions
        if (currentDialogue != null)
        {
            if (GUILayout.Button("Ping", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                EditorGUIUtility.PingObject(currentDialogue);
            }
            if (GUILayout.Button("+ Node", EditorStyles.toolbarButton, GUILayout.Width(55)))
            {
                AddNewNode();
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    // ─── Left Panel ──────────────────────────────────────────

    private void DrawLeftPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(leftPanelWidth));

        // ── Node List ────────────────────────────────────
        EditorGUILayout.LabelField("NODE LIST", DialogueEditorStyles.SectionHeader);
        DialogueEditorStyles.DrawSeparator();

        nodeListScroll = EditorGUILayout.BeginScrollView(nodeListScroll, GUILayout.ExpandHeight(true));

        if (currentDialogue.nodes != null)
        {
            // Build set of referenced node IDs for orphan detection
            var referencedIds = BuildReferencedNodeIds();

            for (int i = 0; i < currentDialogue.nodes.Length; i++)
            {
                DrawNodeListEntry(i, referencedIds);
            }
        }

        EditorGUILayout.EndScrollView();

        // ── Preview Panel ────────────────────────────────
        DialogueEditorStyles.DrawSeparator();
        DrawPreviewPanel();

        EditorGUILayout.EndVertical();
    }

    private void DrawNodeListEntry(int index, HashSet<string> referencedIds)
    {
        var node = currentDialogue.nodes[index];
        bool isSelected = index == selectedNodeIndex;
        bool isOrphan = index > 0 && !string.IsNullOrEmpty(node.nodeId) &&
                        !referencedIds.Contains(node.nodeId);

        // Node color
        Color dotColor = DialogueEditorStyles.GetNodeColor(node, index == 0);
        if (isOrphan) dotColor = DialogueEditorStyles.OrphanNodeColor;

        // Selection highlight
        if (isSelected)
        {
            var selRect = EditorGUILayout.BeginHorizontal("selectionRect");
            GUI.backgroundColor = Color.white;
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
        }

        // Dot
        GUILayout.Space(4);
        var dotRect = GUILayoutUtility.GetRect(8, 16, GUILayout.Width(8));
        DialogueEditorStyles.DrawDot(dotRect, dotColor);
        GUILayout.Space(4);

        // Node label
        string label = string.IsNullOrEmpty(node.nodeId) ? $"(node {index})" : node.nodeId;
        var labelStyle = isSelected ? EditorStyles.boldLabel : EditorStyles.label;

        if (GUILayout.Button(label, labelStyle))
        {
            selectedNodeIndex = index;
        }

        GUILayout.FlexibleSpace();

        // Orphan warning
        if (isOrphan)
        {
            EditorGUILayout.LabelField("!", EditorStyles.miniLabel, GUILayout.Width(10));
        }

        EditorGUILayout.EndHorizontal();

        // Show choice branches
        if (node.HasChoices)
        {
            for (int c = 0; c < node.choices.Length; c++)
            {
                var choice = node.choices[c];
                bool isLast = c == node.choices.Length - 1;
                string prefix = isLast ? "  └─" : "  ├─";
                string inputKey = string.IsNullOrEmpty(choice.inputLabel) ? "?" : choice.inputLabel;
                string target = string.IsNullOrEmpty(choice.nextNodeId) ? "(end)" : choice.nextNodeId;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);

                Color styleColor = DialogueEditorStyles.GetChoiceStyleBorder(choice.choiceStyle);
                var prevColor = GUI.contentColor;
                GUI.contentColor = styleColor;
                EditorGUILayout.LabelField($"{prefix}{inputKey}→ {target}", EditorStyles.miniLabel);
                GUI.contentColor = prevColor;

                EditorGUILayout.EndHorizontal();
            }
        }
        else if (!node.isEnd && !string.IsNullOrEmpty(node.nextNodeId))
        {
            // Show linear connection
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(20);
            EditorGUILayout.LabelField($"  → {node.nextNodeId}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }
    }

    // ─── Right Panel ─────────────────────────────────────────

    private void DrawRightPanel()
    {
        EditorGUILayout.BeginVertical();

        if (selectedNodeIndex < 0 || currentDialogue.nodes == null ||
            selectedNodeIndex >= currentDialogue.nodes.Length)
        {
            EditorGUILayout.LabelField("Select a node from the list", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndVertical();
            return;
        }

        var nodesProp = serializedDialogue.FindProperty("nodes");
        var nodeProp = nodesProp.GetArrayElementAtIndex(selectedNodeIndex);
        var node = currentDialogue.nodes[selectedNodeIndex];

        EditorGUILayout.LabelField("NODE DETAIL", DialogueEditorStyles.SectionHeader);
        DialogueEditorStyles.DrawSeparator();

        nodeDetailScroll = EditorGUILayout.BeginScrollView(nodeDetailScroll);

        // ── Basic Info ───────────────────────────────────
        EditorGUILayout.BeginVertical(DialogueEditorStyles.NodeCard);

        Color nodeColor = DialogueEditorStyles.GetNodeColor(node, selectedNodeIndex == 0);
        var cardRect = GUILayoutUtility.GetLastRect();

        EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("nodeId"), new GUIContent("Node ID"));
        EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("speakerName"), new GUIContent("Speaker"));

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Dialogue Text:", EditorStyles.miniLabel);
        var textProp = nodeProp.FindPropertyRelative("dialogueText");
        textProp.stringValue = EditorGUILayout.TextArea(textProp.stringValue,
            DialogueEditorStyles.DialogueText, GUILayout.MinHeight(60));

        // Voice clip + Animation (per-node media)
        EditorGUILayout.Space(4);
        EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("voiceClip"),
            new GUIContent("Voice Clip"));
        EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("nodeAnimation"),
            new GUIContent("Node Animation"));

        EditorGUILayout.EndVertical();

        // Draw left border
        var finalCardRect = GUILayoutUtility.GetLastRect();
        DialogueEditorStyles.DrawLeftBorder(finalCardRect, nodeColor);

        EditorGUILayout.Space(4);

        // ── Conditions ──────────────────────────────────
        DrawConditionSection(nodeProp.FindPropertyRelative("conditions"), "Conditions (show node if ALL pass)");

        // ── OnShow Actions ──────────────────────────────
        DrawActionSection(nodeProp.FindPropertyRelative("onShowActions"), "On Show Actions");

        DialogueEditorStyles.DrawSeparator();

        // ── Navigation ──────────────────────────────────
        EditorGUILayout.LabelField("Navigation", DialogueEditorStyles.SectionHeader);
        EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("isEnd"), new GUIContent("End Node"));

        if (!node.isEnd)
        {
            DrawNodeIdDropdown(nodeProp.FindPropertyRelative("nextNodeId"), "Next Node");
        }

        DialogueEditorStyles.DrawSeparator();

        // ── Choices ─────────────────────────────────────
        var choicesProp = nodeProp.FindPropertyRelative("choices");
        EditorGUILayout.LabelField($"Choices ({choicesProp.arraySize})", DialogueEditorStyles.SectionHeader);

        for (int i = 0; i < choicesProp.arraySize; i++)
        {
            DrawChoiceDetail(choicesProp.GetArrayElementAtIndex(i), i, choicesProp);
        }

        // Add choice button
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("+ Add Choice", GUILayout.Width(120)))
        {
            choicesProp.InsertArrayElementAtIndex(choicesProp.arraySize);
            var newChoice = choicesProp.GetArrayElementAtIndex(choicesProp.arraySize - 1);
            newChoice.FindPropertyRelative("choiceText").stringValue = "New choice...";
            string[] labels = { "Z", "X", "C" };
            int labelIdx = Mathf.Min(choicesProp.arraySize - 1, labels.Length - 1);
            newChoice.FindPropertyRelative("inputLabel").stringValue = labels[labelIdx];
            newChoice.FindPropertyRelative("nextNodeId").stringValue = "";
            newChoice.FindPropertyRelative("karmaChange").intValue = 0;
            newChoice.FindPropertyRelative("coinChange").intValue = 0;
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // ── Danger Zone ─────────────────────────────────
        DialogueEditorStyles.DrawSeparator();
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f, 0.8f);
        if (GUILayout.Button("Delete This Node", GUILayout.Width(130)))
        {
            string nodeId = node.nodeId;
            if (EditorUtility.DisplayDialog("Delete Node",
                $"Delete node '{nodeId}'? This cannot be undone.", "Delete", "Cancel"))
            {
                nodesProp.DeleteArrayElementAtIndex(selectedNodeIndex);
                selectedNodeIndex = Mathf.Max(0, selectedNodeIndex - 1);
                serializedDialogue.ApplyModifiedProperties();
                Repaint();
            }
        }
        GUI.backgroundColor = Color.white;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawChoiceDetail(SerializedProperty choiceProp, int index, SerializedProperty choicesArr)
    {
        var styleProp = choiceProp.FindPropertyRelative("choiceStyle");
        ChoiceStyle style = (ChoiceStyle)styleProp.enumValueIndex;
        Color bgColor = DialogueEditorStyles.GetChoiceStyleBg(style);
        Color borderColor = DialogueEditorStyles.GetChoiceStyleBorder(style);

        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(bgColor.r + 0.5f, bgColor.g + 0.5f, bgColor.b + 0.5f, 1f);
        EditorGUILayout.BeginVertical("helpBox");
        GUI.backgroundColor = prevBg;

        // Header
        EditorGUILayout.BeginHorizontal();
        string inputLabel = choiceProp.FindPropertyRelative("inputLabel").stringValue;
        EditorGUILayout.LabelField($"[{inputLabel}] Choice {index + 1}", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f, 0.8f);
        if (GUILayout.Button("x", DialogueEditorStyles.MiniButton))
        {
            choicesArr.DeleteArrayElementAtIndex(index);
            serializedDialogue.ApplyModifiedProperties();
            GUIUtility.ExitGUI();
            return;
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        // Fields
        EditorGUILayout.PropertyField(choiceProp.FindPropertyRelative("choiceText"), new GUIContent("Text"));
        EditorGUILayout.PropertyField(choiceProp.FindPropertyRelative("inputLabel"), new GUIContent("Input Key"));
        EditorGUILayout.PropertyField(choiceProp.FindPropertyRelative("choiceStyle"), new GUIContent("Style"));
        DrawNodeIdDropdown(choiceProp.FindPropertyRelative("nextNodeId"), "Next Node");

        // Legacy fields
        var karmaChange = choiceProp.FindPropertyRelative("karmaChange");
        var coinChange = choiceProp.FindPropertyRelative("coinChange");
        if (karmaChange.intValue != 0 || coinChange.intValue != 0)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Legacy", EditorStyles.miniLabel);
            EditorGUILayout.PropertyField(karmaChange, new GUIContent("Karma Change"));
            EditorGUILayout.PropertyField(coinChange, new GUIContent("Coin Change"));
            EditorGUILayout.PropertyField(choiceProp.FindPropertyRelative("requiredKarmaLevel"),
                new GUIContent("Required Karma Lv"));
        }

        EditorGUILayout.Space(2);

        // Conditions & Actions
        DrawConditionSection(choiceProp.FindPropertyRelative("conditions"), "Conditions");
        DrawActionSection(choiceProp.FindPropertyRelative("actions"), "Actions");

        EditorGUILayout.EndVertical();

        // Draw left border
        var rect = GUILayoutUtility.GetLastRect();
        DialogueEditorStyles.DrawLeftBorder(rect, borderColor, 3f);

        EditorGUILayout.Space(2);
    }

    // ─── Condition/Action Sections ───────────────────────────

    private void DrawConditionSection(SerializedProperty listProp, string label)
    {
        if (listProp == null) return;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, DialogueEditorStyles.SectionHeader);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("+", DialogueEditorStyles.MiniButton))
        {
            var menu = DialogueTypeCache.BuildConditionMenu(cond =>
            {
                serializedDialogue.Update();
                listProp.InsertArrayElementAtIndex(listProp.arraySize);
                listProp.GetArrayElementAtIndex(listProp.arraySize - 1).managedReferenceValue = cond;
                serializedDialogue.ApplyModifiedProperties();
                Repaint();
            });
            menu.ShowAsContext();
        }
        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < listProp.arraySize; i++)
        {
            var elemProp = listProp.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16);

            string condLabel = elemProp.managedReferenceValue is IDialogueCondition c ? c.Label : "???";
            DialogueEditorStyles.DrawConditionTag(condLabel);

            // Editable fields
            EditorGUILayout.PropertyField(elemProp, GUIContent.none, true, GUILayout.MinWidth(120));

            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f, 0.8f);
            if (GUILayout.Button("x", DialogueEditorStyles.MiniButton))
            {
                listProp.DeleteArrayElementAtIndex(i);
                serializedDialogue.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
                return;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        if (listProp.arraySize == 0)
            EditorGUILayout.LabelField("  (none)", EditorStyles.miniLabel);
    }

    private void DrawActionSection(SerializedProperty listProp, string label)
    {
        if (listProp == null) return;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, DialogueEditorStyles.SectionHeader);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("+", DialogueEditorStyles.MiniButton))
        {
            var menu = DialogueTypeCache.BuildActionMenu(action =>
            {
                serializedDialogue.Update();
                listProp.InsertArrayElementAtIndex(listProp.arraySize);
                listProp.GetArrayElementAtIndex(listProp.arraySize - 1).managedReferenceValue = action;
                serializedDialogue.ApplyModifiedProperties();
                Repaint();
            });
            menu.ShowAsContext();
        }
        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < listProp.arraySize; i++)
        {
            var elemProp = listProp.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16);

            string actionLabel = elemProp.managedReferenceValue is IDialogueAction a ? a.Label : "???";
            DialogueEditorStyles.DrawActionTag(actionLabel);

            EditorGUILayout.PropertyField(elemProp, GUIContent.none, true, GUILayout.MinWidth(120));

            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f, 0.8f);
            if (GUILayout.Button("x", DialogueEditorStyles.MiniButton))
            {
                listProp.DeleteArrayElementAtIndex(i);
                serializedDialogue.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
                return;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        if (listProp.arraySize == 0)
            EditorGUILayout.LabelField("  (none)", EditorStyles.miniLabel);
    }

    // ─── Preview Panel ───────────────────────────────────────

    private void DrawPreviewPanel()
    {
        EditorGUILayout.LabelField("PREVIEW", DialogueEditorStyles.SectionHeader);

        if (!previewActive)
        {
            if (GUILayout.Button("Start Preview", GUILayout.Height(24)))
            {
                previewActive = true;
                previewNodeIndex = 0;
                previewLog.Clear();
                previewLog.Add("── Preview Started ──");
            }
        }
        else
        {
            previewScroll = EditorGUILayout.BeginScrollView(previewScroll, GUILayout.Height(180));

            if (currentDialogue.nodes != null && previewNodeIndex >= 0 &&
                previewNodeIndex < currentDialogue.nodes.Length)
            {
                var node = currentDialogue.nodes[previewNodeIndex];

                // Speaker + text
                if (!string.IsNullOrEmpty(node.speakerName))
                {
                    EditorGUILayout.LabelField(node.speakerName, EditorStyles.boldLabel);
                }
                EditorGUILayout.LabelField(node.dialogueText, DialogueEditorStyles.PreviewBubble);

                EditorGUILayout.Space(4);

                // Choices
                if (node.HasChoices)
                {
                    foreach (var choice in node.choices)
                    {
                        // Check conditions
                        bool available = true;
                        string lockReason = "";
                        if (choice.conditions != null)
                        {
                            foreach (var cond in choice.conditions)
                            {
                                if (cond != null && !cond.Evaluate())
                                {
                                    available = false;
                                    lockReason = cond.Label;
                                    break;
                                }
                            }
                        }
                        if (choice.requiredKarmaLevel > 0)
                        {
                            int currentLevel = KarmaManager.Instance != null ? KarmaManager.Instance.CurrentLevel : 0;
                            if (currentLevel < choice.requiredKarmaLevel)
                            {
                                available = false;
                                lockReason = $"Karma Lv ≥ {choice.requiredKarmaLevel}";
                            }
                        }

                        GUI.enabled = available;
                        Color styleColor = DialogueEditorStyles.GetChoiceStyleBorder(choice.choiceStyle);
                        var prevColor = GUI.contentColor;
                        GUI.contentColor = available ? styleColor : Color.gray;

                        string btnLabel = $"[{choice.inputLabel}] {choice.choiceText}";
                        if (!available) btnLabel += $" (Locked: {lockReason})";

                        if (GUILayout.Button(btnLabel, GUILayout.Height(22)))
                        {
                            previewLog.Add($"  [{choice.inputLabel}] {choice.choiceText}");

                            // Execute actions in preview
                            if (choice.actions != null)
                            {
                                foreach (var action in choice.actions)
                                {
                                    if (action != null)
                                    {
                                        previewLog.Add($"    → {action.Label}");
                                    }
                                }
                            }

                            // Navigate
                            if (!string.IsNullOrEmpty(choice.nextNodeId))
                            {
                                int nextIdx = FindNodeIndex(choice.nextNodeId);
                                if (nextIdx >= 0)
                                {
                                    previewNodeIndex = nextIdx;
                                    previewLog.Add($"  → {choice.nextNodeId}");
                                }
                                else
                                {
                                    previewLog.Add($"  ✗ Node '{choice.nextNodeId}' not found!");
                                    previewActive = false;
                                }
                            }
                            else
                            {
                                previewLog.Add("  ── End ──");
                                previewActive = false;
                            }
                            Repaint();
                        }

                        GUI.contentColor = prevColor;
                        GUI.enabled = true;
                    }
                }
                else
                {
                    // Auto-advance / end node
                    if (node.isEnd || string.IsNullOrEmpty(node.nextNodeId))
                    {
                        EditorGUILayout.LabelField("[End of dialogue]", EditorStyles.centeredGreyMiniLabel);
                        if (GUILayout.Button("Restart", EditorStyles.miniButton))
                        {
                            previewNodeIndex = 0;
                            previewLog.Add("── Restarted ──");
                            Repaint();
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("Continue →", GUILayout.Height(22)))
                        {
                            int nextIdx = FindNodeIndex(node.nextNodeId);
                            if (nextIdx >= 0)
                            {
                                previewNodeIndex = nextIdx;
                                previewLog.Add($"  → {node.nextNodeId}");
                            }
                            else
                            {
                                previewLog.Add($"  ✗ '{node.nextNodeId}' not found!");
                                previewActive = false;
                            }
                            Repaint();
                        }
                    }
                }
            }

            EditorGUILayout.EndScrollView();

            // Controls
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                previewNodeIndex = 0;
                previewLog.Clear();
                previewLog.Add("── Preview Reset ──");
                Repaint();
            }
            if (GUILayout.Button("Stop", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                previewActive = false;
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    // ─── Splitter ────────────────────────────────────────────

    private void DrawSplitter()
    {
        var splitterRect = GUILayoutUtility.GetRect(4, 4, GUILayout.ExpandHeight(true), GUILayout.Width(4));
        EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);
        EditorGUI.DrawRect(splitterRect, DialogueEditorStyles.SeparatorColor);

        if (Event.current.type == EventType.MouseDown && splitterRect.Contains(Event.current.mousePosition))
        {
            isResizing = true;
            Event.current.Use();
        }
        if (isResizing)
        {
            if (Event.current.type == EventType.MouseDrag)
            {
                leftPanelWidth = Mathf.Clamp(Event.current.mousePosition.x, 180, position.width - 300);
                Repaint();
            }
            if (Event.current.type == EventType.MouseUp)
            {
                isResizing = false;
            }
        }
    }

    // ─── Helpers ─────────────────────────────────────────────

    private void DrawNoDialogueMessage()
    {
        EditorGUILayout.Space(40);
        EditorGUILayout.LabelField("No dialogue asset selected", EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Select a DialogueSO asset in the Project window,",
            EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.LabelField("or use the Asset field in the toolbar above.",
            EditorStyles.centeredGreyMiniLabel);

        EditorGUILayout.Space(16);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Create New Dialogue", GUILayout.Width(160), GUILayout.Height(28)))
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Dialogue", "NewDialogue", "asset", "Create a new dialogue asset");
            if (!string.IsNullOrEmpty(path))
            {
                var newDialogue = ScriptableObject.CreateInstance<DialogueSO>();
                newDialogue.dialogueId = System.IO.Path.GetFileNameWithoutExtension(path);
                newDialogue.nodes = new DialogueNode[]
                {
                    new DialogueNode
                    {
                        nodeId = "start",
                        speakerName = "NPC",
                        dialogueText = "Hello! This is a new dialogue.",
                        choices = new DialogueChoice[0],
                        nextNodeId = "",
                        isEnd = true
                    }
                };
                AssetDatabase.CreateAsset(newDialogue, path);
                AssetDatabase.SaveAssets();
                SetDialogue(newDialogue);
            }
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void AddNewNode()
    {
        serializedDialogue.Update();
        var nodesProp = serializedDialogue.FindProperty("nodes");
        nodesProp.InsertArrayElementAtIndex(nodesProp.arraySize);
        var newNode = nodesProp.GetArrayElementAtIndex(nodesProp.arraySize - 1);
        newNode.FindPropertyRelative("nodeId").stringValue = $"node_{nodesProp.arraySize - 1}";
        newNode.FindPropertyRelative("speakerName").stringValue = "";
        newNode.FindPropertyRelative("dialogueText").stringValue = "";
        newNode.FindPropertyRelative("nextNodeId").stringValue = "";
        newNode.FindPropertyRelative("isEnd").boolValue = false;
        serializedDialogue.ApplyModifiedProperties();
        selectedNodeIndex = currentDialogue.nodes.Length - 1;
        Repaint();
    }

    private void DrawNodeIdDropdown(SerializedProperty prop, string label)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(label);

        var options = new List<string> { "(none)" };
        if (currentDialogue.nodes != null)
        {
            foreach (var n in currentDialogue.nodes)
            {
                if (!string.IsNullOrEmpty(n.nodeId))
                    options.Add(n.nodeId);
            }
        }

        string currentVal = prop.stringValue;
        int currentIdx = string.IsNullOrEmpty(currentVal) ? 0 : options.IndexOf(currentVal);
        if (currentIdx < 0)
        {
            GUI.backgroundColor = new Color(1f, 0.9f, 0.5f, 1f);
            prop.stringValue = EditorGUILayout.TextField(prop.stringValue);
            GUI.backgroundColor = Color.white;
        }
        else
        {
            int newIdx = EditorGUILayout.Popup(currentIdx, options.ToArray());
            prop.stringValue = newIdx == 0 ? "" : options[newIdx];
        }

        EditorGUILayout.EndHorizontal();
    }

    private int FindNodeIndex(string nodeId)
    {
        if (currentDialogue.nodes == null || string.IsNullOrEmpty(nodeId)) return -1;
        for (int i = 0; i < currentDialogue.nodes.Length; i++)
        {
            if (currentDialogue.nodes[i].nodeId == nodeId)
                return i;
        }
        return -1;
    }

    private HashSet<string> BuildReferencedNodeIds()
    {
        var refs = new HashSet<string>();
        if (currentDialogue.nodes == null) return refs;

        foreach (var node in currentDialogue.nodes)
        {
            if (!string.IsNullOrEmpty(node.nextNodeId))
                refs.Add(node.nextNodeId);

            if (node.choices != null)
            {
                foreach (var choice in node.choices)
                {
                    if (!string.IsNullOrEmpty(choice.nextNodeId))
                        refs.Add(choice.nextNodeId);
                }
            }
        }
        return refs;
    }
}
