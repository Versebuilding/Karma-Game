using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the dialogue UI panel. Subscribes to DialogueManager events to show/hide
/// the dialogue panel, display NPC speech text, spawn choice buttons, and handle
/// player input (Z/X/C for choices, E/Space to advance).
///
/// Setup:
///   1. Create a Canvas (Screen Space - Overlay, sort order 10)
///   2. Add a panel at the bottom for the dialogue box (dialoguePanel)
///   3. Add TMP_Text for speaker name and dialogue text
///   4. Add an empty GameObject as choiceContainer (Vertical/Horizontal Layout)
///   5. Create a ChoiceButton prefab with ChoiceButtonUI component
///   6. Add a "Press E to continue" indicator (continuePrompt)
///   7. Attach this script and drag references
/// </summary>
public class DialogueUI : MonoBehaviour
{
    // ─── References ───────────────────────────────────────────
    [Header("Dialogue Panel")]
    [Tooltip("The root dialogue panel (enabled/disabled)")]
    [SerializeField] private GameObject dialoguePanel;

    [Tooltip("Speaker name text")]
    [SerializeField] private TMP_Text speakerNameText;

    [Tooltip("Dialogue body text")]
    [SerializeField] private TMP_Text dialogueText;

    [Header("Choices")]
    [Tooltip("Parent transform for spawned choice buttons")]
    [SerializeField] private Transform choiceContainer;

    [Tooltip("Prefab with ChoiceButtonUI component")]
    [SerializeField] private GameObject choiceButtonPrefab;

    [Header("Continue Prompt")]
    [Tooltip("'Press E to continue' indicator (shown for non-choice nodes)")]
    [SerializeField] private GameObject continuePrompt;

    [Header("Typewriter Effect")]
    [Tooltip("Enable typewriter text reveal effect")]
    [SerializeField] private bool useTypewriter = true;

    [Tooltip("Characters per second for typewriter effect")]
    [Range(10f, 120f)]
    [SerializeField] private float typewriterSpeed = 40f;

    [Header("Audio")]
    [Tooltip("AudioSource for UI sounds (click, advance)")]
    [SerializeField] private AudioSource uiAudioSource;

    [Tooltip("Sound played when advancing dialogue")]
    [SerializeField] private AudioClip advanceSound;

    [Tooltip("Sound played when selecting a choice")]
    [SerializeField] private AudioClip choiceSound;

    [Header("Selection Feedback")]
    [Tooltip("Brief delay after choice selection to show highlight before advancing")]
    [Range(0.1f, 1f)]
    [SerializeField] private float selectionDelay = 0.4f;

    [Header("Player Choice Display")]
    [Tooltip("Speaker name shown when the player speaks their choice")]
    [SerializeField] private string playerSpeakerName = "Sammy";

    [Tooltip("Sound played when the player's choice text starts displaying")]
    [SerializeField] private AudioClip playerSpeakingSound;

    [Header("Auto-Size")]
    [Tooltip("Minimum panel height (for NPC-speaking minimal mode)")]
    [SerializeField] private float minPanelHeight = 130f;

    [Tooltip("Maximum panel height")]
    [SerializeField] private float maxPanelHeight = 300f;

    [Tooltip("Padding added above/below text for auto-size")]
    [SerializeField] private float panelPadding = 60f;

    [Header("Text Sizing (screen-space pixels)")]
    [Tooltip("Default font size for dialogue text")]
    [SerializeField] private float defaultDialogueFontSize = 20f;

    [Tooltip("Minimum font size — text will never shrink below this")]
    [Range(12f, 24f)]
    [SerializeField] private float minDialogueFontSize = 18f;

    [Tooltip("Maximum font size — text will never grow above this")]
    [Range(16f, 30f)]
    [SerializeField] private float maxDialogueFontSize = 22f;

    [Header("Layout Override")]
    [Tooltip("Override panel to fixed-width centered (0 = keep Inspector layout)")]
    [SerializeField] private float fixedPanelWidth = 1000f;

