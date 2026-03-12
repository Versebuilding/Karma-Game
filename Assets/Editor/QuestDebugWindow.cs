using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Quest Debug Console — editor window for inspecting and manipulating quest state at runtime.
/// Menu: Karma > Quest Debug Console
///
/// Features:
///   - All quests listed with current state + progress bars per objective
///   - Force quest stage (change any quest to any state)
///   - Complete objective (instantly fill progress)
///   - Progression blocker finder (show why a quest is Locked)
///   - Event history log (recent quest events)
///   - VariableStore quick-view (flags set by quests)
///
/// Only functional in Play Mode (reads from QuestManager.Instance).
/// </summary>
public class QuestDebugWindow : EditorWindow
{
    // ─── State ──────────────────────────────────────────────────
    private Vector2 scrollPos;
    private string searchFilter = "";
    private bool showEventHistory = true;
    private bool showVariableStore = false;
    private List<string> eventLog = new List<string>();
    private bool isSubscribed;

    // ─── Quest filter tabs ──────────────────────────────────────
    private int selectedTab; // 0=All, 1=Active, 2=Locked, 3=Done
    private static readonly string[] tabNames = { "All", "Active", "Locked", "Done/Failed" };

    [MenuItem("Karma/Quest Debug Console")]
    public static void ShowWindow()
    {
        var window = GetWindow<QuestDebugWindow>("Quest Debug");
        window.minSize = new Vector2(420, 400);
    }

