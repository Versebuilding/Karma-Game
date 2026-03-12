using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Quest UI system: HUD tracker + toast notifications + quest journal panel.
///
/// Components (auto-created at runtime if not assigned):
///   - HUD Tracker: Small panel at top-right showing active quest name + current objective
///   - Toast: Brief popup notification on quest start / objective complete / quest done
///   - Journal: Full-screen quest log toggled with Tab key (deferred — placeholder)
///
/// Subscribes to QuestManager events for reactive updates.
///
/// Setup: Add to the HUDCanvas or as a child of GameManagers.
///        Will auto-create its own Canvas if none is assigned.
/// </summary>
public class QuestLogUI : MonoBehaviour
{
    // ─── References (auto-created if null) ────────────────────
    [Header("HUD Tracker")]
    [Tooltip("Panel showing current quest objective (auto-created if null)")]
    [SerializeField] private GameObject trackerPanel;

    [Tooltip("Text showing the quest name")]
    [SerializeField] private TMP_Text trackerQuestName;

    [Tooltip("Text showing the current objective and progress")]
    [SerializeField] private TMP_Text trackerObjectiveText;

    [Header("Toast Notification")]
    [Tooltip("Panel for toast popups (auto-created if null)")]
    [SerializeField] private GameObject toastPanel;

    [Tooltip("Text for toast message")]
    [SerializeField] private TMP_Text toastText;

    [Header("Settings")]
    [Tooltip("How long toast notifications stay visible")]
    [SerializeField] private float toastDuration = 3f;

    [Tooltip("How long the toast fades out")]
    [SerializeField] private float toastFadeDuration = 0.5f;

    // ─── Runtime State ──────────────────────────────────────────
    private Canvas canvas;
    private Coroutine toastCoroutine;
    private CanvasGroup toastCanvasGroup;
    private bool isSubscribed;

    // ─── Unity Lifecycle ────────────────────────────────────────

    void OnEnable()
    {
        TrySubscribe();
    }

    void Start()
    {
        // Fallback subscription (if QuestManager Awake ran after this OnEnable)
        TrySubscribe();

        EnsureUIBuilt();

        // Start hidden
        if (trackerPanel != null)
            trackerPanel.SetActive(false);
        if (toastPanel != null)
            toastPanel.SetActive(false);
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void OnDestroy()
    {
        Unsubscribe();
    }

    // ─── Subscription ───────────────────────────────────────────

    private void TrySubscribe()
    {
        if (isSubscribed) return;
        if (QuestManager.Instance == null) return;

        QuestManager.Instance.OnQuestStarted += HandleQuestStarted;
        QuestManager.Instance.OnObjectiveUpdated += HandleObjectiveUpdated;
        QuestManager.Instance.OnQuestCompleted += HandleQuestCompleted;
        QuestManager.Instance.OnQuestFailed += HandleQuestFailed;
        QuestManager.Instance.OnObjectiveFailed += HandleObjectiveFailed;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed) return;
        if (QuestManager.Instance == null) return;

        QuestManager.Instance.OnQuestStarted -= HandleQuestStarted;
        QuestManager.Instance.OnObjectiveUpdated -= HandleObjectiveUpdated;
        QuestManager.Instance.OnQuestCompleted -= HandleQuestCompleted;
        QuestManager.Instance.OnQuestFailed -= HandleQuestFailed;
        QuestManager.Instance.OnObjectiveFailed -= HandleObjectiveFailed;
        isSubscribed = false;
    }

    // ─── Event Handlers ─────────────────────────────────────────

    private void HandleQuestStarted(string questId)
    {
        var def = QuestManager.Instance.GetQuestDefinition(questId);
        if (def == null) return;

        ShowToast($"New Quest: {def.displayName}");
        UpdateTracker(questId);
    }

