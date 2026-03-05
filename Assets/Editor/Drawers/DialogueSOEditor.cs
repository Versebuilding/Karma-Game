using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom editor for DialogueSO ScriptableObjects.
/// Replaces the default Inspector with compact, color-coded node cards
/// showing inline conditions, actions, and choice previews.
///
/// Each node is displayed as a visual card with:
///   - Color-coded left border (green=start, red=end, orange=choices, blue=linear)
///   - Speaker name and node ID header
///   - TextArea for dialogue text
///   - Compact condition/action tag badges
///   - Expandable choice cards with style colors
/// </summary>
[CustomEditor(typeof(DialogueSO))]
public class DialogueSOEditor : Editor
{
    private DialogueSO dialogue;
    private SerializedProperty dialogueIdProp;
    private SerializedProperty nodesProp;

    // Track which nodes/choices are expanded
    private HashSet<int> expandedNodes = new HashSet<int>();
    private HashSet<string> expandedChoices = new HashSet<string>();

    // Scrolling
    private Vector2 scrollPos;

    void OnEnable()
    {
        dialogue = (DialogueSO)target;
        dialogueIdProp = serializedObject.FindProperty("dialogueId");
        nodesProp = serializedObject.FindProperty("nodes");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // ─── Header ──────────────────────────────────────────
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Dialogue Tree", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(dialogueIdProp);
        EditorGUILayout.Space(4);

        // Node count + validation
        int nodeCount = nodesProp.arraySize;
        EditorGUILayout.LabelField($"Nodes: {nodeCount}", EditorStyles.miniLabel);

        // Validate for duplicate IDs
        ValidateNodeIds();

        DialogueEditorStyles.DrawSeparator();

        // ─── Node List ───────────────────────────────────────
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        for (int i = 0; i < nodesProp.arraySize; i++)
        {
            DrawNodeCard(i);
            EditorGUILayout.Space(2);
        }

        EditorGUILayout.EndScrollView();

        // ─── Add Node Button ─────────────────────────────────
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("+ Add Node", GUILayout.Width(120), GUILayout.Height(24)))
        {
            nodesProp.InsertArrayElementAtIndex(nodesProp.arraySize);
            var newNode = nodesProp.GetArrayElementAtIndex(nodesProp.arraySize - 1);
            newNode.FindPropertyRelative("nodeId").stringValue = $"node_{nodesProp.arraySize - 1}";
            newNode.FindPropertyRelative("speakerName").stringValue = "";
            newNode.FindPropertyRelative("dialogueText").stringValue = "";
            newNode.FindPropertyRelative("nextNodeId").stringValue = "";
            newNode.FindPropertyRelative("isEnd").boolValue = false;
            expandedNodes.Add(nodesProp.arraySize - 1);
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4);