    // ─── Runtime State ────────────────────────────────────────
    private bool isShowingChoices;
    private bool isTypewriting;
    private bool isWaitingAfterSelection;
    private bool isShowingPlayerChoice;
    private bool isWaitingToShowChoices;       // NPC text shown, waiting for Enter before showing choices
    private int pendingChoiceIndex;
    private Coroutine typewriterCoroutine;
    private Coroutine selectionCoroutine;
    private string fullDialogueText;
    private int currentChoiceCount;
    private ChoiceButtonUI[] currentChoiceButtons;
    private DialogueChoice[] pendingChoicesForDisplay;  // Stored choices while waiting for Enter

    // ─── Cached References (for speaker routing + auto-size) ──
    private RectTransform dialoguePanelRect;
    private GameObject speakerBadgeObj;
    private GameObject dialogueTextAreaObj;
    private Image panelBorderImage;        // orange border Image on DialoguePanel
    private GameObject innerPanelObj;      // "InnerPanel" child (cream bg + text + continue prompt)

    // ─── Input Keys ───────────────────────────────────────────
    private readonly KeyCode[] choiceKeys = { KeyCode.Z, KeyCode.X, KeyCode.C };
    private readonly KeyCode advanceKey1 = KeyCode.E;
    private readonly KeyCode advanceKey2 = KeyCode.Space;
    private readonly KeyCode advanceKey3 = KeyCode.Return;

    // ─── Input Grace Period ──────────────────────────────────
    // Prevents the E key that started the interaction from immediately
    // advancing dialogue in the same/next frame.
    private float inputGraceUntil;
    private const float INPUT_GRACE_SECONDS = 0.3f;

    // ─── Unity Lifecycle ──────────────────────────────────────

    private bool isSubscribed;

    void Awake()
    {
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
            dialoguePanelRect = dialoguePanel.GetComponent<RectTransform>();
        }

        // Cache speaker badge and text area for show/hide during speaker routing
        if (speakerNameText != null)
            speakerBadgeObj = speakerNameText.transform.parent?.gameObject;

        if (dialogueText != null)
            dialogueTextAreaObj = dialogueText.transform.parent?.gameObject;

        // Cache panel visual elements for ghost-panel mode
        if (dialoguePanel != null)
        {
            panelBorderImage = dialoguePanel.GetComponent<Image>();
            var innerTransform = dialoguePanel.transform.Find("InnerPanel");
            if (innerTransform != null)
                innerPanelObj = innerTransform.gameObject;
        }

        if (uiAudioSource == null)
            uiAudioSource = GetComponent<AudioSource>();

        // Auto-migrate panel dimensions for pre-existing scene instances
        if (fixedPanelWidth <= 850f)
            fixedPanelWidth = 1000f;
        if (minPanelHeight <= 100f)
            minPanelHeight = 130f;

        // Auto-migrate font sizes (min=18, default=20, max=22)
        if (minDialogueFontSize > 24f)
            minDialogueFontSize = 18f;
        if (defaultDialogueFontSize > 24f)
            defaultDialogueFontSize = 20f;
        if (maxDialogueFontSize > 28f)
            maxDialogueFontSize = 22f;