    private void HandleObjectiveUpdated(string questId, string objectiveId, int current, int required)
    {
        var def = QuestManager.Instance.GetQuestDefinition(questId);
        if (def == null) return;

        var objective = def.GetObjective(objectiveId);
        if (objective == null) return;

        // Only show toast for visible objectives
        if (objective.visibility != ObjectiveVisibility.Hidden)
        {
            if (current >= required)
                ShowToast($"Objective Complete: {objective.description}");
        }

        UpdateTracker(questId);
    }

    private void HandleObjectiveFailed(string questId, string objectiveId, bool retryAllowed)
    {
        var def = QuestManager.Instance.GetQuestDefinition(questId);
        if (def == null) return;

        var objective = def.GetObjective(objectiveId);
        if (objective == null) return;

        // Only show toast for visible objectives
        if (objective.visibility != ObjectiveVisibility.Hidden)
        {
            if (retryAllowed)
                ShowToast($"Try Again: {objective.description}");
            else
                ShowToast($"Failed: {objective.description}");
        }

        UpdateTracker(questId);
    }

    private void HandleQuestCompleted(string questId)
    {
        var def = QuestManager.Instance.GetQuestDefinition(questId);
        if (def == null) return;

        ShowToast($"Quest Complete: {def.displayName}");

        // Show next active quest, or hide tracker
        var activeQuests = QuestManager.Instance.GetActiveQuests();
        if (activeQuests.Count > 0)
            UpdateTracker(activeQuests[0].questId);
        else
            HideTracker();
    }

    private void HandleQuestFailed(string questId)
    {
        var def = QuestManager.Instance.GetQuestDefinition(questId);
        if (def == null) return;

        ShowToast($"Quest Failed: {def.displayName}");
        HideTracker();
    }

    // ─── Tracker ────────────────────────────────────────────────

    private void UpdateTracker(string questId)
    {
        var def = QuestManager.Instance.GetQuestDefinition(questId);
        var state = QuestManager.Instance.GetRuntimeState(questId);
        if (def == null || state == null || state.state != QuestState.Active) return;

        if (trackerPanel != null)
            trackerPanel.SetActive(true);

        if (trackerQuestName != null)
            trackerQuestName.text = def.displayName;

        if (trackerObjectiveText != null)
        {
            // Find the first incomplete, visible objective
            string objectiveText = "";
            if (def.objectives != null)
            {
                foreach (var obj in def.objectives)
                {
                    if (obj.isOptional) continue;
                    // Skip hidden/silent objectives — they shouldn't appear in tracker
                    if (obj.visibility == ObjectiveVisibility.Hidden) continue;
                    int progress = state.GetProgress(obj.objectiveId);
                    if (progress < obj.requiredCount)
                    {
                        if (obj.visibility == ObjectiveVisibility.SoftHint)
                            objectiveText = obj.description; // No progress counter for soft hints
                        else if (obj.requiredCount > 1)
                            objectiveText = $"{obj.description} ({progress}/{obj.requiredCount})";
                        else
                            objectiveText = obj.description;
                        break;
                    }
                }
            }
            trackerObjectiveText.text = objectiveText;
        }
    }

    private void HideTracker()
    {
        if (trackerPanel != null)
            trackerPanel.SetActive(false);
    }

    // ─── Toast ──────────────────────────────────────────────────

    private void ShowToast(string message)
    {
        if (toastPanel == null || toastText == null) return;

        if (toastCoroutine != null)
            StopCoroutine(toastCoroutine);

        toastText.text = message;
        toastPanel.SetActive(true);

        if (toastCanvasGroup != null)
            toastCanvasGroup.alpha = 1f;

        toastCoroutine = StartCoroutine(ToastFadeCoroutine());
    }

    private IEnumerator ToastFadeCoroutine()
    {
        yield return new WaitForSeconds(toastDuration);

        // Fade out
        if (toastCanvasGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < toastFadeDuration)
            {
                elapsed += Time.deltaTime;
                toastCanvasGroup.alpha = 1f - (elapsed / toastFadeDuration);
                yield return null;
            }
        }

