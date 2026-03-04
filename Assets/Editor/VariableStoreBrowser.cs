using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Variable Store Browser — editor window for inspecting and editing game variables.
/// Menu: Karma > Variable Store
///
/// Displays all flags, counters, and relationships in the VariableStore
/// with inline editing, add/remove, and bulk operations (reset, save/load JSON).
///
/// Essential for testing: set flags here, then preview dialogue to see which choices unlock.
/// </summary>
public class VariableStoreBrowser : EditorWindow
{
    private VariableStore store;
    private SerializedObject serializedStore;
    private Vector2 scrollPos;

    // Add-new fields
    private string newFlagName = "";
    private string newCounterName = "";
    private string newRelationshipName = "";

    // Search
    private string searchFilter = "";

    [MenuItem("Karma/Variable Store")]
    public static void ShowWindow()
    {
        var window = GetWindow<VariableStoreBrowser>("Variable Store");
        window.minSize = new Vector2(350, 300);
    }

    void OnEnable()
    {
        FindStore();
    }

    void OnFocus()
    {
        FindStore();
    }

    private void FindStore()
    {
        // Try to find a VariableStore asset
        var guids = AssetDatabase.FindAssets("t:VariableStore");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            store = AssetDatabase.LoadAssetAtPath<VariableStore>(path);
        }

        if (store == null)
        {
            // Fallback: check Resources
            store = Resources.Load<VariableStore>("GameVariables");
        }