        // Ensure panel uses fixed-width centered layout (safety net if UISetupTool wasn't re-run)
        EnsureLayout();
    }

    /// <summary>Override dialogue panel to fixed-width centered layout at runtime.</summary>
    private void EnsureLayout()
    {
        if (dialoguePanelRect == null || fixedPanelWidth <= 0f) return;

        dialoguePanelRect.anchorMin = new Vector2(0.5f, 0f);
        dialoguePanelRect.anchorMax = new Vector2(0.5f, 0f);
        dialoguePanelRect.pivot = new Vector2(0.5f, 0f);
        dialoguePanelRect.sizeDelta = new Vector2(fixedPanelWidth, dialoguePanelRect.sizeDelta.y);
    }

    void OnEnable()
    {
        TrySubscribe();
    }

    void Start()
    {
        // Fallback: if DialogueManager.Instance was null during OnEnable
        // (common when DialogueCanvas initializes before GameManagers),
        // subscribe now — all Awakes have run by Start time.
        TrySubscribe();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    private void TrySubscribe()
    {
        if (isSubscribed) return;

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnDialogueStarted += HandleDialogueStarted;
            DialogueManager.Instance.OnNodeChanged += HandleNodeChanged;
            DialogueManager.Instance.OnDialogueEnded += HandleDialogueEnded;
            isSubscribed = true;
            Debug.Log("DialogueUI: Subscribed to DialogueManager events.");
        }
    }

    private void Unsubscribe()
    {
        if (!isSubscribed) return;

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnDialogueStarted -= HandleDialogueStarted;
            DialogueManager.Instance.OnNodeChanged -= HandleNodeChanged;
            DialogueManager.Instance.OnDialogueEnded -= HandleDialogueEnded;
        }
        isSubscribed = false;
    }

    void Update()
    {
        // Allow input during player choice display (even though dialogue hasn't advanced yet)
        if (DialogueManager.Instance == null ||
            (!DialogueManager.Instance.IsDialogueActive && !isShowingPlayerChoice))
            return;

        // Don't process input while showing selection highlight
        if (isWaitingAfterSelection) return;

        // Grace period after dialogue start — ignore the E key that triggered interaction
        if (Time.time < inputGraceUntil) return;

        // Player's choice text is showing — wait for Enter/E to confirm, then advance to NPC response
        if (isShowingPlayerChoice)
        {
            if (isTypewriting)
            {
                if (Input.GetKeyDown(advanceKey1) || Input.GetKeyDown(advanceKey2) || Input.GetKeyDown(advanceKey3))
                    SkipTypewriter();
                return;
            }
            if (Input.GetKeyDown(advanceKey1) || Input.GetKeyDown(advanceKey2) || Input.GetKeyDown(advanceKey3))
            {
                ConfirmPlayerChoice();
            }
            return;
        }

        // If typewriter is still running, pressing Enter/E/Space skips to full text
        if (isTypewriting)
        {
            if (Input.GetKeyDown(advanceKey1) || Input.GetKeyDown(advanceKey2) || Input.GetKeyDown(advanceKey3))
            {
                SkipTypewriter();
            }
            return;
        }

        // Waiting for user to acknowledge text before showing choices
        // (applies to both NPC-speaking and Player-speaking nodes with choices)
        if (isWaitingToShowChoices)
        {
            if (Input.GetKeyDown(advanceKey1) || Input.GetKeyDown(advanceKey2) || Input.GetKeyDown(advanceKey3))
            {
                // If bubble is still typewriting, skip it first
                var bubble = GetActiveBubble();
                if (bubble != null && bubble.IsTypewriting)
                {
                    bubble.SkipTypewriter();
                    return;
                }

                // If player panel typewriter is still running, skip it first
                if (isTypewriting)
                {
                    SkipTypewriter();
                    return;
                }

                isWaitingToShowChoices = false;

                // Hide player panel — choice buttons float independently
                if (dialoguePanel != null)
                    dialoguePanel.SetActive(false);

                ShowChoices(pendingChoicesForDisplay);
                pendingChoicesForDisplay = null;

                // Hide "Press Enter >>" prompt in the NPC bubble now that choices are visible
                HideBubbleContinuePrompt();
            }
            return;
        }

        // Handle choice input (Z/X/C or 1/2/3)
        if (isShowingChoices)
        {
            for (int i = 0; i < currentChoiceCount && i < choiceKeys.Length; i++)
            {
                if (Input.GetKeyDown(choiceKeys[i]) || Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    OnChoiceSelected(i);
                    return;
                }
            }
        }
        else
        {
            // Non-choice node: Enter/E/Space to advance
            if (Input.GetKeyDown(advanceKey1) || Input.GetKeyDown(advanceKey2) || Input.GetKeyDown(advanceKey3))
            {
                OnAdvance();
            }
        }
    }

    // ─── Event Handlers ───────────────────────────────────────

    private void HandleDialogueStarted(DialogueSO dialogue)
    {
        if (dialoguePanel == null)
        {
            Debug.LogError("DialogueUI: dialoguePanel reference is NULL! " +
                "Re-run 'Karma > Build UI Canvases' or assign manually in Inspector.");
            return;
        }

        // Panel activation is deferred to HandleNodeChanged to avoid a flash
        // of empty panel before content arrives. HandleNodeChanged fires
        // immediately after this event in the same frame.

        // Grace period: ignore advance input briefly so the E key that
        // triggered the interaction doesn't immediately advance the first node
        inputGraceUntil = Time.time + INPUT_GRACE_SECONDS;

        Debug.Log($"DialogueUI: Dialogue started for '{dialogue?.dialogueId}'.");
    }

    private void HandleNodeChanged(DialogueNode node)
    {
        if (node == null) return;

        bool npcSpeaking = IsNPCSpeaker(node.speakerName);
        bool hasChoices = node.HasChoices;

        // ── NPC speaking, no choices → bubble only, hide entire bottom panel ──
        if (npcSpeaking && !hasChoices)
        {
            HideChoices();
            ShowContinuePrompt(false);

            if (dialoguePanel != null)
                dialoguePanel.SetActive(false);

            return;
        }

        // ── NPC speaking WITH choices → show NPC text first, wait for Enter, then choices ──
        // NPC question text appears in the world-space bubble (NPCSpeechBubble).
        // Player reads NPC text, presses Enter, THEN choice buttons appear on right side.
        if (npcSpeaking && hasChoices)
        {
            if (dialoguePanel != null)
                dialoguePanel.SetActive(false);

            // Don't show choices yet — store them and wait for Enter
            HideChoices();
            isWaitingToShowChoices = true;
            pendingChoicesForDisplay = node.choices;
            return;
        }

        // ── Player speaking (no choices) → full bottom panel with visuals ──
        if (dialoguePanel != null)
            dialoguePanel.SetActive(true);

        SetPanelVisualsVisible(true);        // Restore border + inner panel
        ShowDialogueContent(true);

        // Update speaker name
        if (speakerNameText != null)
            speakerNameText.text = node.speakerName ?? "";

        // Play the node's voice clip for player-speaking nodes.
        // (DialogueNPC.HandleNodeChanged only plays clips for NPC-speaker nodes,
        //  so player/Sammy voice clips must be played here.)
        if (node.voiceClip != null)
            PlayUISound(node.voiceClip);

        // Update dialogue text (with optional typewriter effect)
        fullDialogueText = node.dialogueText ?? "";

        // Adapt font size to fit, then typewriter with maxVisibleCharacters
        AdaptDialogueTextToFit(fullDialogueText);
        if (useTypewriter && fullDialogueText.Length > 0)
        {
            StartTypewriter(fullDialogueText);
        }

        // Choices or continue?
        if (hasChoices)
        {
            // Don't show choices yet — player reads their text first, presses Enter,
            // THEN choices appear (same two-step flow as NPC-speaking-with-choices)
            HideChoices();
            ShowContinuePrompt(true);
            isWaitingToShowChoices = true;
            pendingChoicesForDisplay = node.choices;
        }
        else
        {
            HideChoices();
            ShowContinuePrompt(true);
        }

        // Auto-size panel based on text content
        AutoSizePanelToText();
    }

    private void HandleDialogueEnded()
    {
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }

        if (selectionCoroutine != null)
        {
            StopCoroutine(selectionCoroutine);
            selectionCoroutine = null;
        }

        isTypewriting = false;
        isShowingChoices = false;
        isWaitingAfterSelection = false;
        isShowingPlayerChoice = false;
        isWaitingToShowChoices = false;
        pendingChoiceIndex = -1;
        currentChoiceButtons = null;
        pendingChoicesForDisplay = null;

        SetPanelVisualsVisible(true);  // Reset for next dialogue

        // Reset font sizing state for next dialogue
        if (dialogueText != null)
        {
            dialogueText.enableAutoSizing = false;
            dialogueText.fontSize = defaultDialogueFontSize;
            dialogueText.maxVisibleCharacters = int.MaxValue;
        }

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        Debug.Log("DialogueUI: Panel hidden.");
    }

    // ─── Choice Display ───────────────────────────────────────

    private void ShowChoices(DialogueChoice[] choices)
    {
        ShowContinuePrompt(false);

        // Clear old choice buttons
        ClearChoices();

        if (choices == null || choices.Length == 0) return;

        currentChoiceCount = Mathf.Min(choices.Length, choiceKeys.Length);
        isShowingChoices = true;
        currentChoiceButtons = new ChoiceButtonUI[currentChoiceCount];

        for (int i = 0; i < currentChoiceCount; i++)
        {
            DialogueChoice choice = choices[i];
            string inputLabel = choiceKeys[i].ToString(); // "Z", "X", "C"

            // Check if this choice is available (karma gating)
            bool available = true;
            if (DialogueManager.Instance != null)
                available = DialogueManager.Instance.IsChoiceAvailable(choice);

            currentChoiceButtons[i] = SpawnChoiceButton(choice, inputLabel, i, available);
        }

        // Show choice container
        if (choiceContainer != null)
            choiceContainer.gameObject.SetActive(true);
    }

    private void HideChoices()
    {
        ClearChoices();
        isShowingChoices = false;
        currentChoiceCount = 0;

        if (choiceContainer != null)
            choiceContainer.gameObject.SetActive(false);
    }

    private void ClearChoices()
    {
        if (choiceContainer == null) return;

        for (int i = choiceContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(choiceContainer.GetChild(i).gameObject);
        }
    }

    private ChoiceButtonUI SpawnChoiceButton(DialogueChoice choice, string inputLabel, int index, bool available)
    {
        if (choiceButtonPrefab == null || choiceContainer == null) return null;

        GameObject btnObj = Instantiate(choiceButtonPrefab, choiceContainer);
        ChoiceButtonUI btnUI = btnObj.GetComponent<ChoiceButtonUI>();

        if (btnUI != null)
        {
            btnUI.Setup(choice, inputLabel, index, available);
            // Wire up click callback (for mouse users)
            btnUI.OnClicked += OnChoiceSelected;
        }
        else
        {
            Debug.LogWarning("DialogueUI: ChoiceButton prefab missing ChoiceButtonUI component!");
        }

        return btnUI;
    }

    // ─── Speaker Routing Helpers ─────────────────────────────

    /// <summary>
    /// Returns true if the given speaker name matches the active NPC speaker
    /// (i.e., this is the NPC talking, not the player).
    /// </summary>
    private bool IsNPCSpeaker(string speakerName)
    {
        if (DialogueManager.Instance == null) return false;
        string npcName = DialogueManager.Instance.ActiveNPCSpeakerName;
        if (string.IsNullOrEmpty(npcName) || string.IsNullOrEmpty(speakerName))
            return false;
        return string.Equals(npcName, speakerName, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Shows or hides the speaker badge and dialogue text area.
    /// When hidden, the panel shows only the continue prompt (minimal mode).
    /// </summary>
    private void ShowDialogueContent(bool show)
    {
        if (speakerBadgeObj != null)
            speakerBadgeObj.SetActive(show);

        if (dialogueTextAreaObj != null)
            dialogueTextAreaObj.SetActive(show);
    }

    /// <summary>
    /// Show or hide the panel's visual chrome (border + inner panel).
    /// When hidden with panel still active, only ChoiceContainer renders ("ghost panel").
    /// </summary>
    private void SetPanelVisualsVisible(bool visible)
    {
        if (panelBorderImage != null)
            panelBorderImage.enabled = visible;
        if (innerPanelObj != null)
            innerPanelObj.SetActive(visible);
    }

    /// <summary>Set the panel to a specific height.</summary>
    private void AutoSizePanel(float height)
    {
        if (dialoguePanelRect == null) return;
        var size = dialoguePanelRect.sizeDelta;
        size.y = height;
        dialoguePanelRect.sizeDelta = size;
    }

    /// <summary>Auto-size the panel height based on dialogue text content.</summary>
    private void AutoSizePanelToText()
    {
        if (dialoguePanelRect == null || dialogueText == null) return;

        // Force TMP to calculate preferred values
        dialogueText.ForceMeshUpdate();
        float textHeight = dialogueText.preferredHeight;

        // Panel height = text + padding, clamped
        float panelHeight = Mathf.Clamp(textHeight + panelPadding, minPanelHeight, maxPanelHeight);
        AutoSizePanel(panelHeight);
    }

    /// <summary>
    /// Configure dialogue text with auto-sizing between minDialogueFontSize and
    /// maxDialogueFontSize. The panel background grows vertically to fit all content.
    /// Must be called BEFORE the typewriter starts so layout is stable.
    /// </summary>
    private void AdaptDialogueTextToFit(string fullText)
    {
        if (dialogueText == null) return;

        // Always use auto-sizing so TMP picks the best size in range
        dialogueText.enableAutoSizing = true;
        dialogueText.fontSizeMin = minDialogueFontSize;
        dialogueText.fontSizeMax = maxDialogueFontSize;

        // Prevent TMP from truncating or showing "..." — panel grows to fit
        dialogueText.overflowMode = TextOverflowModes.Overflow;

        dialogueText.text = fullText;
        dialogueText.maxVisibleCharacters = int.MaxValue;

        // Remove any height cap — let the panel grow to fit
        var textLE = dialogueText.GetComponent<LayoutElement>();
        if (textLE != null)
            textLE.preferredHeight = -1;

        dialogueText.ForceMeshUpdate();
    }

    // ─── Continue Prompt ──────────────────────────────────────

    private void ShowContinuePrompt(bool show)
    {
        if (continuePrompt != null)
            continuePrompt.SetActive(show);
    }

    // ─── Input Actions ────────────────────────────────────────

    private void OnChoiceSelected(int choiceIndex)
    {
        if (DialogueManager.Instance == null) return;
        if (isWaitingAfterSelection) return;

        PlayUISound(choiceSound);

        // Show selection highlight (mockup: selected choice turns orange)
        if (currentChoiceButtons != null)
        {
            for (int i = 0; i < currentChoiceButtons.Length; i++)
            {
                if (currentChoiceButtons[i] != null)
                    currentChoiceButtons[i].SetSelected(i == choiceIndex);
            }
        }

        // Brief delay to show the highlight, then advance
        if (selectionCoroutine != null) StopCoroutine(selectionCoroutine);
        selectionCoroutine = StartCoroutine(SelectionDelayCoroutine(choiceIndex));
    }

    private IEnumerator SelectionDelayCoroutine(int choiceIndex)
    {
        isWaitingAfterSelection = true;
        yield return new WaitForSeconds(selectionDelay);

        // Get choice text before advancing to NPC response
        DialogueNode currentNode = DialogueManager.Instance.CurrentNode;
        if (currentNode == null || !currentNode.HasChoices
            || choiceIndex >= currentNode.choices.Length)
        {
            // Fallback: advance immediately if choice data is missing
            isWaitingAfterSelection = false;
            DialogueManager.Instance.SelectChoice(choiceIndex);
            isShowingChoices = false;
            yield break;
        }

        DialogueChoice selectedChoice = currentNode.choices[choiceIndex];
        string choiceText = selectedChoice.choiceText;

        // Hide choice buttons, activate + restore panel visuals, show the player's chosen line
        HideChoices();

        // Activate player panel (was hidden during NPC+choices mode)
        if (dialoguePanel != null)
            dialoguePanel.SetActive(true);

        SetPanelVisualsVisible(true);    // Restore border + inner panel for player's choice
        ShowDialogueContent(true);

        if (speakerNameText != null)
            speakerNameText.text = playerSpeakerName;

        // Play choice voice clip if assigned, otherwise fall back to generic player speaking sound
        if (selectedChoice.voiceClip != null)
            PlayUISound(selectedChoice.voiceClip);
        else
            PlayUISound(playerSpeakingSound);

        // Adapt font size and typewriter the choice text as player dialogue
        fullDialogueText = choiceText;
        AdaptDialogueTextToFit(choiceText);
        if (useTypewriter && choiceText.Length > 0)
            StartTypewriter(choiceText);

        AutoSizePanelToText();
        ShowContinuePrompt(true);

        // Enter "showing player choice" state — Update() handles E press to confirm
        isWaitingAfterSelection = false;
        isShowingPlayerChoice = true;
        pendingChoiceIndex = choiceIndex;
    }

    /// <summary>
    /// Called from Update() when player presses E after reading their choice text.
    /// Advances the dialogue to the NPC response.
    /// </summary>
    private void ConfirmPlayerChoice()
    {
        Debug.Log($"DialogueUI.ConfirmPlayerChoice(): pendingChoiceIndex={pendingChoiceIndex}");

        isShowingPlayerChoice = false;
        ShowContinuePrompt(false);

        // NOW apply karma/coins and advance to the NPC response
        DialogueManager.Instance.SelectChoice(pendingChoiceIndex);
        isShowingChoices = false;
    }

    private void OnAdvance()
    {
        if (DialogueManager.Instance == null) return;

        // If the NPC bubble is still typewriting, skip it instead of advancing
        var bubble = GetActiveBubble();
        if (bubble != null && bubble.IsTypewriting)
        {
            Debug.Log("DialogueUI.OnAdvance(): Bubble still typewriting — skipping typewriter instead.");
            bubble.SkipTypewriter();
            return;
        }

        Debug.Log($"DialogueUI.OnAdvance(): Advancing dialogue. Bubble={(bubble != null ? "found" : "NULL")}, currentNode={DialogueManager.Instance.CurrentNode?.nodeId}");
        DialogueManager.Instance.AdvanceDialogue();
    }

    // ─── Typewriter Effect ────────────────────────────────────

    private void StartTypewriter(string text)
    {
        if (typewriterCoroutine != null)
            StopCoroutine(typewriterCoroutine);

        typewriterCoroutine = StartCoroutine(TypewriterCoroutine(text));
    }

    private void SkipTypewriter()
    {
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }

        isTypewriting = false;

        // Reveal all characters (text + layout already set by AdaptDialogueTextToFit)
        if (dialogueText != null)
            dialogueText.maxVisibleCharacters = int.MaxValue;

        // Resize to final text
        AutoSizePanelToText();
    }

    private IEnumerator TypewriterCoroutine(string text)
    {
        isTypewriting = true;

        // Text is already set by AdaptDialogueTextToFit — just hide all characters
        if (dialogueText != null)
            dialogueText.maxVisibleCharacters = 0;

        float delay = 1f / typewriterSpeed;

        for (int i = 0; i <= text.Length; i++)
        {
            if (dialogueText != null)
                dialogueText.maxVisibleCharacters = i;

            yield return new WaitForSeconds(delay);
        }

        isTypewriting = false;
        typewriterCoroutine = null;
    }

    // ─── Audio ────────────────────────────────────────────────

    private void PlayUISound(AudioClip clip)
    {
        if (clip != null && uiAudioSource != null)
            uiAudioSource.PlayOneShot(clip);
    }

    // ─── Bubble Integration ──────────────────────────────────

    /// <summary>
    /// Gets the active NPC's speech bubble (if any).
    /// Used to coordinate typewriter skip and continue prompt visibility.
    /// </summary>
    private NPCSpeechBubble GetActiveBubble()
    {
        if (DialogueManager.Instance == null) return null;

        var npcTransform = DialogueManager.Instance.ActiveNPCTransform;
        if (npcTransform == null) return null;

        return npcTransform.GetComponentInChildren<NPCSpeechBubble>();
    }

    /// <summary>
    /// Hides the "Press Enter >>" prompt in the active NPC speech bubble.
    /// Called when choices become visible (so the bubble stops showing "Press Enter").
    /// </summary>
    private void HideBubbleContinuePrompt()
    {
        var bubble = GetActiveBubble();
        if (bubble != null)
            bubble.HideContinuePrompt();
    }
}