        serializedObject.ApplyModifiedProperties();
    }

    // ─── Node Card ───────────────────────────────────────────

    private void DrawNodeCard(int index)
    {
        var nodeProp = nodesProp.GetArrayElementAtIndex(index);
        string nodeId = nodeProp.FindPropertyRelative("nodeId").stringValue;
        string speaker = nodeProp.FindPropertyRelative("speakerName").stringValue;
        bool isEnd = nodeProp.FindPropertyRelative("isEnd").boolValue;
        var choicesProp = nodeProp.FindPropertyRelative("choices");
        bool hasChoices = choicesProp != null && choicesProp.arraySize > 0;
        bool isExpanded = expandedNodes.Contains(index);

        // Determine node color
        DialogueNode tempNode = null;
        if (dialogue.nodes != null && index < dialogue.nodes.Length)
            tempNode = dialogue.nodes[index];

        Color nodeColor = DialogueEditorStyles.LinearNodeColor;
        if (index == 0) nodeColor = DialogueEditorStyles.StartNodeColor;
        else if (isEnd) nodeColor = DialogueEditorStyles.EndNodeColor;
        else if (hasChoices) nodeColor = DialogueEditorStyles.ChoiceNodeColor;

        // Card background
        EditorGUILayout.BeginVertical(DialogueEditorStyles.NodeCard);

        // Left border
        var cardRect = GUILayoutUtility.GetLastRect();

        // ─── Header Row ──────────────────────────────────
        EditorGUILayout.BeginHorizontal();

        // Color dot
        var dotRect = GUILayoutUtility.GetRect(10, 16, GUILayout.Width(10));
        DialogueEditorStyles.DrawDot(dotRect, nodeColor);

        // Expand/collapse toggle
        bool newExpanded = EditorGUILayout.Foldout(isExpanded, "", true);
        if (newExpanded != isExpanded)
        {
            if (newExpanded) expandedNodes.Add(index);
            else expandedNodes.Remove(index);
        }

        // Node ID
        EditorGUILayout.LabelField(string.IsNullOrEmpty(nodeId) ? "(no id)" : nodeId,
            DialogueEditorStyles.NodeHeader, GUILayout.MinWidth(80));

        GUILayout.FlexibleSpace();

        // Speaker label
        if (!string.IsNullOrEmpty(speaker))
        {
            EditorGUILayout.LabelField($"Speaker: {speaker}", EditorStyles.miniLabel, GUILayout.Width(150));
        }

        // End badge
        if (isEnd)
        {
            var endStyle = new GUIStyle(EditorStyles.miniLabel);
            endStyle.normal.textColor = DialogueEditorStyles.EndNodeColor;
            EditorGUILayout.LabelField("[END]", endStyle, GUILayout.Width(40));
        }

        // Choice count badge
        if (hasChoices)
        {
            EditorGUILayout.LabelField($"[{choicesProp.arraySize} choices]",
                EditorStyles.miniLabel, GUILayout.Width(70));
        }

        // Delete button
        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f, 0.8f);
        if (GUILayout.Button("x", DialogueEditorStyles.MiniButton))
        {
            if (EditorUtility.DisplayDialog("Delete Node",
                $"Delete node '{nodeId}'? This cannot be undone.", "Delete", "Cancel"))
            {
                nodesProp.DeleteArrayElementAtIndex(index);
                expandedNodes.Remove(index);
                serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
                return;
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        // ─── Expanded Content ────────────────────────────
        if (isExpanded)
        {
            EditorGUI.indentLevel++;

            // Node ID + Speaker
            EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("nodeId"), new GUIContent("Node ID"));
            EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("speakerName"), new GUIContent("Speaker"));

            // Dialogue text
            EditorGUILayout.LabelField("Dialogue Text:", EditorStyles.miniLabel);
            var textProp = nodeProp.FindPropertyRelative("dialogueText");
            textProp.stringValue = EditorGUILayout.TextArea(textProp.stringValue,
                DialogueEditorStyles.DialogueText, GUILayout.MinHeight(50));

            // Voice clip (per-node audio)
            EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("voiceClip"),
                new GUIContent("Voice Clip"));

            // Per-node animation (optional override — plays on NPC until next node)
            EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("nodeAnimation"),
                new GUIContent("Node Animation"));

            EditorGUILayout.Space(4);

            // Conditions
            DrawConditionsList(nodeProp.FindPropertyRelative("conditions"), "Conditions (show node if ALL pass)");

            // OnShow Actions
            DrawActionsList(nodeProp.FindPropertyRelative("onShowActions"), "On Show Actions");

            DialogueEditorStyles.DrawSeparator();

            // Navigation
            EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("isEnd"), new GUIContent("End Node"));
            if (!isEnd)
            {
                DrawNodeIdDropdown(nodeProp.FindPropertyRelative("nextNodeId"), "Next Node");
            }

            // Choices
            if (choicesProp != null)
            {
                EditorGUILayout.Space(4);
                DrawChoicesList(choicesProp, index);
            }

            EditorGUI.indentLevel--;
        }
        else
        {
            // Collapsed preview — show first line of dialogue
            string dialogueText = nodeProp.FindPropertyRelative("dialogueText").stringValue;
            if (!string.IsNullOrEmpty(dialogueText))
            {
                string preview = dialogueText.Length > 80 ? dialogueText.Substring(0, 80) + "..." : dialogueText;
                EditorGUILayout.LabelField($"\"{preview}\"", EditorStyles.miniLabel);
            }

            // Show condition/action tags inline
            DrawInlineTagSummary(nodeProp);
        }

        EditorGUILayout.EndVertical();

        // Draw left border after card is laid out
        var finalRect = GUILayoutUtility.GetLastRect();
        DialogueEditorStyles.DrawLeftBorder(finalRect, nodeColor);
    }

    // ─── Choices List ────────────────────────────────────────

    private void DrawChoicesList(SerializedProperty choicesProp, int nodeIndex)
    {
        EditorGUILayout.LabelField("Choices", DialogueEditorStyles.SectionHeader);

        for (int i = 0; i < choicesProp.arraySize; i++)
        {
            DrawChoiceCard(choicesProp.GetArrayElementAtIndex(i), nodeIndex, i);
        }

        // Add choice button
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("+ Add Choice", EditorStyles.miniButton, GUILayout.Width(100)))
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
            newChoice.FindPropertyRelative("requiredKarmaLevel").intValue = 0;
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawChoiceCard(SerializedProperty choiceProp, int nodeIndex, int choiceIndex)
    {
        string choiceKey = $"{nodeIndex}_{choiceIndex}";
        bool isExpanded = expandedChoices.Contains(choiceKey);

        string choiceText = choiceProp.FindPropertyRelative("choiceText").stringValue;
        string inputLabel = choiceProp.FindPropertyRelative("inputLabel").stringValue;
        var styleProp = choiceProp.FindPropertyRelative("choiceStyle");
        ChoiceStyle style = (ChoiceStyle)styleProp.enumValueIndex;

        // Choice card with style-colored background
        Color bgColor = DialogueEditorStyles.GetChoiceStyleBg(style);
        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(bgColor.r + 0.5f, bgColor.g + 0.5f, bgColor.b + 0.5f, 1f);

        EditorGUILayout.BeginVertical("helpBox");
        GUI.backgroundColor = prevBg;

        // Header row
        EditorGUILayout.BeginHorizontal();

        // Expand toggle
        GUILayout.Space(15);
        bool newExpanded = EditorGUILayout.Foldout(isExpanded, "", true);
        if (newExpanded != isExpanded)
        {
            if (newExpanded) expandedChoices.Add(choiceKey);
            else expandedChoices.Remove(choiceKey);
        }

        // Input label badge
        EditorGUILayout.LabelField($"[{inputLabel}]", EditorStyles.boldLabel, GUILayout.Width(30));

        // Choice text preview
        string preview = choiceText.Length > 50 ? choiceText.Substring(0, 50) + "..." : choiceText;
        EditorGUILayout.LabelField($"\"{preview}\"", GUILayout.MinWidth(100));

        GUILayout.FlexibleSpace();

        // Style label
        EditorGUILayout.LabelField(style.ToString(), EditorStyles.miniLabel, GUILayout.Width(70));

        // Delete
        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f, 0.8f);
        if (GUILayout.Button("x", DialogueEditorStyles.MiniButton))
        {
            var parentProp = choiceProp.serializedObject.FindProperty(choiceProp.propertyPath
                .Substring(0, choiceProp.propertyPath.LastIndexOf('.')));
            // Use the parent array to delete
            // We need the parent choices array
            var nodesPropLocal = serializedObject.FindProperty("nodes");
            var nodeProp = nodesPropLocal.GetArrayElementAtIndex(nodeIndex);
            var choicesArr = nodeProp.FindPropertyRelative("choices");
            choicesArr.DeleteArrayElementAtIndex(choiceIndex);
            expandedChoices.Remove(choiceKey);
            serializedObject.ApplyModifiedProperties();
            GUIUtility.ExitGUI();
            return;
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        // Expanded content
        if (isExpanded)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(choiceProp.FindPropertyRelative("choiceText"), new GUIContent("Text"));
            EditorGUILayout.PropertyField(choiceProp.FindPropertyRelative("inputLabel"), new GUIContent("Input Key"));
            EditorGUILayout.PropertyField(choiceProp.FindPropertyRelative("choiceStyle"), new GUIContent("Style"));

            // Next node dropdown
            DrawNodeIdDropdown(choiceProp.FindPropertyRelative("nextNodeId"), "Next Node");

            EditorGUILayout.Space(2);

            // Legacy fields (collapsed)
            var karmaChange = choiceProp.FindPropertyRelative("karmaChange");
            var coinChange = choiceProp.FindPropertyRelative("coinChange");
            var reqKarma = choiceProp.FindPropertyRelative("requiredKarmaLevel");

            if (karmaChange.intValue != 0 || coinChange.intValue != 0 || reqKarma.intValue != 0)
            {
                EditorGUILayout.LabelField("Legacy Fields", EditorStyles.miniLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(karmaChange, new GUIContent("Karma Change"));
                EditorGUILayout.PropertyField(coinChange, new GUIContent("Coin Change"));
                EditorGUILayout.PropertyField(reqKarma, new GUIContent("Required Karma Lv"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(2);

            // Conditions
            DrawConditionsList(choiceProp.FindPropertyRelative("conditions"), "Conditions (choice available if ALL pass)");

            // Actions
            DrawActionsList(choiceProp.FindPropertyRelative("actions"), "Actions (on selection)");

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    // ─── Conditions/Actions Lists ────────────────────────────

    private void DrawConditionsList(SerializedProperty listProp, string label)
    {
        if (listProp == null) return;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, DialogueEditorStyles.SectionHeader);
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("+", DialogueEditorStyles.MiniButton))
        {
            var menu = DialogueTypeCache.BuildConditionMenu(cond =>
            {
                serializedObject.Update();
                listProp.InsertArrayElementAtIndex(listProp.arraySize);
                var newElem = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
                newElem.managedReferenceValue = cond;
                serializedObject.ApplyModifiedProperties();
            });
            menu.ShowAsContext();
        }
        EditorGUILayout.EndHorizontal();

        // Draw each condition inline
        for (int i = 0; i < listProp.arraySize; i++)
        {
            var elemProp = listProp.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginHorizontal();

            // Try to show label from the interface
            string condLabel = "???";
            if (elemProp.managedReferenceValue is IDialogueCondition cond)
                condLabel = cond.Label;

            DialogueEditorStyles.DrawConditionTag(condLabel);

            GUILayout.FlexibleSpace();

            // Expand to edit fields
            EditorGUILayout.PropertyField(elemProp, GUIContent.none, true, GUILayout.MinWidth(150));

            // Remove button
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f, 0.8f);
            if (GUILayout.Button("x", DialogueEditorStyles.MiniButton))
            {
                listProp.DeleteArrayElementAtIndex(i);
                serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
                return;
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        if (listProp.arraySize == 0)
        {
            EditorGUILayout.LabelField("  (none)", EditorStyles.miniLabel);
        }
    }

    private void DrawActionsList(SerializedProperty listProp, string label)
    {
        if (listProp == null) return;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, DialogueEditorStyles.SectionHeader);
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("+", DialogueEditorStyles.MiniButton))
        {
            var menu = DialogueTypeCache.BuildActionMenu(action =>
            {
                serializedObject.Update();
                listProp.InsertArrayElementAtIndex(listProp.arraySize);
                var newElem = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
                newElem.managedReferenceValue = action;
                serializedObject.ApplyModifiedProperties();
            });
            menu.ShowAsContext();
        }
        EditorGUILayout.EndHorizontal();

        // Draw each action inline
        for (int i = 0; i < listProp.arraySize; i++)
        {
            var elemProp = listProp.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginHorizontal();

            string actionLabel = "???";
            if (elemProp.managedReferenceValue is IDialogueAction action)
                actionLabel = action.Label;

            DialogueEditorStyles.DrawActionTag(actionLabel);

            GUILayout.FlexibleSpace();

            EditorGUILayout.PropertyField(elemProp, GUIContent.none, true, GUILayout.MinWidth(150));

            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f, 0.8f);
            if (GUILayout.Button("x", DialogueEditorStyles.MiniButton))
            {
                listProp.DeleteArrayElementAtIndex(i);
                serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
                return;
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        if (listProp.arraySize == 0)
        {
            EditorGUILayout.LabelField("  (none)", EditorStyles.miniLabel);
        }
    }

    // ─── Helpers ─────────────────────────────────────────────

    /// <summary>Draw a nextNodeId dropdown populated from all nodes in this dialogue.</summary>
    private void DrawNodeIdDropdown(SerializedProperty prop, string label)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(label);

        // Build options list
        var options = new List<string> { "(none)" };
        if (dialogue.nodes != null)
        {
            foreach (var node in dialogue.nodes)
            {
                if (!string.IsNullOrEmpty(node.nodeId))
                    options.Add(node.nodeId);
            }
        }

        string currentVal = prop.stringValue;
        int currentIdx = string.IsNullOrEmpty(currentVal) ? 0 : options.IndexOf(currentVal);
        if (currentIdx < 0)
        {
            // Node ID not in the list — show as text field with warning
            GUI.backgroundColor = new Color(1f, 0.9f, 0.5f, 1f);
            prop.stringValue = EditorGUILayout.TextField(prop.stringValue);
            GUI.backgroundColor = Color.white;
            EditorGUILayout.LabelField("!", EditorStyles.miniLabel, GUILayout.Width(10));
        }
        else
        {
            int newIdx = EditorGUILayout.Popup(currentIdx, options.ToArray());
            prop.stringValue = newIdx == 0 ? "" : options[newIdx];
        }

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>Draw inline condition/action tag summary for collapsed nodes.</summary>
    private void DrawInlineTagSummary(SerializedProperty nodeProp)
    {
        var condProp = nodeProp.FindPropertyRelative("conditions");
        var actionProp = nodeProp.FindPropertyRelative("onShowActions");
        bool hasCond = condProp != null && condProp.arraySize > 0;
        bool hasActions = actionProp != null && actionProp.arraySize > 0;

        if (hasCond || hasActions)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(20);

            if (hasCond)
            {
                for (int i = 0; i < condProp.arraySize; i++)
                {
                    var elem = condProp.GetArrayElementAtIndex(i);
                    if (elem.managedReferenceValue is IDialogueCondition cond)
                        DialogueEditorStyles.DrawConditionTag(cond.Label);
                }
            }
            if (hasActions)
            {
                for (int i = 0; i < actionProp.arraySize; i++)
                {
                    var elem = actionProp.GetArrayElementAtIndex(i);
                    if (elem.managedReferenceValue is IDialogueAction action)
                        DialogueEditorStyles.DrawActionTag(action.Label);
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
    }

    /// <summary>Validate for duplicate node IDs.</summary>
    private void ValidateNodeIds()
    {
        if (dialogue.nodes == null) return;

        var seen = new HashSet<string>();
        foreach (var node in dialogue.nodes)
        {
            if (string.IsNullOrEmpty(node.nodeId)) continue;
            if (!seen.Add(node.nodeId))
            {
                EditorGUILayout.HelpBox($"Duplicate node ID: '{node.nodeId}'", MessageType.Error);
            }
        }
    }
}
