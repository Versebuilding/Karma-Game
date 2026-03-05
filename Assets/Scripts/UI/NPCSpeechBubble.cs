using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// World-space speech bubble that floats above an NPC during dialogue.
/// Matches Figma mockup: brown name badge + white speech text on a subtle bg.
///
/// Subscribes to DialogueManager events to show/hide and update text.
/// The bubble follows the NPC position + offset and always faces the camera.
///
/// Setup:
///   1. The UISetupTool can create this automatically, OR:
///   2. Create a World Space Canvas as child of the NPC
///   3. Add this script to the canvas
///   4. It auto-creates the bubble UI elements on Awake
///
/// Usage:
///   - Attach to an NPC's child canvas, or use SetTarget(transform) to follow any NPC
///   - Automatically shows when dialogue starts with the target NPC
///   - Hides when dialogue ends or when a different NPC talks
/// </summary>
public class NPCSpeechBubble : MonoBehaviour
{
    // ─── Settings ────────────────────────────────────────────────
    [Header("Positioning")]
    [Tooltip("World-space offset above the NPC")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 5f, 0f);

    [Tooltip("The NPC transform this bubble follows (auto-found if parent is NPC)")]
    [SerializeField] private Transform targetNPC;

    [Tooltip("Whether to auto-calculate offset from NPC's renderer bounds")]
    [SerializeField] private bool autoCalculateHeight = true;

    [Tooltip("Extra clearance above the NPC's highest point (world units)")]
    [Range(0f, 5f)]
    [SerializeField] private float heightPadding = 0.2f;

    [Header("References (auto-created if empty)")]
    [Tooltip("The bubble panel root")]
    [SerializeField] private GameObject bubblePanel;

    [Tooltip("Speaker name text (on the badge)")]
    [SerializeField] private TMP_Text speakerNameText;

    [Tooltip("Speech text content")]
    [SerializeField] private TMP_Text speechText;

    [Header("Appearance")]
    [Tooltip("Background color for the speech bubble")]
    [SerializeField] private Color bubbleBgColor = new Color(0f, 0f, 0f, 0.75f);

    [Tooltip("Name badge color (brown from mockup)")]
    [SerializeField] private Color badgeColor = new Color(0.4f, 0.28f, 0.16f, 1f);

    [Tooltip("Text color for speech")]
    [SerializeField] private Color textColor = Color.white;

    [Tooltip("Maximum characters to show (truncates with ...)")]
    [SerializeField] private int maxDisplayChars = 80;

    [Header("Typewriter Effect")]
    [Tooltip("Enable typewriter text reveal in the speech bubble")]
    [SerializeField] private bool useTypewriter = true;

    [Tooltip("Characters per second for the typewriter effect")]
    [Range(10f, 120f)]
    [SerializeField] private float typewriterSpeed = 35f;

    [Header("Animation")]
    [Tooltip("Enable fade in/out animation")]
    [SerializeField] private bool animateFade = true;

    [Tooltip("Fade duration")]
    [Range(0.1f, 0.5f)]
    [SerializeField] private float fadeDuration = 0.2f;

    [Header("Continue Prompt")]
    [Tooltip("Show 'Press E' prompt in the bubble when NPC is speaking")]
    [SerializeField] private bool showContinuePrompt = true;

    [Tooltip("Text for the continue prompt")]
    [SerializeField] private string continuePromptText = "Press Enter >>";

    [Tooltip("Continue prompt text color")]
    [SerializeField] private Color continuePromptColor = new Color(1f, 1f, 1f, 0.6f);

    // ─── Runtime ────────────────────────────────────────────────
    private Canvas worldCanvas;
    private CanvasGroup canvasGroup;
    private Camera mainCamera;
    private Coroutine fadeCoroutine;
    private Coroutine typewriterCoroutine;
    private bool isShowing;
    private bool isSubscribed;
    private bool isTypewriting;
    private string fullBubbleText;
    private TMP_Text continuePromptTMP;

    // ─── Unity Lifecycle ────────────────────────────────────────