        if (toastPanel != null)
            toastPanel.SetActive(false);

        toastCoroutine = null;
    }

    // ─── Auto-Build UI ──────────────────────────────────────────

    private void EnsureUIBuilt()
    {
        // Find or create canvas
        canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            // Look for existing HUDCanvas
            var hudCanvas = GameObject.Find("HUDCanvas");
            if (hudCanvas != null)
                canvas = hudCanvas.GetComponent<Canvas>();
        }

        if (canvas == null)
        {
            Debug.LogWarning("QuestLogUI: No Canvas found. Create a HUDCanvas or attach to an existing Canvas.");
            return;
        }

        // Auto-create tracker panel if not assigned
        if (trackerPanel == null)
            BuildTrackerPanel();

        // Auto-create toast panel if not assigned
        if (toastPanel == null)
            BuildToastPanel();
    }

    private void BuildTrackerPanel()
    {
        // Create tracker panel anchored top-right
        trackerPanel = new GameObject("QuestTracker");
        trackerPanel.transform.SetParent(canvas.transform, false);

        var trackerRect = trackerPanel.AddComponent<RectTransform>();
        trackerRect.anchorMin = new Vector2(1f, 1f);
        trackerRect.anchorMax = new Vector2(1f, 1f);
        trackerRect.pivot = new Vector2(1f, 1f);
        trackerRect.anchoredPosition = new Vector2(-20f, -80f);
        trackerRect.sizeDelta = new Vector2(300f, 80f);

        // Background
        var bg = trackerPanel.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.08f, 0.06f, 0.85f);

        // Add vertical layout
        var layout = trackerPanel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 8, 8);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        // Quest name text
        var nameObj = new GameObject("QuestName");
        nameObj.transform.SetParent(trackerPanel.transform, false);
        trackerQuestName = nameObj.AddComponent<TextMeshProUGUI>();
        trackerQuestName.fontSize = 16f;
        trackerQuestName.fontStyle = FontStyles.Bold;
        trackerQuestName.color = new Color(1f, 0.75f, 0.3f);
        trackerQuestName.text = "";

        // Objective text
        var objObj = new GameObject("ObjectiveText");
        objObj.transform.SetParent(trackerPanel.transform, false);
        trackerObjectiveText = objObj.AddComponent<TextMeshProUGUI>();
        trackerObjectiveText.fontSize = 13f;
        trackerObjectiveText.color = new Color(0.9f, 0.88f, 0.82f);
        trackerObjectiveText.text = "";
    }

    private void BuildToastPanel()
    {
        // Create toast panel anchored top-center
        toastPanel = new GameObject("QuestToast");
        toastPanel.transform.SetParent(canvas.transform, false);

        var toastRect = toastPanel.AddComponent<RectTransform>();
        toastRect.anchorMin = new Vector2(0.5f, 1f);
        toastRect.anchorMax = new Vector2(0.5f, 1f);
        toastRect.pivot = new Vector2(0.5f, 1f);
        toastRect.anchoredPosition = new Vector2(0f, -20f);
        toastRect.sizeDelta = new Vector2(400f, 50f);

        // CanvasGroup for fade
        toastCanvasGroup = toastPanel.AddComponent<CanvasGroup>();

        // Background
        var bg = toastPanel.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.12f, 0.08f, 0.9f);

        // Toast text
        var textObj = new GameObject("ToastText");
        textObj.transform.SetParent(toastPanel.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 4f);
        textRect.offsetMax = new Vector2(-16f, -4f);

        toastText = textObj.AddComponent<TextMeshProUGUI>();
        toastText.fontSize = 16f;
        toastText.fontStyle = FontStyles.Bold;
        toastText.color = new Color(1f, 0.85f, 0.4f);
        toastText.alignment = TextAlignmentOptions.Center;
        toastText.text = "";
    }
}