    void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        TrySubscribe();
    }

    void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        Unsubscribe();
    }

    void Update()
    {
        // Auto-refresh in play mode
        if (Application.isPlaying)
            Repaint();
    }

    private void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            eventLog.Clear();
            TrySubscribe();
        }
        else if (change == PlayModeStateChange.ExitingPlayMode)
        {
            Unsubscribe();
        }
    }

    // ─── Event Subscription ─────────────────────────────────────

    private void TrySubscribe()
    {
        if (isSubscribed || !Application.isPlaying) return;
        if (QuestManager.Instance == null) return;

        QuestManager.Instance.OnQuestStarted += LogQuestStarted;
        QuestManager.Instance.OnObjectiveUpdated += LogObjectiveUpdated;
        QuestManager.Instance.OnQuestCompleted += LogQuestCompleted;
        QuestManager.Instance.OnQuestFailed += LogQuestFailed;
        QuestManager.Instance.OnObjectiveFailed += LogObjectiveFailed;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed) return;
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestStarted -= LogQuestStarted;
            QuestManager.Instance.OnObjectiveUpdated -= LogObjectiveUpdated;
            QuestManager.Instance.OnQuestCompleted -= LogQuestCompleted;
            QuestManager.Instance.OnQuestFailed -= LogQuestFailed;
            QuestManager.Instance.OnObjectiveFailed -= LogObjectiveFailed;
        }
        isSubscribed = false;
    }

    private void LogQuestStarted(string id) =>
        eventLog.Insert(0, $"[{Time.time:F1}] STARTED: {id}");
    private void LogObjectiveUpdated(string qId, string oId, int cur, int req) =>
        eventLog.Insert(0, $"[{Time.time:F1}] OBJECTIVE: {qId}.{oId} = {cur}/{req}");
    private void LogQuestCompleted(string id) =>
        eventLog.Insert(0, $"[{Time.time:F1}] COMPLETED: {id}");
    private void LogQuestFailed(string id) =>
        eventLog.Insert(0, $"[{Time.time:F1}] FAILED: {id}");
    private void LogObjectiveFailed(string qId, string oId, bool retry) =>
        eventLog.Insert(0, $"[{Time.time:F1}] OBJ FAILED: {qId}.{oId} (retry={retry})");

    // ─── GUI ────────────────────────────────────────────────────

    void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Quest Debug Console is only available in Play Mode.", MessageType.Info);
            return;
        }

        if (QuestManager.Instance == null)
        {
            EditorGUILayout.HelpBox("QuestManager.Instance is null. Run 'Karma > Setup Game Systems' and ensure QuestManager exists.", MessageType.Warning);
            TrySubscribe();
            return;
        }

        TrySubscribe();

        // ─── Toolbar ─────────────────────────────────────────
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Reset All", EditorStyles.toolbarButton, GUILayout.Width(70)))
        {
            QuestManager.Instance.ResetAllQuests();
            eventLog.Insert(0, $"[{Time.time:F1}] RESET ALL QUESTS");
        }
        EditorGUILayout.EndHorizontal();

        // ─── Tab Bar ─────────────────────────────────────────
        selectedTab = GUILayout.Toolbar(selectedTab, tabNames);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // ─── Quest List ──────────────────────────────────────
        DrawQuestList();

        EditorGUILayout.Space(16);

        // ─── Event History ───────────────────────────────────
        showEventHistory = EditorGUILayout.Foldout(showEventHistory, $"Event History ({eventLog.Count})", true);
        if (showEventHistory)
            DrawEventHistory();

        EditorGUILayout.Space(8);

        // ─── VariableStore Quick View ────────────────────────
        showVariableStore = EditorGUILayout.Foldout(showVariableStore, "VariableStore Flags", true);
        if (showVariableStore)
            DrawVariableStore();

        EditorGUILayout.EndScrollView();
    }

    // ─── Quest List ─────────────────────────────────────────────

    private void DrawQuestList()
    {
        var registry = QuestManager.Instance.GetQuestRegistry();
        var states = QuestManager.Instance.GetAllQuestStates();

        foreach (var kvp in registry)
        {
            var def = kvp.Value;
            var questId = kvp.Key;

            // Search filter
            if (!string.IsNullOrEmpty(searchFilter))
            {
                bool match = questId.ToLower().Contains(searchFilter.ToLower()) ||
                             def.displayName.ToLower().Contains(searchFilter.ToLower());
                if (def.tags != null)
                    foreach (var tag in def.tags)
                        if (tag.ToLower().Contains(searchFilter.ToLower())) match = true;
                if (!match) continue;
            }

            var state = QuestManager.Instance.GetQuestState(questId);

            // Tab filter
            switch (selectedTab)
            {
                case 1: if (state != QuestState.Active) continue; break;
                case 2: if (state != QuestState.Locked && state != QuestState.Available) continue; break;
                case 3: if (state != QuestState.Done && state != QuestState.Failed) continue; break;
            }

            DrawQuestEntry(def, questId, state, states.ContainsKey(questId) ? states[questId] : null);
        }
    }

    private void DrawQuestEntry(QuestSO def, string questId, QuestState state, QuestRuntimeState runtimeState)
    {
        // State color
        Color stateColor = state switch
        {
            QuestState.Active => new Color(0.3f, 0.8f, 0.3f),
            QuestState.Completed => new Color(0.3f, 0.6f, 1f),
            QuestState.Done => new Color(0.6f, 0.6f, 0.6f),
            QuestState.Failed => new Color(1f, 0.3f, 0.3f),
            QuestState.Available => new Color(1f, 0.8f, 0.3f),
            _ => new Color(0.5f, 0.5f, 0.5f)
        };

        EditorGUILayout.BeginVertical("box");

        // ─── Header ──────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();

        // State badge
        var prevColor = GUI.color;
        GUI.color = stateColor;
        GUILayout.Label($"[{state}]", EditorStyles.boldLabel, GUILayout.Width(90));
        GUI.color = prevColor;

        // Quest name + type
        GUILayout.Label($"{def.displayName}", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        GUILayout.Label($"({def.questType})", EditorStyles.miniLabel);

        EditorGUILayout.EndHorizontal();

        // Tags
        if (def.tags != null && def.tags.Length > 0)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16);
            GUILayout.Label("Tags:", EditorStyles.miniLabel, GUILayout.Width(35));
            GUILayout.Label(string.Join(", ", def.tags), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        // ─── Objectives ──────────────────────────────────────
        if (def.objectives != null && def.objectives.Length > 0)
        {
            EditorGUI.indentLevel++;
            foreach (var obj in def.objectives)
            {
                int current = runtimeState?.GetProgress(obj.objectiveId) ?? 0;
                float progress = (float)current / obj.requiredCount;
                string statusIcon = current >= obj.requiredCount ? "\u2713" : (obj.isOptional ? "?" : "\u2022");
                string visLabel = obj.visibility != ObjectiveVisibility.JournalVisible
                    ? $" [{obj.visibility}]" : "";

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUILayout.Label($"{statusIcon} {obj.description}{visLabel}", GUILayout.Width(220));

                // Progress bar
                var rect = GUILayoutUtility.GetRect(100, 16);
                EditorGUI.ProgressBar(rect, progress, $"{current}/{obj.requiredCount}");

                // Complete button (play mode only)
                if (state == QuestState.Active && current < obj.requiredCount)
                {
                    if (GUILayout.Button("+1", GUILayout.Width(30)))
                        QuestManager.Instance.AdvanceObjective(questId, obj.objectiveId, 1);
                    if (GUILayout.Button("Max", GUILayout.Width(35)))
                        QuestManager.Instance.AdvanceObjective(questId, obj.objectiveId, obj.requiredCount - current);
                }

                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;
        }

        // ─── Progression Blockers (for Locked quests) ────────
        if (state == QuestState.Locked && def.prerequisites != null && def.prerequisites.Length > 0)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(20);
            EditorGUILayout.HelpBox(GetBlockerMessage(def), MessageType.Info);
            EditorGUILayout.EndHorizontal();
        }

        // ─── Force State ─────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label("Force:", EditorStyles.miniLabel);
        if (GUILayout.Button("Start", EditorStyles.miniButton, GUILayout.Width(45)))
            QuestManager.Instance.StartQuest(questId);
        if (GUILayout.Button("Complete", EditorStyles.miniButton, GUILayout.Width(60)))
            QuestManager.Instance.CompleteQuest(questId);
        if (GUILayout.Button("Fail", EditorStyles.miniButton, GUILayout.Width(35)))
            QuestManager.Instance.FailQuest(questId);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private string GetBlockerMessage(QuestSO def)
    {
        var blockers = new List<string>();
        foreach (var prereq in def.prerequisites)
        {
            if (string.IsNullOrEmpty(prereq)) continue;
            var prereqState = QuestManager.Instance.GetQuestState(prereq);
            if (prereqState != QuestState.Done)
                blockers.Add($"Requires: '{prereq}' (currently {prereqState})");
        }
        return blockers.Count > 0 ? string.Join("\n", blockers) : "All prerequisites met";
    }

    // ─── Event History ──────────────────────────────────────────

    private void DrawEventHistory()
    {
        EditorGUI.indentLevel++;
        if (eventLog.Count == 0)
        {
            EditorGUILayout.LabelField("No events yet.", EditorStyles.miniLabel);
        }
        else
        {
            // Show last 20 events
            int count = Mathf.Min(eventLog.Count, 20);
            for (int i = 0; i < count; i++)
            {
                EditorGUILayout.LabelField(eventLog[i], EditorStyles.miniLabel);
            }
            if (eventLog.Count > 20)
                EditorGUILayout.LabelField($"... and {eventLog.Count - 20} more", EditorStyles.miniLabel);

            if (GUILayout.Button("Clear History", EditorStyles.miniButton, GUILayout.Width(90)))
                eventLog.Clear();
        }
        EditorGUI.indentLevel--;
    }

    // ─── VariableStore Quick View ───────────────────────────────

    private void DrawVariableStore()
    {
        if (VariableStore.Instance == null)
        {
            EditorGUILayout.HelpBox("VariableStore.Instance is null.", MessageType.Warning);
            return;
        }

        EditorGUI.indentLevel++;
        var flagNames = VariableStore.Instance.GetAllFlagNames();
        if (flagNames.Count == 0)
        {
            EditorGUILayout.LabelField("No flags set.", EditorStyles.miniLabel);
        }
        else
        {
            foreach (var name in flagNames)
            {
                bool val = VariableStore.Instance.GetFlag(name);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);

                var prevColor = GUI.color;
                GUI.color = val ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.8f, 0.3f, 0.3f);
                GUILayout.Label(val ? "\u2713" : "\u2717", GUILayout.Width(16));
                GUI.color = prevColor;

                GUILayout.Label(name, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Toggle", EditorStyles.miniButton, GUILayout.Width(50)))
                    VariableStore.Instance.SetFlag(name, !val);

                EditorGUILayout.EndHorizontal();
            }
        }

        // Counters
        var counterNames = VariableStore.Instance.GetAllCounterNames();
        if (counterNames.Count > 0)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Counters:", EditorStyles.miniBoldLabel);
            foreach (var name in counterNames)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUILayout.Label(name, EditorStyles.miniLabel, GUILayout.Width(150));
                int val = VariableStore.Instance.GetCounter(name);
                GUILayout.Label(val.ToString(), EditorStyles.miniLabel, GUILayout.Width(40));
                if (GUILayout.Button("+1", EditorStyles.miniButton, GUILayout.Width(25)))
                    VariableStore.Instance.ModifyCounter(name, 1);
                if (GUILayout.Button("-1", EditorStyles.miniButton, GUILayout.Width(25)))
                    VariableStore.Instance.ModifyCounter(name, -1);
                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUI.indentLevel--;
    }
}