    void Awake()
    {
        mainCamera = Camera.main;

        // Auto-find target NPC from parent
        if (targetNPC == null)
        {
            var npc = GetComponentInParent<NPCBase>();
            if (npc != null)
                targetNPC = npc.transform;
            else
                targetNPC = transform.parent;
        }

        // Ensure we have a World Space Canvas
        worldCanvas = GetComponent<Canvas>();
        if (worldCanvas == null)
        {
            worldCanvas = gameObject.AddComponent<Canvas>();
            worldCanvas.renderMode = RenderMode.WorldSpace;
        }
        worldCanvas.renderMode = RenderMode.WorldSpace;

        // Canvas scaler for world space
        var scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;
        }

        // Canvas group for fading
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // Auto-migrate: bump very low offsets to the recommended height
        // so pre-existing scene instances render correctly without
        // manual Inspector edits.
        if (worldOffset.y <= 3f)
            worldOffset = new Vector3(0f, 5f, 0f);

        // Auto-migrate heightPadding: lower from old 0.5 to 0.2
        if (heightPadding >= 0.5f)
            heightPadding = 0.2f;

        // Dynamic height: calculate from NPC's renderer bounds so the bubble
        // truly clears the character's head regardless of model height.
        if (autoCalculateHeight && targetNPC != null)
        {
            var renderers = targetNPC.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds combined = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    combined.Encapsulate(renderers[i].bounds);

                float npcTop = combined.max.y - targetNPC.position.y;
                float suggestedY = npcTop + heightPadding;

                if (worldOffset.y < suggestedY)
                {
                    worldOffset.y = suggestedY;
                    Debug.Log($"NPCSpeechBubble: Auto-adjusted offset to y={suggestedY:F1} " +
                        $"(NPC top from {renderers.Length} renderers={npcTop:F1}, padding={heightPadding:F1})");
                }
            }
        }

        // Auto-migrate prompt text for pre-existing scene instances
        if (continuePromptText == "Press E \u25B6" || continuePromptText == "Press Enter \u25B6")
            continuePromptText = "Press Enter >>";

        // Always enforce world-space scale (even if Inspector references are pre-assigned
        // and BuildBubbleUI() is skipped — without this the canvas renders at full screen)
        var canvasRect = GetComponent<RectTransform>();
        if (canvasRect != null)
        {
            canvasRect.sizeDelta = new Vector2(300, 100);
            canvasRect.localScale = Vector3.one * 0.01f;
            canvasRect.pivot = new Vector2(0.5f, 0f); // bottom-center: bubble sits above the offset point
        }

        // Build UI if not already set up
        if (bubblePanel == null)
            BuildBubbleUI();

        // If bubble was pre-assigned, try to find continue prompt child
        if (bubblePanel != null && continuePromptTMP == null)
        {
            var existing = bubblePanel.transform.Find("ContinuePrompt");
            if (existing != null)
                continuePromptTMP = existing.GetComponent<TMP_Text>();
        }

        // Start hidden
        if (bubblePanel != null)
            bubblePanel.SetActive(false);
        canvasGroup.alpha = 0f;
        isShowing = false;
    }

    void OnEnable()
    {
        TrySubscribe();
    }

    void Start()
    {
        // Fallback if DialogueManager wasn't ready during OnEnable
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

    void LateUpdate()
    {
        if (!isShowing || targetNPC == null) return;

        // Follow target NPC position
        transform.position = targetNPC.position + worldOffset;

        // Billboard: always face the camera
        if (mainCamera != null)
        {
            transform.forward = mainCamera.transform.forward;
        }
    }

    // ─── Event Handlers ─────────────────────────────────────────

    private void HandleDialogueStarted(DialogueSO dialogue)
    {
        // Don't auto-show here — HandleNodeChanged will decide based on speaker.
        // Just verify this NPC is relevant to the dialogue.
        var dialogueNPC = targetNPC?.GetComponent<DialogueNPC>();
        if (dialogueNPC == null) return;

        // Mark that we're participating in this dialogue
        // (HandleNodeChanged will show/hide based on who's speaking)
    }

    private void HandleNodeChanged(DialogueNode node)
    {
        if (node == null) return;

        // Only show bubble when the NPC is speaking (speaker matches ActiveNPCSpeakerName)
        bool isNPCSpeaking = IsActiveNPCSpeaker(node.speakerName);

        if (isNPCSpeaking)
        {
            // Show bubble with NPC's text
            if (!isShowing)
                Show();

            // Update speaker name
            if (speakerNameText != null)
                speakerNameText.text = node.speakerName ?? "";

            // Prepare speech text (truncated for bubble)
            string text = node.dialogueText ?? "";
            if (text.Length > maxDisplayChars)
                text = text.Substring(0, maxDisplayChars) + "...";

            // Hide continue prompt during typewriter — shown when text completes
            if (continuePromptTMP != null)
                continuePromptTMP.gameObject.SetActive(false);

            // Typewriter or instant text
            if (useTypewriter && text.Length > 0)
            {
                StartTypewriter(text);
            }
            else
            {
                if (speechText != null)
                    speechText.text = text;

                // Show continue prompt immediately for instant text
                if (continuePromptTMP != null)
                    continuePromptTMP.gameObject.SetActive(true);
            }
        }
        else
        {
            // Player is speaking — hide the NPC bubble
            if (isShowing)
                Hide();
        }
    }

    /// <summary>
    /// Returns true if this NPC is the active speaker in the current dialogue.
    /// </summary>
    private bool IsActiveNPCSpeaker(string speakerName)
    {
        if (DialogueManager.Instance == null) return false;
        string npcName = DialogueManager.Instance.ActiveNPCSpeakerName;
        if (string.IsNullOrEmpty(npcName) || string.IsNullOrEmpty(speakerName))
            return false;
        return string.Equals(npcName, speakerName, System.StringComparison.OrdinalIgnoreCase);
    }

    private void HandleDialogueEnded()
    {
        // Stop any running typewriter
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }
        isTypewriting = false;

        Hide();
    }

    // ─── Public API ─────────────────────────────────────────────

    /// <summary>Set the NPC this bubble follows.</summary>
    public void SetTarget(Transform npcTransform)
    {
        targetNPC = npcTransform;
    }

    /// <summary>Inspector context menu: reset worldOffset to recommended default.</summary>
    [ContextMenu("Reset Bubble Offset to Default")]
    private void ResetOffset() { worldOffset = new Vector3(0f, 5f, 0f); }

    /// <summary>Show the speech bubble with fade in.</summary>
    public void Show()
    {
        isShowing = true;
        if (bubblePanel != null)
            bubblePanel.SetActive(true);

        if (animateFade)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeCoroutine(0f, 1f));
        }
        else
        {
            canvasGroup.alpha = 1f;
        }
    }

    /// <summary>Hide the speech bubble with fade out.</summary>
    public void Hide()
    {
        if (!isShowing) return;

        if (animateFade)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeCoroutine(1f, 0f, () =>
            {
                isShowing = false;
                if (bubblePanel != null)
                    bubblePanel.SetActive(false);
            }));
        }
        else
        {
            isShowing = false;
            canvasGroup.alpha = 0f;
            if (bubblePanel != null)
                bubblePanel.SetActive(false);
        }
    }

    /// <summary>Hide the continue prompt (called when choices become visible).</summary>
    public void HideContinuePrompt()
    {
        if (continuePromptTMP != null)
            continuePromptTMP.gameObject.SetActive(false);
    }

    /// <summary>Set the speech text directly (for external use).</summary>
    public void SetText(string speaker, string text)
    {
        if (speakerNameText != null)
            speakerNameText.text = speaker;

        if (speechText != null)
        {
            if (text.Length > maxDisplayChars)
                text = text.Substring(0, maxDisplayChars) + "...";
            speechText.text = text;
        }
    }

    // ─── UI Construction ────────────────────────────────────────

    private void BuildBubbleUI()
    {
        // Set canvas size
        var canvasRect = GetComponent<RectTransform>();
        if (canvasRect != null)
        {
            canvasRect.sizeDelta = new Vector2(300, 100);
            canvasRect.localScale = Vector3.one * 0.01f; // world space scale
            canvasRect.pivot = new Vector2(0.5f, 0f); // bottom-center: bubble sits above offset
        }

        // Bubble Panel
        bubblePanel = new GameObject("BubblePanel");
        bubblePanel.transform.SetParent(transform, false);

        var panelRect = bubblePanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var panelImage = bubblePanel.AddComponent<Image>();
        panelImage.color = bubbleBgColor;

        // Vertical layout
        var vlg = bubblePanel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 8, 8);
        vlg.spacing = 4;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var panelFitter = bubblePanel.AddComponent<ContentSizeFitter>();
        panelFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        panelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Name Badge
        var badgeObj = new GameObject("NameBadge");
        badgeObj.transform.SetParent(bubblePanel.transform, false);

        var badgeImage = badgeObj.AddComponent<Image>();
        badgeImage.color = badgeColor;

        var badgeHLG = badgeObj.AddComponent<HorizontalLayoutGroup>();
        badgeHLG.padding = new RectOffset(8, 8, 2, 2);
        badgeHLG.childAlignment = TextAnchor.MiddleCenter;

        var badgeFitter = badgeObj.AddComponent<ContentSizeFitter>();
        badgeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        badgeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var badgeLE = badgeObj.AddComponent<LayoutElement>();
        badgeLE.preferredHeight = 22;

        // Speaker name text
        var nameObj = new GameObject("SpeakerName");
        nameObj.transform.SetParent(badgeObj.transform, false);

        speakerNameText = nameObj.AddComponent<TextMeshProUGUI>();
        speakerNameText.text = "NPC";
        speakerNameText.fontSize = 14;
        speakerNameText.fontStyle = FontStyles.Bold;
        speakerNameText.color = Color.white;
        speakerNameText.alignment = TextAlignmentOptions.Center;
        speakerNameText.enableAutoSizing = false;

        // Speech text
        var textObj = new GameObject("SpeechText");
        textObj.transform.SetParent(bubblePanel.transform, false);

        speechText = textObj.AddComponent<TextMeshProUGUI>();
        speechText.text = "";
        speechText.fontSize = 12;
        speechText.color = textColor;
        speechText.alignment = TextAlignmentOptions.TopLeft;
        speechText.textWrappingMode = TextWrappingModes.Normal;
        speechText.enableAutoSizing = false;

        var textLE = textObj.AddComponent<LayoutElement>();
        textLE.preferredWidth = 250;

        // Continue Prompt (below speech text, right-aligned)
        if (showContinuePrompt)
        {
            var promptObj = new GameObject("ContinuePrompt");
            promptObj.transform.SetParent(bubblePanel.transform, false);

            continuePromptTMP = promptObj.AddComponent<TextMeshProUGUI>();
            continuePromptTMP.text = continuePromptText;
            continuePromptTMP.fontSize = 10;
            continuePromptTMP.fontStyle = FontStyles.Italic;
            continuePromptTMP.color = continuePromptColor;
            continuePromptTMP.alignment = TextAlignmentOptions.BottomRight;
            continuePromptTMP.enableAutoSizing = false;

            var promptLE = promptObj.AddComponent<LayoutElement>();
            promptLE.preferredWidth = 250;
            promptLE.preferredHeight = 16;
        }
    }

    // ─── Typewriter Effect ─────────────────────────────────────

    /// <summary>Whether the bubble is currently revealing text character by character.</summary>
    public bool IsTypewriting => isTypewriting;

    /// <summary>
    /// Skip to the end of the current typewriter animation.
    /// Called by DialogueUI when the player presses Enter during NPC speech.
    /// </summary>
    public void SkipTypewriter()
    {
        if (!isTypewriting) return;

        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }

        isTypewriting = false;

        if (speechText != null)
            speechText.text = fullBubbleText;

        // Show continue prompt now that full text is visible
        if (continuePromptTMP != null)
            continuePromptTMP.gameObject.SetActive(true);
    }

    private void StartTypewriter(string text)
    {
        if (typewriterCoroutine != null)
            StopCoroutine(typewriterCoroutine);

        fullBubbleText = text;
        typewriterCoroutine = StartCoroutine(TypewriterCoroutine(text));
    }

    private IEnumerator TypewriterCoroutine(string text)
    {
        isTypewriting = true;

        if (speechText != null)
            speechText.text = "";

        float delay = 1f / typewriterSpeed;

        for (int i = 0; i <= text.Length; i++)
        {
            if (speechText != null)
                speechText.text = text.Substring(0, i);

            yield return new WaitForSeconds(delay);
        }

        isTypewriting = false;
        typewriterCoroutine = null;

        // Show continue prompt once full text is visible
        if (continuePromptTMP != null)
            continuePromptTMP.gameObject.SetActive(true);
    }

    // ─── Fade Animation ─────────────────────────────────────────

    private IEnumerator FadeCoroutine(float from, float to, System.Action onComplete = null)
    {
        float elapsed = 0f;
        canvasGroup.alpha = from;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = to;
        onComplete?.Invoke();
    }
}
