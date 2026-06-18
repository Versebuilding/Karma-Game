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
    // ─── Static Registry ────────────────────────────────────────
    // Bubbles register themselves here so DialogueUI can find them
    // after they're unparented from the NPC hierarchy.
    private static readonly System.Collections.Generic.Dictionary<Transform, NPCSpeechBubble>
        registry = new System.Collections.Generic.Dictionary<Transform, NPCSpeechBubble>();

    /// <summary>Find the speech bubble associated with a given NPC transform.</summary>
    public static NPCSpeechBubble GetBubbleForNPC(Transform npc)
    {
        if (npc != null && registry.TryGetValue(npc, out var bubble))
            return bubble;
        return null;
    }

    // ─── Settings ────────────────────────────────────────────────
    [Header("Positioning")]
    [Tooltip("World-space offset above the NPC")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 3.5f, 0f);

    [Tooltip("The NPC transform this bubble follows (auto-found if parent is NPC)")]
    [SerializeField] private Transform targetNPC;

    [Tooltip("Whether to auto-calculate offset from NPC's renderer bounds")]
    [SerializeField] private bool autoCalculateHeight = true;

    [Tooltip("Extra clearance above the NPC's highest point (world units)")]
    [Range(0f, 5f)]
    [SerializeField] private float heightPadding = 0.2f;

    [Tooltip("Extra world-unit clearance past the NPC's edge when the bubble is placed to the side during dialogue")]
    [Range(0f, 5f)]
    [SerializeField] private float dialogueSidePadding = 1.5f;

    [Header("Fixed Screen Position (Screen Space canvas only)")]
    [Tooltip("When true, positions the bubble in screen space relative to the NPC using the offset below. Canvas stays as Screen Space Overlay — no world-space math.")]
    [SerializeField] private bool useFixedScreenPosition = false;

    [Tooltip("Screen-pixel offset from the NPC's screen position. X negative = left, X positive = right, Y positive = up. Z is ignored. Tune in Play Mode and it takes effect immediately.")]
    [SerializeField] private Vector3 fixedScreenPosition = new Vector3(-300f, 200f, 0f);

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

    [Tooltip("Maximum characters to show (truncates with ...). Set high to avoid truncation.")]
    [SerializeField] private int maxDisplayChars = 500;

    [Header("Text Sizing (world-space canvas units)")]
    [Tooltip("Default font size for speech text")]
    [Range(16f, 36f)]
    [SerializeField] private float defaultFontSize = 28f;

    [Tooltip("Minimum font size — text will never shrink below this")]
    [Range(16f, 30f)]
    [SerializeField] private float minFontSize = 24f;

    [Tooltip("Maximum font size — text will never grow above this")]
    [Range(20f, 36f)]
    [SerializeField] private float maxFontSize = 28f;

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
    private Vector3 _dialogueOffset;
    private bool _dialogueModeActive;
    private float _npcBoundsHalfWidth;
    private float _npcMidHeight;
    private TMP_Text continuePromptTMP;

    // ─── Unity Lifecycle ────────────────────────────────────────

    void Awake()
    {
        mainCamera = Camera.main;

        // Auto-find target NPC from parent BEFORE unparenting
        if (targetNPC == null)
        {
            var npc = GetComponentInParent<NPCBase>();
            if (npc != null)
                targetNPC = npc.transform;
            else
                targetNPC = transform.parent;
        }

        // Register so DialogueUI can find us by NPC transform
        if (targetNPC != null)
            registry[targetNPC] = this;

        worldCanvas = GetComponent<Canvas>();
        if (worldCanvas == null)
            worldCanvas = gameObject.AddComponent<Canvas>();

        if (!useFixedScreenPosition)
        {
            // Dynamic World Space mode: unparent so parent rotation/scale can't interfere,
            // then switch canvas to World Space for 3D billboard positioning.
            transform.SetParent(null, false);
            worldCanvas.renderMode = RenderMode.WorldSpace;

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.dynamicPixelsPerUnit = 10f;
            }
        }
        // useFixedScreenPosition=true: canvas stays as Screen Space Overlay child of its parent.
        // Position is driven by BubblePanel.anchoredPosition in LateUpdate — no unparenting needed.

        // Canvas group for fading
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // Force a reasonable offset — 3.5 units above NPC origin
        // (auto-calculation below will override based on NPC size)
        worldOffset = new Vector3(0f, 3.5f, 0f);

        // Auto-migrate heightPadding: lower from old 0.5 to 0.2
        if (heightPadding >= 0.5f)
            heightPadding = 0.2f;

        // Auto-migrate font sizes to font 28 world-space canvas units
        // (older instances may have smaller values from previous migrations)
        if (defaultFontSize < 20f)
            defaultFontSize = 28f;
        if (minFontSize < 20f)
            minFontSize = 24f;
        if (maxFontSize < 20f)
            maxFontSize = 28f;

        // Auto-migrate prompt text for pre-existing scene instances
        if (continuePromptText == "Press E \u25B6" || continuePromptText == "Press Enter \u25B6")
            continuePromptText = "Press Enter >>";

        // Enforce canvas size/pivot up front (scale set later, proportional to NPC)
        var canvasRect = GetComponent<RectTransform>();
        if (canvasRect != null)
        {
            canvasRect.sizeDelta = new Vector2(600, 400);
            canvasRect.pivot = new Vector2(0.5f, 0f); // bottom-center: bubble sits above the offset point
        }

        // Build UI if not already set up
        if (bubblePanel == null)
            BuildBubbleUI();

        // Dynamic height + scale: calculate from NPC's renderer bounds so the
        // bubble truly clears the character's head AND is proportional to model size.
        // Works for tiny 2-unit characters and giant 15-unit models alike.
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
                worldOffset.y = suggestedY;
                _npcBoundsHalfWidth = combined.extents.x;
                _npcMidHeight = npcTop * 0.6f;

                // Scale the bubble canvas proportionally to NPC size.
                // Baseline: a 2-unit NPC uses scale 0.007.
                const float BASELINE_NPC_HEIGHT = 2f;
                const float BASELINE_SCALE = 0.007f;
                float scaleMultiplier = Mathf.Max(1f, npcTop / BASELINE_NPC_HEIGHT);
                float finalScale = BASELINE_SCALE * scaleMultiplier;
                // Don't resize a Screen Space Overlay canvas — it fills the screen at its own scale.
                if (canvasRect != null && !useFixedScreenPosition)
                    canvasRect.localScale = Vector3.one * finalScale;

                Debug.Log($"NPCSpeechBubble: Offset y={suggestedY:F1}, scale={finalScale:F3} " +
                    $"(NPC top from {renderers.Length} renderers={npcTop:F1}, padding={heightPadding:F1})");
            }
        }
        else if (canvasRect != null && !useFixedScreenPosition)
        {
            // No auto-sizing: fall back to a conservative scale (World Space only).
            canvasRect.localScale = Vector3.one * 0.007f;
        }

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

        if (useFixedScreenPosition)
        {
            // Screen Space Overlay: move the BubblePanel within the canvas by converting
            // the NPC's screen position + pixel offset into canvas-local coordinates.
            if (bubblePanel != null && mainCamera != null)
            {
                Vector3 npcScreen = mainCamera.WorldToScreenPoint(targetNPC.position);
                if (npcScreen.z > 0f) // NPC is visible in front of camera
                {
                    Vector2 targetScreen = new Vector2(
                        npcScreen.x + fixedScreenPosition.x,
                        npcScreen.y + fixedScreenPosition.y);

                    var canvasRect = worldCanvas.GetComponent<RectTransform>();
                    Camera evtCam = worldCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera;
                    Vector2 localPos;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            canvasRect, targetScreen, evtCam, out localPos))
                    {
                        var rt = bubblePanel.GetComponent<RectTransform>();
                        if (rt != null) rt.anchoredPosition = localPos;
                    }
                }
            }
            return;
        }

        // World Space canvas: position relative to NPC
        transform.position = targetNPC.position + (_dialogueModeActive ? _dialogueOffset : worldOffset);

        // Billboard: face the camera
        if (mainCamera != null)
        {
            transform.forward = mainCamera.transform.forward;
            ClampBubbleToScreen();
        }
    }

    /// <summary>
    /// Clamp the speech bubble so its top edge stays within the visible screen area.
    /// Moves the bubble down in world space if it would go off the top of the viewport.
    /// </summary>
    private void ClampBubbleToScreen()
    {
        if (mainCamera == null) return;

        var canvasRect = GetComponent<RectTransform>();
        if (canvasRect == null) return;

        // Estimate the top of the bubble in world space
        // The canvas pivot is bottom-center (0.5, 0) so the bubble extends upward
        float bubbleWorldHeight = canvasRect.sizeDelta.y * canvasRect.localScale.y;
        Vector3 bubbleTop = transform.position + Vector3.up * bubbleWorldHeight;

        // Convert to viewport coordinates (0-1 range, where 1 = top of screen)
        Vector3 topViewport = mainCamera.WorldToViewportPoint(bubbleTop);

        // Only clamp if the bubble is in front of the camera and going off-screen
        float topMargin = 0.95f; // leave a small margin from the edge
        if (topViewport.z > 0f && topViewport.y > topMargin)
        {
            // Find the world-space position where the top should be (at the margin)
            Vector3 targetTop = mainCamera.ViewportToWorldPoint(
                new Vector3(topViewport.x, topMargin, topViewport.z));
            Vector3 currentTop = mainCamera.ViewportToWorldPoint(
                new Vector3(topViewport.x, topViewport.y, topViewport.z));

            // Slide the entire bubble down by the difference
            transform.position += (targetTop - currentTop);
        }
    }

    // ─── Event Handlers ─────────────────────────────────────────

    private void HandleDialogueStarted(DialogueSO dialogue)
    {
        var dialogueNPC = targetNPC?.GetComponent<DialogueNPC>();
        if (dialogueNPC == null) return;
        ComputeDialogueOffset();
    }

    // Computes a fixed lateral offset so the bubble sits beside the NPC rather than
    // above them, chosen once per dialogue (camera is fixed for the conversation).
    private void ComputeDialogueOffset()
    {
        if (mainCamera == null || targetNPC == null) return;

        // Pick the side with more screen space: if NPC is right of center → bubble goes left
        Vector3 npcScreenPos = mainCamera.WorldToScreenPoint(targetNPC.position);
        bool putLeft = npcScreenPos.x > Screen.width * 0.5f;

        // Horizontal clearance: NPC's rendered half-width + tunable padding
        Vector3 camRight = mainCamera.transform.right;
        camRight.y = 0f;
        if (camRight.sqrMagnitude > 0.001f) camRight.Normalize();
        float clearance = _npcBoundsHalfWidth + dialogueSidePadding;

        // Place at shoulder/chest height rather than above the head
        _dialogueOffset = new Vector3(0f, _npcMidHeight, 0f)
                        + camRight * clearance * (putLeft ? -1f : 1f);
        _dialogueModeActive = true;
    }

    private void HandleNodeChanged(DialogueNode node)
    {
        if (node == null) return;

        // Only show bubble when the NPC is speaking (speaker matches ActiveNPCSpeakerName)
        bool isNPCSpeaking = IsActiveNPCSpeaker(node.speakerName);
        Debug.Log($"NPCSpeechBubble [{gameObject.name}]: NodeChanged speaker='{node.speakerName}' isNPCSpeaking={isNPCSpeaking} speechText={(speechText != null ? "OK" : "NULL")} bubblePanel={(bubblePanel != null ? "OK" : "NULL")} targetNPC={(targetNPC != null ? targetNPC.name : "NULL")}");

        if (isNPCSpeaking)
        {
            // Show bubble with NPC's text
            if (!isShowing)
                Show();

            // Update speaker name
            if (speakerNameText != null)
                speakerNameText.text = node.speakerName ?? "";

            // Prepare speech text (truncated only as safety net)
            string text = node.dialogueText ?? "";
            if (text.Length > maxDisplayChars)
                text = text.Substring(0, maxDisplayChars) + "...";

            // Configure text sizing — sets full text, enables auto-sizing if needed
            AdaptTextToFit(text);

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
                // Full text already set by AdaptTextToFit — just show prompt
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
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }
        isTypewriting = false;
        _dialogueModeActive = false;
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
            AdaptTextToFit(text);
        }
    }

    // ─── UI Construction ────────────────────────────────────────

    private void BuildBubbleUI()
    {
        // Set canvas size — 600×400 at 0.007 scale (wider for font 28, smaller overall)
        var canvasRect = GetComponent<RectTransform>();
        if (canvasRect != null)
        {
            canvasRect.sizeDelta = new Vector2(600, 400);
            canvasRect.localScale = Vector3.one * 0.007f; // world space scale
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
        vlg.padding = new RectOffset(15, 15, 12, 12);
        vlg.spacing = 6;
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
        badgeHLG.padding = new RectOffset(12, 12, 3, 3);
        badgeHLG.childAlignment = TextAnchor.MiddleCenter;

        var badgeFitter = badgeObj.AddComponent<ContentSizeFitter>();
        badgeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        badgeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var badgeLE = badgeObj.AddComponent<LayoutElement>();
        badgeLE.preferredHeight = 30;

        // Speaker name text
        var nameObj = new GameObject("SpeakerName");
        nameObj.transform.SetParent(badgeObj.transform, false);

        speakerNameText = nameObj.AddComponent<TextMeshProUGUI>();
        speakerNameText.text = "NPC";
        speakerNameText.fontSize = 18;
        speakerNameText.fontStyle = FontStyles.Bold;
        speakerNameText.color = Color.white;
        speakerNameText.alignment = TextAlignmentOptions.Center;
        speakerNameText.enableAutoSizing = false;

        // Speech text
        var textObj = new GameObject("SpeechText");
        textObj.transform.SetParent(bubblePanel.transform, false);

        speechText = textObj.AddComponent<TextMeshProUGUI>();
        speechText.text = "";
        speechText.fontSize = defaultFontSize;
        speechText.color = textColor;
        speechText.alignment = TextAlignmentOptions.TopLeft;
        speechText.textWrappingMode = TextWrappingModes.Normal;
        speechText.overflowMode = TextOverflowModes.Overflow; // Never truncate/ellipsis
        speechText.enableAutoSizing = true;
        speechText.fontSizeMin = minFontSize;
        speechText.fontSizeMax = maxFontSize;

        var textLE = textObj.AddComponent<LayoutElement>();
        textLE.preferredWidth = 540;

        // Continue Prompt (below speech text, right-aligned)
        if (showContinuePrompt)
        {
            var promptObj = new GameObject("ContinuePrompt");
            promptObj.transform.SetParent(bubblePanel.transform, false);

            continuePromptTMP = promptObj.AddComponent<TextMeshProUGUI>();
            continuePromptTMP.text = continuePromptText;
            continuePromptTMP.fontSize = 14;
            continuePromptTMP.fontStyle = FontStyles.Italic;
            continuePromptTMP.color = continuePromptColor;
            continuePromptTMP.alignment = TextAlignmentOptions.BottomRight;
            continuePromptTMP.enableAutoSizing = false;

            var promptLE = promptObj.AddComponent<LayoutElement>();
            promptLE.preferredWidth = 540;
            promptLE.preferredHeight = 22;
        }
    }

    // ─── Adaptive Text Sizing ────────────────────────────────────

    /// <summary>
    /// Configure speech text sizing for the given content. Sets the full text
    /// with auto-sizing between minFontSize and maxFontSize. The bubble
    /// background (ContentSizeFitter) grows vertically to fit all content —
    /// no height cap, no text masking.
    /// </summary>
    private void AdaptTextToFit(string fullText)
    {
        if (speechText == null) return;

        // Always use auto-sizing so TMP picks the best size in range
        speechText.enableAutoSizing = true;
        speechText.fontSizeMin = minFontSize;
        speechText.fontSizeMax = maxFontSize;

        // Prevent TMP from truncating or showing "..." — content grows the bubble instead
        speechText.overflowMode = TextOverflowModes.Overflow;

        speechText.text = fullText;
        speechText.maxVisibleCharacters = int.MaxValue;

        // Remove any height cap — let ContentSizeFitter grow the bubble to fit
        var textLE = speechText.GetComponent<LayoutElement>();
        if (textLE != null)
            textLE.preferredHeight = -1;

        speechText.ForceMeshUpdate();
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

        // Reveal all characters (text was already set by AdaptTextToFit)
        if (speechText != null)
            speechText.maxVisibleCharacters = int.MaxValue;

        // Show continue prompt now that full text is visible
        if (continuePromptTMP != null)
            continuePromptTMP.gameObject.SetActive(true);
    }

    private void StartTypewriter(string text)
    {
        if (typewriterCoroutine != null)
            StopCoroutine(typewriterCoroutine);

        fullBubbleText = text;
        // Text is already set by AdaptTextToFit — typewriter just reveals characters
        typewriterCoroutine = StartCoroutine(TypewriterCoroutine(text));
    }

    private IEnumerator TypewriterCoroutine(string text)
    {
        isTypewriting = true;

        // Full text already set by AdaptTextToFit — hide all characters initially
        // Using maxVisibleCharacters keeps the bubble at its final size from the start
        // (no jank from growing during reveal) and preserves auto-sizing calculations.
        if (speechText != null)
            speechText.maxVisibleCharacters = 0;

        float delay = 1f / typewriterSpeed;

        for (int i = 0; i <= text.Length; i++)
        {
            if (speechText != null)
                speechText.maxVisibleCharacters = i;

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