        if (store != null)
        {
            serializedStore = new SerializedObject(store);
            store.BuildCaches();
        }
    }

    void OnGUI()
    {
        // ─── Header ──────────────────────────────────────
        DrawToolbar();

        if (store == null)
        {
            DrawNoStoreMessage();
            return;
        }

        serializedStore.Update();

        // Search bar
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
        searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField);
        if (GUILayout.Button("x", EditorStyles.toolbarButton, GUILayout.Width(20)))
            searchFilter = "";
        EditorGUILayout.EndHorizontal();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // ─── Flags Section ───────────────────────────────
        DrawFlagsSection();

        EditorGUILayout.Space(8);

        // ─── Counters Section ────────────────────────────
        DrawCountersSection();

        EditorGUILayout.Space(8);

        // ─── Relationships Section ───────────────────────
        DrawRelationshipsSection();

        EditorGUILayout.EndScrollView();

        // ─── Bottom Controls ─────────────────────────────
        DialogueEditorStyles.DrawSeparator();
        DrawBottomControls();

        serializedStore.ApplyModifiedProperties();
    }

    // ─── Toolbar ─────────────────────────────────────────────

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField("Variable Store Browser", EditorStyles.boldLabel, GUILayout.Width(180));
        GUILayout.FlexibleSpace();

        // Asset selector
        var newStore = (VariableStore)EditorGUILayout.ObjectField(
            store, typeof(VariableStore), false, GUILayout.Width(180));
        if (newStore != store && newStore != null)
        {
            store = newStore;
            serializedStore = new SerializedObject(store);
            store.BuildCaches();
        }

        EditorGUILayout.EndHorizontal();
    }

    // ─── Flags ───────────────────────────────────────────────

    private void DrawFlagsSection()
    {
        var flagsProp = serializedStore.FindProperty("flagEntries");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("FLAGS", DialogueEditorStyles.SectionHeader);
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"({flagsProp.arraySize})", EditorStyles.miniLabel, GUILayout.Width(30));
        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < flagsProp.arraySize; i++)
        {
            var entry = flagsProp.GetArrayElementAtIndex(i);
            var keyProp = entry.FindPropertyRelative("key");
            var valueProp = entry.FindPropertyRelative("value");

            if (!PassesFilter(keyProp.stringValue)) continue;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);

            // Flag name
            EditorGUILayout.LabelField(keyProp.stringValue, GUILayout.MinWidth(150));

            // Toggle
            bool newVal = EditorGUILayout.Toggle(valueProp.boolValue, GUILayout.Width(20));
            if (newVal != valueProp.boolValue)
            {
                valueProp.boolValue = newVal;
                // Also update runtime cache
                store.SetFlag(keyProp.stringValue, newVal);
            }

            EditorGUILayout.LabelField(valueProp.boolValue ? "true" : "false",
                EditorStyles.miniLabel, GUILayout.Width(40));

            // Remove button
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f, 0.7f);
            if (GUILayout.Button("x", DialogueEditorStyles.MiniButton))
            {
                flagsProp.DeleteArrayElementAtIndex(i);
                store.BuildCaches();
                serializedStore.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
                return;
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        // Add new flag
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(12);
        newFlagName = EditorGUILayout.TextField(newFlagName, GUILayout.MinWidth(150));
        if (GUILayout.Button("+ Add Flag", EditorStyles.miniButton, GUILayout.Width(75)))
        {
            if (!string.IsNullOrEmpty(newFlagName))
            {
                store.SetFlag(newFlagName, false);
                serializedStore.Update();
                newFlagName = "";
                Repaint();
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    // ─── Counters ────────────────────────────────────────────

    private void DrawCountersSection()
    {
        var countersProp = serializedStore.FindProperty("counterEntries");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("COUNTERS", DialogueEditorStyles.SectionHeader);
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"({countersProp.arraySize})", EditorStyles.miniLabel, GUILayout.Width(30));
        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < countersProp.arraySize; i++)
        {
            var entry = countersProp.GetArrayElementAtIndex(i);
            var keyProp = entry.FindPropertyRelative("key");
            var valueProp = entry.FindPropertyRelative("value");

            if (!PassesFilter(keyProp.stringValue)) continue;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);

            EditorGUILayout.LabelField(keyProp.stringValue, GUILayout.MinWidth(150));

            int newVal = EditorGUILayout.IntField(valueProp.intValue, GUILayout.Width(60));
            if (newVal != valueProp.intValue)
            {
                valueProp.intValue = newVal;
                store.SetCounter(keyProp.stringValue, newVal);
            }

            // Quick increment/decrement
            if (GUILayout.Button("-", EditorStyles.miniButton, GUILayout.Width(20)))
            {
                valueProp.intValue--;
                store.SetCounter(keyProp.stringValue, valueProp.intValue);
            }
            if (GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(20)))
            {
                valueProp.intValue++;
                store.SetCounter(keyProp.stringValue, valueProp.intValue);
            }

            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f, 0.7f);
            if (GUILayout.Button("x", DialogueEditorStyles.MiniButton))
            {
                countersProp.DeleteArrayElementAtIndex(i);
                store.BuildCaches();
                serializedStore.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
                return;
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        // Add new counter
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(12);
        newCounterName = EditorGUILayout.TextField(newCounterName, GUILayout.MinWidth(150));
        if (GUILayout.Button("+ Add Counter", EditorStyles.miniButton, GUILayout.Width(90)))
        {
            if (!string.IsNullOrEmpty(newCounterName))
            {
                store.SetCounter(newCounterName, 0);
                serializedStore.Update();
                newCounterName = "";
                Repaint();
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    // ─── Relationships ───────────────────────────────────────

    private void DrawRelationshipsSection()
    {
        var relsProp = serializedStore.FindProperty("relationshipEntries");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("RELATIONSHIPS", DialogueEditorStyles.SectionHeader);
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"({relsProp.arraySize})", EditorStyles.miniLabel, GUILayout.Width(30));
        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < relsProp.arraySize; i++)
        {
            var entry = relsProp.GetArrayElementAtIndex(i);
            var keyProp = entry.FindPropertyRelative("key");
            var valueProp = entry.FindPropertyRelative("value");

            if (!PassesFilter(keyProp.stringValue)) continue;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);

            EditorGUILayout.LabelField(keyProp.stringValue, GUILayout.MinWidth(150));

            // Relationship bar
            int val = valueProp.intValue;
            float normalized = Mathf.Clamp01((val + 100f) / 200f); // -100 to 100 → 0 to 1
            var barRect = GUILayoutUtility.GetRect(80, 16, GUILayout.Width(80));
            EditorGUI.ProgressBar(barRect, normalized, $"{val}");

            int newVal = EditorGUILayout.IntField(val, GUILayout.Width(50));
            if (newVal != val)
            {
                valueProp.intValue = newVal;
                store.SetRelationship(keyProp.stringValue, newVal);
            }

            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f, 0.7f);
            if (GUILayout.Button("x", DialogueEditorStyles.MiniButton))
            {
                relsProp.DeleteArrayElementAtIndex(i);
                store.BuildCaches();
                serializedStore.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
                return;
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        // Add new relationship
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(12);
        newRelationshipName = EditorGUILayout.TextField(newRelationshipName, GUILayout.MinWidth(150));
        if (GUILayout.Button("+ Add Relation", EditorStyles.miniButton, GUILayout.Width(95)))
        {
            if (!string.IsNullOrEmpty(newRelationshipName))
            {
                store.SetRelationship(newRelationshipName, 0);
                serializedStore.Update();
                newRelationshipName = "";
                Repaint();
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    // ─── Bottom Controls ─────────────────────────────────────

    private void DrawBottomControls()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f, 0.8f);
        if (GUILayout.Button("Reset All", GUILayout.Height(24)))
        {
            if (EditorUtility.DisplayDialog("Reset All Variables",
                "This will clear all flags, counters, and relationships. Continue?",
                "Reset", "Cancel"))
            {
                store.ResetAll();
                serializedStore.Update();
                EditorUtility.SetDirty(store);
                Repaint();
            }
        }
        GUI.backgroundColor = Color.white;

        if (GUILayout.Button("Save Asset", GUILayout.Height(24)))
        {
            EditorUtility.SetDirty(store);
            AssetDatabase.SaveAssets();
            Debug.Log("VariableStore: Asset saved.");
        }

        if (GUILayout.Button("Ping Asset", GUILayout.Height(24)))
        {
            EditorGUIUtility.PingObject(store);
            Selection.activeObject = store;
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4);
    }

    // ─── Helpers ─────────────────────────────────────────────

    private void DrawNoStoreMessage()
    {
        EditorGUILayout.Space(30);
        EditorGUILayout.LabelField("No VariableStore asset found", EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.Space(8);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Create VariableStore", GUILayout.Width(160), GUILayout.Height(28)))
        {
            // Ensure folders exist
            if (!AssetDatabase.IsValidFolder("Assets/Data"))
                AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var newStore = ScriptableObject.CreateInstance<VariableStore>();
            AssetDatabase.CreateAsset(newStore, "Assets/Resources/GameVariables.asset");
            AssetDatabase.SaveAssets();
            store = newStore;
            serializedStore = new SerializedObject(store);
            Debug.Log("Created VariableStore at Assets/Resources/GameVariables.asset");
            Repaint();
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private bool PassesFilter(string name)
    {
        if (string.IsNullOrEmpty(searchFilter)) return true;
        return name.ToLower().Contains(searchFilter.ToLower());
    }
}
